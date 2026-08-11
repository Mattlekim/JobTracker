using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kernel;

namespace UiInterface
{
    /// <summary>
    /// Takes direct debit payments through GoCardless.
    ///
    /// Setup (once, in Settings): paste a GoCardless access token
    /// (Dashboard -> Developers -> Create -> Access token, read-write).
    /// Then link each customer to their GoCardless direct debit from the
    /// customer details page, and payments can be collected with one tap
    /// or by marking a job paid with the GoCardless payment type.
    /// </summary>
    public static class GoCardless
    {
        const string LiveUrl = "https://api.gocardless.com";
        const string SandboxUrl = "https://api-sandbox.gocardless.com";
        const string ApiVersion = "2015-07-06";

        static readonly HttpClient Http = new HttpClient();

        public static string AccessToken
        {
            get => Preferences.Get("GoCardless_Token", string.Empty);
            set => Preferences.Set("GoCardless_Token", value ?? string.Empty);
        }

        /// <summary>use the GoCardless sandbox (test) environment instead of live</summary>
        public static bool UseSandbox
        {
            get => Preferences.Get("GoCardless_Sandbox", false);
            set => Preferences.Set("GoCardless_Sandbox", value);
        }

        public static bool IsConnected => !string.IsNullOrWhiteSpace(AccessToken);

        public static void Disconnect()
        {
            AccessToken = string.Empty;
        }

        static string BaseUrl => UseSandbox ? SandboxUrl : LiveUrl;

        static HttpRequestMessage NewRequest(HttpMethod method, string path)
        {
            HttpRequestMessage req = new HttpRequestMessage(method, $"{BaseUrl}{path}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            req.Headers.Add("GoCardless-Version", ApiVersion);
            return req;
        }

        static async Task<JsonDocument> SendAsync(HttpRequestMessage req)
        {
            HttpResponseMessage resp = await Http.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception(ExtractError(body, (int)resp.StatusCode));
            return JsonDocument.Parse(body);
        }

        /// <summary>pull a readable message out of a GoCardless error response</summary>
        static string ExtractError(string body, int statusCode)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                JsonElement error = doc.RootElement.GetProperty("error");
                string msg = error.GetProperty("message").GetString();
                if (error.TryGetProperty("errors", out JsonElement errors))
                    foreach (JsonElement e in errors.EnumerateArray())
                        if (e.TryGetProperty("message", out JsonElement m))
                        {
                            string detail = m.GetString();
                            if (detail != msg)
                                msg += $" - {detail}";
                            break;
                        }
                return $"GoCardless: {msg}";
            }
            catch
            {
                return $"GoCardless request failed (HTTP {statusCode})";
            }
        }

        /// <summary>
        /// checks the token works. returns the name of the GoCardless account
        /// </summary>
        public static async Task<string> VerifyAsync()
        {
            using JsonDocument doc = await SendAsync(NewRequest(HttpMethod.Get, "/creditors?limit=1"));
            foreach (JsonElement c in doc.RootElement.GetProperty("creditors").EnumerateArray())
                return c.GetProperty("name").GetString();
            return "GoCardless account";
        }

        public class GcCustomer
        {
            public string Id;
            public string Email = string.Empty;
            public string GivenName = string.Empty;
            public string FamilyName = string.Empty;

            public string Display
            {
                get
                {
                    string name = $"{GivenName} {FamilyName}".Trim();
                    if (name == string.Empty)
                        name = Id;
                    if (Email == string.Empty)
                        return name;
                    return $"{name} ({Email})";
                }
            }
        }

