namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Every job on the round, once each.
///
/// The work list is the work in hand - a fortnight of it - and the paper view
/// is the sheet you take out with you. Neither of them answers "what have I
/// actually got?", because the job list keeps every visit of a house: the
/// clean done last month, the one before it, and the one still to come are
/// three entries for one house. So this page shows one row per **job** rather
/// than per visit, found through <see cref="Job.SameJobKey"/> - the base job
/// every visit is copied from - and the row is the visit that is next due,
/// which is what the house is next wanted for.
///
/// A house is on the round while it has work outstanding, so the finished
/// visits and the cancelled ones are not on here. A finished one off is not
/// on the round any more and is not listed either.
///
/// It is grouped the way a round is actually thought about: which round, then
/// the area, the town and the street. Every level is a heading rather than an
/// indent, because a phone has no room to indent four deep, and a level the
/// round has nothing to say about - most rounds have no area - draws no
/// heading at all instead of a row of blanks.
/// </summary>
public partial class AllJobs : ContentPage
{
    /// <summary>the round of each job, keyed by Job.SameJobKey</summary>
    private Dictionary<string, string> _rounds = new Dictionary<string, string>();

    public AllJobs()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Build();
    }

    private void Build()
    {
        List<Job> all = new List<Job>(Job.Query());

        //the round belongs to the job rather than to one visit of it, so it
        //is read off the whole job. The same call the stats page makes, so
        //the two pages cannot put a house on different rounds
        _rounds = Job.RoundsOfEveryJob(all);

        List<Job> jobs = OneVisitPerJob(all);

        jobs = jobs
            //work nobody has put on a round goes last rather than first, the
            //same as the work list and the paper view sort it
            .OrderBy(x => RoundOf(x).Length == 0 ? 1 : 0)
            .ThenBy(x => RoundOf(x), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => AreaOf(x), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => CityOf(x), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SortStreet, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SortHouseNumber)
            .ThenBy(x => x.SortHouseSuffix, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ShowTotals(jobs);

        l_empty.IsVisible = jobs.Count == 0;
        cv_jobs.IsVisible = jobs.Count > 0;
        cv_jobs.ItemsSource = Rows(jobs);
    }

    /// <summary>
    /// One visit per job: the one that is next due.
    ///
    /// The whole point of the page. Every visit of a house is in the job list
    /// - the ones already done and the one still to come - and listing them
    /// all would put the same house on the page a dozen times over.
    /// </summary>
    private static List<Job> OneVisitPerJob(List<Job> all)
    {
        Dictionary<string, Job> jobs = new Dictionary<string, Job>();

        foreach (Job j in all)
        {
            //finished and cancelled work is not on the round. the booking
            //summary rows are not work at all
            if (j.IsCompleted || j.HaveCanceled || j.CustomerId == -1)
                continue;

            Job kept;
            jobs.TryGetValue(j.SameJobKey, out kept);
            jobs[j.SameJobKey] = Job.NextDue(kept, j);
        }

        return new List<Job>(jobs.Values);
    }

    private void ShowTotals(List<Job> jobs)
    {
        float value = 0;
        float owed = 0;
        HashSet<int> counted = new HashSet<int>();

        foreach (Job j in jobs)
        {
            value += j.EffectivePrice;

            //a balance belongs to a customer, not to a job, so somebody with
            //three houses on the round is only owing the once
            if (!counted.Add(j.CustomerId))
                continue;

            Customer c = j.GetCustomer();
            if (c != null && c.Balance > 0)
                owed += c.Balance;
        }

        l_total.Text = jobs.Count == 0
            ? "No jobs on the round"
            : $"{(jobs.Count == 1 ? "1 job" : $"{jobs.Count} jobs")} · {Money(value)} a time round · {Money(owed)} owed";

        l_note.Text = "Every job on the round, one row each rather than one per visit, grouped by round, "
            + "area, town and street. Tap a house to open it.";
    }

    /// <summary>
    /// the list as it is drawn: a heading whenever the round, the area, the
    /// town or the street changes, and a row for each house under it
    /// </summary>
    private List<AllJobsRow> Rows(List<Job> jobs)
    {
        List<AllJobsRow> rows = new List<AllJobsRow>();

        //null rather than empty, so the first job always starts a group even
        //when what it is grouped under has no name
        string round = null;
        string area = null;
        string city = null;
        string street = null;

        foreach (Job j in jobs)
        {
            if (!Same(round, RoundOf(j)))
            {
                round = RoundOf(j);
                area = city = street = null;
                rows.Add(AllJobsRow.AsHeading(round.Length == 0 ? RoundStats.NoRound : round, HeadingLevel.Round));
            }

            if (!Same(area, AreaOf(j)))
            {
                area = AreaOf(j);
                city = street = null;

                //most rounds have no area at all, and a heading saying
                //nothing is worse than no heading
                if (area.Length > 0)
                    rows.Add(AllJobsRow.AsHeading(j.JobFormattedArea.Trim(), HeadingLevel.Place));
            }

            if (!Same(city, CityOf(j)))
            {
                city = CityOf(j);
                street = null;

                if (city.Length > 0)
                    rows.Add(AllJobsRow.AsHeading(j.JobFormattedCity.Trim(), HeadingLevel.Place));
            }

            if (!Same(street, j.SortStreet))
            {
                street = j.SortStreet;
                rows.Add(AllJobsRow.AsHeading(street.Length == 0 ? "No Street" : j.JobFormattedStreetOnly.Trim(), HeadingLevel.Street));
            }

            rows.Add(Row(j));
        }

        return rows;
    }

    private AllJobsRow Row(Job job)
    {
        AllJobsRow row = new AllJobsRow();
        row.Job = job;

        //the street is already the heading above the row, so the house only
        //has to say which one it is. A house with no number at all falls back
        //to whoever lives there, which is the next best way to tell it apart
        string number = job.JobFormattedHouseNumber.Trim();
        Customer c = job.GetCustomer();
        string who = c == null ? string.Empty : $"{c.FName} {c.SName}".Trim();

        if (number.Length > 0)
            row.Where = number;
        else if (who.Length > 0)
            row.Where = who;
        else
            row.Where = "(no address)";

        row.Price = Money(job.EffectivePrice);

        //days past the day it fell due. positive is overdue, which is the way
        //round somebody stood in front of the house wants to read it
        int days = (int)(UsfulFuctions.DateNow.Date - job.DueDate.Date).TotalDays;
        row.Due = $"Due {job.DueDate.ToString("ddd d MMM yyyy")}";

        if (days > 0)
        {
            row.Due = $"{row.Due} · {days} day{(days == 1 ? string.Empty : "s")} overdue";
            row.DueColour = Color.FromArgb("#C62828");
        }
        else if (days == 0)
        {
            row.Due = $"{row.Due} · today";
            row.DueColour = Color.FromArgb("#2E7D32");
        }
        else
        {
            row.DueColour = Color.FromArgb("#6B7280");
        }

        List<string> about = new List<string>();
        about.Add(HowOften(job));

        string round = RoundOf(job);
        about.Add(round.Length == 0 ? RoundStats.NoRound : round);

        row.HowOftenAndRound = string.Join(" · ", about);

        float balance = c == null ? 0 : c.Balance;

        if (c == null)
        {
            row.Balance = string.Empty;
            row.BalanceColour = Colors.Transparent;
        }
        else if (balance > 0)
        {
            row.Balance = $"Owes {Money(balance)}";
            row.BalanceColour = Color.FromArgb("#C62828");
        }
        else if (balance < 0)
        {
            row.Balance = $"{Money(Math.Abs(balance))} in credit";
            row.BalanceColour = Color.FromArgb("#2E7D32");
        }
        else
        {
            row.Balance = "Nothing owed";
            row.BalanceColour = Color.FromArgb("#6B7280");
        }

        return row;
    }

    /// <summary>how often the house comes round, in words</summary>
    private static string HowOften(Job job)
    {
        if (job.IsOneOff)
            return "One off";

        string unit = job.Frequence_Type.ToString().ToLower();
        return $"Every {job.Frequence} {unit}{(job.Frequence == 1 ? string.Empty : "s")}";
    }

    private string RoundOf(Job job)
    {
        string round;
        if (_rounds.TryGetValue(job.SameJobKey, out round))
            return round;

        return job.HaveRound ? job.Round.Trim() : string.Empty;
    }

    /// <summary>
    /// the real area and town rather than the ones on screen. Screenshot mode
    /// swaps the names shown for made up ones, and grouping on those would
    /// group on whatever the swap happened to produce - see
    /// Kernel/ScreenshotMode. The headings are drawn from the display names
    /// </summary>
    private static string AreaOf(Job job)
    {
        return job.Address == null || job.Address.Area == null ? string.Empty : job.Address.Area.Trim();
    }

    private static string CityOf(Job job)
    {
        return job.Address == null || job.Address.City == null ? string.Empty : job.Address.City.Trim();
    }

    /// <summary>
    /// whether a job is still in the group being drawn. null is not the same
    /// as blank here: null means no group has been started yet
    /// </summary>
    private static bool Same(string sofar, string next)
    {
        return sofar != null && string.Equals(sofar, next, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string Money(float amount)
    {
        return $"{Gloable.CurrenceSymbol}{amount:0.00}";
    }

    /// <summary>
    /// tapping a house opens it, the same window the info button on the work
    /// list opens. The headings are on the same list and are not jobs, so
    /// they are ignored
    /// </summary>
    private void cv_jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AllJobsRow row = cv_jobs.SelectedItem as AllJobsRow;

        //cleared straight away, so the same house can be opened twice running
        cv_jobs.SelectedItem = null;

        if (row == null || row.Job == null)
            return;

        WorkPlanner.ShowJobInfo(row.Job, this);
    }
}

/// <summary>
/// what a heading names. The area and the town are the same kind of thing to
/// look at - where the work is rather than how it is organised - so they share
/// a level and read the same
/// </summary>
public enum HeadingLevel
{
    Round,
    Place,
    Street,
}

/// <summary>
/// a row on the All Jobs list. Either a heading or a house - one list holds
/// both, so the whole page virtualises rather than building every group as a
/// stack of views a long round would choke on
/// </summary>
public class AllJobsRow
{
    public static AllJobsRow AsHeading(string text, HeadingLevel level)
    {
        return new AllJobsRow()
        {
            HeadingText = text,
            Level = level,
        };
    }

    /// <summary>the house this row is, or null on a heading</summary>
    public Job Job { get; set; }

    private HeadingLevel Level { get; set; }

    private string HeadingText { get; set; }

    public string Heading
    {
        get { return HeadingText ?? string.Empty; }
    }

    public bool IsJob
    {
        get { return Job != null; }
    }

    public bool IsRoundHeading
    {
        get { return !IsJob && Level == HeadingLevel.Round; }
    }

    public bool IsPlaceHeading
    {
        get { return !IsJob && Level == HeadingLevel.Place; }
    }

    public bool IsStreetHeading
    {
        get { return !IsJob && Level == HeadingLevel.Street; }
    }

    public string Where { get; set; }

    public string Price { get; set; }

    public string Due { get; set; }

    public Color DueColour { get; set; }

    public string HowOftenAndRound { get; set; }

    public string Balance { get; set; }

    public Color BalanceColour { get; set; }
}
