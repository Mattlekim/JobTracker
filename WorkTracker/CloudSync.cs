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
        static readonly string[] SyncFiles = { "customers.rjt", "jobs.rjt", "quotes.rjt", "payment.rjt", "expenses.rjt", "directdebits.rjt" };

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

        //the app's own google credentials. fill these in once (Google Cloud
        //Console -> APIs & Services -> Credentials -> Create OAuth client ID,
        //type 'Desktop app') and signing in becomes just the Connect button -
        //nobody has to paste anything. a desktop-app client secret is not
        //actually secret, google expects it to ship inside installed apps.
        const string BuiltInClientId = "";
        const string BuiltInClientSecret = "";

        /// <summary>true when credentials are compiled into the app, so the
        /// settings page can hide the paste-a-key fields</summary>
        public static bool HasBuiltInClient => BuiltInClientId != string.Empty;

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
            foreach (string f in SyncFiles)
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

                foreach (string name in SyncFiles)
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

                //receipt photos, so an expense's evidence follows the records
                (int photosUp, int photosDown) = await SyncReceiptsAsync(remote);
                uploaded += photosUp;
                downloaded += photosDown;

                if (reloadNeeded)
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Customer.Load();
                        Job.Reset();
                        Job.Load();
                        Payment.Load();
                        Expense.Load();
                        GoCardlessRequest.Load();
                        DataRefreshNotifier.NotifyDataChanged();
                    });

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
        /// </summary>
        static async Task<(int uploaded, int downloaded)> SyncReceiptsAsync(List<RemoteFile> remote)
        {
            int uploaded = 0, downloaded = 0;

            try
            {
                string folder = Kernel.Expense.GetReceiptFolderPath();

                HashSet<string> remoteNames = new HashSet<string>(
                    remote.Where(x => x.Name.StartsWith(ReceiptPrefix)).Select(x => x.Name));

                foreach (string path in Directory.GetFiles(folder))
                {
                    string name = Path.GetFileName(path);
                    if (!name.StartsWith(ReceiptPrefix) || remoteNames.Contains(name))
                        continue;

                    await UploadAsync(name, path, null);
                    uploaded++;
                }

                foreach (RemoteFile rf in remote.Where(x => x.Name.StartsWith(ReceiptPrefix)))
                {
                    string path = Path.Combine(folder, rf.Name);
                    if (File.Exists(path))
                        continue;

                    await DownloadAsync(rf.Id, path);
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

        /// <summary>how receipt photos are named, see NewExpense</summary>
        const string ReceiptPrefix = "receipt_";

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
