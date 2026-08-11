namespace UiInterface.Layouts;

using Kernel;
using Microsoft.Maui.Dispatching;

public partial class BookedWork : ContentPage
{
    public class BookingGroup : List<Job>
    {
        public string Header { get; set; }

        public BookingGroup(string header, List<Job> jobs) : base(jobs)
        {
            Header = header;
        }
    }

    public BookedWork()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Reload();
        Reload();
    }

    private void Reload()
    {
        List<BookingGroup> groups = new List<BookingGroup>();

        List<Job> booked = Job.Query().FindAll(x => x.IsBookedIn && !x.HaveCanceled);
        foreach (var day in booked.GroupBy(x => x.DateJobBookinFor.Date).OrderBy(x => x.Key))
        {
            float total = day.Sum(x => x.Price);
            string header = $"{day.Key:ddd dd MMM yyyy} - {day.Count()} jobs {Gloable.CurrenceSymbol}{total}";
            groups.Add(new BookingGroup(header, day.OrderBy(x => x.JobFormattedStreet).ToList()));
        }

        cv_bookings.ItemsSource = groups;
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
