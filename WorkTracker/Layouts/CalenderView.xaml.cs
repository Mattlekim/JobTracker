namespace UiInterface.Layouts;

using Microsoft.Maui.Controls.Shapes;
using Kernel;
using System.Collections.ObjectModel;
using System.ComponentModel;

public class CalenderDay: INotifyPropertyChanged
{
    private int _day;
	public int Day { get
        {
            return _day;
        }
        set
        {
            _day = value;
            RaisePropertyChanged("Day");
        }
    }
	public float Amount;
	public int JobCount;
	public int EstimatedDuration;
	public DateTime Date;


    private bool _showAmount = true;
    public bool ShowAmount
    {
        get
        {
            return _showAmount;
        }
        set
        {
            _showAmount = value;
            RaisePropertyChanged("ShowAmount");
        }
    }
    public string FormatAmount
    {
        get
        {
            
            return $"{Gloable.CurrenceSymbol}{Amount}";
         
        }
    }

    public bool _showJobCount;

    public bool ShowJobCount
    {
        get
        {
            return _showJobCount;
        }
        set
        { 
            _showJobCount = value;
            RaisePropertyChanged("ShowJobCount");
        }
    }
    public string FormatJobCount
    {
        get
        {
            return $"{Jobs.Count} Jobs";
        }
    }

    public float PaymentsTotal;

    private bool _showPayments;
    public bool ShowPayments
    {
        get
        {
            return _showPayments;
        }
        set
        {
            _showPayments = value;
            RaisePropertyChanged("ShowPayments");
        }
    }
    public string FormatPayments
    {
        get
        {
            return $"Paid {Gloable.CurrenceSymbol}{PaymentsTotal}";
        }
    }

    public float ExpensesTotal;

    private bool _showExpenses;
    public bool ShowExpenses
    {
        get
        {
            return _showExpenses;
        }
        set
        {
            _showExpenses = value;
            RaisePropertyChanged("ShowExpenses");
        }
    }
    public string FormatExpenses
    {
        get
        {
            return $"Spent {Gloable.CurrenceSymbol}{ExpensesTotal:0.00}";
        }
    }
	public ObservableCollection<Job> Jobs = new ObservableCollection<Job>();

    private Color _bgColor = Colors.Transparent;
    public Color BgColour
    {
        get
        {
            return _bgColor;
        }
        set
        {
            _bgColor = value;
            RaisePropertyChanged("BgColour");
        }
    }

    private Color _textColor;
	public Color TextColor
    {
        get { return _textColor; }
        set
        {
            _textColor = value;
            RaisePropertyChanged("TextColor");
        }
    }

    private Color _selectedDayColor = Colors.White;

    public Color SelectedDayColor
    {
        get
        {
            return _selectedDayColor;
        }
        set
        {
            _selectedDayColor = value;
            RaisePropertyChanged("SelectedDayColor");
        }
    }

    private int _selectedDayBorderSize = 1;

    public int SelectedDayBorderSize
    {
        get
        {
            return _selectedDayBorderSize;
        }
        set
        {
            _selectedDayBorderSize = value;
            RaisePropertyChanged("SelectedDayBorderSize");
        }
    }

    /// <summary>today's date, so only the real today is marked as today</summary>
    public static DateTime DateNow;

    /// <summary>
    /// the month being looked at. days spilling in from the months either
    /// side of it are greyed out
    /// </summary>
    public static DateTime ViewedMonth;

	private static Color NeedBookingColor = Color.FromArgb("601515"), BookinColor = Color.FromArgb("313A70");
	private static Color ColourCurrentDay = Color.FromArgb("00477A");
    private static Color MyGray = Color.FromArgb("1E1E1E");

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsBookedIn = false;

    public CalenderDay(int day, DateTime date)
    {
		Day = day;
		Date = date;
    }

