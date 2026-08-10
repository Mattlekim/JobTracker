namespace UiInterface.Layouts;

using Kernel;

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
        Job j = e.Parameter as Job;
        if (j == null)
            return;
        WorkPlanner.EditJobDetails(j, this);
    }
}
