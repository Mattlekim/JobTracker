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

            //when the data was last changed, which is kept with the data
            //rather than read off the files: a copy of the round - a backup,
            //a restore, a phone swap - stamps every file with the day the
            //copy was taken and says nothing about how old the work in it is
            DataStamp.Load();

            Customer.Load();
            Job.Load();
            Payment.Load();
            Expense.Load();
            ExpenseRule.Load();

            //after Settings.Load: the layout an old settings file kept is
            //stashed there, and this is what turns it into the first account
            BankAccount.Load();
            StatementRecord.Load();
            GoCardlessRequest.Load();
            BalanceAdjustment.Load();

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

            //a backup opened from a file manager, an email or the downloads
            //list, and a shared work list, which arrives the same ways. both
            //can land before this page exists - on a cold start the file is
            //what opened the app - so they are offered from here as well as
            //when they arrive. after _instance, which is what the hook works
            //through
            HookFileOpening();

            //a desktop opens a file by handing it to the app on the command
            //line
            UiInterface.ImportExport.BackupRestore.CheckCommandLine();

            WorkShare.LoadRecords();
            UiInterface.ImportExport.WorkShareOpen.CheckCommandLine();

            //the squeegee tab only shows while there is extra work to get
            //at. after _instance: it is what the refresh works on
            RefreshShareTabs();

            //said outright rather than left to the shell: the extra work
            //tabs sit first in the xaml (hidden until they are wanted), and
            //the first tab is what a shell falls back to
            SelectTab(tab_work);

            UiInterface.DataRefreshNotifier.DataChanged += () =>
                MainThread.BeginInvokeOnMainThread(RefreshBookedBadge);
            RefreshBookedBadge();
        }

        private static AppShell _instance;

        private static bool _hookedFileOpening;

        /// <summary>
        /// Listens once, for the life of the app, for a file being opened
        /// with Work Tracker.
        ///
        /// Both events are static and a shell can be built more than once -
        /// the crash log page builds a fresh one, and so does a restart of the
        /// app inside the same process. Subscribing from each shell left every
        /// shell that had ever been built listening, and the oldest - long
        /// since off screen - answered first, claimed the file and put its
        /// question up on a page nobody could see. So the hook goes on once
        /// and asks whichever shell is the current one.
        /// </summary>
        private static void HookFileOpening()
        {
            if (_hookedFileOpening)
                return;
            _hookedFileOpening = true;

            UiInterface.ImportExport.BackupRestore.Opened += () =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AppShell shell = _instance;
                    if (shell != null)
                        shell.OfferPendingBackup();
                });

            UiInterface.ImportExport.WorkShareOpen.Opened += () =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AppShell shell = _instance;
                    if (shell != null)
                        shell.OfferPendingShare();
                });
        }

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
            OfferPendingShare();
        }

        //  ---------------------------------------------  extra work tabs
        //
        //  Extra work is somebody else's round, sent over as a .rwk and
        //  opened with a PIN (Kernel/WorkShare.cs). While it is being
        //  worked the phone's own round is out of reach: the tab bar is cut
        //  down to Extra Work, My Work and Settings, and the way back out -
        //  the My Work gate - says the PIN will be wanted again. That is
        //  the point of the PIN: the list is not this phone's to leave
        //  lying open.

        /// <summary>true while the tab bar is cut down to the extra work</summary>
        public static bool InExtraWork { get; private set; }

        //  the shell's CurrentItem is the tab *bar* - there is only one -
        //  and the bar's CurrentItem is the tab. assigning a Tab straight to
        //  Shell.CurrentItem compiles, but only through an implicit
        //  conversion that wraps the tab in a new item, which is not
        //  selecting it - so the tab is always reached through the bar

        /// <summary>the tab the shell is standing on</summary>
        private static Tab SelectedTab()
        {
            return _instance?.CurrentItem?.CurrentItem as Tab;
        }

        /// <summary>moves the shell to this tab</summary>
        private static void SelectTab(Tab tab)
        {
            if (_instance?.CurrentItem != null && tab != null)
                _instance.CurrentItem.CurrentItem = tab;
        }

        /// <summary>
        /// puts the right tabs up for whichever side of the fence the app is
        /// on. called whenever extra work arrives, is removed, or is entered
        /// or left
        /// </summary>
        public static void RefreshShareTabs()
        {
            if (_instance == null)
                return;

            bool haveExtra = WorkShare.HaveExtraWork();

            if (InExtraWork && !haveExtra)
                InExtraWork = false;

            //the squeegee on the end of the normal tabs, the way in
            bool squeegeeOut = !InExtraWork && haveExtra;

            //the phone's own round
            _instance.tab_work.IsVisible = !InExtraWork;
            _instance.tab_booked.IsVisible = !InExtraWork;
            _instance.tab_calendar.IsVisible = !InExtraWork;
            _instance.tab_money.IsVisible = !InExtraWork;

            //  The squeegee takes the settings tab's slot rather than being
            //  a sixth tab. Android's bottom bar shows five: a sixth pushes
            //  the overflow behind a More tab of the platform's own, and the
            //  squeegee - the thing that only earns a place when it is one
            //  tap away - is exactly what ended up behind it.
            //
            //  So while the squeegee is out, the money tab is retitled More
            //  and settings joins it as a fourth page. Tapping it still
            //  lands on Payments, so nothing about the money pages moves -
            //  settings is one tap further away, and it is the tab that can
            //  afford that.
            _instance.tab_money.Title = squeegeeOut ? "More" : "Money";
            //moretab.png, not more.png: that one is stroked white for the
            //swipe actions, and a white icon disappears into a light tab bar
            _instance.tab_money.Icon = squeegeeOut ? "moretab.png" : "payments.png";

            //  Never hide the page the shell is standing on. Settings can be
            //  on screen through either of its two doors when the swap runs -
            //  extra work can arrive while it is open - so whichever door is
            //  closing, the other is opened and stepped through first.
            if (squeegeeOut)
            {
                _instance.sc_moneySettings.IsVisible = true;
                if (SelectedTab() == _instance.tab_settings)
                {
                    SelectTab(_instance.tab_money);
                    _instance.tab_money.CurrentItem = _instance.sc_moneySettings;
                }
                _instance.tab_settings.IsVisible = false;
            }
            else
            {
                _instance.tab_settings.IsVisible = true;
                if (_instance.tab_money.CurrentItem == _instance.sc_moneySettings)
                {
                    _instance.tab_money.CurrentItem = _instance.sc_moneyPayments;
                    if (!InExtraWork && SelectedTab() == _instance.tab_money)
                        SelectTab(_instance.tab_settings);
                }
                _instance.sc_moneySettings.IsVisible = false;
            }

            //the extra work side: the work itself, and the gate back out.
            //the gate only earns its place when there is any of your own
            //work to go back to
            _instance.tab_extraWork.IsVisible = InExtraWork;
            _instance.tab_myWork.IsVisible = InExtraWork && HasOwnWork();

            _instance.tab_extraShortcut.IsVisible = squeegeeOut;
        }

        /// <summary>
        /// anything of this phone's own worth going back to. a phone that
        /// only ever took on extra work has no My Work to offer
        /// </summary>
        private static bool HasOwnWork()
        {
            return Job.Query().Count > 0
                || Job.QueryQuotes().Count > 0
                || Customer.Query().Count > 0;
        }

        /// <summary>
        /// the PIN has been given: cut the tabs down to the extra work.
        /// called by the ExtraWork page once WorkShare is unlocked
        /// </summary>
        public static void EnterExtraWork()
        {
            if (_instance == null)
                return;

            InExtraWork = true;

            //the destination is made visible and current before anything is
            //hidden, so the shell is never left standing on a hidden tab
            _instance.tab_extraWork.IsVisible = true;
            SelectTab(_instance.tab_extraWork);
            RefreshShareTabs();
        }

        /// <summary>
        /// back to the phone's own round. the list and the PIN are forgotten
        /// with it, so getting back in means the PIN again
        /// </summary>
        public static void LeaveExtraWork()
        {
            if (_instance == null)
                return;

            WorkShare.Lock();
            InExtraWork = false;

            //same order as EnterExtraWork, for the same reason
            _instance.tab_work.IsVisible = true;
            SelectTab(_instance.tab_work);
            RefreshShareTabs();
        }

        /// <summary>
        /// the ExtraWork page could not unlock - the PIN prompt was walked
        /// away from. back to the normal tabs without asking anything
        /// </summary>
        public static void BackOutOfExtraWork()
        {
            if (_instance == null)
                return;

            if (InExtraWork)
                LeaveExtraWork();
            else
                SelectTab(_instance.tab_work);
        }

        private bool _askingAboutShare;

        /// <summary>
        /// Deals with a .rwk the phone has handed to the app. The plain
        /// header says which way the file is going, and that is everything
        /// the routing needs - no PIN is asked for here.
        ///
        /// Work sent to this phone is offered as extra work. Work coming
        /// back is matched to the record kept when it went out, opened with
        /// the PIN filed there, and put up for review - the sender never
        /// types the PIN again, which is why the key rides in the clear.
        /// </summary>
        private async void OfferPendingShare()
        {
            if (_askingAboutShare)
                return;

            //looked at rather than claimed, for the same reason the backup is:
            //this runs while the shell is still being built, and taking the
            //file before there was anywhere to ask lost it
            bool haveUnreadable = !string.IsNullOrWhiteSpace(
                UiInterface.ImportExport.WorkShareOpen.PeekPendingUnreadable());

            if (!haveUnreadable && string.IsNullOrWhiteSpace(
                    UiInterface.ImportExport.WorkShareOpen.PeekPending()))
                return;

            _askingAboutShare = true;

            try
            {
                Page page = await WaitForSomewhereToAsk();
                if (page == null)
                    return;         //still nowhere: it stays pending

                //a file that was opened with the app and could not be dealt
                //with gets told to the user, not swallowed - "nothing
                //happened" is a bug report waiting to be written
                //what to say is worked out where the failure was - a file
                //that is not one of ours and a file the sending app would not
                //part with read the same from here and have different answers
                string unreadable = UiInterface.ImportExport.WorkShareOpen.TakePendingUnreadable();
                if (!string.IsNullOrWhiteSpace(unreadable))
                {
                    await page.DisplayAlert("Opened File", unreadable, "Ok");
                    return;
                }

                string path = UiInterface.ImportExport.WorkShareOpen.TakePending();
                if (string.IsNullOrWhiteSpace(path))
                    return;

                WorkShareHeader header = WorkShare.ReadHeader(path);

                if (header == null)
                {
                    await page.DisplayAlert("Shared Work",
                        "That file is not a shared work list, or it has been damaged on the way.", "Ok");
                    return;
                }

                if (header.Kind == WorkShareKind.ReturnedWork)
                {
                    SentWorkRecord record = WorkShare.FindRecord(header.Key);
                    if (record == null)
                    {
                        await page.DisplayAlert("Returned Work",
                            "This work was not sent out from this device, so there is no PIN kept for it here. "
                            + "It can only be opened on the device it was sent from.", "Ok");
                        return;
                    }

                    SharedWorkData data = WorkShare.ReadFile(path, record.Pin);
                    if (data == null)
                    {
                        await page.DisplayAlert("Returned Work",
                            "That returned work could not be opened. The file may have been damaged on the way.", "Ok");
                        return;
                    }

                    await Navigation.PushAsync(new UiInterface.Layouts.ReturnedWork(data, record));
                    return;
                }

                //a sent file whose key is in this phone's own records is work
                //this phone sent out. offering to take it on would treat your
                //own round as somebody else's extra work - so it is named for
                //what it is, and can be looked at since the PIN is kept here
                SentWorkRecord mine = WorkShare.FindRecord(header.Key);
                if (mine != null)
                {
                    if (!await page.DisplayAlert("Sent Work",
                            $"This is the work you sent to {mine.WorkerTag} on {mine.SentOn.ToShortDateString()}. "
                            + "It is for them to open on their phone - the marks they make are what comes back to update your round. "
                            + "Do you want to look at what was sent?",
                            "Look At It", "Close"))
                        return;

                    SharedWorkData sent = WorkShare.ReadFile(path, mine.Pin);
                    if (sent == null)
                    {
                        await page.DisplayAlert("Sent Work",
                            "That file could not be opened. It may have been damaged since it was sent.", "Ok");
                        return;
                    }

                    await Navigation.PushAsync(new UiInterface.Layouts.ReturnedWork(sent, mine, sentPreview: true));
                    return;
                }

                //work sent to this phone
                string warning = WorkShare.HaveExtraWork()
                    ? "\n\nThere is already extra work on this phone, and taking this on replaces it."
                    : string.Empty;

                if (!await page.DisplayAlert("Extra Work",
                        $"This is a work list somebody has sent you. Take it on?{warning}", "Take It On", "Not Now"))
                    return;

                WorkShare.TakeOnExtraWork(path);
                RefreshShareTabs();

                if (await page.DisplayAlert("Extra Work",
                        "The work is on the Extra Work tab at the end of the tab bar. You will need the PIN it was sent with to open it.",
                        "Open It Now", "Later"))
                    SelectTab(tab_extraShortcut);
            }
            catch (Exception ex)
            {
                CrashLogger.Log("AppShell.OfferPendingShare", ex);

                try
                {
                    Page page = ReadyPage();
                    if (page != null)
                        await page.DisplayAlert("Shared Work", ex.Message, "Ok");
                }
                catch
                {
                }
            }
            finally
            {
                _askingAboutShare = false;
            }
        }

        private bool _askingAboutBackup;

        /// <summary>
        /// offers to put back a backup the phone has handed to the app.
        ///
        /// Only one question at a time: this is reached both when the file
        /// arrives and on every navigation, and being asked twice about the
        /// same file is how somebody ends up restoring it twice.
        ///
        /// The file is looked at before it is claimed. A backup that opened
        /// the app lands while the shell is still being built, and taking it
        /// first and then finding there was nowhere to put the question threw
        /// it away for good - which is the "it opens the app and nothing
        /// happens" this fixes. Now it is left where it is until there really
        /// is a page, so a later navigation can still offer it.
        /// </summary>
        private async void OfferPendingBackup()
        {
            if (_askingAboutBackup)
                return;

            if (string.IsNullOrWhiteSpace(UiInterface.ImportExport.BackupRestore.PeekPending()))
                return;

            _askingAboutBackup = true;

            try
            {
                Page page = await WaitForSomewhereToAsk();
                if (page == null)
                    return;         //still nowhere: it stays pending

                string path = UiInterface.ImportExport.BackupRestore.TakePending();
                if (string.IsNullOrWhiteSpace(path))
                    return;

                await UiInterface.ImportExport.BackupRestore.RestoreAsync(path, System.IO.Path.GetFileName(path), page);
            }
            catch (Exception ex)
            {
                //async void: an exception out of here is the app going down,
                //and it used to go down on the alert itself
                CrashLogger.Log("AppShell.OfferPendingBackup", ex);
            }
            finally
            {
                _askingAboutBackup = false;
            }
        }

        /// <summary>
        /// waits until there is a page that can actually put an alert up, and
        /// says so.
        ///
        /// A file opened from outside is what starts the app, so it arrives
        /// before the shell has a page - and an alert on a page with no
        /// handler behind it either throws or never comes back. Neither is
        /// visible to whoever opened the file: both read as nothing having
        /// happened. So the question waits for somewhere to be asked rather
        /// than being asked into thin air.
        /// </summary>
        private async Task<Page> WaitForSomewhereToAsk()
        {
            //about ten seconds, which is longer than a cold start and short
            //enough that a file opened into an app that never comes up is not
            //held on to for ever
            for (int i = 0; i < 40; i++)
            {
                Page page = ReadyPage();
                if (page != null)
                    return page;

                await Task.Delay(250);
            }

            return null;
        }

        /// <summary>a page an alert can be put on, or null</summary>
        private Page ReadyPage()
        {
            try
            {
                Page page = CurrentPage ?? (Page)this;

                //the handler is what says the page is on screen rather than
                //merely built - an alert on one that is not goes nowhere
                if (page?.Handler?.MauiContext == null)
                    return null;

                return page;
            }
            catch
            {
                return null;
            }
        }
    }
}