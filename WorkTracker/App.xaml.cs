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

#if WINDOWS
        protected override Window CreateWindow(IActivationState activationState)
        {
            Window window = base.CreateWindow(activationState);

            double w = Preferences.Get("WindowWidth", 0d);
            double h = Preferences.Get("WindowHeight", 0d);
            if (w > 200 && h > 200)
            {
                window.Width = w;
                window.Height = h;
            }

            window.Destroying += (s, e) =>
            {
                Preferences.Set("WindowWidth", window.Width);
                Preferences.Set("WindowHeight", window.Height);
            };

            return window;
        }
#endif
    }
}
