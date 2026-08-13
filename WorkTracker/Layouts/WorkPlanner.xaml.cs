namespace UiInterface.Layouts;
using System.Diagnostics;
using Kernel;
using System.Collections.ObjectModel;
/*#if ANDROID
using Android.Telephony;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;

using Android.Content;
using Android;
using Android.Content.PM;
#endif

{AppThemeBinding Light=White, Dark=Black}
*/
using System.ComponentModel;

public class BookingCatch
{
    public string Date;
    public List<Job> Jobs;
}
public partial class WorkPlanner : ContentPage
{

    private const string bntAddText = "Add Job";
    private const string bntBookWorkText = "Select Jobs";
    private const string bntFilterText = "Filter";

    public enum SecondryFilterType
    {
        None,
        JobType,
        Owed,
        NothingOwed,
        JobPrice,
        Credit,
        Street,
        City,
        Area
    }


    public ToolbarItem bnt_Filters;
    public ToolbarItem bnt_addNewJob;
    public ToolbarItem bnt_selectJobs;
    public ToolbarItem bnt_bookInWork;
    public ToolbarItem bnt_textCustomers;
    public ToolbarItem bnt_CreateGroup;

    public ToolbarItem bnt_cancelSelection;

    public void UpdateToolBarSelectJobs()
    {
        this.ToolbarItems.Clear();
        this.ToolbarItems.Add(bnt_cancelSelection);
        this.ToolbarItems.Add(bnt_bookInWork);
        this.ToolbarItems.Add(bnt_textCustomers);
        this.ToolbarItems.Add(bnt_CreateGroup);
    }

    public void UpdateToolBarNoraml()
    {

        this.ToolbarItems.Clear();
        this.ToolbarItems.Add(bnt_Filters);
        this.ToolbarItems.Add(bnt_addNewJob);
        this.ToolbarItems.Add(bnt_selectJobs);

     
    }

    public void UpdateToolBarViewBooking()
    {
        this.ToolbarItems.Clear();
    }
    public WorkPlanner()
    {
        Job.RefreshJobs();
        List<Job> tmpJobs = Job.Query();

        InitializeComponent();

        int jCount = Job.Query().Count;

        bnt_Filters = new ToolbarItem();
        bnt_Filters.Text = "Filters";
        bnt_Filters.Clicked += l_filterText_Clicked;

        bnt_addNewJob = new ToolbarItem();
        bnt_addNewJob.Text = "Add Job";
        bnt_addNewJob.Clicked += bnt_addJob_Clicked;

        bnt_selectJobs = new ToolbarItem();
        bnt_selectJobs.Text = "Select Jobs";
        bnt_selectJobs.Clicked += bnt_selectJobs_Clicked;

        bnt_bookInWork = new ToolbarItem();
        bnt_bookInWork.Text = "Bookin Work";
        bnt_bookInWork.Clicked += bnt_BookinWork_Clicked;

        bnt_textCustomers = new ToolbarItem();
        bnt_textCustomers.Text = "Text Jobs";
 
        bnt_textCustomers.Clicked += bnt_textCustomer_Clicked;
        bnt_textCustomers.Order = ToolbarItemOrder.Secondary;

        bnt_CreateGroup = new ToolbarItem();
        bnt_CreateGroup.Text = "Create Group";
        bnt_CreateGroup.IsEnabled = false;
        bnt_CreateGroup.Clicked += Bnt_textCreateGroup_Clicked;
        bnt_CreateGroup.Order = ToolbarItemOrder.Secondary;

        bnt_cancelSelection = new ToolbarItem();
        bnt_cancelSelection.Text = "Cancel Select Jobs";
        bnt_cancelSelection.Clicked += Bnt_cancelSelection_Clicked;

        if (jCount > 0)
            this.ToolbarItems.Add(bnt_Filters);
        this.ToolbarItems.Add(bnt_addNewJob);
        if (jCount > 0)
            this.ToolbarItems.Add(bnt_selectJobs);

        ResetDateFilter();
        dp_StartSearchDate.Date = StartFilterDate;
        dp_EndSearchDate.Date = EndFilterDate;

        //built from the booked jobs rather than added to what is already
        //there - opening this page used to pile another copy of every
        //booking on top of the last
        DataRefreshNotifier.RebuildBookings();

        NavigatedTo += WorkPlanner_NavigatedTo;
        SizeChanged += (s, e) => UpdateMoreInfoLayout();
    }

    // minimum window width before the more-info panel docks beside the job list
    private const double MoreInfoSideBySideMinWidth = 900;

    private void UpdateMoreInfoLayout()
    {
        bool sideBySide = g_more.IsVisible && Width >= MoreInfoSideBySideMinWidth;

        if (sideBySide)
        {
            cd_sidePanel.Width = new GridLength(0.45, GridUnitType.Star);
            Grid.SetRow(g_more, 0);
            Grid.SetRowSpan(g_more, 4);
            Grid.SetColumn(g_more, 1);
            Grid.SetColumnSpan(g_more, 1);
            Grid.SetColumnSpan(vsl_header, 1);
            Grid.SetColumnSpan(g_undoCancel, 1);
            Grid.SetColumnSpan(lv_Jobs, 1);
        }
        else
        {
            cd_sidePanel.Width = new GridLength(0);
            Grid.SetRow(g_more, 1);
            Grid.SetRowSpan(g_more, 1);
            Grid.SetColumn(g_more, 0);
            Grid.SetColumnSpan(g_more, 2);
            Grid.SetColumnSpan(vsl_header, 2);
            Grid.SetColumnSpan(g_undoCancel, 2);
            Grid.SetColumnSpan(lv_Jobs, 2);
        }
    }

    private void Bnt_textCreateGroup_Clicked(object sender, EventArgs e)
    {
        throw new NotImplementedException();
    }

  

    private void CancelSelectingJobs()
    {
     
        _selectingJobs = false;
        //Job.SelectionModeEnabled = _selectingJobs;
        foreach (Job j in _tmpJobs)
            j.SelectionModeEnabled = _selectingJobs;


        var vt = lv_Jobs.GetVisualTreeDescendants();
        CheckBox cb;
        SwipeView sv;
        foreach (object o in vt)
        {
            sv = o as SwipeView;
            if (sv != null)
            {
                sv.IsEnabled = true;
            }
        }
        UpdateToolBarNoraml();
    }
    private void Bnt_cancelSelection_Clicked(object sender, EventArgs e)
    {
        CancelSelectingJobs();
    }

    /// <summary>what the tag filter is matching, for saying so above the list</summary>
    private string FilterString;

    /// <summary>the tag filter itself, or null while the list is the round</summary>
    private Func<List<Job>> Filter;
    private void WorkPlanner_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        if (!Settings.HaveShowenJobIntro)
        {
            Navigation.PushAsync(new TutorialWorkPlanner());
            Settings.HaveShowenJobIntro = true;
            Settings.Save();
            return;
        }

