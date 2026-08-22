using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kernel;

namespace UiInterface
{
    /// <summary>
    /// Two-way sync of the app's data files with the user's Google Drive
    /// (hidden appDataFolder). Signing in opens the normal Google login
    /// page in the browser and the app catches the redirect on a local
    /// port (OAuth authorization code flow with PKCE), so it is just
    /// click - log in - approve, no codes to type.
    ///
    /// Sign in once per device from Settings. After that:
    ///  - saves are pushed automatically a few seconds after data changes
    ///  - app start pulls any newer files down and reloads
    /// Conflicts are resolved per file, newest write wins.
    /// </summary>
    public static class CloudSync
    {
        /// <summary>
        /// the round itself, which belongs to no tax year and is always the
        /// same handful of files
        /// </summary>
        static readonly string[] GlobalFiles = { "customers.rjt", "jobs.rjt", "quotes.rjt", "expenserules.rjt", "bankaccounts.rjt", "directdebits.rjt", "invoices.rjt", Kernel.Payment.IgnoreFilePath };

        /// <summary>
        /// the tax records, which are one file per tax year. a year that has
        /// not been touched keeps its timestamp, so nothing is sent for it -
        /// only the year being worked in moves.
        /// </summary>
        static readonly string[] YearlyPrefixes = { Kernel.Expense.FilePrefix, Kernel.Payment.FilePrefix, Kernel.StatementRecord.FilePrefix };

        /// <summary>
        /// the single files expenses and income used to be kept in, before
        /// they were split by tax year, with the prefix each turns into
        /// </summary>
        static readonly (string Name, string Prefix)[] LegacyFiles =
        {
            ("expenses.rjt", Kernel.Expense.FilePrefix),
            ("payment.rjt", Kernel.Payment.FilePrefix),
        };

        /// <summary>
        /// data left in the cloud by a version from before the tax years were
        /// split up. A device installed fresh has none of it locally, so it is
        /// pulled down once and the loader turns it into year files. After
        /// that it is left where it is - never downloaded again, so nothing
        /// deleted since can come back.
        /// </summary>
        static async Task<bool> PullLegacyFilesAsync(List<RemoteFile> remote)
        {
            bool pulled = false;

            foreach ((string name, string prefix) in LegacyFiles)
            {
                if (Preferences.Get($"CloudSync_LegacyDone_{name}", false))
                    continue;

                string path = LocalPath(name);

                //this device has its own copy, or has already split it up
                if (File.Exists(path) || Kernel.YearlyStore.YearsOnDisk(prefix).Count > 0)
                {
                    Preferences.Set($"CloudSync_LegacyDone_{name}", true);
                    continue;
                }

                RemoteFile rf = remote.FirstOrDefault(x => x.Name == name);
                if (rf == null)
                    continue;

                await DownloadAsync(rf.Id, path);
                Preferences.Set($"CloudSync_LegacyDone_{name}", true);
                pulled = true;
            }

            return pulled;
        }

        /// <summary>
        /// everything worth syncing: the round, plus every tax year file
        /// either side has. built fresh each sync because a new tax year -
        /// or one pulled down from another device - adds files
        /// </summary>
        static List<string> FilesToSync(List<RemoteFile> remote)
        {
            List<string> names = new List<string>(GlobalFiles);

            void AddName(string name)
            {
                if (!names.Contains(name))
                    names.Add(name);
            }

            foreach (string prefix in YearlyPrefixes)
            {
                foreach (int year in Kernel.YearlyStore.YearsOnDisk(prefix))
                    AddName($"{prefix}-{year}.rjt");

                foreach (RemoteFile rf in remote)
                    if (Kernel.YearlyStore.IsYearFile(rf.Name, prefix))
                        AddName(rf.Name);
            }

            return names;
        }

        const string Scope = "https://www.googleapis.com/auth/drive.appdata";
        const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        const string TokenUrl = "https://oauth2.googleapis.com/token";
        const string FilesUrl = "https://www.googleapis.com/drive/v3/files";
        const string UploadUrl = "https://www.googleapis.com/upload/drive/v3/files";

        static readonly HttpClient Http = new HttpClient();

        static string _accessToken;
        static DateTime _accessTokenExpires = DateTime.MinValue;
        static CancellationTokenSource _pendingUpload;
        static bool _syncing;

