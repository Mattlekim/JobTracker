namespace UiInterface.Layouts;
using System.Diagnostics;
using Kernel;
using UiInterface.Controles;
using System.Collections.ObjectModel;
//the hold that starts picking jobs out is timed with one of these
using Microsoft.Maui.Dispatching;
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
using UiInterface.Controles;

public class BookingCatch
{
    public string Date;
    public List<Job> Jobs;
}
public partial class WorkPlanner : ContentPage, IHoldRows
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
        Area,
        Round
    }


    public ToolbarItem bnt_search;
    public ToolbarItem bnt_Filters;
    public ToolbarItem bnt_addNewJob;
    public ToolbarItem bnt_selectJobs;
    public ToolbarItem bnt_bookInWork;
    public ToolbarItem bnt_textCustomers;
    public ToolbarItem bnt_CreateGroup;

    public ToolbarItem bnt_cancelSelection;
    public ToolbarItem bnt_setRound;
    public ToolbarItem bnt_sendWork;
    public ToolbarItem bnt_sentOut;

    /// <summary>
    /// Select All on the toolbar as well as on the bar. The bar is where the
    /// eye is, but the toolbar is where somebody looks for a thing that acts
    /// on the whole list - and it says the words rather than just "All".
    /// </summary>
    public ToolbarItem tbi_selectAll;

    /// <summary>what the page is doing, which is what the toolbar says</summary>
    private enum ToolBarMode
    {
        Normal,
        SelectingJobs,
        ViewingBooking
    }

    private ToolBarMode _toolBarMode = ToolBarMode.Normal;

    /// <summary>
    /// The one place the toolbar is built, from what the page is doing at the
    /// time it is asked.
    ///
    /// Every mode used to build it for itself, and all three started by
    /// emptying it - which threw away Search, because that one was put on in
    /// the xaml and never put back. Picking jobs out once and coming back out
    /// of it was enough to lose it for the rest of the run. Filters and Select
    /// Jobs went the other way: they were added in the constructor if the
    /// round had work at that moment, so a first run, or a page built before
    /// the jobs were loaded, had a toolbar with nothing on it but Add Job and
    /// no way of getting the rest back.
    ///
    /// So nothing is remembered here: the items are worked out again every
    /// time, and this is called on the way back to the page as well as on
    /// every change of mode.
    /// </summary>
    private void UpdateToolBar()
    {
        this.ToolbarItems.Clear();

        //search is on every mode: finding a house is not something to be
        //stopped from doing by what else the page happens to be showing
        this.ToolbarItems.Add(bnt_search);

        if (_toolBarMode == ToolBarMode.ViewingBooking)
            return;

        if (_toolBarMode == ToolBarMode.SelectingJobs)
        {
            this.ToolbarItems.Add(bnt_cancelSelection);
            this.ToolbarItems.Add(tbi_selectAll);
            this.ToolbarItems.Add(bnt_bookInWork);
            this.ToolbarItems.Add(bnt_setRound);
            //sending work out is opt-in on the settings page: most rounds
            //are one person, and the button would only be in the way
            if (Settings.EnableWorkSharing)
                this.ToolbarItems.Add(bnt_sendWork);
            this.ToolbarItems.Add(bnt_textCustomers);
            this.ToolbarItems.Add(bnt_CreateGroup);
            return;
        }

        //filtering and picking out are about work that is there. asked again
        //every time, so the first job added brings them with it
        bool haveWork = Job.Query().Count > 0;

        if (haveWork)
            this.ToolbarItems.Add(bnt_Filters);

        this.ToolbarItems.Add(bnt_addNewJob);

        if (haveWork)
            this.ToolbarItems.Add(bnt_selectJobs);

        //only while any send is remembered: the page it opens is the sends,
        //so with none there is nothing for the item to say
        if (WorkShare.HaveSentRecords())
            this.ToolbarItems.Add(bnt_sentOut);
    }

    public void UpdateToolBarSelectJobs()
    {
        _toolBarMode = ToolBarMode.SelectingJobs;
        UpdateToolBar();
    }

    public void UpdateToolBarNoraml()
    {
        _toolBarMode = ToolBarMode.Normal;
        UpdateToolBar();
    }

    public void UpdateToolBarViewBooking()
    {
        _toolBarMode = ToolBarMode.ViewingBooking;
        UpdateToolBar();
    }
    public WorkPlanner()
    {
        Job.RefreshJobs();
        List<Job> tmpJobs = Job.Query();

        InitializeComponent();

        //a magnifier, a funnel and a plus say what these are to anybody. the
        //Text stays on them: Android puts it up on a long press and reads it
        //out in the ... menu, so the icon never leaves somebody guessing
        bnt_search = new ToolbarItem();
        bnt_search.Text = "Search";
        bnt_search.IconImageSource = "search.png";
        bnt_search.Clicked += tbi_Search_Clicked;

        bnt_Filters = new ToolbarItem();
        bnt_Filters.Text = "Filters";
        bnt_Filters.IconImageSource = "filter.png";
        bnt_Filters.Clicked += l_filterText_Clicked;

        bnt_addNewJob = new ToolbarItem();
        bnt_addNewJob.Text = "Add Job";
        bnt_addNewJob.IconImageSource = "add.png";
        bnt_addNewJob.Clicked += bnt_addJob_Clicked;

        //a ticked box says picking-things-out to anybody. the Text stays on
        //it like the other icons: Android puts it up on a long press and
        //reads it out in the ... menu
        bnt_selectJobs = new ToolbarItem();
        bnt_selectJobs.Text = "Select Jobs";
        bnt_selectJobs.IconImageSource = "select.png";
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

        tbi_selectAll = new ToolbarItem();
        tbi_selectAll.Text = "Select All";
        tbi_selectAll.Clicked += Bnt_selectAllToolbar_Clicked;

        //putting a round together is picking the houses on it, so it belongs
        //with the other things done to a handful of jobs at once
        bnt_setRound = new ToolbarItem();
        bnt_setRound.Text = "Put On A Round";
        bnt_setRound.Clicked += bnt_setRound_Clicked;
        bnt_setRound.Order = ToolbarItemOrder.Secondary;

        //handing a list of jobs to somebody else is picking them too
        bnt_sendWork = new ToolbarItem();
        bnt_sendWork.Text = "Send To Someone";
        bnt_sendWork.Clicked += bnt_sendWork_Clicked;
        bnt_sendWork.Order = ToolbarItemOrder.Secondary;

        //what is out with somebody, and the way to clear it when the return
        //is not coming back as a file
        bnt_sentOut = new ToolbarItem();
        bnt_sentOut.Text = "Work Sent Out";
        bnt_sentOut.Clicked += (s, e) => Navigation.PushAsync(new SentWorkList());
        bnt_sentOut.Order = ToolbarItemOrder.Secondary;

        UpdateToolBar();

        ResetDateFilter();
        dp_StartSearchDate.Date = StartFilterDate;
        dp_EndSearchDate.Date = EndFilterDate;

        //the panel is the list's header in the xaml and it starts closed, so
        //it comes straight back off again - otherwise the list opens with an
        //empty panel's worth of space above the first job
        ShowFilterPanel(false);

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

        //one switch for the whole list, which also clears what was picked
        Job.SetSelectionMode(false);
        _selectedJobs.Clear();
        ShowSelectionBar();

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

    /// <summary>
    /// The bar across the top while jobs are being picked out: how many are
    /// picked, and the way back out.
    ///
    /// The toolbar has a Cancel on it as well, but on a phone it is as
    /// likely as not to be behind the ... menu, and a mode you did not mean
    /// to be in needs a way out that is actually on the screen.
    /// </summary>
    private void ShowSelectionBar()
    {
        brd_selecting.IsVisible = _selectingJobs;

        if (!_selectingJobs)
            return;

        l_selectedCount.Text = _selectedJobs.Count switch
        {
            0 => "Tap the jobs you want",
            1 => "1 job picked",
            _ => $"{_selectedJobs.Count} jobs picked",
        };

        //the same button does both, and says which it is about to do
        bnt_selectAll.Text = EverythingPicked ? "None" : "All";
    }

    /// <summary>the work on the list that can actually be picked</summary>
    private List<Job> Pickable()
    {
        List<Job> pickable = new List<Job>();

        if (_sourceJobs == null)
            return pickable;

        foreach (Job j in _sourceJobs)
            if (j != null && j.CustomerId != -1)
                pickable.Add(j);

        return pickable;
    }

    /// <summary>true while every job on the list is picked</summary>
    private bool EverythingPicked
    {
        get
        {
            List<Job> pickable = Pickable();
            return pickable.Count > 0 && _selectedJobs.Count >= pickable.Count;
        }
    }

    /// <summary>
    /// Picks everything on the list, or puts it all back.
    ///
    /// It is the list as it stands, filter and all - booking a whole street
    /// in is tapping the street's tag and then this, and picking twenty
    /// houses one at a time is not something anybody would do twice.
    ///
    /// The booking summary rows are left out because they are not work.
    /// </summary>
    private void SelectAllOrNone()
    {
        bool putBack = EverythingPicked;

        foreach (Job j in Pickable())
        {
            if (j.IsSelected == !putBack)
                continue;

            //through the same toggle as a tap, so nothing can disagree about
            //what is picked
            ToggleSelected(j);
        }

        ShowSelectionBar();
    }

    private void bnt_selectAll_Clicked(object sender, EventArgs e)
    {
        SelectAllOrNone();
    }

    private void Bnt_selectAllToolbar_Clicked(object sender, EventArgs e)
    {
        SelectAllOrNone();
    }

    /// <summary>
    /// picks a job, or puts it back. the tick boxes, the row taps and the
    /// hold all come through here so they cannot disagree about what is
    /// picked
    /// </summary>
    private void ToggleSelected(Job j)
    {
        if (j == null || j.CustomerId == -1)
            return;

        j.IsSelected = !j.IsSelected;

        if (j.IsSelected)
        {
            if (!_selectedJobs.Contains(j.Id))
                _selectedJobs.Add(j.Id);
        }
        else
            _selectedJobs.Remove(j.Id);

        ShowSelectionBar();
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

        //the tick boxes are one switch for the whole round, so coming back to
        //the page puts it back to whatever this page is actually doing rather
        //than trusting what it was left as
        Job.SetSelectionMode(_selectingJobs);
        ShowSelectionBar();

        //what is on the toolbar depends on there being work, and the first
        //job may well have been added on the page just come back from
        UpdateToolBar();

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

        //round first, then the date it is due or was done. work with no
        //round goes last rather than first, so what nobody has organised is
        //not the first thing on every page
        jobs = jobs
            .OrderBy(x => x.SortRoundFirst)
            .ThenBy(x => x.SortRound, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.tmpDate)
            .ToList();

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


        //lv_Jobs.ItemsSource = null;

        //lv_Jobs.ItemsSource = GetJobs(fullrefresh);

        Job.RefreshJobs();
        _tmpJobs = GetJobs();

        //the rows used to be striped light and dark down the list to tell one
        //from the next. they are cards now, like Layouts/AllJobs, so the gap
        //between them is what does that and there is nothing to work out here.
        //AltColour itself stays - the calendar and the customer page still
        //stripe their own lists with it

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
    /// <summary>
    /// the classic pull down: everything built again from the jobs, booked
    /// days included. IsRefreshing off in finally, or a throw would leave
    /// the spinner going round for ever
    /// </summary>
    private void rv_jobs_Refreshing(object sender, EventArgs e)
    {
        try
        {
            DataRefreshNotifier.RebuildBookings();
            Job.RefreshJobs();
            RefreshPage();
        }
        finally
        {
            rv_jobs.IsRefreshing = false;
        }
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
        MarkJobSkipped(j, UsfulFuctions.DateNow);
    }

    /// <summary>
    /// skips a job, taking it off any day it was booked for.
    ///
    /// <see cref="Job.SkipJob(DateTime)"/> clears the booking on the job
    /// itself, but the day in <see cref="Booking.Bookings"/> is a cache built
    /// from the jobs, and the kernel cannot see it. So the job comes out of
    /// that first: <see cref="Booking.RemoveJobFromBooking"/> has nothing to
    /// go on once the job says it is not booked in, and the day would be left
    /// with a summary row counting work that is no longer on it
    /// </summary>
    /// <param name="j">the job being passed over</param>
    /// <param name="dateSkipped">
    /// the day you were there and passed it over, which is not necessarily
    /// today when a round is being written up afterwards
    /// </param>
    public static void MarkJobSkipped(Job j, DateTime dateSkipped)
    {
        if (j == null)
            return;

        Booking.RemoveJobFromBooking(j);
        j.SkipJob(dateSkipped);
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

        //the list is rebuilt because the job has moved between the two halves
        //of it: skipping takes it off the day it was booked for, so it goes
        //back on the round with its new due date and the booking's summary
        //row has one less house on it
        RefreshPage();
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
            //out of the cached day first, exactly like MarkJobSkipped: the
            //cache cannot find the day once CancelJob has unbooked the job,
            //and the day would keep a summary row counting cancelled work
            //that no list will show
            Booking.RemoveJobFromBooking(j);
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

        //a finger put down on a row and held is taken by the swipe before
        //anything else can see it - see HoldWasReallyASwipe
        _swipeStartedAt = DateTime.Now;
        _swipeJob = j;

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
        //(the commented out block that used to sit here turned the tick boxes
        //off a job at a time. that is one switch now - Job.SetSelectionMode)
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

            case SecondryFilterType.Round:
                return $"the {FilterString} round";

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
    /// A filter tap on a card - the street, the price, a chip. The card says
    /// which piece and which job, because a page cannot reach inside a row
    /// template. The booking summary rows at the top of the list are not
    /// real jobs and have nothing worth filtering by, so they are ignored -
    /// they used to be told apart through the last job *selected*, which was
    /// not the row that was tapped and was nothing at all on a freshly
    /// opened list, and a tag tapped first thing took the app down with it.
    /// </summary>
    private void Card_PartTapped(object sender, JobCardEventArgs e)
    {
        Job j = e.Job;
        if (j == null || j.CustomerId == -1)
            return;

        switch (e.Part)
        {
            case JobCardPart.Street: Job_Street_Filter(j); break;
            case JobCardPart.City: Job_City_Filter(j); break;
            case JobCardPart.Area: Job_Area_Filter(j); break;
            case JobCardPart.Price: Job_Price_Filter(j); break;
            case JobCardPart.Owed: Money_Owed_Filter(j); break;
            case JobCardPart.Type: Job_Type_Filter(j); break;
            case JobCardPart.Round: Job_Round_Filter(j); break;
        }
    }

    private void Job_Type_Filter(Job j)
    {
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

    private void Job_Street_Filter(Job j)
    {
        if (j?.Address == null)
            return;

        SetTagFilter(SecondryFilterType.Street, j.Address.DisplayStreet,
            x => x.Address != null && x.Address.Street == j.Address.Street);
    }

    private void Job_City_Filter(Job j)
    {
        if (j?.Address == null)
            return;

        SetTagFilter(SecondryFilterType.City, j.Address.DisplayCity,
            x => x.Address != null && x.Address.City == j.Address.City);
    }

    private void Job_Area_Filter(Job j)
    {
        if (j?.Address == null)
            return;

        SetTagFilter(SecondryFilterType.Area, j.Address.DisplayArea,
            x => x.Address != null && x.Address.Area == j.Address.Area);
    }

    /// <summary>
    /// everything on the same round as the job whose round was tapped. this
    /// is the one people work by: a round is a day's work, or a patch
    /// </summary>
    private void Job_Round_Filter(Job j)
    {
        if (j == null || !j.HaveRound)
            return;

        SetTagFilter(SecondryFilterType.Round, j.Round,
            x => string.Equals(x.Round ?? string.Empty, j.Round, StringComparison.CurrentCultureIgnoreCase));
    }

    private void Job_Price_Filter(Job j)
    {
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
    private void Money_Owed_Filter(Job j)
    {
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
            return;
        }

        //nothing was opened, so this may have been a hold rather than a swipe
        HoldWasReallyASwipe();
    }

    /// <summary>when the swipe on a row began, and what it was on</summary>
    private DateTime _swipeStartedAt = DateTime.MinValue;
    private Job _swipeJob;

    /// <summary>
    /// A press held on a row, arriving as a swipe.
    ///
    /// The rows are SwipeViews, and the swipe takes the finger the moment it
    /// moves at all - which a finger held on a phone always does. The press
    /// is then somebody else's gesture: the long press android would have
    /// raised is cancelled, and nothing on the row ever hears about it. That
    /// is why holding a row did nothing however it was hooked up.
    ///
    /// So the swipe is read instead of fought. A swipe that ran for as long
    /// as a hold and opened nothing is not a swipe - it is somebody holding
    /// the row - and that is what starts picking jobs out.
    /// </summary>
    private void HoldWasReallyASwipe()
    {
        Job j = _swipeJob;
        DateTime started = _swipeStartedAt;

        _swipeJob = null;
        _swipeStartedAt = DateTime.MinValue;

        if (j == null || started == DateTime.MinValue)
            return;

        if ((DateTime.Now - started).TotalMilliseconds < HoldMilliseconds)
            return;

        HoldToSelect(j);
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
        ShowFilterPanel(false);
    }

    /// <summary>
    /// Opens or closes the filter panel.
    ///
    /// It comes off the list altogether rather than just being hidden. A
    /// header that is only made invisible still holds its place, so the top
    /// job sat a panel's worth of empty space down the screen with nothing
    /// in it. Taken off, the work starts where the list starts.
    /// </summary>
    private void ShowFilterPanel(bool show)
    {
        g_filter.IsVisible = show;
        lv_Jobs.Header = show ? g_filter : null;
    }

    /// <summary>
    /// the Filters toolbar item. it opens and closes the panel rather than
    /// only opening it, so the same button puts it away again
    /// </summary>
    private void l_filterText_Clicked(object sender, EventArgs e)
    {
        ShowFilterPanel(!g_filter.IsVisible);

        if (!g_filter.IsVisible)
            return;

        //the panel is filled in from what is actually being filtered by, so
        //it can never say one thing while the list is doing another
        dp_StartSearchDate.Date = StartFilterDate;
        dp_EndSearchDate.Date = EndFilterDate;
        cb_filterDates.IsChecked = FilterDate;
        g_dateRange.IsVisible = FilterDate;

        ShowActiveFilter(_sourceJobs == null ? 0 : _sourceJobs.Count);

        ScrollToTheTop();
    }

    /// <summary>
    /// Back to the very top of the list, the filter panel included.
    ///
    /// The panel sits at the top of the list's own content now rather than
    /// above it, so it scrolls away and gives the screen back - but that
    /// means opening it while the list is scrolled down would put it out of
    /// sight, and the button would look like it had done nothing.
    ///
    /// Asking for the first job to be at the *bottom* of the screen is what
    /// does it: that is further up than the list can go, so it stops at the
    /// top with the whole panel showing. Asking for the top of the first job
    /// would scroll the panel just off the screen instead, which is the one
    /// thing this must not do.
    /// </summary>
    private void ScrollToTheTop()
    {
        if (_sourceJobs == null || _sourceJobs.Count == 0)
            return;

        lv_Jobs.ScrollTo(_sourceJobs[0], position: ScrollToPosition.End, animate: false);
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

    /// <summary>
    /// the tick box on a card. it only follows what the box now says, so
    /// nothing here can argue with a tick that has just been put in
    /// </summary>
    private void Card_SelectionToggled(object sender, JobCardEventArgs e)
    {
        Job j = e.Job;

        if (j == null || j.CustomerId == -1)
            return;

        j.IsSelected = e.Selected;

        if (e.Selected)
        {
            if (!_selectedJobs.Contains(j.Id))
                _selectedJobs.Add(j.Id);
        }
        else
            _selectedJobs.Remove(j.Id);

        ShowSelectionBar();
    }

    //  -----------------------------------------------------  hold to select
    //
    //  There is no long press gesture of its own. On a phone the hold is left
    //  to android, through LongPressBehavior on the row, which is the only
    //  way it happens at all: the pointer events below are raised for a mouse
    //  or a stylus and never for a finger. On a desktop they still time it -
    //  the press counts once it has stayed put for half a second, and a
    //  scroll or a swipe calls it off. Same as the booked work page, so a
    //  hold means the same thing on both.

    private const int HoldMilliseconds = 500;
    private const double HoldMoveTolerance = 20;

    private IDispatcherTimer _holdTimer;
    private Job _holdJob;
    private Point _holdFrom;

    /// <summary>
    /// when the hold last picked something. the finger coming up off a hold
    /// is a tap as well, and that tap would put straight back what the hold
    /// had just picked
    /// </summary>
    private DateTime _heldAt = DateTime.MinValue;

    /// <summary>the row the last hold was on</summary>
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
            _holdTimer.Tick += (s, a) => HoldToSelect(_holdJob);
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
        HoldToSelect(item as Job);
    }

    /// <summary>
    /// holding a row starts picking jobs out with that one already picked,
    /// and holding another one after that picks that too
    /// </summary>
    private void HoldToSelect(Job j)
    {
        CancelHold();

        //the booking summary rows are not work and cannot be picked out
        if (j == null || j.CustomerId == -1)
            return;

        //a platform that both has a long press and raises the pointer events
        //would otherwise pick the row and put it straight back
        if (ReferenceEquals(j, _lastHeld) && HoldJustHappened)
            return;

        _lastHeld = j;
        _heldAt = DateTime.Now;

        if (_selectingJobs)
            ToggleSelected(j);
        else
            StartSelectingJobs(j);
    }

    /// <summary>true while the tap that ended a hold is still coming</summary>
    private bool HoldJustHappened
    {
        get { return (DateTime.Now - _heldAt).TotalMilliseconds < 1000; }
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


            //the ids of what was picked are read below, so they are left
            //alone here - it is the tick boxes that are being put away
            _selectingJobs = false;
            Job.SetSelectionMode(false);
            ShowSelectionBar();

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
    /// <summary>
    /// Puts everything picked on a round in one go.
    ///
    /// Without this a round is built by opening twenty houses one at a time,
    /// which nobody is going to do - and a round that is never filled in is
    /// a round that may as well not exist.
    /// </summary>
    /// <summary>
    /// hands the picked jobs to somebody else's copy of the app, as an
    /// encrypted file. the SendWork page asks what travels with them - the
    /// jobs themselves are not changed by being sent
    /// </summary>
    private async void bnt_sendWork_Clicked(object sender, EventArgs e)
    {
        List<Job> picked = new List<Job>();
        List<Job> alreadyOut = new List<Job>();

        foreach (int id in _selectedJobs)
        {
            Job j = Job.Query(QueryType.JobId, id).FirstOrDefault();
            if (j == null || j.CustomerId == -1 || j.IsCompleted || j.HaveCanceled)
                continue;

            //work already with somebody is not sent again - two copies of the
            //same job with two people ends with the house cleaned twice or
            //not at all. said rather than silently dropped, because a count
            //that quietly shrinks reads as the app losing jobs
            if (WorkShare.IsOut(j))
                alreadyOut.Add(j);
            else
                picked.Add(j);
        }

        if (picked.Count == 0 && alreadyOut.Count > 0)
        {
            string with = string.Join(", ", WorkShare.OutWith(alreadyOut));
            await DisplayAlert("Already Sent",
                $"The job(s) you picked are already out with {with}. Work Sent Out on the toolbar clears a send that is not coming back.",
                "Ok");
            return;
        }

        if (picked.Count == 0)
        {
            await DisplayAlert("No Jobs", "Pick the jobs you want to send first.", "Ok");
            return;
        }

        if (alreadyOut.Count > 0
            && !await DisplayAlert("Already Sent",
                $"{alreadyOut.Count} of the picked job(s) are already out with {string.Join(", ", WorkShare.OutWith(alreadyOut))} and will not be sent again. Send the other {picked.Count}?",
                "Send Them", "Cancel"))
            return;

        CancelSelectingJobs();
        await Navigation.PushAsync(new SendWork(picked));
    }

    private async void bnt_setRound_Clicked(object sender, EventArgs e)
    {
        List<Job> picked = new List<Job>();
        foreach (int id in _selectedJobs)
        {
            Job j = Job.Query(QueryType.JobId, id).FirstOrDefault();
            if (j != null && j.CustomerId != -1)
                picked.Add(j);
        }

        if (picked.Count == 0)
        {
            await DisplayAlert("No Jobs", "Pick the jobs you want on a round first.", "Ok");
            return;
        }

        string round = await RoundPicker.AskAsync(this, picked.Count == 1
            ? "Put this job on which round?"
            : $"Put these {picked.Count} jobs on which round?");

        if (round == null)
            return;

        int known = Job.RoundNames.Count;

        foreach (Job j in picked)
            j.SetRound(round);

        Job.Save();

        //a round typed in rather than picked is new, and the list of rounds
        //lives with the settings
        if (Job.RoundNames.Count != known)
            Settings.Save();

        CancelSelectingJobs();
        RefreshPage();

        //this list is the work in hand - a fortnight of it - so a house that
        //is not due for a month is not on it to be picked. Somebody putting
        //their round together is not going to guess that from a list that
        //looks complete, and the figures on the stats page are worked out
        //over everything rather than over this fortnight
        string rest = string.Empty;
        if (round.Length > 0)
        {
            int without = 0;
            foreach (Job j in Job.Query())
                if (!j.IsCompleted && !j.HaveCanceled && !j.HaveRound && j.CustomerId != -1)
                    without++;

            if (without > 0)
                rest = $"\n\n{without} job(s) are still not on any round. This list only reaches a fortnight ahead - "
                    + "open Filters and push the end date out to get at the rest of the work.";
        }

        await DisplayAlert("Round",
            round.Length == 0
                ? $"{picked.Count} job(s) taken off their round."
                : $"{picked.Count} job(s) put on {round}, and every visit of them.{rest}", "Ok");
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
       
        StartSelectingJobs();
    }

    /// <summary>
    /// turns the tick boxes on, with the job that was held already picked
    /// when it was a hold that started it
    /// </summary>
    private void StartSelectingJobs(Job first = null)
    {
        SwipeView sv;

        _selectingJobs = true;

        //nothing carried over from the last time
        _selectedJobs.Clear();
        Job.SetSelectionMode(true);

        UpdateToolBarSelectJobs();

        if (first != null)
            ToggleSelected(first);

        ShowSelectionBar();

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
                $"Send each one, come back here, and the next will be offered.{skipped}",
                "Start", "Cancel"))
                return;
        }

        int sent = 0;
        for (int i = 0; i < toText.Count; i++)
        {
            Job j = toText[i];

            //  Sms.ComposeAsync only starts the messaging app - it comes back
            //  the moment the app is on screen, long before anything has been
            //  sent. Running the whole list through it in one go therefore
            //  fired every message off at once, each opening the messaging
            //  app over the last, and only one of them was ever left in front
            //  of anybody to send. A round texted the night before went out
            //  to one house.
            //
            //  So the next one is offered rather than launched: this alert
            //  cannot be answered until the messaging app has been left and
            //  Work Tracker is back in front, which is exactly the wait that
            //  was missing - and it is also the way out of a queue of texts
            //  part way through.
            if (i > 0)
            {
                string where = j.JobFormattedStreet;

                if (!await page.DisplayAlert("Next Text",
                        $"{sent} of {toText.Count} done.\n\nNext is {where}.",
                        "Text Them", "Stop"))
                    break;
            }

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

        //what was put in front of them and what was not, so a round texted
        //the night before is not left half done without saying so
        if (toText.Count > 1)
            await page.DisplayAlert("Texts",
                sent == toText.Count
                    ? $"All {sent} texts have been opened for sending."
                    : $"{sent} of {toText.Count} texted. The rest have not been.", "Ok");
    }

    private DateTime ViewBookingAtDate;
    private bool ViewBooking = false;

    private void lv_Jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

        if (lv_Jobs.SelectedItem == null)
            return;

        Job j = lv_Jobs.SelectedItem as Job;
        lv_Jobs.SelectedItem = null;

        RowTapped(j);
    }

    /// <summary>
    /// The row's own tap.
    ///
    /// Android only delivers touches to a view that is handling them, and a
    /// row carrying nothing but the hold's pointer recogniser was never given
    /// a finger to time - which is why holding a row on this list did nothing
    /// while the same code worked on the booked work page, where the row has
    /// a tap recogniser as well.
    ///
    /// So the row takes the tap, and the list's selection is left to hand it
    /// on for whichever platform delivers it that way instead.
    /// </summary>
    private void job_Row_Tapped(object sender, TappedEventArgs e)
    {
        RowTapped(e.Parameter as Job);
    }

    private DateTime _rowTappedAt = DateTime.MinValue;

    private void RowTapped(Job j)
    {
        if (j == null)
            return;

        //the finger coming up off a hold arrives as a tap, and would put
        //straight back what the hold had just picked
        if (HoldJustHappened)
            return;

        //the row and the list can both report the same tap. one is enough
        if ((DateTime.Now - _rowTappedAt).TotalMilliseconds < 400)
            return;

        _rowTappedAt = DateTime.Now;

        if (_selectingJobs)
        {
            //tapping anywhere on the row picks the job while selecting
            ToggleSelected(j);
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
    }

    private void HideSelectBookingJobs()
    {
        bnt_cancel_booking.IsVisible = false;
        bnt_reschedule_booking.IsVisible = false;
          _selectingJobs = false;
        Job.SetSelectionMode(false);
        UpdateToolBarNoraml();

            _selectedJobs.Clear();
            ShowSelectionBar();
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

    /// <summary>
    /// Takes a day's work off the booking it was on.
    ///
    /// The jobs are not cancelled: they go back on the round to be done when
    /// they are due, which is what makes this different from cancelling the
    /// work itself. Anything on the day that is already done stays done.
    ///
    /// Any list of jobs will do - the work list hands it the booking it is
    /// looking at, the calendar a day's work, the booked work page one of
    /// its days.
    /// </summary>
    /// <returns>true when the booking was cancelled</returns>
    public static async Task<bool> CancelBooking(IEnumerable<Job> jobs, Page page, DateTime date)
    {
        if (await page.DisplayAlert("Cancel Booking",
            $"Take all the work booked for {date:ddd dd MMM yyyy} off the booking?\n\n" +
            "The jobs are not cancelled - they go back on the round and are due as they were. Anything already done stays done.",
            "Cancel The Booking", "Leave It"))
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
            return true;
        }

        //said no. the answer is handed back so a caller does not tear its
        //page down over a booking that is still there
        return false;
    }
    private async void bnt_cancel_booking_clicked(object sender, EventArgs e)
    {
        if (!await CancelBooking(_sourceJobs, this, ViewBookingAtDate))
            return;

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

    private void Card_Info(object sender, JobCardEventArgs e)
    {
        ShowJobInfo(e.Job, this);
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