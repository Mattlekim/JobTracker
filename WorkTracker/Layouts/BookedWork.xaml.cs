namespace UiInterface.Layouts;

using Kernel;
using Microsoft.Maui.Dispatching;
using UiInterface.Controles;

public partial class BookedWork : ContentPage, IHoldRows
{
    /// <summary>
    /// One booked day on the list. The rows the list draws are street
    /// headings with the day's houses under them - the same street format
    /// Layouts/AllJobs reads the round in - while <see cref="Jobs"/> keeps
    /// the actual work, which is what everything done to the day (sending,
    /// tagging, moving, cancelling) works off.
    /// </summary>
    public class BookingGroup : List<object>
    {
        /// <summary>the day's work itself, headings not included</summary>
        public List<Job> Jobs { get; }

        public string Header { get; set; }

        /// <summary>the day this work is booked for</summary>
        public DateTime Date { get; set; }

        /// <summary>how much of the day is done, e.g. "3 of 8 done, 5 left"</summary>
        public string Progress { get; set; } = string.Empty;

        /// <summary>
        /// what the day comes to and how much of that has been earned, e.g.
        /// "£45.00 of £120.00 done". The count cannot answer that on its own:
        /// eight houses of twelve is not two thirds of the money when the
        /// four left are the expensive ones
        /// </summary>
        public string DoneValue { get; set; } = string.Empty;

        public bool ShowDoneValue { get; set; }

        /// <summary>roughly how long what is left will take</summary>
        public string TimeLeft { get; set; } = string.Empty;

        public bool ShowTimeLeft { get; set; }

        /// <summary>the jobs on this day that have been done</summary>
        public int DoneCount { get; set; }

        /// <summary>the jobs on this day still to do</summary>
        public int LeftCount { get; set; }

        /// <summary>the day has been and gone and there is still work on it</summary>
        public bool IsOverdue { get; set; }

        public string OverdueText { get; set; } = string.Empty;

        public Color HeaderColour { get; set; } = Colors.Grey;

        /// <summary>the tags on this day's work, each one once</summary>
        public string TagsText { get; set; } = string.Empty;

        public bool HaveTags { get; set; }

        public BookingGroup(string header, DateTime date, List<Job> jobs) : base(StreetSplit.WithHeadings(jobs))
        {
            Jobs = jobs;
            Header = header;
            Date = date;
            WorkOutProgress();
            WorkOutOverdue();
            WorkOutTags();

            //a day that has been sent to somebody says so in its title, read
            //off the Sent To tags the send put on - a week planned out and
            //handed over should say whose hands each day is in at a glance
            List<string> outWith = WorkShare.OutWith(Jobs);
            if (outWith.Count > 0)
                Header = $"{Header} • With {string.Join(", ", outWith)}";
        }

        /// <summary>
        /// a tag put on a day goes on the work that day, so the day shows the
        /// tags its work is carrying rather than keeping any of its own
        /// </summary>
        private void WorkOutTags()
        {
            List<string> tags = Booking.TagsOn(Jobs);
            TagsText = string.Join(" • ", tags);
            HaveTags = tags.Count > 0;
        }

        /// <summary>
        /// work planned for a day that has passed and never got done. it is
        /// not finished, it is late, so it says so in red rather than sitting
        /// in the list looking like everything else
        /// </summary>
        private void WorkOutOverdue()
        {
            HeaderColour = Application.Current != null && Application.Current.PlatformAppTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black;

            int daysLate = (UsfulFuctions.DateNow.Date - Date.Date).Days;
            IsOverdue = daysLate > 0 && LeftCount > 0;

            if (!IsOverdue)
                return;

            HeaderColour = Color.FromArgb("#C62828");
            OverdueText = daysLate == 1
                ? $"1 day late - {LeftCount} not done"
                : $"{daysLate} days late - {LeftCount} not done";
        }

        /// <summary>
        /// How the day is going - and it is <see cref="DayProgress"/> that
        /// works it out, not this page. The calendar shows a day the same way
        /// and the two must not be able to disagree about how far through it
        /// you are, so the counting and the wording are in the kernel with
        /// the work.
        /// </summary>
        private void WorkOutProgress()
        {
            DayProgress day = DayProgress.For(Jobs);

            DoneCount = day.Done;
            LeftCount = day.Left;

            Progress = day.CountText;

            DoneValue = day.ValueText;
            ShowDoneValue = day.ShowValue;

            TimeLeft = day.TimeLeftText;
            ShowTimeLeft = day.ShowTimeLeft;
        }
    }

