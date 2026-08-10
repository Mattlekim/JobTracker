using Kernel;
using UiInterface.Layouts;

namespace WorkTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            Customer.Load();
            Job.Load();
            Payment.Load();
            Settings.Load();

            InitializeComponent();

            //reopen whichever work view (overview / list) was used last
            if (Preferences.Get("WorkTabView", "overview") == "list")
                tab_work.CurrentItem = sc_workList;
            Navigated += AppShell_Navigated;
        }

        private void AppShell_Navigated(object sender, ShellNavigatedEventArgs e)
        {
            string location = e.Current?.Location?.ToString() ?? string.Empty;
            if (location.Contains("work_list"))
                Preferences.Set("WorkTabView", "list");
            else if (location.Contains("work_overview"))
                Preferences.Set("WorkTabView", "overview");
        }
    }
}