        /// <summary>all customers in the GoCardless account</summary>
        public static async Task<List<GcCustomer>> ListCustomersAsync()
        {
            List<GcCustomer> all = new List<GcCustomer>();
            string after = null;

            //pages of 500, follow the cursor until done (bounded for safety)
            for (int page = 0; page < 20; page++)
            {
                string path = "/customers?limit=500";
                if (after != null)
                    path += $"&after={Uri.EscapeDataString(after)}";

                using JsonDocument doc = await SendAsync(NewRequest(HttpMethod.Get, path));
                foreach (JsonElement c in doc.RootElement.GetProperty("customers").EnumerateArray())
                {
                    all.Add(new GcCustomer
                    {
                        Id = c.GetProperty("id").GetString(),
                        Email = c.TryGetProperty("email", out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : string.Empty,
                        GivenName = c.TryGetProperty("given_name", out JsonElement g) && g.ValueKind == JsonValueKind.String ? g.GetString() : string.Empty,
                        FamilyName = c.TryGetProperty("family_name", out JsonElement f) && f.ValueKind == JsonValueKind.String ? f.GetString() : string.Empty,
                    });
                }

                after = null;
                if (doc.RootElement.TryGetProperty("meta", out JsonElement meta) &&
                    meta.TryGetProperty("cursors", out JsonElement cursors) &&
                    cursors.TryGetProperty("after", out JsonElement a) &&
                    a.ValueKind == JsonValueKind.String)
                    after = a.GetString();

                if (after == null)
                    break;
            }
            return all;
        }

        /// <summary>
        /// the id of a mandate for this customer that payments can be
        /// collected against, or null when they have none
        /// </summary>
        public static async Task<string> FindUsableMandateAsync(string gcCustomerId)
        {
            using JsonDocument doc = await SendAsync(NewRequest(HttpMethod.Get,
                $"/mandates?customer={Uri.EscapeDataString(gcCustomerId)}&limit=100"));

            //payments can be created against these states
            string[] usable = { "active", "submitted", "pending_submission", "pending_customer_approval" };

            string best = null;
            foreach (JsonElement m in doc.RootElement.GetProperty("mandates").EnumerateArray())
            {
                string status = m.GetProperty("status").GetString();
                if (!usable.Contains(status))
                    continue;
                //prefer an active mandate over one still being set up
                if (best == null || status == "active")
                    best = m.GetProperty("id").GetString();
                if (status == "active")
                    break;
            }
            return best;
        }

        public class GcPayment
        {
            public string Id;
            public string Status;
            public DateTime ChargeDate;
        }

        /// <summary>
        /// collect a payment by direct debit. amount is in pounds; the
        /// charge date (when the money actually leaves the customer's bank)
        /// is picked by GoCardless, normally a few working days out
        /// </summary>
        public static async Task<GcPayment> CreatePaymentAsync(string mandateId, float amount, string description)
        {
            int pence = (int)Math.Round(amount * 100f);
            if (pence <= 0)
                throw new Exception("The amount must be more than 0");

            string body = JsonSerializer.Serialize(new
            {
                payments = new
                {
                    amount = pence,
                    currency = "GBP",
                    description = description,
                    links = new { mandate = mandateId },
                }
            });

            HttpRequestMessage req = NewRequest(HttpMethod.Post, "/payments");
            //stops a double tap taking the money twice
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using JsonDocument doc = await SendAsync(req);
            return ReadPayment(doc.RootElement.GetProperty("payments"));
        }

        /// <summary>look up one payment to see where it has got to</summary>
        public static async Task<GcPayment> GetPaymentAsync(string paymentId)
        {
            using JsonDocument doc = await SendAsync(NewRequest(HttpMethod.Get,
                $"/payments/{Uri.EscapeDataString(paymentId)}"));
            return ReadPayment(doc.RootElement.GetProperty("payments"));
        }

        static GcPayment ReadPayment(JsonElement p)
        {
            GcPayment result = new GcPayment
            {
                Id = p.GetProperty("id").GetString(),
                Status = p.GetProperty("status").GetString(),
            };
            if (p.TryGetProperty("charge_date", out JsonElement cd) && cd.ValueKind == JsonValueKind.String)
                DateTime.TryParse(cd.GetString(), out result.ChargeDate);
            return result;
        }

        // ---------------- keeping track of what has been requested ----------------

        /// <summary>
        /// statuses that mean the money has actually been collected
        /// </summary>
        static readonly string[] PaidStatuses = { "confirmed", "paid_out" };

        /// <summary>
        /// statuses that mean the money will never arrive, so the job can be
        /// charged again
        /// </summary>
        static readonly string[] DeadStatuses = { "cancelled", "customer_approval_denied", "failed", "charged_back" };

        static bool _refreshing;

        /// <summary>
        /// asks GoCardless where every outstanding payment request has got
        /// to. Requests that have been collected mark their job as paid and
        /// record the payment on the day the money was taken; requests that
        /// died free the job up to be charged again. Returns a short summary.
        /// </summary>
        public static async Task<string> RefreshPendingAsync()
        {
            if (!IsConnected)
                return "GoCardless not connected";
            if (_refreshing)
                return "Already checking";

            _refreshing = true;
            try
            {
                List<GoCardlessRequest> pending = GoCardlessRequest.QueryPending();
                if (pending.Count == 0)
                    return "No direct debits waiting";

                int paid = 0, failed = 0, stillPending = 0;
                bool changed = false;

                foreach (GoCardlessRequest r in pending)
                {
                    if (string.IsNullOrWhiteSpace(r.GoCardlessPaymentId))
                        continue;

                    GcPayment p;
                    try
                    {
                        p = await GetPaymentAsync(r.GoCardlessPaymentId);
                    }
                    catch
                    {
                        //leave it pending and try again next time rather than
                        //losing track of a payment because the network blipped
                        stillPending++;
                        continue;
                    }

                    r.GoCardlessStatus = p.Status;

                    if (PaidStatuses.Contains(p.Status))
                    {
                        //record it on the day the money actually left their bank
                        DateTime taken = p.ChargeDate != default ? p.ChargeDate : UsfulFuctions.DateNow;
                        r.SettleAsPaid(new DateTime(taken.Year, taken.Month, taken.Day));
                        paid++;
                        changed = true;
                    }
                    else if (DeadStatuses.Contains(p.Status))
                    {
                        r.SettleAsFailed(p.Status.Replace('_', ' '));
                        failed++;
                        changed = true;
                    }
                    else
                    {
                        //still on its way - keep the charge date up to date
                        if (p.ChargeDate != default)
                            r.ChargeDate = new DateTime(p.ChargeDate.Year, p.ChargeDate.Month, p.ChargeDate.Day);
                        stillPending++;
                        changed = true;
                    }
                }

                if (changed)
                {
                    GoCardlessRequest.Save();
                    if (paid > 0)
                    {
                        Payment.Save();
                        Customer.Save();
                        Job.Save();
                    }
                }

                if (paid == 0 && failed == 0)
                    return $"{stillPending} direct debit(s) still on the way";

                string summary = string.Empty;
                if (paid > 0)
                    summary = $"{paid} payment(s) received";
                if (failed > 0)
                    summary += (summary == string.Empty ? string.Empty : ", ") + $"{failed} failed";
                if (stillPending > 0)
                    summary += $", {stillPending} still on the way";
                return summary;
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>
        /// send a payment request for a job and log it. refuses when a
        /// request for that job is already waiting, so the same job can never
        /// be charged twice. Returns the logged request.
        /// </summary>
        public static async Task<GoCardlessRequest> RequestJobPaymentAsync(Job job, float amount)
        {
            if (!IsConnected)
                throw new Exception("GoCardless is not connected. Connect it in Settings first.");

            GoCardlessRequest existing = GoCardlessRequest.PendingForJob(job.Id);
            if (existing != null)
                throw new Exception($"A payment request has already been sent for this job ({existing.FormattedSummary}). Wait for it to clear before sending another.");

            if (job.IsPaidFor)
                throw new Exception("This job has already been paid for.");

            Customer c = job.GetCustomer();
            if (c == null || !c.HasGoCardless())
                throw new Exception("This customer is not linked to a GoCardless direct debit yet. Open the job's info page and use the GoCardless option to link them.");

            GcPayment p = await CreatePaymentAsync(c.GoCardlessMandateId, amount,
                $"Window cleaning {job.JobFormattedStreet}".Trim());

            GoCardlessRequest request = GoCardlessRequest.Add(new GoCardlessRequest
            {
                JobId = job.Id,
                CustomerId = c.Id,
                GoCardlessPaymentId = p.Id,
                Amount = amount,
                DateRequested = UsfulFuctions.DateNow,
                ChargeDate = p.ChargeDate != default ? new DateTime(p.ChargeDate.Year, p.ChargeDate.Month, p.ChargeDate.Day) : UsfulFuctions.DateBase,
                Status = DirectDebitStatus.Pending,
                GoCardlessStatus = p.Status,
            });

            GoCardlessRequest.Save();
            job.Refresh();
            return request;
        }
    }
}