    public void MoveDay(CalenderDay newDay)
    {
        foreach (Job j in Jobs)
        {
            if (!j.IsCompleted)
            {
                j.DueDate = newDay.Date;
                if (!j.IsBookedIn)
                    IsBookedIn = false;
            }
            newDay.Jobs.Add(j);
        }

        newDay.CalculateDay();

        Jobs.Clear();
        CalculateDay();
    }

    public void ResetColor()
    {
        BgColour = Colors.Transparent;
        TextColor = Colors.White;
        SelectedDayColor = Colors.White;
        SelectedDayBorderSize = 1;

        bool hasBookedinJobs = false;
        foreach (Job j in Jobs)
            if (j.IsBookedIn)
                hasBookedinJobs = true;
        //only the actual today is marked as today. this used to compare
        //against whichever month was on screen, so paging forward lit up the
        //same day number in that month as though it were today
        if (UsfulFuctions.Difference(Date, DateNow) == 0)
        {
            BgColour = ColourCurrentDay;
            return;
        }

        //days belonging to the months either side of the one being viewed
        if (Date.Month != ViewedMonth.Month || Date.Year != ViewedMonth.Year)
        {
            TextColor = Colors.Grey;
            BgColour = MyGray;
            return;
        }

        //days already gone
        if (UsfulFuctions.DifferenceSigned(Date, DateNow) < 0)
        {
            TextColor = Colors.Grey;
            BgColour = MyGray;
            return;
        }

        if (JobCount > 0)
            if (hasBookedinJobs)
                BgColour = BookinColor;
            else
                BgColour = NeedBookingColor;
    }
	public bool CalculateDay()
    {
		JobCount = 0;
		Amount = 0;
		EstimatedDuration = 0;
		foreach(Job j in Jobs)
        {
            JobCount++;
			Amount += j.Price;
			EstimatedDuration += j.EstimatedTime;
        }

        if (Amount == 0)
            ShowAmount = false;
        else
            ShowAmount = true;

        if (Jobs.Count == 0)
            ShowJobCount = false;
        else
            ShowJobCount = true;

        PaymentsTotal = 0;
        foreach (Payment p in Payment.Query())
            if (UsfulFuctions.Difference(p.Date, Date) == 0)
                PaymentsTotal += p.Amount;
        ShowPayments = PaymentsTotal != 0;

        ExpensesTotal = Expense.TotalForDate(Date);
        ShowExpenses = ExpensesTotal != 0;

        RaisePropertyChanged("FormatAmount");
        RaisePropertyChanged("FormatJobCount");
        RaisePropertyChanged("FormatPayments");
        RaisePropertyChanged("FormatExpenses");
        
        ResetColor();
		if (UsfulFuctions.Difference(Date, DateNow) == 0)
			return true;

        return false;
    }

    public void RaisePropertyChanged(string propertyName)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null)
        {
            handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}

public partial class CalenderView : ContentPage
{

    private ObservableCollection<Job> _jobsToDisplay = new ObservableCollection<Job>();
	public CalenderView()
	{
		InitializeComponent();
		_date = DateTime.Now;


    }

	private List<string> _days = new List<string>()
	{
		"Mo",
		"Tu",
		"We",
		"Th",
		"Fr",
		"Sa",
		"Su"
	};

	private List<string> _months = new List<string>()
	{
		"January",
		"Febuary",
		"March",
		"April",
		"May",
		"June",
		"July",
		"August",
		"September",
		"October",
		"November",
		"December"
	};

	private List<CalenderDay> _calenderDays = new List<CalenderDay>();

	private	DateTime _date;
    private CalenderDay _selectedDay = null;

