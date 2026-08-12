namespace UiInterface.Layouts;

using Kernel;
using Microsoft.Maui.Dispatching;

public partial class BookedWork : ContentPage
{
    public class BookingGroup : List<Job>
    {
        public string Header { get; set; }

        /// <summary>the day this work is booked for</summary>
        public DateTime Date { get; set; }

        public BookingGroup(string header, DateTime date, List<Job> jobs) : base(jobs)
        {
            Header = header;
            Date = date;
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

    private void Reload()
    {
        List<BookingGroup> groups = new List<BookingGroup>();

        List<Job> booked = Job.Query().FindAll(x => x.IsBookedIn && !x.HaveCanceled);

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
            //street order, then up the street by house number - the order the
            //day is actually worked. sorting on the formatted address sorted
            //by house number as text, which put 10 before 2 and split streets
            //up all over the list
            List<Job> dayInStreetOrder = day
                .OrderBy(x => x.SortStreet, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.SortHouseNumber)
                .ThenBy(x => x.SortHouseSuffix, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            groups.Add(new BookingGroup(header, day.Key, dayInStreetOrder));
        }

        cv_bookings.ItemsSource = groups;
    }

    //the day whose work is being moved, while the date bar is up
    private DateTime _dayToMove = DateTime.MinValue;

    /// <summary>
    /// puts up the date bar for a day. the work is not moved until a date is
    /// picked and confirmed
    /// </summary>
    private void On_Change_Day_Date(object sender, EventArgs e)
    {
        BookingGroup g = (sender as Element)?.BindingContext as BookingGroup;
        if (g == null)
            return;

        _dayToMove = g.Date.Date;
        l_moveDay.Text = $"Move all {g.Count} jobs booked for {g.Date:ddd dd MMM yyyy} to:";
        dp_moveTo.Date = g.Date.Date;
        vsl_moveDay.IsVisible = true;
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
            await DisplayAlert("Nothing To Move",
                $"Everything booked for {from:ddd dd MMM yyyy} is already done, so there is nothing to move.", "OK");
            CloseMoveDay();
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

    //there is no long press gesture of its own, so the hold is timed from the
    //finger going down: it counts once it has stayed put for half a second,
    //and a scroll or a swipe calls it off
    private const int HoldMilliseconds = 500;
    private const double HoldMoveTolerance = 20;

    private IDispatcherTimer _holdTimer;
    private Job _holdJob;
    private Point _holdFrom;
    private DateTime _optionsShownAt = DateTime.MinValue;

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
            _holdTimer.Tick += (s, a) => ShowHoldOptions();
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

    /// <summary>
    /// everything that can be done to a booked job, the two kept off the
    /// swipe included
    /// </summary>
    private async void ShowHoldOptions()
    {
        Job j = _holdJob;
        CancelHold();

        if (j == null)
            return;

        _optionsShownAt = DateTime.Now;

        string result = await DisplayActionSheet(j.JobFormattedStreet, "Close", null,
            "Done", "Done & Paid", "More", "Skip", "Cancel Job", "Unbook", "Edit Details");

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
            case "Done & Paid":
                //marking it paid writes it up as done as well
                await WorkPlanner.MarkJobPaid(j, this);
                Reload();
                break;
            case "More":
                WorkPlanner.ShowJobInfo(j, this);
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

    private void On_Job_Compleated(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;
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
        Job j = JobFrom(sender);
        if (j == null)
            return;
        WorkPlanner.ShowJobInfo(j, this);
    }

    private void On_Job_Details(object sender, EventArgs e)
    {
        Job j = JobFrom(sender);
        if (j == null)
            return;
        WorkPlanner.EditJobDetails(j, this);
    }
}