        /// <summary>status text for the settings page ("Synced 12:03" etc)</summary>
        public static event Action<string> StatusChanged;

        //the app's own google credentials. these are stand-ins showing the
        //shape google issues - they cannot sign in to anything. replace both
        //with the real values (Google Cloud Console -> APIs & Services ->
        //Credentials -> Create OAuth client ID, type 'Desktop app') and
        //signing in becomes just the Connect button, nobody has to paste
        //anything. a desktop-app client secret is not actually secret,
        //google expects it to ship inside installed apps.
        const string BuiltInClientId = "000000000000-REPLACE-ME-BEFORE-RELEASE.apps.googleusercontent.com";
        const string BuiltInClientSecret = "GOCSPX-REPLACE-ME-BEFORE-RELEASE";

        /// <summary>anything still carrying this marker is a stand-in and is
        /// treated exactly as if no credential had been supplied</summary>
        const string PlaceholderMarker = "REPLACE-ME";

        static bool IsPlaceholder(string value)
            => value != null && value.Contains(PlaceholderMarker);

        /// <summary>true when real credentials are compiled into the app, so the
        /// settings page can hide the paste-a-key fields. the stand-ins above do
        /// not count - hiding the fields while they are in place would leave no
        /// way to enter a working Client ID by hand.</summary>
        public static bool HasBuiltInClient => !IsPlaceholder(BuiltInClientId) && BuiltInClientId != string.Empty;

        /// <summary>false while the Client ID is missing or still a stand-in, so
        /// the settings page can show the setup steps rather than sending the
        /// user to a Google "invalid_client" error page</summary>
        public static bool HasUsableClientId => !string.IsNullOrWhiteSpace(ClientId) && !IsPlaceholder(ClientId);

        public static string ClientId
        {
            get
            {
                string v = Preferences.Get("CloudSync_ClientId", string.Empty);
                return string.IsNullOrWhiteSpace(v) ? BuiltInClientId : v;
            }
            set => Preferences.Set("CloudSync_ClientId", value ?? string.Empty);
        }

        public static string ClientSecret
        {
            get
            {
                string v = Preferences.Get("CloudSync_ClientSecret", string.Empty);
                return string.IsNullOrWhiteSpace(v) ? BuiltInClientSecret : v;
            }
            set => Preferences.Set("CloudSync_ClientSecret", value ?? string.Empty);
        }

        public static bool AutoSync
        {
            get => Preferences.Get("CloudSync_Auto", true);
            set => Preferences.Set("CloudSync_Auto", value);
        }

        public static string LastSyncText
        {
            get => Preferences.Get("CloudSync_LastSync", "Never");
            private set
            {
                Preferences.Set("CloudSync_LastSync", value);
                StatusChanged?.Invoke(value);
            }
        }

        public static bool IsSignedIn => !string.IsNullOrEmpty(RefreshToken);

        static string RefreshToken
        {
            get => Preferences.Get("CloudSync_RefreshToken", string.Empty);
            set => Preferences.Set("CloudSync_RefreshToken", value ?? string.Empty);
        }

        public static void SignOut()
        {
            RefreshToken = string.Empty;
            _accessToken = null;
            _accessTokenExpires = DateTime.MinValue;
            foreach (string f in FilesToSync(new List<RemoteFile>()))
            {
                Preferences.Remove($"CloudSync_Remote_{f}");
                Preferences.Remove($"CloudSync_Local_{f}");
            }
            LastSyncText = "Never";
        }

        /// <summary>
        /// call once at startup: wires the auto-upload hook and, when signed in,
        /// pulls newer data down in the background
        /// </summary>
        public static void Start()
        {
            SyncNotifier.DataSaved += QueueUpload;

            if (IsSignedIn && AutoSync)
                _ = SyncNowAsync();
        }