        RefreshPage();
    }

    private List<Job> GetJobs()
    {
      

        List<Job> jobs;

        //a tag filter narrows the round to one thing - a street, a price, a
        //job type. it used to be thrown away here rather than run, because
        //there was no way of telling it was on and no obvious way back out.
        //what says so is ShowActiveFilter, and the Clear on the bar it puts up
        if (Filter != null)
            jobs = Filter();
        else
            jobs = MasterFilter();

        foreach (Job j in jobs)
            if (j.IsCompleted)
                j.tmpDate = j.DateCompleated;
            else
                j.tmpDate = j.DueDate;

        jobs = jobs.OrderBy(x => x.tmpDate).ToList();

        //the booking summary rows are the round's diary, not work matching
        //what was tapped, so they stay off a filtered list
        if (!ViewBooking && Filter == null)
        foreach (Booking b in Booking.Bookings)
                jobs.Insert(0,b.BookingInfo);

      
        //what is typed in the search box narrows whatever the filters left.
        //the booking summary rows are not jobs, so they go while searching
        if (!string.IsNullOrWhiteSpace(_searchText))
            jobs = jobs.FindAll(x => x.CustomerId != -1 && x.MatchesSearch(_searchText));

        //counted after the search rather than before it, so the bar says how
        //much work is actually on the list. the booking rows are not work
        ShowActiveFilter(jobs.FindAll(x => x.CustomerId != -1).Count);

        //quotes are not on this list. they are not due and cannot be done, so
        //they have their own page under Work - see Layouts/Quotes
        return jobs;
    }

    /// <summary>what is typed in the search box, empty for the whole round</summary>
    private string _searchText = string.Empty;

    private void tbi_Search_Clicked(object sender, EventArgs e)
    {
        g_search.IsVisible = !g_search.IsVisible;

        if (g_search.IsVisible)
        {
            e_search.Focus();
            return;
        }

        //closing the box puts the whole round back
        if (_searchText.Length > 0)
        {
            _searchText = string.Empty;
            e_search.Text = string.Empty;
            RefreshPage();
        }
    }

    private void e_search_Changed(object sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue ?? string.Empty;
        RefreshPage();
    }

    private void bnt_clearSearch_Clicked(object sender, EventArgs e)
    {
        e_search.Text = string.Empty;
    }

    private void Lv_Jobs_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        return;
        ListView lv = sender as ListView;
        IReadOnlyList<IVisualTreeElement> v = lv.GetVisualTreeDescendants();
        int i = 0;
        foreach (IVisualTreeElement vchild in v)
            if (vchild.GetType() == typeof(SwipeView))
            {
                if (i == e.ItemIndex)
                {
                    SwipeView sv = vchild as SwipeView;
                    sv.Open(OpenSwipeItem.LeftItems);

                }
                i++;
            }
    }


    private Job _currentJob;
    private Job GetJobForSwipe(object sender)
    {
        //  List<Job> j = Job.Query(QueryType.JobId, Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
        if (_sourceJobs == null)
            return null;
        Job j = _sourceJobs.FirstOrDefault(x => x.Id == Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
        if (j != null && j.CustomerId == -1) //booking summary rows are not real jobs
            return null;

        return j;
    }
    private async void On_Job_Compleated(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;

        //this slot says Clear once the job has been marked
        if (j.IsMarked)
            await ClearJob(j, this);
        else
            MarkJobDone(j, this);
    }

    private ObservableCollection<Job> _sourceJobs = new ObservableCollection<Job>();
    private List<Job> _tmpJobs = new List<Job>();
    private int _jobsToAddFrom = 0;
    private bool _isRefreshing = false;

    private void RPage()
    {


        altColour = false;
        //lv_Jobs.ItemsSource = null;

        //lv_Jobs.ItemsSource = GetJobs(fullrefresh);

        Job.RefreshJobs();
        _tmpJobs = GetJobs();
        bool darkTheme = Application.Current.PlatformAppTheme == AppTheme.Dark;
        for (int i = 0; i < _tmpJobs.Count; i++)
        {
            if (darkTheme)
                _tmpJobs[i].AltColour = altColour ? MainColorDark : altColorDark;
            else
                _tmpJobs[i].AltColour = altColour ? MainColor : altColor;
            altColour = !altColour;
        }

        //swap the whole collection in one go: clearing and re-adding one job at a
        //time makes the list re-layout on every add, which is what made this page slow
        _sourceJobs = new ObservableCollection<Job>(_tmpJobs);
        lv_Jobs.ItemsSource = _sourceJobs;


        /*foreach (Job j in tmpJobs)
        {
            _sourceJobs.Add(j);

        }*/




        int jobsDue = 0;
        float amountDue = 0;
        int bookedInJobs = 0;
        DateTime today = UsfulFuctions.DateNow;

        //what is due is about the work on the list, so it is counted off it
        foreach (Job j in _sourceJobs)
        {
            bookedInJobs++;

            if (j.IsCompleted || j.HaveCanceled)
                continue;

            if ((UsfulFuctions.DateNow - j.DueDate).Days >= 0 || ViewBooking)
            {
                jobsDue++;
                amountDue += j.Price;
            }
        }

        //Cleaned today is a figure for the day, not for whatever the list
        //happens to be showing. Counting it off the rows on screen missed
        //everything done from a booking: booked work is taken out of this
        //list, and marking a job done does not unbook it.
        float amountCleaned = 0;
        foreach (Job j in Job.Query())
            if (j.IsCompleted && !j.HaveCanceled && j.DateCompleated.Date == today.Date)
                amountCleaned += j.EffectivePrice;

        //and what is owed is owed by a customer, not by a job - adding it up
        //job by job charged the same customer once for every job of theirs on
        //the list, and left out the ones whose work is booked in
        float moneyOwed = 0;
        foreach (Customer c in Customer.Query())
            if (c.Balance > 0)
                moneyOwed += c.Balance;



        if (Job.Query().Count > 0)

            if (ViewBooking)
                t_job_overview.Text = $"{bookedInJobs} booked in. Total amount {Gloable.CurrenceSymbol}{amountDue}\nMoney Owed {Gloable.CurrenceSymbol}{moneyOwed}";
            else
                t_job_overview.Text = $"Jobs Due {jobsDue}. Value of due jobs {Gloable.CurrenceSymbol}{amountDue}\nCleanded Today {Gloable.CurrenceSymbol}{amountCleaned}. Money Owed {Gloable.CurrenceSymbol}{moneyOwed}";

        _isRefreshing = false;
    }
    private void RefreshPage()
    {
     
       

        //Task t = new Task(() =>
        //{
            RPage();
        //});
        //t.Start();
        //rv_refreshAnimation.IsRefreshing = false;
    }

    private void Lv_Jobs_SizeChanged(object sender, EventArgs e)
    {
        throw new NotImplementedException();
    }


    public static void MarkJobDone(Job j, Page page)
    {
        if (j.IsCompleted)
        {
            if (j.UnMarkJobDone())
            {
                j.UseAlterativePrice = -1;
            }
            else
                page.DisplayAlert("Error", "This job cannot be marked undone as this job has been done since", "Ok");
        }
        else
        {
            j.UseAlterativePrice = -1;
            j.MarkJobDone();
            if (j.TAC)
                TextCustomerReceipt(j, page);
        }

        j.Refresh();
        j.RefreshColors();
    }

    /// <summary>
    /// A day's work with what is still to do at the top and the jobs already
    /// done pushed to the bottom, so the list stays about the work left
    /// rather than what has been finished with.
    ///
    /// OrderBy is a stable sort, so within each of the two groups the jobs
    /// keep whatever order they arrived in - street order on the booked tab,
    /// the day's own order on the calendar.
    /// </summary>
    public static List<Job> DoneAtTheBottom(IEnumerable<Job> jobs)
    {
        return jobs.OrderBy(x => x.IsCompleted).ToList();
    }

    /// <summary>
    /// Puts a job back to not done and not paid.
    ///
    /// The payment only comes off for cash taken at the door. Money that came
    /// in through the bank was read off a statement, so taking it back here
    /// would leave the books disagreeing with the bank - that has to be
    /// sorted out on the payments page instead.
    /// </summary>
    public static async Task ClearJob(Job j, Page page)
    {
        if (j == null)
            return;

        if (j.IsPaidFor && !j.CanClearPayment)
        {
            Payment p = j.JobPayment;
            await page.DisplayAlert("Paid By Bank",
                $"This job was paid by {p.PaymentMethod}, not cash, so clearing it here would take money off the books that really did come in.\n\n" +
                "Remove the payment from the payments page if it is wrong, and the job can be cleared afterwards.", "Ok");
            return;
        }

        if (j.IsPaidFor)
            j.UnMarkJobPaid();

        if (j.IsCompleted && !j.UnMarkJobDone())
            await page.DisplayAlert("Cannot Clear",
                "This job cannot be put back to not done, because the next one after it has been done since.", "Ok");
        else
            j.UseAlterativePrice = -1;

        j.Refresh();
        j.RefreshColors();

        Job.Save();
        Payment.Save();
        Customer.Save();
    }

    public static async Task MarkJobPaid(Job j, Page page)
    {
        //a direct debit is already on its way for this job, so taking the
        //money again here would charge the customer twice
        GoCardlessRequest pending = GoCardlessRequest.PendingForJob(j.Id);
        if (!j.IsPaidFor && pending != null)
        {
            await page.DisplayAlert("Payment Pending",
                $"A direct debit is already on its way for this job ({pending.FormattedSummary}). " +
                "It will be marked paid automatically once the money comes through.", "Ok");
            return;
        }

        if (j.IsPaidFor)
        {
            j.UnMarkJobPaid();
            if (j.IsCompleted)
                j.UnMarkJobDone();
        }
        else
        {
            j.MarkJobPaid();
            j.MarkJobDone();
        }

        j.Refresh();
        j.RefreshColors();
    }
    /// <summary>
    /// paid and done
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void On_Job_Paid(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        await MarkJobPaid(j, this);
       // RefreshPage();
    }

  

    private bool altColour = false;
    public static Color altColor = Color.FromArgb("#EDF2F7");
    public static Color altColorDark = Color.FromArgb("#262A2E");
    public static Color MainColor = Colors.White;
    public static Color MainColorDark = Color.FromArgb("#1E1E1E");
   /* private void list_child_added(object sender, ElementEventArgs e)
    {
        return;
        if (e.Element is HorizontalStackLayout)
        {
            VerticalStackLayout vsl = sender as VerticalStackLayout;

            if (altColour)
            {
                vsl.SetAppThemeColor(VerticalStackLayout.BackgroundColorProperty, altColor, altColorDark);
                //vsl.BackgroundColor = altColor;
            }
            else
                vsl.SetAppThemeColor(VerticalStackLayout.BackgroundColorProperty, Colors.White, Colors.Black);
            altColour = !altColour;
                

        }

    }*/

    public static void MarkJobSkipped(Job j)
    {
        j.SkipJob();
        j.Refresh();
        j.RefreshColors();
        Job.Save();
    }
    private void On_Job_Skipped(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        MarkJobSkipped(j);
     
    }

    /// <summary>
    /// put a finished one off back on the round.
    ///
    /// a job that repeats brings itself back the moment it is marked done. A
    /// one off has no frequency to work that date out from, so when it is
    /// wanted next has to be asked - and it is asked in the stretches work
    /// actually comes back round in rather than as a date to type, because
    /// this gets used standing at the door with one hand.
    ///
    /// the finished visit is left exactly as it was: what was charged and
    /// when it was done is the record of that job, so the repeat is a new
    /// one alongside it rather than the same one moved on.
    /// </summary>
    /// <returns>true when the job was put back on</returns>
    public static async Task<bool> DoJobAgain(Job j, Page page)
    {
        if (j == null)
            return false;

        //the menu greys the entry out rather than hiding it, so say why
        //instead of doing nothing when it is tapped anyway
        if (!j.CanDoAgain)
        {
            if (!j.IsOneOff)
                await page.DisplayAlert("Already Repeats", "This job comes round on its own, so there is nothing to put back on.", "Ok");
            else if (!j.IsCompleted)
                await page.DisplayAlert("Not Done Yet", "This job is still on the round. Mark it done first and it can be put back on afterwards.", "Ok");
            else
                await page.DisplayAlert("Already Back On", "This job has already been put back on the round.", "Ok");
            return false;
        }

        string[] choices = { "Today", "In a week", "In 2 weeks", "In a month", "In 3 months", "In 6 months", "In a year" };
        string chosen = await page.DisplayActionSheet("Do it again when?", "Cancel", null, choices);

        DateTime due = UsfulFuctions.DateNow;
        switch (chosen)
        {
            case "Today":
                break;

            case "In a week":
                due = due.AddDays(7);
                break;

            case "In 2 weeks":
                due = due.AddDays(14);
                break;

            case "In a month":
                due = due.AddMonths(1);
                break;

            case "In 3 months":
                due = due.AddMonths(3);
                break;

            case "In 6 months":
                due = due.AddMonths(6);
                break;

            case "In a year":
                due = due.AddYears(1);
                break;

            //Cancel, or the sheet was dismissed
            default:
                return false;
        }

        Job again = j.DoAgain(due);
        if (again == null)
            return false;

        again.Refresh();
        again.RefreshColors();
        DataRefreshNotifier.NotifyDataChanged();

        await page.DisplayAlert("Back On The Round", $"{again.JobFormattedStreet} is due again on {due.ToShortDateString()}.", "Ok");
        return true;
    }

    private async void On_Job_DoAgain(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;

        if (await DoJobAgain(j, this))
            RefreshPage();
    }

    /// <returns>true when the job was cancelled (not un-cancelled or aborted)</returns>
    public static async Task<bool> MarkJobCancled(Job j,Page page)
    {

        if (j.HaveCanceled)
        {
            j.UnCancelJob();
            j.Refresh();
            j.RefreshColors();
        }
        else
            if (await page.DisplayAlert("Cancel Job?", "Are you sure you want to cancel this job? Canceling the job will stop it from showing up in your job list!", "Yes", "No"))
        {
            j.CancelJob();
            j.Refresh();
            j.RefreshColors();
            return true;
        }
        return false;
    }
    private async void On_Job_Canceled(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        bool canceled = await MarkJobCancled(j, this);
        RefreshPage();
        if (canceled)
            ShowUndoCancelBanner(j);
    }

    private Job _lastCanceledJob;
    private CancellationTokenSource _undoCancelCts;

    private async void ShowUndoCancelBanner(Job j)
    {
        _lastCanceledJob = j;
        _undoCancelCts?.Cancel();
        var cts = new CancellationTokenSource();
        _undoCancelCts = cts;
        g_undoCancel.IsVisible = true;
        try
        {
            await Task.Delay(10000, cts.Token);
            g_undoCancel.IsVisible = false;
            _lastCanceledJob = null;
        }
        catch (TaskCanceledException)
        {
            //a newer banner replaced this one, or undo was clicked
        }
    }

    private void bnt_undoCancel_Clicked(object sender, EventArgs e)
    {
        _undoCancelCts?.Cancel();
        g_undoCancel.IsVisible = false;

        Job j = _lastCanceledJob;
        _lastCanceledJob = null;
        if (j == null)
            return;

        j.UnCancelJob();
        j.Refresh();
        j.RefreshColors();
        RefreshPage();
    }

    /// <summary>
    /// open the new expense page attached to a job (used by the swipe
    /// menus here and on the calendar)
    /// </summary>
    public static void AddExpenseForJob(Job j, Page page)
    {
        NewExpense.ExpenseToEdit = null;
        NewExpense.DateToUse = null;
        NewExpense.JobToLink = j;

        page.Navigation.PushAsync(new NewExpense());
    }

    private void On_Job_Expense(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        AddExpenseForJob(j, this);
    }

    public static void EditJobDetails(Job j, Page page)
    {
        NewJob.JobToAdd = j;
        NewJob.AddNewJob = false;

        page.Navigation.PushAsync(new NewJob());

    }
    private void On_Job_Detials(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        EditJobDetails(j, this);
    }

    private SwipeView oldSwipeView;
    private void swip_started(object sender, SwipeStartedEventArgs e)
    {
        SwipeView sv = sender as SwipeView;
       
        if (oldSwipeView != null && sv != oldSwipeView)
        {
            oldSwipeView.Close();
        }

        oldSwipeView = sv;
        Job j = GetJobForSwipe(sv.LeftItems[0]);
        if (j == null)
            j = GetJobForSwipe(sv.RightItems[0]);

        //a job already marked has nothing to gain from Done or Done & Paid
        //again - what is wanted then is to clear it, or open it up
        SwipeItem si = sv.LeftItems[0] as SwipeItem;
        si.Text = j.DoneActionText;

        si = sv.LeftItems[1] as SwipeItem;
        si.Text = "Done & Paid";
        si.IsVisible = j.ShowPaidAction;

        si = sv.RightItems[1] as SwipeItem;
        if (j.HaveCanceled)
        {
            
            si.Text = "Resume Job";
        }
        else
            si.Text = "Cancel Job";


    }

    private async void bnt_addJob_Clicked(object sender, EventArgs e)
    {
    /*    if (_selectingJobs)
        {
            Update/ToolBarNoraml();
            _selectingJobs = false;
            foreach (Job j in _tmpJobs)
                j.SelectionModeEnabled = true;
            _selectedJobs.Clear();
            foreach (Job j in _sourceJobs)
                j.IsSelected = false;
            var vt = lv_Jobs.GetVisualTreeDescendants();
            CheckBox cb;
            SwipeView sv;
            foreach (object o in vt)
            {
                cb = o as CheckBox;
                if (cb != null)
                    cb.IsVisible = false;
                sv = o as SwipeView;
                if (sv != null)
                {
                    sv.IsEnabled = true;
                }
            }

           
            return;
        }*/
        NewJob.JobToAdd = new Job();
        NewJob.AddNewJob = true;
       
        Navigation.PushAsync(new NewJob());
    }

    private SecondryFilterType SecondryFilter = SecondryFilterType.None;

    private DateTime StartFilterDate = UsfulFuctions.DateNow;
    private DateTime EndFilterDate = UsfulFuctions.DateNow.AddDays(28);
    private bool FilterDate = true;
    //=======================================================FILTERS=====================================================

    private void ResetDateFilter()
    {
        DateTime dt = UsfulFuctions.DateNow;
        StartFilterDate = new DateTime(dt.Year, dt.Month, dt.Day);

        EndFilterDate = StartFilterDate.AddDays(14);
        StartFilterDate = StartFilterDate.AddDays(-7);

    }
    /// <summary>
    /// everything back to how the page opens: the fortnight either side of
    /// today, no tag filter, cancelled work out of the way.
    ///
    /// It used to give up straight away unless a tag filter happened to be
    /// on, so Reset did nothing at all to a date range that had been dragged
    /// somewhere unhelpful - which is the thing most likely to need it.
    /// </summary>
    private void bnt_Clear_Filter_Clicked(object sender, EventArgs e)
    {
        ResetDateFilter();
        dp_StartSearchDate.Date = StartFilterDate;
        dp_EndSearchDate.Date = EndFilterDate;

        FilterDate = true;
        cb_filterDates.IsChecked = true;
        cb_showCancelled.IsChecked = false;

        ClearTagFilter();
        RefreshPage();
    }

    /// <summary>
    /// the Clear on the bar above the list, and the one next to the tag in
    /// the panel. it takes off what it is sat next to and nothing else - a
    /// date range that has been set on purpose is not what was being cleared
    /// </summary>
    private void bnt_ClearTagFilter_Clicked(object sender, EventArgs e)
    {
        if (Filter == null)
            return;

        ClearTagFilter();
        RefreshPage();
    }

    private void ClearTagFilter()
    {
        Filter = null;
        SecondryFilter = SecondryFilterType.None;
        FilterString = string.Empty;
    }

    /// <summary>
    /// What the list is being narrowed by, said in the bar above it whether
    /// the filter panel is open or not, along with how much of the round is
    /// left showing. A list quietly showing a fraction of the work with
    /// nothing to say why is what had the tag filters switched off.
    /// </summary>
    private void ShowActiveFilter(int showing)
    {
        bool on = Filter != null;

        brd_filterOn.IsVisible = on;
        hsl_tagFilter.IsVisible = on;

        if (!on)
            return;

        l_filterBy.Text = FilterDescription();
        l_filterResultsText.Text = $"Showing only {FilterDescription()}";
        l_filterResultsCount.Text = showing == 1
            ? "1 job, from the whole round"
            : $"{showing} jobs, from the whole round";
    }

    /// <summary>the tag filter in the words it would be described in</summary>
    private string FilterDescription()
    {
        switch (SecondryFilter)
        {
            case SecondryFilterType.JobType:
                return $"{FilterString} jobs";

            case SecondryFilterType.JobPrice:
                return $"work at {FilterString}";

            case SecondryFilterType.Owed:
                return "customers who owe money";

            case SecondryFilterType.Credit:
                return "customers in credit";

            case SecondryFilterType.NothingOwed:
                return "customers who owe nothing";
        }

        //the street, the town and the area say what they are on their own
        return FilterString;
    }

    /// <summary>
    /// the three money filters are a question about the customer rather than
    /// a value off the job, so they have nothing to put in the description
    /// and are the only ones allowed to set a filter without one
    /// </summary>
    private static bool NamesNoValue(SecondryFilterType type)
    {
        return type == SecondryFilterType.Owed
            || type == SecondryFilterType.Credit
            || type == SecondryFilterType.NothingOwed;
    }

    /// <summary>
    /// What a tag filter picks from.
    ///
    /// The whole round rather than the date range the list is normally kept
    /// to: tapping High Street and being shown three of its twelve houses,
    /// because the rest are not due for a fortnight, is not what anybody
    /// means by tapping it.
    ///
    /// Work that is finished is left out for the same reason - every visit
    /// ever made to that street is a history, not a list of work to do - and
    /// cancelled work follows the same tick as everywhere else on the page.
    /// </summary>
    private List<Job> FilterSource()
    {
        List<Job> jobs = new List<Job>(Job.Query());

        jobs.RemoveAll(x => x.IsCompleted);

        if (!cb_showCancelled.IsChecked)
            jobs.RemoveAll(x => x.HaveCanceled);

        return jobs;
    }

    /// <summary>
    /// Puts a tag filter on: what is being matched, for saying so above the
    /// list, and the test that decides what stays.
    ///
    /// The test is given the job that was tapped rather than the words off
    /// its tag. Reading a tag back out of its own label is what put £8.50
    /// through a whole-number parse and took the app down with it.
    /// </summary>
    private void SetTagFilter(SecondryFilterType type, string what, Func<Job, bool> keep)
    {
        //the money tags name no value of their own. everything else does, and
        //a filter on a blank street, or on a job with no type, could only
        //ever say "Showing only "
        if (!NamesNoValue(type) && string.IsNullOrWhiteSpace(what))
            return;

        SecondryFilter = type;
        FilterString = what ?? string.Empty;

        Filter = () =>
        {
            List<Job> jobs = FilterSource();
            jobs.RemoveAll(x => !keep(x));
            return jobs;
        };

        RefreshPage();
    }

    /// <summary>
    /// The job whose tag was tapped. The booking summary rows at the top of
    /// the list are not real jobs and have nothing worth filtering by, so
    /// they answer null.
    ///
    /// The tags used to ask the last job *selected* whether it was a booking
    /// row. That is not the row that was tapped - and it is nothing at all
    /// until something has been selected, so a tag tapped as the first thing
    /// done on a freshly opened list took the app down with it.
    /// </summary>
    private static Job TaggedJob(object sender)
    {
        Job j = (sender as Element)?.BindingContext as Job;
        return j == null || j.CustomerId == -1 ? null : j;
    }

    private void Job_Type_Filter(object sender, EventArgs e)
    {
        Job j = TaggedJob(sender);
        if (j == null)
            return;

        SetTagFilter(SecondryFilterType.JobType, j.Name, x => x.Name == j.Name);
    }





    private void cb_showCancelled_Changed(object sender, CheckedChangedEventArgs e)
    {
        RefreshPage();
    }

    private static List<Job> tmpJobList;
    private List<Job> MasterFilter()
    {
        //a copy: Job.Query hands out one buffer it refills for everybody, and
        //what follows here takes jobs out of it
        tmpJobList = new List<Job>(Job.Query());

        if (!cb_showCancelled.IsChecked)
            tmpJobList.RemoveAll(x => x.HaveCanceled);
        if (FilterDate)
        {


            if (ViewBooking)
            {
                tmpJobList.RemoveAll(x => x.DateJobBookinFor != ViewBookingAtDate);
                string tmp = string.Empty;
                foreach (Job j in tmpJobList)
                {
                    if (j.IsBookedIn)
                        tmp += $"{j.DateJobBookinFor} - {ViewBookingAtDate}\n";

                }

              //  DisplayAlert("test", $"{tmp}", "Ok");


            }
            else
            {
                tmpJobList.RemoveAll(x => x.IsCompleted && x.DateCompleated < StartFilterDate);
                tmpJobList.RemoveAll(x => x.DueDate > EndFilterDate);
                tmpJobList.RemoveAll(x => x.IsBookedIn);
            }

            dp_StartSearchDate.Date = StartFilterDate;
            dp_EndSearchDate.Date = EndFilterDate;
        }
        return tmpJobList;
    }

    private void Job_Street_Filter(object sender, EventArgs e)
    {
        Job j = TaggedJob(sender);
        if (j?.Address == null)
            return;

        SetTagFilter(SecondryFilterType.Street, j.Address.Street,
            x => x.Address != null && x.Address.Street == j.Address.Street);
    }

    private void Job_City_Filter(object sender, EventArgs e)
    {
        Job j = TaggedJob(sender);
        if (j?.Address == null)
            return;

        SetTagFilter(SecondryFilterType.City, j.Address.City,
            x => x.Address != null && x.Address.City == j.Address.City);
    }

    private void Job_Area_Filter(object sender, EventArgs e)
    {
        Job j = TaggedJob(sender);
        if (j?.Address == null)
            return;

        SetTagFilter(SecondryFilterType.Area, j.Address.Area,
            x => x.Address != null && x.Address.Area == j.Address.Area);
    }

    private void Job_Price_Filter(object sender, EventArgs e)
    {
        Job j = TaggedJob(sender);
        if (j == null)
            return;

        //compared as money rather than as an exact float
        SetTagFilter(SecondryFilterType.JobPrice, $"{Gloable.CurrenceSymbol}{j.Price}",
            x => Math.Abs(x.Price - j.Price) < 0.005f);
    }

    /// <summary>
    /// the money tag asks about the customer behind the job, so tapping it
    /// on somebody who owes turns up everybody who owes - which is the round
    /// to knock on before the next one starts
    /// </summary>
    private void Money_Owed_Filter(object sender, EventArgs e)
    {
        Job j = TaggedJob(sender);
        if (j == null)
            return;

        Customer c = j.GetCustomer();
        if (c == null)
            return;

        if (c.Balance > 0)
        {
            SetTagFilter(SecondryFilterType.Owed, null, x => Balance(x) > 0);
            return;
        }

        if (c.Balance < 0)
        {
            SetTagFilter(SecondryFilterType.Credit, null, x => Balance(x) < 0);
            return;
        }

        SetTagFilter(SecondryFilterType.NothingOwed, null, x => Balance(x) == 0);
    }

    /// <summary>
    /// what the customer behind a job owes. work whose customer has gone
    /// counts as owing nothing rather than dropping out of every one of the
    /// three answers
    /// </summary>
    private static float Balance(Job j)
    {
        Customer c = j.GetCustomer();
        return c == null ? 0 : c.Balance;
    }

    private void swip_ended(object sender, SwipeEndedEventArgs e)
    {
        if (e.IsOpen)
        {
            g_more.IsVisible = false;
            UpdateMoreInfoLayout();
        }
       

    }

    private void On_Job_More(object sender, EventArgs e)
    {
        ShowJobStatus(GetJobForSwipe(sender), this, RefreshPage);
    }

    /// <summary>
    /// tags this visit - front only, nobody in, whatever it was. it stays on
    /// this time of doing the job, so the customer's history shows which
    /// times it was like that
    /// </summary>
    private async void On_Job_Tag(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;

        if (await TagPicker.EditAsync(this, new List<Job>() { j }, j.JobFormattedStreet))
            RefreshPage();
    }

    /// <summary>
    /// the old inline more panel. no longer opened from the list - More goes
    /// to the job's own window now - and due to come out once that has been
    /// used in anger
    /// </summary>
    private void ShowMorePanel()
    {
        if (_currentJob == null)
            return;
        g_more.IsVisible = true;
        UpdateMoreInfoLayout();
        l_customerDescription.Text = $"{_currentJob.JobFormattedStreet} {_currentJob.JobFormattedCity}";
        p_paymentType.Items.Clear();
        foreach (string s in Enum.GetNames(typeof(PaymentMethod)))
            p_paymentType.Items.Add(s);

        p_paymentType.SelectedItem = "Cash";

        l_jobType.Text = _currentJob.Name;
        l_jobType.BackgroundColor = Colors.Orange;
        l_jobPrice.Text = $"Price {Gloable.CurrenceSymbol}{_currentJob.Price}";
        l_jobPrice.BackgroundColor = Colors.Green;

        l_jobOwed.BackgroundColor = _currentJob.OwedColorCode;
        l_jobOwed.Text = _currentJob.JobFormattedOwed;

        if (_currentJob.AlternativePrices == null || _currentJob.AlternativePrices.Count == 0)
            _currentJob.UseAlterativePrice = -1;

        if (_currentJob.UseAlterativePrice < 0)
        {
            l_amoutToPay.Text = $"{_currentJob.JobFormattedOwedShort}";
            //bnt_removeAlternatePayment.IsVisible = false;
        }
        else
        {
            l_amoutToPay.Text = $"{_currentJob.AlternativePrices[_currentJob.UseAlterativePrice].Price}";
          //  bnt_removeAlternatePayment.IsVisible = true;
        }

        l_currencyType.Text = Gloable.CurrenceSymbol;

        ignoreCheckedIsCompleated = true;
        cb_isCompleated.IsChecked = _currentJob.IsCompleted;
        
        cb_isPaid.IsChecked = _currentJob.IsPaidFor;

        if (cb_isPaid.IsChecked)
        {
            p_paymentType.IsEnabled = true;
            l_amoutToPay.IsEnabled = true;
            l_currencyType.TextColor = Colors.White;
        }
        else
        {
            p_paymentType.IsEnabled = false;
            l_amoutToPay.IsEnabled = false;
            l_currencyType.TextColor = Color.FromArgb("4E5151");
        }

        if (cb_isCompleated.IsChecked)
        {
            p_dateCompleated.IsEnabled = true;
            l_dateCompleated.TextColor = Colors.White;
           
        }
        else
        {
            p_dateCompleated.IsEnabled = false;
            l_dateCompleated.TextColor = Color.FromArgb("4E5151");
        }

        if (_currentJob.DateCompleated <= UsfulFuctions.DateBase)
            p_dateCompleated.Date = UsfulFuctions.DateNow;
        else
            p_dateCompleated.Date = _currentJob.DateCompleated;

        if (_currentJob.AlternativePrices != null && _currentJob.AlternativePrices.Count > 0)
        {
            p_priceToUse.Items.Clear();
            p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{_currentJob.Price}");
            for (int i=0; i <_currentJob.AlternativePrices.Count; i++)
                p_priceToUse.Items.Add($"{_currentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{_currentJob.AlternativePrices[i].Price}");

          
            
            p_priceToUse.SelectedIndex = _currentJob.UseAlterativePrice + 1;
            h_pick_alterativePrice.IsVisible = true;
            bnt_addAlterativePrice.IsVisible = false;
            h_pick_alterativePricebnt.IsVisible = true;
        }
        else
        {
            h_pick_alterativePrice.IsVisible = false;
            bnt_addAlterativePrice.IsVisible = true;
            h_pick_alterativePricebnt.IsVisible = false;
        }
        h_createAlterativePrice.IsVisible = false;
        Payment p = Payment.Get(_currentJob.PaymentId);
        if (p.Id == -1) //if not valid
            return;

        string tmp = $"{p.PaymentMethod}";
        p_paymentType.SelectedItem = $"{p.PaymentMethod}";
        l_amoutToPay.Text = $"{p.Amount}";

    
    }

    private void on_isPaid_Changed(object sender, CheckedChangedEventArgs e)
    {
        if (cb_isPaid.IsChecked)
        {
            p_paymentType.IsEnabled = true;
            l_amoutToPay.IsEnabled = true;
        }
        else
        {
            p_paymentType.IsEnabled = false;
            l_amoutToPay.IsEnabled = false;
        }
    }

    bool ignoreCheckedIsCompleated = false;
    private void cb_IsCompleated_Changed(object sender, CheckedChangedEventArgs e)
    {
        CheckBox cb = sender as CheckBox;
        
        if (cb.IsChecked)
        {
            p_dateCompleated.IsEnabled = true;
            l_dateCompleated.TextColor = Colors.White;

            float ballence = 0;
            
            if (_currentJob.GetCustomer() != null)
                ballence = _currentJob.GetCustomer().Balance;

            if (_currentJob.IsCompleted)
                ballence -= _currentJob.EffectivePrice;
            if (p_priceToUse.SelectedIndex - 1 < 0)
                ballence += _currentJob.Price;
            else
                ballence += _currentJob.AlternativePrices[p_priceToUse.SelectedIndex - 1].Price;
          
                l_amoutToPay.Text = $"{ballence}";
        }
        else
        {
            p_dateCompleated.IsEnabled = false;
            l_dateCompleated.TextColor = Colors.DarkGray;
            
        }
    }

    private void bnt_cancel_clicked(object sender, EventArgs e)
    {
        g_more.IsVisible = false;
        UpdateMoreInfoLayout();
        _currentJob = null;
    }

    /// <summary>
    /// send a direct debit payment request for a job and log it. the job is
    /// left unpaid: it only becomes paid once GoCardless confirms the money
    /// has been collected. Returns false when nothing was requested
    /// </summary>
    public static async Task<bool> RequestGoCardlessPayment(Job j, float amount, Page page)
    {
        try
        {
            GoCardlessRequest request = await GoCardless.RequestJobPaymentAsync(j, amount);

            string when = request.ChargeDate > UsfulFuctions.DateBase
                ? $" It should leave their bank on {request.ChargeDate.ToShortDateString()}."
                : string.Empty;
            await page.DisplayAlert("Payment Requested",
                $"{request.FormattedAmount} has been requested by direct debit.{when}\n\n" +
                "The job stays unpaid and will be marked paid automatically once the money comes through.", "Ok");
            return true;
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("GoCardless", ex.Message, "Ok");
            return false;
        }
    }

    private async void bnt_confirm_clicked(object sender, EventArgs e)
    {
        if (_currentJob == null)
            return;

        //direct debits are requested, not paid on the spot: the tick box is
        //left off and the job is marked paid later when the money arrives
        if (!_currentJob.IsPaidFor && cb_isPaid.IsChecked &&
            (string)p_paymentType.SelectedItem == PaymentMethod.GoCardless.ToString())
        {
            float gcAmount;
            try
            {
                gcAmount = (float)Convert.ToDouble(l_amoutToPay.Text);
            }
            catch
            {
                await DisplayAlert("Error", "Invalid price entered", "Ok");
                return;
            }

            await RequestGoCardlessPayment(_currentJob, gcAmount, this);

            //whatever happened, the job is not paid yet
            cb_isPaid.IsChecked = false;
        }

        

        

        if (_currentJob.IsPaidFor && cb_isPaid.IsChecked) //if still paid we need to check that there is no differnce in payment details
        {

           

            //payment code looking for difference in payment
            Payment p = Payment.Get(_currentJob.PaymentId);
            if (p.Id != -1)
            {
                p.PaymentMethod = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)p_paymentType.SelectedItem);
                try
                {
                    float diff = (float)Convert.ToDouble(l_amoutToPay.Text) - p.Amount;
                    p.Amount = (float)Convert.ToDouble(l_amoutToPay.Text);
                    _currentJob.AddToBalenceCredit(diff);
                }
                catch
                {
                    DisplayAlert("Error", "Invalid price entered", "Ok");
                    return;
                }
            }
        }

        g_more.IsVisible = false;
        UpdateMoreInfoLayout();
        if (_currentJob.IsCompleted && !cb_isCompleated.IsChecked)
            _currentJob.UnMarkJobDone(true);


        int paymentRequired = p_priceToUse.SelectedIndex - 1;

        if (_currentJob.IsCompleted && cb_isCompleated.IsChecked) //if job is still compleated
        {
            //price for done checking for a difference.
            if (paymentRequired != _currentJob.UseAlterativePrice)
            {
                _currentJob.UnMarkJobDone(true);
                _currentJob.UseAlterativePrice = paymentRequired;
                _currentJob.MarkJobDone(true);
            }
        }

        _currentJob.UseAlterativePrice = p_priceToUse.SelectedIndex - 1;

        if (!_currentJob.IsCompleted && cb_isCompleated.IsChecked)
            _currentJob.MarkJobDone(p_dateCompleated.Date);

        if (_currentJob.IsPaidFor && !cb_isPaid.IsChecked)
            _currentJob.UnMarkJobPaid();

        if (!_currentJob.IsPaidFor && cb_isPaid.IsChecked)
        {
            //a direct debit already on its way marks the job paid itself
            //when the money arrives
            GoCardlessRequest pendingDD = GoCardlessRequest.PendingForJob(_currentJob.Id);
            if (pendingDD != null)
                await DisplayAlert("Payment Pending",
                    $"A direct debit is already on its way for this job ({pendingDD.FormattedSummary}). " +
                    "It will be marked paid automatically once the money comes through.", "Ok");
            else
                _currentJob.MarkJobPaid((float)Convert.ToDouble(l_amoutToPay.Text), (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)p_paymentType.SelectedItem));
        }

        if (_currentJob.IsCompleted && cb_isCompleated.IsChecked)
            _currentJob.DateCompleated = p_dateCompleated.Date;

        _currentJob.Refresh();
        _currentJob.RefreshColors();
        //RefreshPage();
        Job.Save();
        Payment.Save();
        Customer.Save();
    }

    private void bnt_addAlterativePrice_Clicked(object sender, EventArgs e)
    {
        h_createAlterativePrice.IsVisible = true;
        bnt_addAlterativePrice.IsVisible = false;
        h_pick_alterativePrice.IsVisible = false;
        h_pick_alterativePricebnt.IsVisible = false;
    }

    private void bnt_saveAlterativePrice(object sender, EventArgs e)
    {
        
        try
        {
            if (_currentJob.AlternativePrices == null)
                _currentJob.AlternativePrices = new List<AlternativePrice>();

            _currentJob.AlternativePrices.Add(new AlternativePrice()
            {
                Description = e_alterativeName.Text,
                Price = (float)Convert.ToDouble(e_alterativePrice.Text)
            });
            p_priceToUse.Items.Clear();
            p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{_currentJob.Price}");
            
            for (int i =0;i < _currentJob.AlternativePrices.Count;i++)
                p_priceToUse.Items.Add($"{_currentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{_currentJob.AlternativePrices[i].Price}");

            p_priceToUse.SelectedIndex = _currentJob.AlternativePrices.Count;
            h_createAlterativePrice.IsVisible = false;
            h_pick_alterativePrice.IsVisible = true;
            h_pick_alterativePricebnt.IsVisible = true;
            Job.Save();
        }
        catch
        {
            p_priceToUse.Items.Clear();
            p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{_currentJob.Price}");
            for (int i = 0; i < _currentJob.AlternativePrices.Count; i++)
                p_priceToUse.Items.Add($"{_currentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{_currentJob.AlternativePrices[i].Price}");
            DisplayAlert("Error", "Invalid information for alternative price", "Ok");
            h_createAlterativePrice.IsVisible = false;
        }
    }

    private void bnt_AlterativePrice2_Clicked(object sender, EventArgs e)
    {
        h_createAlterativePrice.IsVisible = true;
        bnt_addAlterativePrice.IsVisible = false;
    }

    private void bnt_cancelAlterativePrice(object sender, EventArgs e)
    {
        h_createAlterativePrice.IsVisible = false;
        if (p_priceToUse.Items.Count > 1)
        {
         
            h_pick_alterativePrice.IsVisible = true;
            h_pick_alterativePricebnt.IsVisible = true;
        }
        else
            bnt_addAlterativePrice.IsVisible = true;
    }

    private void bnt_hideFilter(object sender, EventArgs e)
    {
        g_filter.IsVisible = false;
    }

    /// <summary>
    /// the Filters toolbar item. it opens and closes the panel rather than
    /// only opening it, so the same button puts it away again
    /// </summary>
    private void l_filterText_Clicked(object sender, EventArgs e)
    {
        g_filter.IsVisible = !g_filter.IsVisible;

        if (!g_filter.IsVisible)
            return;

        //the panel is filled in from what is actually being filtered by, so
        //it can never say one thing while the list is doing another
        dp_StartSearchDate.Date = StartFilterDate;
        dp_EndSearchDate.Date = EndFilterDate;
        cb_filterDates.IsChecked = FilterDate;
        g_dateRange.IsVisible = FilterDate;

        ShowActiveFilter(_sourceJobs == null ? 0 : _sourceJobs.Count);
    }

    private void UpdateMasterFileterStart(object sender, DateChangedEventArgs e)
    {
        StartFilterDate = dp_StartSearchDate.Date;

        if (!g_filter.IsVisible)
            return;

        RefreshPage();
    }

    private void UpdateMasterFileterEnd(object sender, DateChangedEventArgs e)
    {
        EndFilterDate = dp_EndSearchDate.Date;

        if (!g_filter.IsVisible)
            return;

        RefreshPage();
    }

    private void cb_UpdateMasterFilter(object sender, CheckedChangedEventArgs e)
    {
        CheckBox cb = sender as CheckBox;
        FilterDate = cb.IsChecked;

        //the two dates say nothing while the list is not being kept to them
        g_dateRange.IsVisible = FilterDate;
        RefreshPage();
    }

    public List<int> _selectedJobs = new List<int>();
    private void cb_streetSelected(object sender, CheckedChangedEventArgs e)
    {
        CheckBox ck = sender as CheckBox;


        int id = Convert.ToInt32(ck.ClassId);
        if (ck.IsChecked)
        {
            if (!_selectedJobs.Contains(id))
            {
                _selectedJobs.Add(id);
                Job j = _sourceJobs.FirstOrDefault(x => x.Id == id);
                if (j != null)
                    j.IsSelected = true;
            }

        }
        else
        {
            _selectedJobs.Remove(id);
            Job j = _sourceJobs.FirstOrDefault(x => x.Id == id);
            if (j!= null)
                j.IsSelected = false;
        }
    }

    private bool _selectingJobs = false;

    private async void bnt_BookinWork_Clicked(object sender, EventArgs e)
    {
        if (_selectingJobs)
        {
            if (_selectedJobs.Count == 0)
            {
                await DisplayAlert("No Jobs", "You have not selected any jobs to text", "Ok");
                return;
            }


            UpdateToolBarNoraml();


            _selectingJobs = false;
            //Job.SelectionMode = _selectingJobs;
            foreach (Job j in _tmpJobs)
                j.SelectionModeEnabled = _selectingJobs;
            var vt = lv_Jobs.GetVisualTreeDescendants();

            CheckBox cb;
            SwipeView sv;
            foreach (object o in vt)
            {
                cb = o as CheckBox;
                if (cb != null)
                {
                    cb.IsVisible = false;
                }
                sv = o as SwipeView;
                if (sv != null)
                {
                    sv.IsEnabled = true;
                }
            }

            //now lets do some stuff and check each customer for texting

            string msgBody = string.Empty;
            List<Job> jobs;
            List<Job> jobsToBookin = new List<Job>();
            foreach (int i in _selectedJobs)
            {
                jobs = Job.Query(QueryType.JobId, i);
                if (jobs.Count > 0)
                {
                    if (jobs[0].TNB)
                    {
                        if (msgBody == String.Empty)
                            msgBody = "The following customers will be texted";

                        msgBody = $"{msgBody}\n{jobs[0].JobFormattedStreet}";
                    }
                    jobsToBookin.Add(jobs[0]);
                }
            }


            BookJobFormcs.jobs = jobsToBookin;
            await Navigation.PushAsync(new BookJobFormcs());
            /*  if (msgBody.Length > 0)
              {
                  if (await DisplayAlert("Send Text Messages?", msgBody, "Yes", "No"))
                  {
                      TextCustomer(jobsToBookin);
                  }

              }*/
            return;
        }
    }
    private async void bnt_selectJobs_Clicked(object sender, EventArgs e)
    {
        if (ViewBooking)
        {
            ViewBooking = false;
            ViewBookingAtDate = new DateTime(2000, 1, 1);
            jobOverviewBackground.BackgroundColor = Colors.Transparent;

            UpdateToolBarNoraml();

            bnt_cancel_booking.IsVisible = false;
            bnt_reschedule_booking.IsVisible = false;
            RefreshPage();
            return;
        }
       
        CheckBox cb;
        SwipeView sv;
     



        _selectingJobs = true;
        ColumnDefinition cd;



        //Job.SelectionMode = _selectingJobs;
        foreach (Job j in _tmpJobs)
            j.SelectionModeEnabled = _selectingJobs;
       // g_jobList.TranslationX = 0;
        UpdateToolBarSelectJobs();
      
    
        _selectedJobs.Clear();
        foreach (Job j in _sourceJobs)
            j.IsSelected = false;
        var v = lv_Jobs.GetVisualTreeDescendants();

    
        foreach (object o in v)
        {
      /*      cb = o as CheckBox;
            if (cb != null)
            {
                if (Convert.ToInt32(cb.ClassId) >= 0)
                {
                    cb.IsVisible = true;
                    Grid g = cb.Parent as Grid;


                    ColumnDefinition cold = g.ColumnDefinitions[0];
                    cold.Width = new GridLength(0.2, GridUnitType.Star);
                }
                cb.IsChecked = false;
            }
      */
            sv = o as SwipeView;
            if (sv != null)
            {
                //winui disables the whole subtree (checkboxes included) when a
                //parent is disabled, and mouse users cannot swipe anyway
                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                    sv.IsEnabled = false;
                sv.Close();
            }
        }
    }

    public static string DefaultTNBMessage = "Hi window cleaner here, we will be cleaning your windows <date>. If applicable can you please unlock your gate. Many Thanks";
    public static string DefaultNotCommingMessage = "Hi window cleaner here, sorry we have been unable to get to your property today, please accept our appologises. We will notify you when we will try again";
    public static string DefaultRearangeMessage = "Hi window cleaner here, sorry we have been unable to get to your property today, please accept out appologises.  Will will attempt to clean your windows <date>. If applicable can please unlock your gate. Many Thanks";
    public static string DefaultJobCompleateMessage = "Your windows have been cleaned. You now owe <owing>. Many Thanks";

    private static string tmpString = string.Empty;
    public static string ReplaceTags(string msg, DateTime dt, Job j = null)
    {
        //date replace tag
        string newString = $"({dt.ToShortDateString()})";
        if ((dt - UsfulFuctions.DateNow).Days == 1)
            newString = newString.Insert(0, "Tommorow ");
        else
            if ((dt - UsfulFuctions.DateNow).Days == 0)
                newString = newString.Insert(0, "Today ");
                else
                    newString = newString.Insert(0, "On ");

        tmpString = msg.Replace("<date>", newString);


        //owes replace tag
        if (j != null)
        {
            Customer c = j.GetCustomer();
            if (c == null)
                newString = $"{Gloable.CurrenceSymbol}{j.Price} for today";
            else
            {

                if (c.Balance == j.Price)
                    newString = $"{Gloable.CurrenceSymbol}{j.Price} for today";
                else
                {
                    if (c.Balance == 0)
                        newString = $"Nothing";
                    else
                        if (c.Balance < 0)
                        newString = $"Nothing you are in {Gloable.CurrenceSymbol}{Math.Abs(c.Balance)} credit";
                    if (c.Balance >= j.Price)
                    {
                        newString = $"{Gloable.CurrenceSymbol}{j.Price} for today.";
                        newString += $"You also owe for previous times totalling {Gloable.CurrenceSymbol}{c.Balance}";
                    }
                    else
                    {
                     
                        newString = $"{Gloable.CurrenceSymbol}{c.Balance}";
                    }
                }
            }
            tmpString = tmpString.Replace("<owing>", newString);

        }
        return tmpString;
    }

    public static void TextIndividualCustomer(Job j, DateTime dt, string msg, Page page)
    {

        try
        {
            Customer c = j.GetCustomer();
            if (c == null || string.IsNullOrWhiteSpace(c.Phone))
            {
                page.DisplayAlert("Failed", "This customer has no phone number.", "OK");
                return;
            }

            List<string> numbers = new List<string>() { c.Phone };

            SmsMessage message = new SmsMessage(ReplaceTags(msg, dt, j), numbers);
            Sms.ComposeAsync(message);
            j.HaveBeenText = true;
        }
        catch (FeatureNotSupportedException ex)
        {
            page.DisplayAlert("Failed", "Sms is not supported on this device.", "OK");
        }
        catch (Exception ex)
        {
            page.DisplayAlert("Failed", ex.Message, "OK");
        }
#if ANDROID
        //  if (ContextCompat.CheckSelfPermission(WorkTracker.AndroidGloable.Main_Application, Manifest.Permission.SendSms) == (int)Permission.Granted)
        {

            // SmsManager.Default.SendTextMessage("+447810342307", null, "Hello this is a test text message sent for the work tracker app. Let me know if you get it.", null, null);
        }
        //else
        {
            //    int result = 0;

            //     ActivityCompat.RequestPermissions(WorkTracker.AndroidGloable.Main_Activity, new string[] { Manifest.Permission.SendSms }, result);
        }
#endif
    }

    /// <summary>
    /// emails the customers on this list. addresses go in the blind copy
    /// field so one customer cannot see another's email address
    /// </summary>
    /// <param name="onlyFlagged">
    /// true to email only the jobs set to email the night before; false when
    /// the customer was picked deliberately
    /// </param>
    public async static Task EmailCustomers(List<Job> jobs, DateTime dt, string msg, Page page, bool onlyFlagged = true)
    {
        if (string.IsNullOrWhiteSpace(msg))
            msg = DefaultTNBMessage;

        List<Job> toEmail = new List<Job>();
        int noAddress = 0;
        foreach (Job j in jobs)
        {
            if (onlyFlagged && !j.ENB)
                continue;
            Customer c = j.GetCustomer();
            if (c == null || string.IsNullOrWhiteSpace(c.Email))
            {
                noAddress++;
                continue;
            }
            toEmail.Add(j);
        }

        if (toEmail.Count == 0)
        {
            await page.DisplayAlert("No Emails",
                noAddress > 0
                    ? $"None of these customers have an email address ({noAddress} skipped)."
                    : "There is nobody to email.", "OK");
            return;
        }

        try
        {
            EmailMessage message = new EmailMessage
            {
                Subject = "Window Cleaning",
                Body = ReplaceTags(msg, dt),
                //blind copy keeps every customer's address to themselves
                Bcc = toEmail.Select(x => x.GetCustomer().Email).ToList(),
            };

            await Email.ComposeAsync(message);

            foreach (Job j in toEmail)
                j.HaveBeenEmailed = true;
            Job.Save();
        }
        catch (FeatureNotSupportedException)
        {
            await page.DisplayAlert("Failed", "Email is not supported on this device.", "OK");
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Failed", ex.Message, "OK");
        }
    }

    /// <summary>
    /// texts each customer separately rather than as one group message.
    /// a group message would show every customer each other's phone number
    /// and could not carry anything personal like what they owe
    /// </summary>
    /// <param name="onlyFlagged">
    /// true to text only the jobs set to text the night before; false when
    /// the customer was picked deliberately
    /// </param>
    public async static Task TextCustomers(List<Job> jobs, DateTime dt, string msg, Page page, bool onlyFlagged = true)
    {
        if (string.IsNullOrWhiteSpace(msg))
            msg = DefaultTNBMessage;

        List<Job> toText = new List<Job>();
        int noNumber = 0;
        foreach (Job j in jobs)
        {
            if (onlyFlagged && !j.TNB)
                continue;
            Customer c = j.GetCustomer();
            if (c == null || string.IsNullOrWhiteSpace(c.Phone))
            {
                noNumber++;
                continue;
            }
            toText.Add(j);
        }

        if (toText.Count == 0)
        {
            await page.DisplayAlert("No Texts",
                noNumber > 0
                    ? $"None of these customers have a phone number ({noNumber} skipped)."
                    : "There is nobody to text.", "OK");
            return;
        }

        //each text opens the messaging app in turn, so say how many that is
        //before starting rather than surprising them with a queue of them
        if (toText.Count > 1)
        {
            string skipped = noNumber > 0 ? $"\n\n{noNumber} skipped with no phone number." : string.Empty;
            if (!await page.DisplayAlert("Send Texts",
                $"{toText.Count} customers will be texted, one at a time so each gets their own message. " +
                $"Your messaging app opens for each one so you can check it before sending.{skipped}",
                "Start", "Cancel"))
                return;
        }

        int sent = 0;
        foreach (Job j in toText)
        {
            try
            {
                SmsMessage message = new SmsMessage(
                    ReplaceTags(msg, dt, j),
                    new List<string> { j.GetCustomer().Phone });
                await Sms.ComposeAsync(message);

                //the messaging app does not tell us whether it was actually
                //sent, so this records that it was put in front of them
                j.HaveBeenText = true;
                sent++;
            }
            catch (FeatureNotSupportedException)
            {
                await page.DisplayAlert("Failed", "Sms is not supported on this device.", "OK");
                break;
            }
            catch (Exception ex)
            {
                if (!await page.DisplayAlert("Failed",
                    $"Could not text {j.JobFormattedStreet}: {ex.Message}", "Carry On", "Stop"))
                    break;
            }
        }

        if (sent > 0)
            Job.Save();
    }

    private DateTime ViewBookingAtDate;
    private bool ViewBooking = false;

    private void lv_Jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

        if (lv_Jobs.SelectedItem == null)
            return;

        Job j = lv_Jobs.SelectedItem as Job;

        if (_selectingJobs)
        {
            lv_Jobs.SelectedItem = null;
            //clicking anywhere on the row toggles the job while selecting
            if (j != null && j.CustomerId != -1)
            {
                j.IsSelected = !j.IsSelected;
                if (j.IsSelected)
                {
                    if (!_selectedJobs.Contains(j.Id))
                        _selectedJobs.Add(j.Id);
                }
                else
                    _selectedJobs.Remove(j.Id);
            }
            return;
        }

        
        if (j.Name == "Booking")
        {
            _rescheduleDate = UsfulFuctions.DateNow;
            dp_rescedualDate.Date = UsfulFuctions.DateNow;
            ViewBooking = true;
            ViewBookingAtDate = j.DateJobBookinFor;

            UpdateToolBarViewBooking();

            t_job_overview.Text = $"Viewing Booking for {ViewBookingAtDate.ToShortDateString()}\n Total Jobs {_sourceJobs.Count}. Total Price {Gloable.CurrenceSymbol}{j.Price}";

            bnt_cancel_booking.IsVisible = true;
            bnt_reschedule_booking.IsVisible = true;

            bnt_reschedule_booking.IsEnabled = true;
            hsl_rescheduleDate.IsVisible = false;

            RefreshPage();
        }
        lv_Jobs.SelectedItem = null;

        
    }

    private void HideSelectBookingJobs()
    {
        bnt_cancel_booking.IsVisible = false;
        bnt_reschedule_booking.IsVisible = false;
          _selectingJobs = false;
        //Job.SelectionMode = _selectingJobs;
        foreach (Job j in _tmpJobs)
            j.SelectionModeEnabled = _selectingJobs;
        UpdateToolBarNoraml();

            _selectedJobs.Clear();
            foreach (Job j in _sourceJobs)
                j.IsSelected = false;
        var vt = lv_Jobs.GetVisualTreeDescendants();
            CheckBox cb;
            SwipeView sv;
            foreach (object o in vt)
            {
                cb = o as CheckBox;
                if (cb != null)
                    cb.IsVisible = false;
                sv = o as SwipeView;
                if (sv != null)
                {
                    sv.IsEnabled = true;
                }
            }

        
        hsl_rescheduleDate.IsVisible = false;
        RefreshPage();
    }
    protected override bool OnBackButtonPressed()
    {

        if (_selectingJobs)
        {
            CancelSelectingJobs();
            return true;
        }

        if (ViewBooking)
        {
            ViewBooking = false;
            ViewBookingAtDate = new DateTime(2000, 1, 1);
            jobOverviewBackground.BackgroundColor = Colors.Transparent;

            UpdateToolBarNoraml();
            bnt_cancel_booking.IsVisible = false;
            bnt_reschedule_booking.IsVisible = false;

            bnt_reschedule_booking.IsEnabled = false;
            hsl_rescheduleDate.IsVisible = false;

            RefreshPage();
            return true;
        }
        return false;
    }

    public static async Task<bool> CancelBooking(ObservableCollection<Job> jobs, Page page, DateTime date)
    {
        if (await page.DisplayAlert("Cancel Booking", "Are you sure you wish to cancel the booking? This cannot be undone!", "Yes", "No"))
        {
            bool customersToText = false;
            string textCustomers = "The following customers may be expecting you.\n";

            List<Job> jobsToText = new List<Job>();
            foreach (Job j in jobs)
                if (j.TNB && !j.IsCompleted)
                {
                    customersToText = true;
                    textCustomers = $"{textCustomers}\n{j.Address}";
                    jobsToText.Add(j);
                }

            textCustomers = $"{textCustomers}\n\nDo you wish to notify them you will not be comming?";
            if (customersToText)
                if (await page.DisplayAlert("Text Customers", textCustomers, "Yes", "No"))
                    await TextCustomers(jobsToText, UsfulFuctions.DateNow, DefaultNotCommingMessage, page);

            int i = 0;
            foreach (Job j in jobs)
                if (j.IsBookedIn)
                {
                    j.UnBookInJob();
                    i++;
                }

      
            Booking.RemoveBooking(date);
          

            Job.Save();  
        }
        return true;
    }
    private async void bnt_cancel_booking_clicked(object sender, EventArgs e)
    {
        await CancelBooking(_sourceJobs, this, ViewBookingAtDate);
        ViewBooking = false;
        ViewBookingAtDate = new DateTime(2000, 1, 1);
        jobOverviewBackground.IsVisible = false;
        bnt_cancel_booking.IsVisible = false;
        bnt_reschedule_booking.IsVisible = false;

        jobOverviewBackground.IsVisible = true;
        jobOverviewBackground.BackgroundColor = Colors.Transparent;
        UpdateToolBarNoraml();

        bnt_cancel_booking.IsVisible = false;
        bnt_reschedule_booking.IsVisible = false;

        hsl_rescheduleDate.IsVisible = false;
        RefreshPage();
    }

    private void bnt_reschedule_booking_Clicked(object sender, EventArgs e)
    {
      //  bnt_reschedule_booking.BackgroundColor = Color.FromArgb("B56000");
        bnt_reschedule_booking.IsEnabled = false;
        hsl_rescheduleDate.IsVisible = true;
    }

    private DateTime _rescheduleDate;

    private async void RescheduleBooking()
    {
        if (await DisplayAlert("Reschedule Booking?", $"Are you sure you wish to reschedule this booking to {_rescheduleDate.ToShortDateString()}?", "Yes", "No"))
        {
            bool customersToText = false;
            string textCustomers = "The following customers may be expecting you.\n";

            List<Job> jobsToText = new List<Job>();
            foreach (Job j in _sourceJobs)
                if (j.TNB && !j.IsCompleted)
                {
                    customersToText = true;
                    textCustomers = $"{textCustomers}\n{j.Address}";
                    jobsToText.Add(j);
                }

            textCustomers = $"{textCustomers}\n\nDo you wish to notify them you will now be comming on {_rescheduleDate.ToShortDateString()}?";
            if (customersToText)
                if (await DisplayAlert("Text Customers", textCustomers, "Yes", "No"))
                    TextCustomers(jobsToText, UsfulFuctions.DateNow, DefaultRearangeMessage, this);


            Booking.ReseduleBooking(ViewBookingAtDate, _rescheduleDate);
            ViewBooking = false;
            ViewBookingAtDate = new DateTime(2000, 1, 1);
            jobOverviewBackground.IsVisible = false;
            bnt_cancel_booking.IsVisible = false;
            bnt_reschedule_booking.IsVisible = false;
            
            jobOverviewBackground.IsVisible = true;
            jobOverviewBackground.BackgroundColor = Colors.Transparent;
            UpdateToolBarNoraml();

            bnt_cancel_booking.IsVisible = false;
            bnt_reschedule_booking.IsVisible = false;

            hsl_rescheduleDate.IsVisible = false;
            RefreshPage();

            Job.Save();
        }
    }

    private void bnt_ReschedualConfirm_Clicked(object sender, EventArgs e)
    {
        RescheduleBooking();   
    }

    private void dp_dateSelected(object sender, DateChangedEventArgs e)
    {
        _rescheduleDate = dp_rescedualDate.Date;

    }

    public async static void TextCustomerReceipt(Job j, Page page)
    {
        if (await page.DisplayAlert("Text Customer?", $"Do you want to text {j.JobFormattedStreet} a job completed receipt?", "Yes", "No"))
        {
            TextIndividualCustomer(j, DateTime.Now, DefaultJobCompleateMessage, page);
        }
    }

    private async void bnt_textCustomer_Clicked(object sender, EventArgs e)
    {
        string msgBody = string.Empty;
        List<Job> jobs;
        List<Job> jobsToText = new List<Job>();
        foreach (int i in _selectedJobs)
        {
            jobs = Job.Query(QueryType.JobId, i);
            if (jobs.Count > 0)
            {
                if (jobs[0].TNB)
                {
                    if (msgBody == String.Empty)
                        msgBody = "The following customers will be texted";

                    msgBody = $"{msgBody}\n{jobs[0].JobFormattedStreet}";
                }
                jobsToText.Add(jobs[0]);
            }
        }

        if (jobsToText.Count == 0)
        {
            await DisplayAlert("No Jobs", "You have not selected any jobs to text", "Ok");
            return;
        }

        //these jobs were selected by hand, so text them whether or not they
        //are set to text the night before
        await TextCustomers(jobsToText, DateTime.Now, DefaultTNBMessage, this, false);
    }


    public static void ShowJobInfo(Job j, Page page)
    {
        ViewCustomerDetails.CurrentJob = j;
        page.Navigation.PushAsync(new ViewCustomerDetails());
    }

    /// <summary>
    /// the job's own window: done, paid, how much of what is owed the money
    /// covers, and which price this visit is charged at
    /// </summary>
    public static void ShowJobStatus(Job j, Page page, Action onSaved = null)
    {
        if (j == null)
            return;

        JobStatus.JobToShow = j;
        JobStatus.OnSaved = onSaved;
        page.Navigation.PushAsync(new JobStatus());
    }

    private void bnt_info_Clicked(object sender, EventArgs e)
    {
        ImageButton ib = sender as ImageButton;
        Job j = Job.Query(QueryType.JobId, Convert.ToInt32(ib.ClassId)).FirstOrDefault();
        ShowJobInfo(j, this);
    }

    private void p_priceToUse_SelectedIndexChanged(object sender, EventArgs e)
    {

        if (p_priceToUse.SelectedIndex == 0)
            bnt_removeAlternatePayment.IsVisible = false;
        else
            bnt_removeAlternatePayment.IsVisible = true;

        if (cb_isCompleated.IsChecked)
        {
            cb_isCompleated.IsChecked = !cb_isCompleated.IsChecked;
            cb_isCompleated.IsChecked = !cb_isCompleated.IsChecked;
        }
    }

    private void bnt_deleteAlternativePrice_Clicked(object sender, EventArgs e)
    {
        _currentJob.AlternativePrices.RemoveAt(p_priceToUse.SelectedIndex - 1);

        p_priceToUse.Items.Clear();
        p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{_currentJob.Price}");

        for (int i = 0; i < _currentJob.AlternativePrices.Count; i++)
            p_priceToUse.Items.Add($"{_currentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{_currentJob.AlternativePrices[i].Price}");

        p_priceToUse.SelectedIndex = 0;

        
    }

}