using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
            JsonElement p = doc.RootElement.GetProperty("payments");
            GcPayment result = new GcPayment
            {
                Id = p.GetProperty("id").GetString(),
                Status = p.GetProperty("status").GetString(),
            };
            if (p.TryGetProperty("charge_date", out JsonElement cd) && cd.ValueKind == JsonValueKind.String)
                DateTime.TryParse(cd.GetString(), out result.ChargeDate);
            return result;
        }
    }
}