        static void QueueUpload()
        {
            if (!IsSignedIn || !AutoSync || _syncing)
                return;

            //debounce: lots of saves happen in quick bursts
            _pendingUpload?.Cancel();
            CancellationTokenSource cts = new CancellationTokenSource();
            _pendingUpload = cts;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(8), cts.Token);
                    await SyncNowAsync();
                }
                catch (TaskCanceledException) { }
            });
        }

        // ---------------- sign in (browser + loopback redirect) ----------------

        /// <summary>
        /// one-click sign in: opens the Google login page in the browser and
        /// waits for Google to redirect back to a little listener on
        /// 127.0.0.1. Returns true when signed in, false when the user gave
        /// up (cancelled / timed out). Throws when Google reports an error.
        /// </summary>
        public static async Task<bool> SignInWithBrowserAsync(CancellationToken cancel)
        {
            //PKCE keeps the exchange safe even though the redirect is plain http
            string verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
            string challenge;
            using (SHA256 sha = SHA256.Create())
                challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));

            //listen on a random free port on loopback for the redirect
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string redirect = $"http://127.0.0.1:{port}/";

            string url = $"{AuthUrl}?client_id={Uri.EscapeDataString(ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirect)}" +
                "&response_type=code" +
                $"&scope={Uri.EscapeDataString(Scope)}" +
                "&access_type=offline&prompt=consent" +
                $"&code_challenge={challenge}&code_challenge_method=S256";

            string code = null;
            try
            {
                await Browser.OpenAsync(url, BrowserLaunchMode.SystemPreferred);

                while (code == null)
                {
                    TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync(cancel);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }

                    using (client)
                    {
                        NetworkStream stream = client.GetStream();
                        string requestLine = await ReadRequestLineAsync(stream);

                        string error = GetQueryParam(requestLine, "error");
                        if (error != null)
                        {
                            await WriteBrowserPageAsync(stream, "Sign in failed",
                                $"Google said: {error}. You can close this window and try again in Work Tracker.");
                            throw new Exception($"Sign in failed: {error}");
                        }

                        string c = GetQueryParam(requestLine, "code");
                        if (c != null)
                        {
                            await WriteBrowserPageAsync(stream, "Connected!",
                                "Work Tracker is now connected to Google Drive. You can close this window and return to the app.");
                            code = c;
                        }
                        else
                        {
                            //favicon requests and the like
                            await WriteBrowserPageAsync(stream, "Work Tracker", "Waiting for Google sign in...");
                        }
                    }
                }
            }
            finally
            {
                listener.Stop();
            }

            //swap the one-time code for the tokens
            Dictionary<string, string> fields = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = redirect,
                ["grant_type"] = "authorization_code",
            };
            if (!string.IsNullOrWhiteSpace(ClientSecret))
                fields["client_secret"] = ClientSecret;

            HttpResponseMessage resp = await Http.PostAsync(TokenUrl, new FormUrlEncodedContent(fields), cancel);
            string body = await resp.Content.ReadAsStringAsync(cancel);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Google rejected the sign in: {body}");

            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            _accessToken = root.GetProperty("access_token").GetString();
            _accessTokenExpires = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32() - 60);
            if (root.TryGetProperty("refresh_token", out JsonElement rt))
                RefreshToken = rt.GetString();
            return true;
        }

        /// <summary>first line of the http request the browser redirect makes, e.g. "GET /?code=... HTTP/1.1"</summary>
        static async Task<string> ReadRequestLineAsync(NetworkStream stream)
        {
            byte[] buffer = new byte[8192];
            StringBuilder sb = new StringBuilder();
            while (!sb.ToString().Contains("\r\n") && sb.Length < 16384)
            {
                int n = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (n <= 0)
                    break;
                sb.Append(Encoding.ASCII.GetString(buffer, 0, n));
            }
            string s = sb.ToString();
            int end = s.IndexOf("\r\n");
            return end == -1 ? s : s.Substring(0, end);
        }

        static string GetQueryParam(string requestLine, string name)
        {
            int qs = requestLine.IndexOf('?');
            if (qs == -1)
                return null;
            int end = requestLine.LastIndexOf(" HTTP");
            string query = end > qs ? requestLine.Substring(qs + 1, end - qs - 1) : requestLine.Substring(qs + 1);

            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq == -1)
                    continue;
                if (pair.Substring(0, eq) == name)
                    return Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            return null;
        }

        static async Task WriteBrowserPageAsync(NetworkStream stream, string title, string message)
        {
            string html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                $"<title>{title}</title></head>" +
                "<body style=\"font-family:sans-serif;background:#1E1E1E;color:#EEE;display:flex;align-items:center;justify-content:center;height:100vh;margin:0\">" +
                $"<div style=\"text-align:center;padding:24px\"><h1 style=\"color:#4CAF50\">{title}</h1><p>{message}</p></div></body></html>";
            byte[] content = Encoding.UTF8.GetBytes(html);
            string header = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {content.Length}\r\nConnection: close\r\n\r\n";
            byte[] head = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(head, 0, head.Length);
            await stream.WriteAsync(content, 0, content.Length);
            await stream.FlushAsync();
        }

        static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static async Task<string> GetAccessTokenAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _accessTokenExpires)
                return _accessToken;

            FormUrlEncodedContent form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["refresh_token"] = RefreshToken,
                ["grant_type"] = "refresh_token",
            });
            HttpResponseMessage resp = await Http.PostAsync(TokenUrl, form);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Could not refresh sign in: {body}");

            using JsonDocument doc = JsonDocument.Parse(body);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            _accessTokenExpires = DateTime.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 60);
            return _accessToken;
        }

        // ---------------- the sync itself ----------------

        class RemoteFile
        {
            public string Id;
            public string Name;
            public DateTime Modified;
        }

        static string LocalPath(string name)
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);

        /// <summary>
        /// two-way sync: per file the newest copy wins. Returns a short summary.
        /// </summary>
        public static async Task<string> SyncNowAsync()
        {
            if (!IsSignedIn)
                return "Not signed in";
            if (_syncing)
                return "Already syncing";

            _syncing = true;
            try
            {
                string token = await GetAccessTokenAsync();
                Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                //what's in the cloud?
                List<RemoteFile> remote = await ListRemoteAsync();

                int uploaded = 0, downloaded = 0;
                bool reloadNeeded = false;

                //a device installed fresh takes what an older version left in
                //the cloud, and the loader splits it into tax years
                if (await PullLegacyFilesAsync(remote))
                {
                    reloadNeeded = true;
                    downloaded++;
                }

                foreach (string name in FilesToSync(remote))
                {
                    string path = LocalPath(name);
                    bool localExists = File.Exists(path);
                    RemoteFile rf = remote.FirstOrDefault(x => x.Name == name);

                    if (!localExists && rf == null)
                        continue;

                    //what did each side look like after the last sync?
                    long storedRemote = Preferences.Get($"CloudSync_Remote_{name}", 0L);
                    long storedLocal = Preferences.Get($"CloudSync_Local_{name}", 0L);
                    long localTicks = localExists ? File.GetLastWriteTimeUtc(path).Ticks : 0;
                    long remoteTicks = rf?.Modified.Ticks ?? 0;

                    bool localChanged = localExists && localTicks != storedLocal;
                    bool remoteChanged = rf != null && remoteTicks != storedRemote;

                    bool doUpload = false, doDownload = false;
                    if (localChanged && remoteChanged)
                    {
                        //both sides moved since last sync - newest wins
                        if (localTicks >= remoteTicks) doUpload = true;
                        else doDownload = true;
                    }
                    else if (localChanged || (localExists && rf == null))
                        doUpload = true;
                    else if (remoteChanged || (!localExists && rf != null))
                        doDownload = true;

                    if (doUpload)
                    {
                        DateTime newRemote = await UploadAsync(name, path, rf?.Id);
                        Preferences.Set($"CloudSync_Remote_{name}", newRemote.Ticks);
                        Preferences.Set($"CloudSync_Local_{name}", File.GetLastWriteTimeUtc(path).Ticks);
                        uploaded++;
                    }
                    else if (doDownload)
                    {
                        await DownloadAsync(rf.Id, path);
                        Preferences.Set($"CloudSync_Remote_{name}", remoteTicks);
                        Preferences.Set($"CloudSync_Local_{name}", File.GetLastWriteTimeUtc(path).Ticks);
                        downloaded++;
                        reloadNeeded = true;
                    }
                }

                if (reloadNeeded)
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Customer.Load();
                        Job.Reset();
                        Job.Load();
                        Payment.Load();
                        Expense.Load();
                        ExpenseRule.Load();
                        BankAccount.Load();
                        StatementRecord.Load();
                        GoCardlessRequest.Load();
                        Invoice.Load();
                        DataRefreshNotifier.NotifyDataChanged();
                    });

                //the paperwork behind the figures. done after the records are
                //reloaded, so a photo coming down knows which expense - and
                //so which tax year folder - it belongs to
                (int photosUp, int photosDown) = await SyncReceiptsAsync(remote);
                uploaded += photosUp;
                downloaded += photosDown;

                (int stmtUp, int stmtDown) = await SyncStatementsAsync(remote);
                uploaded += stmtUp;
                downloaded += stmtDown;

                //photos that arrived before the expense that claims them get
                //put in their year folder now the records have caught up
                Expense.FileLooseReceipts();

                LastSyncText = $"Synced {DateTime.Now:HH:mm} (up {uploaded}, down {downloaded})";
                return LastSyncText;
            }
            catch (Exception ex)
            {
                LastSyncText = $"Sync failed {DateTime.Now:HH:mm}";
                return $"Sync failed: {ex.Message}";
            }
            finally
            {
                _syncing = false;
            }
        }

        /// <summary>
        /// everything in the hidden app folder, following the paging cursor
        /// so a big pile of receipt photos does not get cut off
        /// </summary>
        static async Task<List<RemoteFile>> ListRemoteAsync()
        {
            List<RemoteFile> remote = new List<RemoteFile>();
            string pageToken = null;

            do
            {
                string url = $"{FilesUrl}?spaces=appDataFolder&fields=nextPageToken,files(id,name,modifiedTime)&pageSize=1000";
                if (pageToken != null)
                    url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

                HttpResponseMessage resp = await Http.GetAsync(url);
                string body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"Could not list cloud files: {body}");

                using JsonDocument doc = JsonDocument.Parse(body);
                foreach (JsonElement f in doc.RootElement.GetProperty("files").EnumerateArray())
                    remote.Add(new RemoteFile
                    {
                        Id = f.GetProperty("id").GetString(),
                        Name = f.GetProperty("name").GetString(),
                        Modified = f.GetProperty("modifiedTime").GetDateTime().ToUniversalTime(),
                    });

                pageToken = doc.RootElement.TryGetProperty("nextPageToken", out JsonElement t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString()
                    : null;
            }
            while (pageToken != null);

            return remote;
        }

        /// <summary>
        /// receipt photos are named uniquely when they are taken and never
        /// change afterwards, so there is nothing to resolve: a photo either
        /// side is missing is simply copied across. Photos are not deleted
        /// from the cloud, so removing an expense on one device cannot take
        /// the evidence away from another.
        ///
        /// Locally they live in a folder per tax year. Drive's app folder is
        /// flat, so the name is all there is to go on - a photo coming down
        /// is put in the folder for the tax year of the expense that claims
        /// it, and in 'unfiled' when nothing does yet. The next sync, once
        /// the expense has arrived, puts it right.
        /// </summary>
        static async Task<(int uploaded, int downloaded)> SyncReceiptsAsync(List<RemoteFile> remote)
        {
            int uploaded = 0, downloaded = 0;

            try
            {
                string root = Kernel.Expense.GetReceiptFolderPath();

                HashSet<string> remoteNames = new HashSet<string>(
                    remote.Where(x => x.Name.StartsWith(ReceiptPrefix)).Select(x => x.Name));

                //every year folder, and any photo still loose in the root
                foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(path);
                    if (!name.StartsWith(ReceiptPrefix) || remoteNames.Contains(name))
                        continue;

                    await UploadAsync(name, path, null);
                    uploaded++;
                }

                HashSet<string> localNames = new HashSet<string>(
                    Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(Path.GetFileName));

                foreach (RemoteFile rf in remote.Where(x => x.Name.StartsWith(ReceiptPrefix)))
                {
                    if (localNames.Contains(rf.Name))
                        continue;

                    await DownloadAsync(rf.Id, ReceiptDestination(root, rf.Name));
                    downloaded++;
                }
            }
            catch
            {
                //a photo that will not copy must not sink the whole sync -
                //the records matter more and it is retried next time
            }

            return (uploaded, downloaded);
        }

        /// <summary>the tax year folder a downloaded photo belongs in</summary>
        static string ReceiptDestination(string root, string name)
        {
            foreach (Expense e in Expense.Query())
                if (e.ReceiptFileName == name)
                    return Path.Combine(Kernel.Expense.GetReceiptFolderPath(e.TaxYear), name);

            //nothing claims it yet, so it waits loose in the receipts folder
            //where FileLooseReceipts will find it once the expense arrives
            return Path.Combine(root, name);
        }

        /// <summary>
        /// the bank statements themselves, which are the evidence the figures
        /// were read off. They are filed by tax year here and flat in Drive,
        /// so the year is carried in the name they are stored under.
        /// </summary>
        static async Task<(int uploaded, int downloaded)> SyncStatementsAsync(List<RemoteFile> remote)
        {
            int uploaded = 0, downloaded = 0;

            try
            {
                string root = Kernel.StatementRecord.GetStatementFolderPath();

                HashSet<string> remoteNames = new HashSet<string>(
                    remote.Where(x => x.Name.StartsWith(StatementPrefix)).Select(x => x.Name));

                foreach (StatementRecord record in StatementRecord.Query())
                {
                    if (!record.FileKept)
                        continue;

                    string name = RemoteStatementName(record.TaxYear, record.StoredFileName);
                    if (remoteNames.Contains(name))
                        continue;

                    await UploadAsync(name, record.StoredPath, null);
                    uploaded++;
                }

                foreach (RemoteFile rf in remote.Where(x => x.Name.StartsWith(StatementPrefix)))
                {
                    string path = LocalStatementPath(root, rf.Name);
                    if (path == null || File.Exists(path))
                        continue;

                    await DownloadAsync(rf.Id, path);
                    downloaded++;
                }
            }
            catch
            {
                //same as the photos - worth retrying, not worth failing over
            }

            return (uploaded, downloaded);
        }

        /// <summary>how receipt photos are named, see NewExpense</summary>
        const string ReceiptPrefix = "receipt_";

        /// <summary>how a kept statement is named in the cloud: stmt_2026-27__statement_....pdf</summary>
        const string StatementPrefix = "stmt_";

        const string StatementYearSeparator = "__";

        static string RemoteStatementName(int taxYear, string storedFileName)
        {
            return $"{StatementPrefix}{TaxCalendar.YearFolderName(taxYear)}{StatementYearSeparator}{storedFileName}";
        }

        /// <summary>where a statement coming down from the cloud goes, from its name</summary>
        static string LocalStatementPath(string root, string remoteName)
        {
            string rest = remoteName.Substring(StatementPrefix.Length);
            int split = rest.IndexOf(StatementYearSeparator, StringComparison.Ordinal);
            if (split <= 0)
                return null;

            string yearFolder = rest.Substring(0, split);
            string fileName = rest.Substring(split + StatementYearSeparator.Length);
            if (fileName.Length == 0)
                return null;

            string folder = Path.Combine(root, yearFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, fileName);
        }

        static async Task<DateTime> UploadAsync(string name, string path, string existingId)
        {
            byte[] data = await File.ReadAllBytesAsync(path);
            HttpResponseMessage resp;

            if (existingId == null)
            {
                //create with metadata so it lands in the hidden app folder
                string meta = JsonSerializer.Serialize(new { name = name, parents = new[] { "appDataFolder" } });
                MultipartContent content = new MultipartContent("related");
                content.Add(new StringContent(meta, Encoding.UTF8, "application/json"));
                ByteArrayContent file = new ByteArrayContent(data);
                file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(file);
                resp = await Http.PostAsync($"{UploadUrl}?uploadType=multipart&fields=modifiedTime", content);
            }
            else
            {
                ByteArrayContent file = new ByteArrayContent(data);
                file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                resp = await Http.PatchAsync($"{UploadUrl}/{existingId}?uploadType=media&fields=modifiedTime", file);
            }

            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Upload of {name} failed: {body}");

            using JsonDocument doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("modifiedTime").GetDateTime().ToUniversalTime();
        }

        static async Task DownloadAsync(string id, string path)
        {
            HttpResponseMessage resp = await Http.GetAsync($"{FilesUrl}/{id}?alt=media");
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Download failed: {await resp.Content.ReadAsStringAsync()}");
            byte[] data = await resp.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(path, data);
        }
    }
}
