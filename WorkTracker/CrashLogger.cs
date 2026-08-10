using System.Text;

namespace WorkTracker
{
    // Captures unhandled exceptions to a log file so crashes can be
    // diagnosed from a device without a debugger attached.
    public static class CrashLogger
    {
        static bool _initialized;

        public static string LogPath => Path.Combine(FileSystem.AppDataDirectory, "crashlog.txt");

        public static void Init()
        {
            if (_initialized)
                return;
            _initialized = true;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Log("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

#if ANDROID
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
                Log("AndroidEnvironment.UnhandledExceptionRaiser", e.Exception);
#endif
        }

        public static void Log(string source, Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("==== CRASH " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====");
                sb.AppendLine("Source: " + source);
                try
                {
                    sb.AppendLine("App: " + AppInfo.Current.VersionString + " (" + AppInfo.Current.BuildString + ")");
                    sb.AppendLine("Device: " + DeviceInfo.Current.Manufacturer + " " + DeviceInfo.Current.Model
                        + ", " + DeviceInfo.Current.Platform + " " + DeviceInfo.Current.VersionString);
                }
                catch { }
                sb.AppendLine(ex?.ToString() ?? "(no exception object)");
                sb.AppendLine();
                File.AppendAllText(LogPath, sb.ToString());
            }
            catch
            {
                // Never let the logger itself take the app down.
            }
        }

        public static bool HasLog()
        {
            try
            {
                return File.Exists(LogPath) && new FileInfo(LogPath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static string ReadLog()
        {
            try
            {
                return File.ReadAllText(LogPath);
            }
            catch (Exception ex)
            {
                return "Could not read crash log: " + ex.Message;
            }
        }

        public static void Clear()
        {
            try
            {
                File.Delete(LogPath);
            }
            catch { }
        }
    }
}
