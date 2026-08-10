namespace WorkTracker
{
    public partial class App : Application
    {
        public App()
        {

            InitializeComponent();

            if (CrashLogger.HasLog())
            {
                // The app crashed last time - show the log before anything
                // else so it can be shared even if startup keeps crashing.
                MainPage = new CrashLogPage();
                return;
            }

            try
            {
                MainPage = new AppShell();
            }
            catch (Exception ex)
            {
                CrashLogger.Log("Startup (AppShell)", ex);
                MainPage = new CrashLogPage();
            }

        }
    }
}