    public BookedWork()
    {
        InitializeComponent();
        //coming back to the page starts again: a date bar left open on the way
        //out is for a day that may no longer be the one on screen
        NavigatedTo += (s, e) =>
        {
            CloseMoveDay();
            Reload();
        };
        Reload();
    }

    /// <summary>the one day being looked at, or MinValue for the whole lot</summary>
    private DateTime _showingDay = DateTime.MinValue;

    /// <summary>the classic pull down: the days built again from the jobs</summary>
    private void rv_bookings_Refreshing(object sender, EventArgs e)
    {
        try
        {
            DataRefreshNotifier.RebuildBookings();
            Reload();
        }
        finally
        {
            rv_bookings.IsRefreshing = false;
        }
    }

    private void Reload()
    {
        //a day that has passed with all of its work done is a plan for a day
        //that is over - it clears itself away
        Booking.ClearFinishedPastDays();

        List<BookingGroup> groups = new List<BookingGroup>();

        List<Job> booked = Job.Query().FindAll(x => x.IsBookedIn && !x.HaveCanceled);

        BuildDayPicker(booked);

        if (_showingDay != DateTime.MinValue)
            booked = booked.FindAll(x => x.DateJobBookinFor.Date == _showingDay);

        //taking a payment on this page moves the customer's balance, so the
        //owed tag has to be worked out again every time the list is built
        foreach (Job j in booked)
        {
            j.Refresh();
            j.RefreshColors();
        }

        foreach (var day in booked.GroupBy(x => x.DateJobBookinFor.Date).OrderBy(x => x.Key))
        {
            float total = day.Sum(x => x.Price);
            string header = $"{day.Key:ddd dd MMM yyyy} - {day.Count()} jobs {Gloable.CurrenceSymbol}{total}";
            //street order and up the street by house number, with a heading
            //naming each street, is the group's own doing - StreetSplit, the
            //same rule All Jobs reads the round in. Done work stays on its
            //street with a Done chip rather than sinking to the bottom, so a
            //street is one run of houses however far through it the day is
            groups.Add(new BookingGroup(header, day.Key, day.ToList()));
        }

        cv_bookings.ItemsSource = groups;

        //marking work off changes what is overdue
        WorkTracker.AppShell.RefreshBookedBadge();
    }

    /// <summary>
    /// A button per booked day across the top, so a week planned out can be
    /// worked through a day at a time rather than as one long list. The day
    /// being looked at is kept if it is still booked, otherwise it falls back
    /// to showing everything.
    /// </summary>
    private void BuildDayPicker(List<Job> booked)
    {
        List<DateTime> days = new List<DateTime>();
        foreach (Job j in booked)
        {
            DateTime day = j.DateJobBookinFor.Date;
            if (!days.Contains(day))
                days.Add(day);
        }
        days.Sort();

        //the day being looked at has gone - back to showing the lot
        if (_showingDay != DateTime.MinValue && !days.Contains(_showingDay))
            _showingDay = DateTime.MinValue;

        hsl_dayPicker.Clear();

        //no point offering a choice of one
        sv_dayPicker.IsVisible = days.Count > 1;
        if (!sv_dayPicker.IsVisible)
            return;

        hsl_dayPicker.Add(DayButton("All Days", DateTime.MinValue, _showingDay == DateTime.MinValue, false));

        DateTime today = UsfulFuctions.DateNow.Date;
        foreach (DateTime day in days)
        {
            List<Job> onDay = booked.FindAll(x => x.DateJobBookinFor.Date == day);
            int left = onDay.FindAll(x => !x.IsCompleted).Count;
            bool late = day < today && left > 0;

            string text = day == today ? "Today" : day.ToString("ddd dd MMM");
            if (left > 0)
                text += $" ({left})";

            hsl_dayPicker.Add(DayButton(text, day, _showingDay == day, late));
        }
    }

    private Button DayButton(string text, DateTime day, bool showing, bool late)
    {
        string colour = late ? "#C62828" : "#1E88E5";

        Button b = new Button()
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(12, 4),
            CornerRadius = 8,
            BorderWidth = 2,
            BorderColor = Color.FromArgb(colour),
            BackgroundColor = showing ? Color.FromArgb(colour) : Colors.Transparent,
            TextColor = showing ? Colors.White : Color.FromArgb(colour),
            FontAttributes = showing ? FontAttributes.Bold : FontAttributes.None,
        };

