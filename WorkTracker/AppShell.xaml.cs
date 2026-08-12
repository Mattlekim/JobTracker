using Kernel;
using UiInterface.Layouts;

namespace WorkTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            //settings first: the job types live in there, and work read off
            //the file with no type on it is given the first of them. loaded
            //after the jobs, that would be the first of the built in types
            //rather than the first of this round's own
            Settings.Load();

            Customer.Load();
            Job.Load();
            Payment.Load();
            Expense.Load();
            ExpenseRule.Load();
            StatementRecord.Load();
            GoCardlessRequest.Load();

            //photos taken before receipts were filed by tax year are still
            //loose in the receipts folder - put them where they belong
            Expense.FileLooseReceipts();

            //a booked day that has passed with all of its work done is a plan
            //for a day that is over - it clears itself away
            Booking.ClearFinishedPastDays();

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

            _instance = this;
            UiInterface.DataRefreshNotifier.DataChanged += () =>
                MainThread.BeginInvokeOnMainThread(RefreshBookedBadge);
            RefreshBookedBadge();
        }

        private static AppShell _instance;

        /// <summary>
        /// Puts the number of overdue days on the Booked tab, so work left
        /// behind on a day that has passed is noticed without having to go
        /// looking for it. Shell has no badge of its own, so the count rides
        /// on the tab's name - which works the same on every platform.
        /// </summary>
        public static void RefreshBookedBadge()
        {
            if (_instance == null || _instance.tab_booked == null)
                return;

            try
            {
                int overdue = Booking.OverdueDays().Count;
                _instance.tab_booked.Title = overdue == 0 ? "Booked" : $"Booked ({overdue})";
            }
            catch
            {
                //a badge is not worth an exception on the way in
            }
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