using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kernel;

namespace UiInterface
{
    /// <summary>
    /// Two-way sync of the app's data files with the user's Google Drive
    /// (hidden appDataFolder), using the OAuth device flow so the same code
    /// works on Windows and Android with no redirect URLs.
    ///
    /// Sign in once per device from Settings. After that:
    ///  - saves are pushed automatically a few seconds after data changes
    ///  - app start pulls any newer files down and reloads
    /// Conflicts are resolved per file, newest write wins.
    /// </summary>
    public static class CloudSync
    {
        static readonly string[] SyncFiles = { "customers.rjt", "jobs.rjt", "quotes.rjt", "payment.rjt" };

        const string Scope = "https://www.googleapis.com/auth/drive.appdata";
        const string DeviceCodeUrl = "https://oauth2.googleapis.com/device/code";
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

        public static string ClientId
        {
            get => Preferences.Get("CloudSync_ClientId", string.Empty);
            set => Preferences.Set("CloudSync_ClientId", value ?? string.Empty);
        }

        public static string ClientSecret
        {
            get => Preferences.Get("CloudSync_ClientSecret", string.Empty);
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

        // ---------------- sign in (device flow) ----------------

        public class DeviceCodeInfo
        {
            public string UserCode;
            public string VerificationUrl;
            public string DeviceCode;
            public int Interval;
            public int ExpiresIn;
        }

        /// <summary>step 1: get the code the user has to type in at google.com/device</summary>
        public static async Task<DeviceCodeInfo> BeginSignInAsync()
        {
            FormUrlEncodedContent form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = Scope,
            });
            HttpResponseMessage resp = await Http.PostAsync(DeviceCodeUrl, form);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Google rejected the request: {body}");

            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            return new DeviceCodeInfo
            {
                UserCode = root.GetProperty("user_code").GetString(),
                VerificationUrl = root.TryGetProperty("verification_url", out JsonElement v) ? v.GetString() : "https://www.google.com/device",
                DeviceCode = root.GetProperty("device_code").GetString(),
                Interval = root.TryGetProperty("interval", out JsonElement i) ? i.GetInt32() : 5,
                ExpiresIn = root.TryGetProperty("expires_in", out JsonElement ex) ? ex.GetInt32() : 1800,
            };
        }

        /// <summary>step 2: poll until the user has approved (or gives up)</summary>
        public static async Task<bool> WaitForSignInAsync(DeviceCodeInfo info, CancellationToken cancel)
        {
            int interval = Math.Max(info.Interval, 5);
            DateTime deadline = DateTime.UtcNow.AddSeconds(info.ExpiresIn);

            while (DateTime.UtcNow < deadline && !cancel.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), cancel);

                FormUrlEncodedContent form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["device_code"] = info.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                });
                HttpResponseMessage resp = await Http.PostAsync(TokenUrl, form, cancel);
                string body = await resp.Content.ReadAsStringAsync(cancel);
                using JsonDocument doc = JsonDocument.Parse(body);
                JsonElement root = doc.RootElement;

                if (resp.IsSuccessStatusCode)
                {
                    _accessToken = root.GetProperty("access_token").GetString();
                    _accessTokenExpires = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32() - 60);
                    if (root.TryGetProperty("refresh_token", out JsonElement rt))
                        RefreshToken = rt.GetString();
                    return true;
                }

                string error = root.TryGetProperty("error", out JsonElement err) ? err.GetString() : "unknown";
                if (error == "authorization_pending")
                    continue;
                if (error == "slow_down")
                {
                    interval += 5;
                    continue;
                }
                throw new Exception($"Sign in failed: {error}");
            }
            return false;
        }

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
                List<RemoteFile> remote = new List<RemoteFile>();
                HttpResponseMessage listResp = await Http.GetAsync(
                    $"{FilesUrl}?spaces=appDataFolder&fields=files(id,name,modifiedTime)&pageSize=100");
                string listBody = await listResp.Content.ReadAsStringAsync();
                if (!listResp.IsSuccessStatusCode)
                    throw new Exception($"Could not list cloud files: {listBody}");
                using (JsonDocument doc = JsonDocument.Parse(listBody))
                    foreach (JsonElement f in doc.RootElement.GetProperty("files").EnumerateArray())
                        remote.Add(new RemoteFile
                        {
                            Id = f.GetProperty("id").GetString(),
                            Name = f.GetProperty("name").GetString(),
                            Modified = f.GetProperty("modifiedTime").GetDateTime().ToUniversalTime(),
                        });

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

                if (reloadNeeded)
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Customer.Load();
                        Job.Reset();
                        Job.Load();
                        Payment.Load();
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