    private bool _isPageBuilt = false;
	private bool BuildPage()
    {
        if (_isPageBuilt)
            return false;

        _isPageBuilt = true;
        //first day of our calinder will be the monday;

        l_date.Text = $"{_months[_date.Month - 1]} {_date.Year}";

        CalenderDay.DateNow = UsfulFuctions.DateNow;
        CalenderDay.ViewedMonth = _date;

        //calender always starts on the monday on or before the 1st of the month
        //and shows 6 full weeks so every month fits
        DateTime firstOfMonth = new DateTime(_date.Year, _date.Month, 1);
        DateTime startDate = firstOfMonth.AddDays(-(((int)firstOfMonth.DayOfWeek + 6) % 7));

        int x = 0, y = 1;

        Border border = null;
        _calenderDays.Clear();
        for (int i = 0; i < 7 * 6; i++)
        {
            DateTime d = startDate.AddDays(i);
            _calenderDays.Add(new CalenderDay(d.Day, d));
        }

        //day of week headers along the top row
        for (int i = 0; i < _days.Count; i++)
        {
            Label dayHeader = new Label()
            {
                Text = _days[i],
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            g_calender.Add(dayHeader, i, 0);
        }

        //now we go through all jobs and find out when they where cleanded
        //we only want jobs within range

        List<Job> jobs = Job.Query();

        DateTime startFileter = _calenderDays[0].Date;
        DateTime endFilter = _calenderDays[_calenderDays.Count - 1].Date;

        //a cancelled job is not work to turn up for, so it only stays on the
        //calendar if it was actually cleaned - that day's takings still count
        jobs.RemoveAll(x => x.HaveCanceled && !x.IsCompleted);

        jobs.RemoveAll(x => x.DateCompleated < startFileter && x.IsCompleted);
        jobs.RemoveAll(x => x.DateCompleated > endFilter && x.IsCompleted);
        jobs.RemoveAll(x => !x.IsCompleted && !x.IsBookedIn && x.DueDate < startFileter);
        jobs.RemoveAll(x => !x.IsCompleted && !x.IsBookedIn && x.DueDate > endFilter);
        jobs.RemoveAll(x => !x.IsCompleted && x.IsBookedIn && x.DateJobBookinFor < startFileter);
        jobs.RemoveAll(x => !x.IsCompleted && x.IsBookedIn && x.DateJobBookinFor > endFilter);

        foreach (CalenderDay cd in _calenderDays)
        {
            foreach (Job j in jobs)
            {
                if (j.IsCompleted)
                {
                    if (UsfulFuctions.Difference(cd.Date, j.DateCompleated) == 0)
                        cd.Jobs.Add(j);
                }
                else
                    if (j.IsBookedIn)
                {
                    if (UsfulFuctions.Difference(cd.Date, j.DateJobBookinFor) == 0)
                        cd.Jobs.Add(j);
                }
                else
                    if (UsfulFuctions.Difference(cd.Date, j.DueDate) == 0)
                        cd.Jobs.Add(j);

            }

            if (cd.CalculateDay())
                if (_selectedDay == null)
                    _selectedDay = cd;
        }

        Label l = null;
        for (int i = 0; i < _calenderDays.Count; i++)
        {
            //day number on top, then each figure on its own row
            VerticalStackLayout cell = new VerticalStackLayout();
            cell.VerticalOptions = LayoutOptions.Start;

            l = new Label() { FontSize = 10 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "Day");
            l.SetBinding(Label.TextColorProperty, "TextColor");
            cell.Add(l);

            l = new Label() { FontSize = 12 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "FormatAmount");
            l.SetBinding(Label.TextColorProperty, "TextColor");
            l.SetBinding(Label.IsVisibleProperty, "ShowAmount");
            cell.Add(l);

            l = new Label() { FontSize = 12 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "FormatJobCount");
            l.SetBinding(Label.TextColorProperty, "TextColor");
            l.SetBinding(Label.IsVisibleProperty, "ShowJobCount");
            cell.Add(l);

            l = new Label() { FontSize = 12 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "FormatPayments");
            l.SetBinding(Label.TextColorProperty, "TextColor");
            l.SetBinding(Label.IsVisibleProperty, "ShowPayments");
            cell.Add(l);

            l = new Label() { FontSize = 12 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "FormatExpenses");
            l.SetBinding(Label.TextColorProperty, "TextColor");
            l.SetBinding(Label.IsVisibleProperty, "ShowExpenses");
            cell.Add(l);

            border = new Border();
            border.ClassId = i.ToString();
            border.SetAppThemeColor(Border.StrokeProperty, Colors.Black, Colors.White);
            border.StrokeThickness = 1;
            border.StrokeShape = new Rectangle();
            border.Padding = 1;
            border.MinimumHeightRequest = 42; //keep empty days tappable and the grid legible
            border.Content = cell;
            //border.BackgroundColor = _calenderDays[i].BgColour;
            border.BindingContext = _calenderDays[i];
            border.SetBinding(Border.BackgroundColorProperty, "BgColour");
            border.SetBinding(Border.StrokeProperty, "SelectedDayColor");
            border.SetBinding(Border.StrokeThicknessProperty, "SelectedDayBorderSize");
            
            border.ClassId = i.ToString();
            TapGestureRecognizer tgr = new TapGestureRecognizer();
            tgr.Tapped += Tgr_Tapped;
            
            border.GestureRecognizers.Add(tgr);
            DragGestureRecognizer dgr = new DragGestureRecognizer();

            
            
            dgr.ClassId = i.ToString();
            dgr.DragStarting += Dgr_DragStarting;
            dgr.DropCompleted += Dgr_DropCompleted;
            border.GestureRecognizers.Add(dgr);

            DropGestureRecognizer dropgr = new DropGestureRecognizer();
            dropgr.DragOver += Dropgr_DragOver;
            dropgr.Drop += Dropgr_Drop;
            dropgr.ClassId = i.ToString();

            border.GestureRecognizers.Add(dropgr);

            TapGestureRecognizer dtgr = new TapGestureRecognizer();
            dtgr.NumberOfTapsRequired = 2;
            dtgr.ClassId = i.ToString();
            dtgr.Tapped += Dtgr_Tapped;

            border.GestureRecognizers.Add(dtgr);

            g_calender.Add(border, x, y);
            x++;
            if (x >= 7)
            {
                x = 0;
                y++;
            }
        }

      
        //at the point we need to refresh the current day
      

        if (_selectedDay.Jobs.Count > 0)
        {
            bool altColor = false;
            foreach (Job j in _selectedDay.Jobs)
            {

                if (Application.Current.PlatformAppTheme == AppTheme.Dark)
                {
                    if (altColor)
                        j.AltColour = WorkPlanner.altColorDark;
                    else
                        j.AltColour = WorkPlanner.MainColorDark;
                }
                else
                {
                    if (altColor)
                        j.AltColour = WorkPlanner.altColor;
                    else
                        j.AltColour = WorkPlanner.MainColor;
                }

                altColor = !altColor;
            }

            _jobsToDisplay.Clear();
            foreach (Job j in _selectedDay.Jobs)
                _jobsToDisplay.Add(j);
            
            l_noJobs.IsVisible = false;
        }
        else
        {
            l_noJobs.IsVisible = true;
            _jobsToDisplay.Clear();
            
        }


    //    _selectedDay.ResetColor();
//_selectedDay = dayTapped;
        _selectedDay.SelectedDayColor = Colors.Orange;
        _selectedDay.SelectedDayBorderSize = 2;


        RefreshPageDate();

        return true;
    }

    public async void ShowActionMenu(CalenderDay day)
    {
        int numberOfJobsBookedIn = 0;
        int numberOfJobsNotBookedIn = 0;
        int bookinJobToMsg = 0;
        int notbookinJobToMsg = 0;

        List<Job> jobsToText = new List<Job>();
        List<Job> jobsToEmail = new List<Job>();
        foreach (Job j in day.Jobs)
        {
            if (j.IsBookedIn)
            {
                numberOfJobsBookedIn++;
                if (j.TNB)
                {
                    jobsToText.Add(j);
                    bookinJobToMsg++;
                }
                if (j.ENB)
                {
                    jobsToEmail.Add(j);
                    bookinJobToMsg++;
                }
            }
            else
            {
                numberOfJobsNotBookedIn++;
                if (j.TNB)
                {
                    jobsToText.Add(j);
                    notbookinJobToMsg++;
                }
                if (j.ENB)
                {
                    jobsToEmail.Add(j);
                    notbookinJobToMsg++;
                }
            }
            
        }

        


        List<string> options = new List<string>();

        options.Add("Add Expense");

        if (day.Jobs.Count > 0)
        {
            if (numberOfJobsBookedIn > 0)
            {
                if (numberOfJobsNotBookedIn > 0)
                    options.Add($"Bookin Remaining {numberOfJobsNotBookedIn} Jobs");
                options.Add($"Cancel {numberOfJobsBookedIn} Jobs Booked In");
            }
            else
                options.Add("Bookin All Jobs");
        }

        if (notbookinJobToMsg + bookinJobToMsg > 0)
        {
            if (notbookinJobToMsg > 0)
                options.Add("Message Jobs Not Booked In");

            if (bookinJobToMsg > 0)
                options.Add("Message Jobs Booked In");

            options.Add("Message All Jobs");
        }

        string result = await DisplayActionSheet($"{day.Date.DayOfWeek} {day.Date.ToShortDateString()}", "Cancel", null, options.ToArray());
        if (result == null)
            return;
        if (result == "Add Expense")
        {
            NewExpense.ExpenseToEdit = null;
            NewExpense.JobToLink = null;
            NewExpense.DateToUse = day.Date;
            await Navigation.PushAsync(new NewExpense());
        }
        else
        if (result.Contains("Bookin Remaining"))
        {
            BookJobFormcs.jobs = day.Jobs.ToList();
            BookJobFormcs.jobs.RemoveAll(x => x.IsBookedIn);
            await Navigation.PushAsync(new BookJobFormcs());
            RefreshCalenderData();
            RefreshPageDate();
        }
        else
            if (result == "Bookin All Jobs")
        {
            BookJobFormcs.jobs = day.Jobs.ToList();
            await Navigation.PushAsync(new BookJobFormcs());
            RefreshCalenderData();
            RefreshPageDate();
        }
        else
            if (result.Contains("Cancel "))
        {
            await WorkPlanner.CancelBooking(day.Jobs, this, DateTime.Now);
            RefreshCalenderData();
            RefreshPageDate();
        }
        else
            if (result == "Message All Jobs")
        {
            if (jobsToText.Count > 0)
                await WorkPlanner.TextCustomers(jobsToText, day.Date, "", this);
            if (jobsToEmail.Count > 0)
                await WorkPlanner.EmailCustomers(jobsToEmail, day.Date, "", this);
        }
        else
            if (result == "Message Jobs Not Booked In")
        {
            jobsToEmail.RemoveAll(x => x.IsBookedIn);
            jobsToText.RemoveAll(x => x.IsBookedIn);
            if (jobsToText.Count > 0)
                await WorkPlanner.TextCustomers(jobsToText, day.Date, "", this);
            if (jobsToEmail.Count > 0)
                await WorkPlanner.EmailCustomers(jobsToEmail, day.Date, "", this);
        }
        else
            if (result == "Message Jobs Booked In")
        {
            jobsToEmail.RemoveAll(x => !x.IsBookedIn);
            jobsToText.RemoveAll(x => !x.IsBookedIn);
            if (jobsToText.Count > 0)
                await WorkPlanner.TextCustomers(jobsToText, day.Date, "", this);
            if (jobsToEmail.Count > 0)
                await WorkPlanner.EmailCustomers(jobsToEmail, day.Date, "", this);
        }

    }
    private void Dtgr_Tapped(object sender, EventArgs e)
    {
        Border b = sender as Border;
        CalenderDay dayTapped = _calenderDays[Convert.ToInt32(b.ClassId)];

        //nothing may be picked yet after moving month
        if (_selectedDay != null)
            _selectedDay.ResetColor();
        _selectedDay = dayTapped;
        dayTapped.SelectedDayColor = Colors.Orange;
        dayTapped.SelectedDayBorderSize = 2;

        if (dayTapped.Jobs.Count > 0)
            l_noJobs.IsVisible = false;
        else
            l_noJobs.IsVisible = true;
        RefreshPageDate();

        int index = Convert.ToInt32(b.ClassId);
        ShowActionMenu(_calenderDays[index]);
    }

    private void Dropgr_DragOver(object sender, DragEventArgs e)
    {
        
        DropGestureRecognizer dgr = sender as DropGestureRecognizer;
        dgr.BindingContext = null;
        int index = Convert.ToInt32(dgr.ClassId);
        if (_jobDraging != index)
        {

        }
    }

    private void Dgr_DropCompleted(object sender, DropCompletedEventArgs e)
    {
        //DragGestureRecognizer dgr = sender as DragGestureRecognizer;
        //int index = Convert.ToInt32(dgr.ClassId);
        _jobDraging = -1;
        //throw new NotImplementedException();
    }

    private async void MoveDay(int startday, int endDay)
    {

        
        List<Job> jobsText = new List<Job>();
        List<Job> jobsEmail = new List<Job>();

        foreach (Job j in _calenderDays[startday].Jobs)
            if (j.IsBookedIn)
            {
                if (j.TNB || j.ENB)
                {
                    jobsText.Add(j);
                }
                if (j.ENB)
                {
                    jobsEmail.Add(j);
                }
            }

        if (_calenderDays[endDay].Jobs.Count > 0)
        {
            if (_calenderDays[startday].Jobs.Count == 0)
                return;

            if (await DisplayAlert("Merge?", "This day already has work scedualed. Would You like to mearge the days?", "Yes", "No"))
            {

                if (jobsText.Count > 0 || jobsEmail.Count > 0)
                    if (await DisplayAlert("Notify Customers?", "Jobs for this day have been booked in. Whould you like to message customers to inform them of the change of date?", "Yes", "No"))
                    {
                        if (jobsText.Count > 0)
                            await WorkPlanner.TextCustomers(jobsText, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);
                        if (jobsEmail.Count > 0)
                            await WorkPlanner.EmailCustomers(jobsEmail, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);
                        return;
                    }

                _calenderDays[startday].MoveDay(_calenderDays[endDay]);
                _selectedDay = _calenderDays[endDay];
                RefreshPageDate();
                //BuildPage();
               // RefreshPageDate();
                Job.Save();
                return;
            }

            return;
        }

        if (jobsText.Count > 0 || jobsEmail.Count > 0)
            if (await DisplayAlert("Notify Customers?", "Jobs for this day have been booked in. Whould you like to message customers to inform them of the change of date?", "Yes", "No"))
            {
                if (jobsText.Count > 0)
                    await WorkPlanner.TextCustomers(jobsText, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);
                if (jobsEmail.Count > 0)
                    await WorkPlanner.EmailCustomers(jobsEmail, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);
                return;
            }
        _calenderDays[startday].MoveDay(_calenderDays[endDay]);
        _selectedDay = _calenderDays[endDay];
        RefreshPageDate();
        Job.Save();
      //  BuildPage();
    }
    private void Dropgr_Drop(object sender, DropEventArgs e)
    {
        DropGestureRecognizer dgr = sender as DropGestureRecognizer;
        int index = Convert.ToInt32(dgr.ClassId);
        e.Handled = true;

        //no need to move the day if its the same day
        if (_jobDraging == index)
            return;

        MoveDay(_jobDraging, index);
    }

    /// <summary>
    /// the current job that is been dragged.
    /// </summary>
    private int _jobDraging = -1;

    
    private void Dgr_DragStarting(object sender, DragStartingEventArgs e)
    {
        
        DragGestureRecognizer dgr = sender as DragGestureRecognizer;
      
        int index = Convert.ToInt32(dgr.ClassId);
        _jobDraging = index;

                 
        
     
    }

    private void Tgr_Tapped(object sender, EventArgs e)
    {
        Border b = sender as Border;
        CalenderDay dayTapped = _calenderDays[Convert.ToInt32(b.ClassId)];

        //nothing may be picked yet after moving month
        if (_selectedDay != null)
            _selectedDay.ResetColor();
        _selectedDay = dayTapped;
        dayTapped.SelectedDayColor = Colors.Orange;
        dayTapped.SelectedDayBorderSize = 2;

        if (dayTapped.Jobs.Count > 0)
            l_noJobs.IsVisible = false;
        else
            l_noJobs.IsVisible = true;
        RefreshPageDate();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        lv_Jobs.ItemsSource = _jobsToDisplay;

        BuildPage();
        RefreshCalenderData();
        RefreshPageDate();
        base.OnNavigatedTo(args);
    }

    private void bnt_nextMonthClicked(object sender, EventArgs e)
    {
		_date = _date.AddMonths(1);
        ClearDaySelection();
        RefreshCalenderData();
        RefreshPageDate();
    }

    private void bnt_previousMonthClicked(object sender, EventArgs e)
    {
        _date = _date.AddMonths(-1);
        ClearDaySelection();
        RefreshCalenderData();
        RefreshPageDate();
    }

    /// <summary>
    /// drops the picked day when moving to another month. the day that was
    /// picked is not in this month, so nothing should be marked as picked
    /// until one here is tapped
    /// </summary>
    private void ClearDaySelection()
    {
        if (_selectedDay != null)
        {
            _selectedDay.SelectedDayColor = Colors.White;
            _selectedDay.SelectedDayBorderSize = 1;
        }
        _selectedDay = null;
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
  

    private void swip_ended(object sender, SwipeEndedEventArgs e)
    {

    }

    private void On_Job_Compleated(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.MarkJobDone(j,this);
        RefreshPageDate();
    }

    private Job GetJobForSwipe(object sender)
    {
        //  List<Job> j = Job.Query(QueryType.JobId, Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
        if (_selectedDay.Jobs == null || _selectedDay.Jobs.Count == 0)
            return null;
        Job j = _selectedDay.Jobs.First(x => x.Id == Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
        if (j != null)
            return j;
        return null;
    }
    private async void On_Job_Paid(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        await WorkPlanner.MarkJobPaid(j, this);
        RefreshPageDate();
    }

    public void RefreshCalenderData()
    {
        l_date.Text = $"{_months[_date.Month - 1]} {_date.Year}";

        CalenderDay.DateNow = UsfulFuctions.DateNow;
        CalenderDay.ViewedMonth = _date;

        //same layout rule as BuildPage: 6 full weeks starting on the monday
        //on or before the 1st of the month
        DateTime firstOfMonth = new DateTime(_date.Year, _date.Month, 1);
        DateTime startDate = firstOfMonth.AddDays(-(((int)firstOfMonth.DayOfWeek + 6) % 7));

        for (int i = 0; i < _calenderDays.Count; i++)
        {
            DateTime d = startDate.AddDays(i);
            _calenderDays[i].Day = d.Day;
            _calenderDays[i].Date = d;
            _calenderDays[i].Jobs.Clear();
        }

        List<Job> jobs = Job.Query();

        DateTime startFileter = _calenderDays[0].Date;
        DateTime endFilter = _calenderDays[_calenderDays.Count - 1].Date;

        //see BuildPage: cancelled work only shows if it was cleaned anyway
        jobs.RemoveAll(x => x.HaveCanceled && !x.IsCompleted);

        jobs.RemoveAll(x => x.DateCompleated < startFileter && x.IsCompleted);
        jobs.RemoveAll(x => x.DateCompleated > endFilter && x.IsCompleted);
        jobs.RemoveAll(x => !x.IsCompleted && x.DueDate < startFileter);
        jobs.RemoveAll(x => !x.IsCompleted && x.DueDate > endFilter);

        foreach (CalenderDay cd in _calenderDays)
        {
           
            foreach (Job j in jobs)
            {
                if (j.IsCompleted)
                {
                    if (UsfulFuctions.Difference(cd.Date, j.DateCompleated) == 0)
                        cd.Jobs.Add(j);
                }
                else
                    if (UsfulFuctions.Difference(cd.Date, j.DueDate) == 0)
                    cd.Jobs.Add(j);

            }

            if (cd.CalculateDay())
                if (_selectedDay == null)
                    _selectedDay = cd;

        }

    }
    public void RefreshPageDate()
    {
        //no day picked - looking at a month that is not the one today is in
        if (_selectedDay == null)
        {
            l_currentDayName.Text = _date.ToString("MMMM yyyy");
            _jobsToDisplay.Clear();
            l_noJobs.Text = "Tap a day to see its work";
            l_noJobs.IsVisible = true;
            l_dayJobTotal.Text = $"Jobs {Gloable.CurrenceSymbol}0";
            l_dayPaymentTotal.Text = $"Paid {Gloable.CurrenceSymbol}0";
            l_dayExpenseTotal.IsVisible = false;
            return;
        }

        l_noJobs.Text = "No Jobs To Do";
        l_currentDayName.Text = $"{_selectedDay.Date.DayOfWeek} {_selectedDay.Day}/{_selectedDay.Date.Month}/{_selectedDay.Date.Year}";

        _jobsToDisplay.Clear();
        foreach (Job j in _selectedDay.Jobs)
        {
            j.CollapsedInList = j.IsCompleted;
            _jobsToDisplay.Add(j);
        }
        l_noJobs.IsVisible = _selectedDay.Jobs.Count == 0;

        float jobTotal = 0;
        foreach (Job j in _selectedDay.Jobs)
            jobTotal += j.Price;

        float paymentsTotal = 0;
        foreach (Payment p in Payment.Query())
            if (UsfulFuctions.Difference(p.Date, _selectedDay.Date) == 0)
                paymentsTotal += p.Amount;

        float expensesTotal = Expense.TotalForDate(_selectedDay.Date);

        l_dayJobTotal.Text = $"Jobs {Gloable.CurrenceSymbol}{jobTotal}";
        l_dayPaymentTotal.Text = $"Paid {Gloable.CurrenceSymbol}{paymentsTotal}";
        l_dayExpenseTotal.Text = $"Spent {Gloable.CurrenceSymbol}{expensesTotal:0.00}";
        l_dayExpenseTotal.IsVisible = expensesTotal != 0;
    }

    private void On_Job_More(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        WorkPlanner.ShowJobInfo(j, this);
    }

    private void On_Job_Skipped(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.MarkJobSkipped(j);
        RefreshPageDate();
    }

    private async void On_Job_Canceled(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        await WorkPlanner.MarkJobCancled(j, this);
        RefreshPageDate();
    }

    private void On_Job_Detials(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.EditJobDetails(j, this);
    }

    private void On_Job_Expense(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;
        WorkPlanner.AddExpenseForJob(j, this);
    }

    

    private void CollapsedJob_Tapped(object sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is Job j)
            j.CollapsedInList = false;
    }

    private void bnt_info_Clicked(object sender, EventArgs e)
    {
        ImageButton ib = sender as ImageButton;
        Job j = Job.Query(QueryType.JobId, Convert.ToInt32(ib.ClassId)).FirstOrDefault();
        WorkPlanner.ShowJobInfo(j, this);
    }

    private void Job_Street_Filter(object sender, EventArgs e)
    {

    }

}