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

            //a backup opened from a file manager, an email or the downloads
            //list. it can land before this page exists - on a cold start it
            //is what opened the app - so it is offered from here as well as
            //when it arrives
            UiInterface.ImportExport.BackupRestore.Opened += () =>
                MainThread.BeginInvokeOnMainThread(OfferPendingBackup);

            //a desktop opens a file by handing it to the app on the command
            //line
            UiInterface.ImportExport.BackupRestore.CheckCommandLine();

            _instance = this;
            UiInterface.DataRefreshNotifier.DataChanged += () =>
                MainThread.BeginInvokeOnMainThread(RefreshBookedBadge);
            RefreshBookedBadge();
        }

        private static AppShell _instance;

        /// <summary>
        /// Moves to the All Jobs page under Work.
        ///
        /// It is the same switch the constructor makes to reopen the work
        /// view that was used last, rather than a route: everything that
        /// wants this is already on the Work tab - the stats page sending
        /// somebody to the round they tapped - so only which page of the tab
        /// is showing has to change.
        /// </summary>
        public static void ShowAllJobs()
        {
            if (_instance == null || _instance.tab_work == null)
                return;

            _instance.tab_work.CurrentItem = _instance.sc_workAll;
        }

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

            //a backup that opened the app was handed over before there was
            //anywhere to ask, so this is the first chance to
            OfferPendingBackup();
        }

        private bool _askingAboutBackup;

        /// <summary>
        /// offers to put back a backup the phone has handed to the app.
        ///
        /// Only one question at a time: this is reached both when the file
        /// arrives and on every navigation, and being asked twice about the
        /// same file is how somebody ends up restoring it twice.
        /// </summary>
        private async void OfferPendingBackup()
        {
            if (_askingAboutBackup)
                return;

            string path = UiInterface.ImportExport.BackupRestore.TakePending();
            if (string.IsNullOrWhiteSpace(path))
                return;

            _askingAboutBackup = true;

            try
            {
                Page page = CurrentPage ?? this;
                await UiInterface.ImportExport.BackupRestore.RestoreAsync(path, System.IO.Path.GetFileName(path), page);
            }
            finally
            {
                _askingAboutBackup = false;
            }
        }
    }
}