        b.Clicked += (s, e) =>
        {
            _showingDay = day;
            CloseMoveDay();
            Reload();
        };

        return b;
    }

    //the day whose work is being moved, while the date bar is up
    private DateTime _dayToMove = DateTime.MinValue;

    /// <summary>
    /// puts up the date bar for a day. the work is not moved until a date is
    /// picked and confirmed
    /// </summary>
    /// <summary>
    /// Everything that can be done to a whole day, in one place.
    ///
    /// One button rather than three across the top of the day: the date and
    /// how far through it you are is the thing that has to be readable, and
    /// three buttons on a phone leave it nowhere to go.
    /// </summary>
    private async void On_Day_Options(object sender, EventArgs e)
    {
        BookingGroup g = (sender as Element)?.BindingContext as BookingGroup;
        if (g == null)
            return;

        //sending work out is opt-in on the settings page, so the menu only
        //offers it to a round that has turned it on - and a day already out
        //with somebody is not offered again, because two copies of the same
        //job with two people ends with the house cleaned twice or not at all
        List<string> options = new List<string>() { "Tag The Work", "Change The Date" };
        if (Settings.EnableWorkSharing
            && g.Jobs.Exists(x => x != null && x.CustomerId != -1
                && !x.IsCompleted && !x.HaveCanceled && !WorkShare.IsOut(x)))
            options.Add("Send To Someone");
        options.Add("Cancel The Booking");

        string result = await DisplayActionSheet($"{g.Date:ddd dd MMM yyyy}", "Close", null,
            options.ToArray());

        if (result == null)
            return;

        switch (result)
        {
            case "Tag The Work":
                await TagDay(g);
                break;
            case "Change The Date":
                ShowMoveDay(g);
                break;
            case "Send To Someone":
                await SendDay(g);
                break;
            case "Cancel The Booking":
                await CancelDay(g);
                break;
        }
    }

    /// <summary>
    /// hands the day's outstanding work to somebody else's copy of the app -
    /// the same Send Work the work list's selection toolbar goes through, so
    /// what travels and how it comes back are asked in the same words. what
    /// is already done or cancelled stays home: it is not work to hand over.
    /// sending changes nothing about the booking - the day is still this
    /// round's plan until the return says what happened to it.
    /// </summary>
    private async Task SendDay(BookingGroup g)
    {
        //what is done, cancelled or already out with somebody stays home
        List<Job> toSend = g.Jobs.FindAll(x => x != null && x.CustomerId != -1
            && !x.IsCompleted && !x.HaveCanceled && !WorkShare.IsOut(x));

        if (toSend.Count == 0)
        {
            await DisplayAlert("Send To Someone",
                "Everything on this day is already done or already out with somebody - there is nothing left to send.", "Ok");
            return;
        }

        await Navigation.PushAsync(new SendWork(toSend));
    }

    /// <summary>
    /// takes the whole day's work off the booking. the jobs themselves are
    /// not cancelled - they go back on the round to be done when they are
    /// due, which is what makes this different from cancelling the work
    /// </summary>
    private async Task CancelDay(BookingGroup g)
    {
        if (!await WorkPlanner.CancelBooking(g.Jobs, this, g.Date))
            return;

        //the bookings are a cache keyed on the day, so they are built again
        //now that nothing is booked for this one
        DataRefreshNotifier.RebuildBookings();
        Reload();
    }

    private void ShowMoveDay(BookingGroup g)
    {
        _dayToMove = g.Date.Date;

        //houses already done stay on the day they were done - say so here
        //rather than let the bar promise to move the lot
        string staying = g.DoneCount == 0
            ? string.Empty
            : g.DoneCount == 1
                ? " (1 already done stays put)"
                : $" ({g.DoneCount} already done stay put)";

        l_moveDay.Text = g.LeftCount == 1
            ? $"Move the 1 job still to do on {g.Date:ddd dd MMM yyyy} to:{staying}"
            : $"Move the {g.LeftCount} jobs still to do on {g.Date:ddd dd MMM yyyy} to:{staying}";

        dp_moveTo.Date = g.Date.Date;
        vsl_moveDay.IsVisible = true;
    }

    /// <summary>
    /// tags a whole day's work at once - the day was wet, or the whole street
    /// was done front only. the tag goes on each job on the day, because that
    /// is where it is any use afterwards
    /// </summary>
    private async Task TagDay(BookingGroup g)
    {
        if (await TagPicker.EditAsync(this, g.Jobs, $"The Work On {g.Date:ddd dd MMM}"))
            Reload();
    }

    private void On_Move_Day_Cancel(object sender, EventArgs e)
    {
        CloseMoveDay();
    }

    private void CloseMoveDay()
    {
        vsl_moveDay.IsVisible = false;
        _dayToMove = DateTime.MinValue;
    }

    /// <summary>
    /// moves everything still to do on one day over to another day
    /// </summary>
    private async void On_Move_Day_Confirm(object sender, EventArgs e)
    {
        if (_dayToMove == DateTime.MinValue)
            return;

        DateTime from = _dayToMove;
        DateTime to = dp_moveTo.Date.Date;

        if (to == from)
        {
            CloseMoveDay();
            return;
        }

        //work that is already done stays on the day it was done on, whatever
        //happens to the rest of that day
        List<Job> toMove = Job.Query().FindAll(x => x.IsBookedIn && !x.HaveCanceled
            && !x.IsCompleted && x.DateJobBookinFor.Date == from);

        if (toMove.Count == 0)
        {
            //the whole day is finished with, so the only thing left worth
            //doing to it is clearing it off the board
            await DisplayAlert("Nothing To Move",
                $"Everything booked for {from:ddd dd MMM yyyy} is already done, so there is nothing to move.", "OK");

            await OfferToClearDoneJobs(from);

            DataRefreshNotifier.RebuildBookings();
            CloseMoveDay();
            Reload();
            return;
        }

        //moving onto a day that already has work does not replace it, the two
        //days end up as one
        int alreadyBooked = Job.Query().FindAll(x => x.IsBookedIn && !x.HaveCanceled
            && x.DateJobBookinFor.Date == to).Count;
        string joining = alreadyBooked > 0
            ? $"\n\n{to:ddd dd MMM} already has {alreadyBooked} jobs booked in. The two days will join up."
            : string.Empty;

        if (!await DisplayAlert("Change Booking Date",
            $"Move all {toMove.Count} jobs booked for {from:ddd dd MMM yyyy} to {to:ddd dd MMM yyyy}?{joining}",
            "Move", "Cancel"))
            return;

        foreach (Job j in toMove)
            j.BookInJob(to);

        Job.Save();

        //what is left on the old day is work that is finished with. offer to
        //take it off the booking so the day clears out rather than sitting
        //there looking like there is still something to go back for
        await OfferToClearDoneJobs(from);

        //the bookings are a cache keyed on the day, so they have to be built
        //again now this work sits on a different one
        DataRefreshNotifier.RebuildBookings();

        CloseMoveDay();
        Reload();

        await OfferToNotify(toMove, to);
    }

    /// <summary>
    /// customers set to be told when work is coming are expecting it on the
    /// old day, so offer to let them know the new one
    /// </summary>
    private async Task OfferToNotify(List<Job> moved, DateTime to)
    {
        List<Job> toText = moved.FindAll(x => x.TNB);
        List<Job> toEmail = moved.FindAll(x => x.ENB);

        if (toText.Count == 0 && toEmail.Count == 0)
            return;

        string who = "The following customers may be expecting you.\n";
        foreach (Job j in moved)
            if (j.TNB || j.ENB)
                who = $"{who}\n{j.JobFormattedStreet}";

        who = $"{who}\n\nDo you wish to tell them you will now be coming on {to.ToShortDateString()}?";

        if (!await DisplayAlert("Notify Customers", who, "Yes", "No"))
            return;

        if (toText.Count > 0)
            await WorkPlanner.TextCustomers(toText, to, WorkPlanner.DefaultRearangeMessage, this);
        if (toEmail.Count > 0)
            await WorkPlanner.EmailCustomers(toEmail, to, WorkPlanner.DefaultRearangeMessage, this);
    }

    private void job_Tapped(object sender, TappedEventArgs e)
    {
        //a hold that has just put the options up must not open the job as well
        if ((DateTime.Now - _optionsShownAt).TotalMilliseconds < 1000)
            return;

        Job j = e.Parameter as Job;
        if (j == null)
            return;
        WorkPlanner.EditJobDetails(j, this);
    }

    //There is no long press gesture of its own. On a phone the hold is left
    //to android, through LongPressBehavior on the row, which is the only way
    //it happens at all: the pointer events below are raised for a mouse or a
    //stylus and never for a finger. On a desktop they still time it - the
    //press counts once it has stayed put for half a second, and a scroll or a
    //swipe calls it off.
    private const int HoldMilliseconds = 500;
    private const double HoldMoveTolerance = 20;

    private IDispatcherTimer _holdTimer;
    private Job _holdJob;
    private Point _holdFrom;
    private DateTime _optionsShownAt = DateTime.MinValue;
    private Job _lastHeld;

    private void Job_PointerPressed(object sender, PointerEventArgs e)
    {
        Element row = sender as Element;
        _holdJob = row?.BindingContext as Job;
        if (_holdJob == null)
            return;

        _holdFrom = e.GetPosition(row) ?? Point.Zero;

        if (_holdTimer == null)
        {
            _holdTimer = Dispatcher.CreateTimer();
            _holdTimer.Interval = TimeSpan.FromMilliseconds(HoldMilliseconds);
            _holdTimer.IsRepeating = false;
            _holdTimer.Tick += (s, a) => ShowHoldOptions(_holdJob);
        }

        _holdTimer.Stop();
        _holdTimer.Start();
    }

    private void Job_PointerMoved(object sender, PointerEventArgs e)
    {
        if (_holdJob == null)
            return;

        Point? now = e.GetPosition(sender as Element);
        if (now == null)
            return;

        if (Math.Abs(now.Value.X - _holdFrom.X) > HoldMoveTolerance
            || Math.Abs(now.Value.Y - _holdFrom.Y) > HoldMoveTolerance)
            CancelHold();
    }

    private void Job_PointerReleased(object sender, PointerEventArgs e)
    {
        CancelHold();
    }

    private void CancelHold()
    {
        _holdTimer?.Stop();
        _holdJob = null;
    }

    /// <summary>a row has been held on a platform that has a long press of its own</summary>
    public void RowHeld(object item)
    {
        ShowHoldOptions(item as Job);
    }

    /// <summary>when the swipe on a row began, and what it was on</summary>
    private DateTime _swipeStartedAt = DateTime.MinValue;
    private Job _swipeJob;

    private void swip_started(object sender, SwipeStartedEventArgs e)
    {
        SwipeView sv = sender as SwipeView;

        _swipeStartedAt = DateTime.Now;
        _swipeJob = sv == null ? null : sv.BindingContext as Job;
    }

    /// <summary>
    /// A press held on a row arrives as a swipe: the rows are SwipeViews, and
    /// the swipe takes the finger the moment it moves at all, which a finger
    /// held on a phone always does. The long press android would have raised
    /// is cancelled with it, so the row never hears about the hold.
    ///
    /// A swipe that ran as long as a hold and opened nothing was not a swipe.
    /// Same as the work list, so a hold means the same thing on both.
    /// </summary>
    private void swip_ended(object sender, SwipeEndedEventArgs e)
    {
        Job j = _swipeJob;
        DateTime started = _swipeStartedAt;

        _swipeJob = null;
        _swipeStartedAt = DateTime.MinValue;

        if (e.IsOpen || j == null || started == DateTime.MinValue)
            return;

        if ((DateTime.Now - started).TotalMilliseconds < HoldMilliseconds)
            return;

        ShowHoldOptions(j);
    }

    /// <summary>
    /// everything that can be done to a booked job, the two kept off the
    /// swipe included
    /// </summary>
    private async void ShowHoldOptions(Job j)
    {
        CancelHold();

        if (j == null)
            return;

        //a platform that both has a long press and raises the pointer events
        //would otherwise put the sheet up twice
        if (ReferenceEquals(j, _lastHeld) && (DateTime.Now - _optionsShownAt).TotalMilliseconds < 1000)
            return;

        _lastHeld = j;
        _optionsShownAt = DateTime.Now;

        //a job already marked wants clearing or opening up, not marking again
        string[] options = j.IsMarked
            ? new[] { "Clear", "More", "Tag", "Skip", "Cancel Job", "Unbook", "Edit Details" }
            : new[] { "Done", "Done & Paid", "More", "Tag", "Skip", "Cancel Job", "Unbook", "Edit Details" };

        string result = await DisplayActionSheet(j.JobFormattedStreet, "Close", null, options);

        //the finger comes off the row while the sheet is up, so the guard has
        //to still be running when it closes
        _optionsShownAt = DateTime.Now;

        if (result == null)
            return;

        switch (result)
        {
            case "Done":
                WorkPlanner.MarkJobDone(j, this);
                Reload();
                break;
            case "Clear":
                await WorkPlanner.ClearJob(j, this);
                Reload();
                break;
            case "Done & Paid":
                //marking it paid writes it up as done as well
                await WorkPlanner.MarkJobPaid(j, this);
                Reload();
                break;
            case "More":
                WorkPlanner.ShowJobStatus(j, this, Reload);
                break;
            case "Tag":
                await TagJob(j);
                break;
            case "Skip":
                WorkPlanner.MarkJobSkipped(j);
                Reload();
                break;
            case "Cancel Job":
                await WorkPlanner.MarkJobCancled(j, this);
                Reload();
                break;
            case "Unbook":
                await UnbookJob(j);
                break;
            case "Edit Details":
                WorkPlanner.EditJobDetails(j, this);
                break;
        }
    }

    //swipe items and context menu items are both MenuItems carrying the job
    private static Job JobFrom(object sender)
        => (sender as MenuItem)?.CommandParameter as Job;

    private async void On_Job_Compleated(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;

        //this slot says Clear once the job has been marked
        if (j.IsMarked)
            await WorkPlanner.ClearJob(j, this);
        else
            WorkPlanner.MarkJobDone(j, this);

        Reload();
    }

    private async void On_Job_Paid(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;
        await WorkPlanner.MarkJobPaid(j, this);
        Reload();
    }

    private void On_Job_Skipped(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;
        WorkPlanner.MarkJobSkipped(j);
        Reload();
    }

    private async void On_Job_Canceled(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;
        await WorkPlanner.MarkJobCancled(j, this);
        Reload();
    }

    /// <summary>
    /// takes one job back off the day it was booked for, leaving the rest of
    /// that day's work booked in
    /// </summary>
    private async void On_Job_Unbook(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;

        await UnbookJob(j);
    }

    /// <summary>
    /// Takes the finished houses off a day's booking, once the rest of that
    /// day has been moved somewhere else. They keep their done mark and the
    /// date they were done on - all that goes is the booking, which has
    /// nothing left to say once the day it belonged to has moved on.
    /// </summary>
    private async Task OfferToClearDoneJobs(DateTime day)
    {
        List<Job> done = Job.Query().FindAll(x => x.IsBookedIn && !x.HaveCanceled
            && x.IsCompleted && x.DateJobBookinFor.Date == day.Date);

        if (done.Count == 0)
            return;

        string what = done.Count == 1
            ? $"1 house on {day:ddd dd MMM yyyy} is already done and has stayed there."
            : $"{done.Count} houses on {day:ddd dd MMM yyyy} are already done and have stayed there.";

        if (!await DisplayAlert("Clear The Day?",
            $"{what}\n\nTake them off the booking so the day clears? The work stays marked done either way.",
            "Take Them Off", "Leave Them"))
            return;

        foreach (Job j in done)
            Booking.RemoveJobFromBooking(j);

        Job.Save();
    }

    private async Task UnbookJob(Job j)
    {
        if (j == null)
            return;

        if (!await DisplayAlert("Unbook Job",
            $"Take {j.JobFormattedStreet} off {j.DateJobBookinFor.ToShortDateString()}? The rest of that day stays booked in.",
            "Unbook", "Cancel"))
            return;

        Booking.RemoveJobFromBooking(j);
        Job.Save();
        j.Refresh();
        Reload();
    }

    private void On_Job_More(object sender, EventArgs e)
    {
        WorkPlanner.ShowJobStatus(JobFrom(sender), this, Reload);
    }

    private async void On_Job_Tag(object sender, EventArgs e)
    {
        await TagJob(JobFrom(sender));
    }

    /// <summary>tags this one house, leaving the rest of the day alone</summary>
    private async Task TagJob(Job j)
    {
        if (j == null)
            return;

        if (await TagPicker.EditAsync(this, new List<Job>() { j }, j.JobFormattedStreet))
            Reload();
    }

    private async void On_Job_DoAgain(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;

        if (await WorkPlanner.DoJobAgain(j, this))
            Reload();
    }

    private void On_Job_Details(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;
        WorkPlanner.EditJobDetails(j, this);
    }
}
