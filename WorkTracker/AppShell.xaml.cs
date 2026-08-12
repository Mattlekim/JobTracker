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
            Expense.Load();
            ExpenseRule.Load();
            StatementRecord.Load();
            GoCardlessRequest.Load();
            Settings.Load();

            //photos taken before receipts were filed by tax year are still
            //loose in the receipts folder - put them where they belong
            Expense.FileLooseReceipts();

            //pulls newer cloud data in the background and pushes future saves
            UiInterface.CloudSync.Start();

            //catch up on any direct debits that have cleared since last time
            if (UiInterface.GoCardless.IsConnected)
                _ = UiInterface.GoCardless.RefreshPendingAsync();

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