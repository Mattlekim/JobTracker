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
        //loop through all jobs looking for bookings
        List<string> dateTimes = new List<string>();
        List<BookingCatch> bookings = new List<BookingCatch>();

        string tmpstring = string.Empty;
        int tmpint;
        foreach (Job j in tmpJobs)
        {
            if (j.IsBookedIn)
            {
                tmpstring = j.DateJobBookinFor.ToShortDateString();
                tmpint = dateTimes.IndexOf(tmpstring);
                if (tmpint == -1)
                {
                    dateTimes.Add(tmpstring);
                    bookings.Add(new BookingCatch()
                    {
                        Date = tmpstring,
                        Jobs = new List<Job>()
                    });
                    bookings[bookings.Count - 1].Jobs.Add(j);
                }
                else
                    bookings[tmpint].Jobs.Add(j);
            }
        }

        foreach (BookingCatch bc in bookings)
        {
            Booking.AddBooking(bc.Jobs, bc.Jobs[0].DateJobBookinFor);
        }

        lv_Jobs.ItemsSource = _sourceJobs;

        //Grid g = NameScopeExtensions.FindByName<Grid>(this, "g_workList");
        
        NavigatedTo += WorkPlanner_NavigatedTo;
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

    private string FilterString;
    private float FilterFloat;
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


        Filter = null; //disable filters for now due to problems
        if (Filter != null)
        {

            jobs = Filter();
            l_filterResultsText.IsVisible = true;
            l_filterResultsText.Text = $"Filtering Results By {SecondryFilter}";

        }
        else
        {
            jobs = MasterFilter();
            l_filterResultsText.IsVisible = false;
        }


        foreach (Job j in jobs)
            if (j.IsCompleted)
                j.tmpDate = j.DateCompleated;
            else
                j.tmpDate = j.DueDate;

        jobs = jobs.OrderBy(x => x.tmpDate).ToList();
        if (!ViewBooking)
        foreach (Booking b in Booking.Bookings)
                jobs.Insert(0,b.BookingInfo);

      
        //_jobCatch = jobs;
        return jobs;
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
        Job j = _sourceJobs.First(x => x.Id == Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
        if (j != null)
            return j;
        return null;
    }
    private void On_Job_Compleated(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        MarkJobDone(j,this);

       // RefreshPage();
        
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
        _sourceJobs.Clear();
        _tmpJobs = GetJobs();
        for (int i = 0; i < _tmpJobs.Count; i++)
        {
            if (Application.Current.PlatformAppTheme == AppTheme.Dark)
            {
                if (altColour)
                    _tmpJobs[i].AltColour = MainColorDark;
                else
                    _tmpJobs[i].AltColour = altColorDark;
            }
            else
            {
                if (altColour)
                    _tmpJobs[i].AltColour = MainColor;
                else
                    _tmpJobs[i].AltColour = altColor;
            }
            _sourceJobs.Add(_tmpJobs[i]);
            altColour = !altColour;
            //_jobsToAddFrom = i;
        }


        /*foreach (Job j in tmpJobs)
        {
            _sourceJobs.Add(j);

        }*/




        int jobsDue = 0;
        float moneyOwed = 0;
        float amountDue = 0;
        float amountCleaned = 0;
        int bookedInJobs = 0;
        DateTime today = UsfulFuctions.DateNow;
        foreach (Job j in lv_Jobs.ItemsSource)
        {
            bookedInJobs++;
            if (j.IsCompleted)
            {
                if (j.DateCompleated.Year == today.Year && j.DateCompleated.Month == today.Month && j.DateCompleated.Day == today.Day)
                    if (j.UseAlterativePrice < 0)
                        amountCleaned += j.Price;
                    else
                        amountCleaned += j.AlternativePrices[j.UseAlterativePrice].Price;
            }
            else
            if (!j.HaveCanceled)
                if (!j.IsCompleted)
                    if ((UsfulFuctions.DateNow - j.DueDate).Days >= 0 || ViewBooking)
                    {
                        jobsDue++;
                        amountDue += j.Price;
                    }

            if (j.GetCustomer() != null)
                if (j.GetCustomer().Balance > 0)
                    moneyOwed += j.GetCustomer().Balance;
        }



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

    public static void MarkJobPaid(Job j)
    {
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
    private void On_Job_Paid(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        MarkJobPaid(j);
       // RefreshPage();
    }

  

    private bool altColour = false;
    public static Color altColor = new Color(235, 235, 255);
    public static Color altColorDark = new Color(25, 25, 45);
    public static Color MainColor = Colors.White;
    public static Color MainColorDark = Colors.Black;
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
        MarkJobSkipped(j);
     
    }

    public static async void MarkJobCancled(Job j,Page page)
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
        }
    }
    private async void On_Job_Canceled(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        MarkJobCancled(j, this);

       //RefreshPage();
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

        SwipeItem si = sv.LeftItems[0] as SwipeItem;
        if (j.IsCompleted)
        {
            
            si.Text = "Not Done";
        }
        else
            si.Text = "Done";


        si = sv.LeftItems[1] as SwipeItem;
        if (j.IsPaidFor)
        {
            si.Text = "Rest";
        }
        else
            si.Text = "Done & Paid";

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
    private void bnt_Clear_Filter_Clicked(object sender, EventArgs e)
    {
        if (Filter == null)
            return;
        ResetDateFilter();
        SecondryFilter = SecondryFilterType.None;
        l_filterBy.Text = $"{SecondryFilter}";
        Filter = null;
        RefreshPage();
    }

    private void Job_Type_Filter(object sender, EventArgs e)
    {
        if (_lastSelectedJob.Name == "Booking")
            return;

        Label l = sender as Label;
        FilterString = l.Text;
        Filter = () =>
        {
            SecondryFilter = SecondryFilterType.JobType;
            l_filterBy.Text = $"{SecondryFilter}";
            List<Job> jobs = MasterFilter();
            jobs.RemoveAll(x=>x.Name != FilterString);
            return jobs;
            
        };
        RefreshPage();

    }





    private static List<Job> tmpJobList;
    private List<Job> MasterFilter()
    {
        tmpJobList = Job.Query();
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
        if (_lastSelectedJob.Name == "Booking")
            return;

        Label l = sender as Label;
        FilterString = l.Text;
        Filter = () =>
        {
            SecondryFilter = SecondryFilterType.Street;
            l_filterBy.Text = $"{SecondryFilter}";
            List<Job> jobs = MasterFilter();
            jobs.RemoveAll(x => x.Address?.Street != FilterString);
            return jobs;

        };
        RefreshPage();
    }

    private void Job_City_Filter(object sender, EventArgs e)
    {
        if (_lastSelectedJob.Name == "Booking")
            return;

        Label l = sender as Label;
        FilterString = l.Text;
        Filter = () =>
        {
            SecondryFilter = SecondryFilterType.City;
            l_filterBy.Text = $"{SecondryFilter}";
            List<Job> jobs = MasterFilter();
            jobs.RemoveAll(x => x.Address?.City != FilterString);
            return jobs;

        };
        RefreshPage();
    }

    private void Job_Area_Filter(object sender, EventArgs e)
    {
        if (_lastSelectedJob.Name == "Booking")
            return;

        Label l = sender as Label;
        FilterString = l.Text;
        Filter = () =>
        {
            SecondryFilter = SecondryFilterType.Area;
            l_filterBy.Text = $"{SecondryFilter}";
            List<Job> jobs = MasterFilter();
            jobs.RemoveAll(x => x.Address?.Area != FilterString);
            return jobs;

        };
        RefreshPage();
    }

    private void Job_Price_Filter(object sender, EventArgs e)
    {
        if (_lastSelectedJob.Name == "Booking")
            return;

        Label l = sender as Label;
        FilterString = l.Text;
        FilterString = FilterString.Replace(Gloable.CurrenceSymbol,String.Empty);
        FilterString = FilterString.Replace("Price", String.Empty); ;
        FilterString = FilterString.Replace(" ", String.Empty); ;
        FilterFloat = Convert.ToInt32(FilterString);
        Filter = () =>
        {
            SecondryFilter = SecondryFilterType.JobPrice;
            l_filterBy.Text = $"{SecondryFilter}";
            List<Job> jobs = MasterFilter();
            jobs.RemoveAll(x => x.Price != FilterFloat);
            return jobs;

        };
        RefreshPage();
    }

    private void Money_Owed_Filter(object sender, EventArgs e)
    {
        if (_lastSelectedJob.Name == "Booking")
            return;

        Label l = sender as Label;
        FilterString = l.Text;

        Filter = () =>
        {
            List<Job> jobs = MasterFilter();

            if (FilterString.Contains("Owes"))
            {
                SecondryFilter = SecondryFilterType.Owed;
                l_filterBy.Text = $"{SecondryFilter}";
                jobs.RemoveAll(x => x.GetCustomer()?.Balance <= 0);
                return jobs;
            }

            if (FilterString.Contains("Credit"))
            {
                SecondryFilter = SecondryFilterType.Credit;
                l_filterBy.Text = $"{SecondryFilter}";
                jobs.RemoveAll(x => x.GetCustomer()?.Balance >= 0);
                return jobs;
            }

            if (FilterString.Contains("Nothing"))
            {
                SecondryFilter = SecondryFilterType.NothingOwed;
                l_filterBy.Text = $"{SecondryFilter}";
                jobs.RemoveAll(x => x.GetCustomer()?.Balance != 0);
                return jobs;
            }
            return jobs;

        };
        RefreshPage();
    }

    private void swip_ended(object sender, SwipeEndedEventArgs e)
    {
        if (e.IsOpen)
            g_more.IsVisible = false;
       

    }

    private void On_Job_More(object sender, EventArgs e)
    {
        g_more.IsVisible = true;
        _currentJob = GetJobForSwipe(sender);
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
            {
                if (_currentJob.UseAlterativePrice == -1)
                    ballence -= _currentJob.Price;
                else
                    ballence -= _currentJob.AlternativePrices[_currentJob.UseAlterativePrice].Price;
            }
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
        _currentJob = null;
    }

    private void bnt_confirm_clicked(object sender, EventArgs e)
    {
        if (_currentJob == null)
            return;

        

        

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
            _currentJob.MarkJobPaid((float)Convert.ToDouble(l_amoutToPay.Text), (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)p_paymentType.SelectedItem));

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

    private void l_filterText_Clicked(object sender, EventArgs e)
    {
        g_filter.IsVisible = true;

        l_filterBy.Text = $"{SecondryFilter}";

        dp_StartSearchDate.Date = StartFilterDate;
        dp_EndSearchDate.Date = EndFilterDate;
        p_viewFilter.SelectedItem = "Jobs";
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
        RefreshPage();
    }

    public List<int> _selectedJobs = new List<int>();
    private void cb_streetSelected(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value == false)
            return;
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
            List<string> numbers = new List<string>();
          
            SmsMessage message = new SmsMessage(ReplaceTags(msg, dt, j), numbers);
            Sms.ComposeAsync(message);

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

    public async static Task EmailCustomers(List<Job> jobs, DateTime dt, string msg, Page page)
    {
        try
        {
            List<string> emails = new List<string>();
            foreach (Job j in jobs)
            {
                if (j.ENB)
                {
                    emails.Add(j.GetCustomer().Email);
                    j.HaveBeenEmailed = true;
                }
            }
            EmailMessage message = new EmailMessage("Windows Cleaning", ReplaceTags(msg, dt), emails.ToArray());
          
            await Email.ComposeAsync(message);

        }
        catch (FeatureNotSupportedException ex)
        {
            await page.DisplayAlert("Failed", "Email is not supported on this device.", "OK");
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Failed", ex.Message, "OK");
        }
    }

  
    public async static Task TextCustomers(List<Job> jobs, DateTime dt, string msg, Page page)
    {

        try
        {
            List<string> numbers = new List<string>();
            foreach (Job j in jobs)
            {
                if (j.TNB)
                {
                    numbers.Add(j.GetCustomer().Phone);
                    j.HaveBeenText = true;
                }
            }
            SmsMessage message = new SmsMessage(ReplaceTags(msg, dt), numbers);
            await Sms.ComposeAsync(message);
            
        }
        catch (FeatureNotSupportedException ex)
        {
            await page.DisplayAlert("Failed", "Sms is not supported on this device.", "OK");
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Failed", ex.Message, "OK");
        }

    }

    private DateTime ViewBookingAtDate;
    private bool ViewBooking = false;

    private Job _lastSelectedJob;
    private void lv_Jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

        if (lv_Jobs.SelectedItem == null)
            return;

        Job j = lv_Jobs.SelectedItem as Job;
        _lastSelectedJob = j;

        if (_selectingJobs)
        {
            lv_Jobs.SelectedItem = null;
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
        if (await page.DisplayAlert("Text Customer?", $"Do you want to text {j.JobFormattedStreet} a job compleated receipt?", "Yes", "No"))
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

        if (await DisplayAlert("Send Text", msgBody, "Yes", "No"))
            TextCustomers(jobsToText, DateTime.Now, "", this);
    }


    public static void ShowJobInfo(Job j, Page page)
    {
        ViewCustomerDetails.CurrentJob = j;
        page.Navigation.PushAsync(new ViewCustomerDetails());
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

    Grid g_jobList;
    private void g_childAdded(object sender, ElementEventArgs e)
    {
        if (g_jobList == null)
            g_jobList = sender as Grid;
    }

    private void test(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsVisible")
        {
            CheckBox cb = sender as CheckBox;
            Grid g = cb.Parent as Grid;
            if (cb.IsVisible)
            {
                g.TranslationX = 0;
            }
            else
            {
                g.TranslationX = -32;
            }

        }
    }
}