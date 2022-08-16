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

    public static DateTime DateNow;

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
        if (UsfulFuctions.Difference(Date, DateNow) == 0)
        {
            BgColour = ColourCurrentDay;
            return;
        }

        if (Date.Month != DateNow.Month || Date.Day < DateNow.Day)
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
        RaisePropertyChanged("FormatAmount");
        RaisePropertyChanged("FormatJobCount");
        
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

        CalenderDay.DateNow = _date;
        _date.AddDays(1);
        int monday = (int)DayOfWeek.Monday + 1;

        int startDayOfWeek = (int)new DateTime(_date.Year, _date.Month, 1).DayOfWeek; //get the frist of the month
        int daysInMonth = DateTime.DaysInMonth(_date.Year, _date.Month);

        DateTime lastMonth = _date.AddMonths(-1); //go back one month
        DateTime nextMonth = _date.AddMonths(1);
        int daysInLastMonth = DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month);
        DateTime[] dates = new DateTime[7 * 5];

        int todaysLocation = startDayOfWeek + _date.Day;
        int x = 0, y = 1;

     

        VerticalStackLayout vsl = null;
        Border border = null;
        _calenderDays.Clear();
        for (int i = monday; i < 7 * 5 + monday; i++)
        {
            if (i <= startDayOfWeek)
                _calenderDays.Add(new CalenderDay(daysInLastMonth - (startDayOfWeek - i), new DateTime(lastMonth.Year, lastMonth.Month, daysInLastMonth - (startDayOfWeek - i))));
            else
                if (i - startDayOfWeek > daysInMonth)
                _calenderDays.Add(new CalenderDay(i - startDayOfWeek - daysInMonth, new DateTime(nextMonth.Year, nextMonth.Month, i - startDayOfWeek - daysInMonth)));
            else
                _calenderDays.Add(new CalenderDay(i - startDayOfWeek, new DateTime(_date.Year, _date.Month, i - startDayOfWeek)));
        }

        //now we go through all jobs and find out when they where cleanded
        //we only want jobs within range

        List<Job> jobs = Job.Query();

        DateTime startFileter = _calenderDays[0].Date;
        DateTime endFilter = _calenderDays[_calenderDays.Count - 1].Date;

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
            vsl = new VerticalStackLayout();
            
            vsl.HorizontalOptions = LayoutOptions.StartAndExpand;
            vsl.VerticalOptions = LayoutOptions.StartAndExpand;

            l = new Label() { FontSize = 10 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "Day");
            l.SetBinding(Label.TextColorProperty, "TextColor");


            vsl.Add(l);


            l = new Label() { FontSize = 12 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "FormatAmount");
            l.SetBinding(Label.TextColorProperty, "TextColor");
            l.SetBinding(Label.IsVisibleProperty, "ShowAmount");
            vsl.Add(l);

            
            l = new Label() { FontSize = 12 };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextProperty, "FormatJobCount");
            l.SetBinding(Label.TextColorProperty, "TextColor");
            l.SetBinding(Label.IsVisibleProperty, "ShowJobCount");

            vsl.Add(l);
            // vsl.Add(new Label() { Text = $"{_calenderDays[i].EstimatedDuration}" });



            border = new Border();
            border.ClassId = i.ToString();
            border.SetAppThemeColor(Border.StrokeProperty, Colors.Black, Colors.White);
            border.StrokeThickness = 1;
            border.StrokeShape = new Rectangle();
            border.Padding = 1;
            border.Content = vsl;
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
        if (day.Jobs.Count == 0)
            return;

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

        if (numberOfJobsBookedIn > 0)
        {
            if (numberOfJobsNotBookedIn > 0)
                options.Add($"Bookin Remaining {numberOfJobsNotBookedIn} Jobs");
            options.Add($"Cancel {numberOfJobsBookedIn} Jobs Booked In");
        }
        else
            options.Add("Bookin All Jobs");

        if (notbookinJobToMsg + bookinJobToMsg > 0)
        {
            if (notbookinJobToMsg > 0)
                options.Add("Message Jobs Not Booked In");

            if (bookinJobToMsg > 0)
                options.Add("Message Jobs Booked In");

            options.Add("Message All Jobs");
        }

        string result = await DisplayActionSheet($"{day.Date.DayOfWeek} {day.Date.ToShortDateString()}", "Cancel", "", options.ToArray());

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
                            await WorkPlanner.EmailCustomers(jobsText, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);
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
                    await WorkPlanner.EmailCustomers(jobsText, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);
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
        RefreshPageDate();
        RefreshCalenderData();
    }

    private void bnt_previousMonthClicked(object sender, EventArgs e)
    {
        _date = _date.AddMonths(-1);
        RefreshPageDate();
        RefreshCalenderData();
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
    private void On_Job_Paid(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.MarkJobPaid(j);
        RefreshPageDate();
    }

    public void RefreshCalenderData()
    {
        l_date.Text = $"{_months[_date.Month - 1]} {_date.Year}";

        CalenderDay.DateNow = _date;
        _date.AddDays(1);
        int monday = (int)DayOfWeek.Monday + 1;

        int startDayOfWeek = (int)new DateTime(_date.Year, _date.Month, 1).DayOfWeek; //get the frist of the month
        int daysInMonth = DateTime.DaysInMonth(_date.Year, _date.Month);

        DateTime lastMonth = _date.AddMonths(-1); //go back one month
        DateTime nextMonth = _date.AddMonths(1);
        int daysInLastMonth = DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month);
        DateTime[] dates = new DateTime[7 * 5];

        int todaysLocation = startDayOfWeek + _date.Day;
        int x = 0, y = 1;



        VerticalStackLayout vsl = null;
        Border border = null;
      

        //now we go through all jobs and find out when they where cleanded
        //we only want jobs within range

       

        int c = 0;
        for (int i = monday; i < 7 * 5 + monday; i++)
        {
            if (i <= startDayOfWeek)
            {
                _calenderDays[c].Day = daysInLastMonth - (startDayOfWeek - i);
                _calenderDays[c].Date = new DateTime(lastMonth.Year, lastMonth.Month, daysInLastMonth - (startDayOfWeek - i));
            }
            else
                if (i - startDayOfWeek > daysInMonth)
            {
                _calenderDays[c].Day = i - startDayOfWeek - daysInMonth;
                _calenderDays[c].Date = new DateTime(nextMonth.Year, nextMonth.Month, i - startDayOfWeek - daysInMonth);
            }
            else
            {
                _calenderDays[c].Day = i - startDayOfWeek;
                _calenderDays[c].Date = new DateTime(_date.Year, _date.Month, i - startDayOfWeek);
            }
            _calenderDays[c].Jobs.Clear();
            c++;
        }

        List<Job> jobs = Job.Query();

        DateTime startFileter = _calenderDays[0].Date;
        DateTime endFilter = _calenderDays[_calenderDays.Count - 1].Date;

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
        l_currentDayName.Text = $"{_selectedDay.Date.DayOfWeek} {_selectedDay.Day}/{_selectedDay.Date.Month}/{_selectedDay.Date.Year}";

        _jobsToDisplay.Clear();
        foreach (Job j in _selectedDay.Jobs)
            _jobsToDisplay.Add(j);
    }

    private void On_Job_More(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);

        RefreshPageDate();
    }

    private void On_Job_Skipped(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.MarkJobSkipped(j);
        RefreshPageDate();
    }

    private void On_Job_Canceled(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.MarkJobCancled(j, this);
        RefreshPageDate();
    }

    private void On_Job_Detials(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.EditJobDetails(j, this);
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

    private bool _gridIsVisble = true;
    private void bnt_minimizeCalender_Clicked(object sender, EventArgs e)
    {
        g_calender.IsVisible = _gridIsVisble = !_gridIsVisble;
        hsl_monthSelector.IsVisible = _gridIsVisble;
        if (!g_calender.IsVisible)
        {
            bnt_minimizeCalender.Text = ">";
            bnt_minimizeCalender1.Text = ">";

        }
        else
        {
            bnt_minimizeCalender.Text = "<";
            bnt_minimizeCalender1.Text = "<";
        }
    }
}