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

	//how a day is filled: work still to do, work all done, and work left behind
	private static Color BookedColour = Color.FromArgb("EF6C00"), CompletedColour = Color.FromArgb("2E7D32");
	private static Color OverdueColour = Color.FromArgb("C62828");

	//work still to come that nobody has said when they are doing
	private static Color UnarrangedColour = Color.FromArgb("BF360C");
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
        SelectedDayColor = Colors.White;
        SelectedDayBorderSize = 1;

        //only the actual today counts as today. this used to compare against
        //whichever month was on screen, so paging forward lit up the same day
        //number in that month as though it were today
        bool today = UsfulFuctions.Difference(Date, DateNow) == 0;
        bool outsideMonth = Date.Month != ViewedMonth.Month || Date.Year != ViewedMonth.Year;
        bool past = UsfulFuctions.DifferenceSigned(Date, DateNow) < 0;

        //days from the neighbouring months, and days already gone, are dimmed
        //so the month being viewed still reads as a block
        TextColor = outsideMonth || past ? Colors.Grey : Colors.White;

        if (Jobs.Count == 0)
        {
            //a day with nothing on it is left clear, so the days carrying work
            //are the only ones with any colour to them
            if (today)
                BgColour = ColourCurrentDay;
            else if (outsideMonth)
                BgColour = MyGray;
            else
                BgColour = Colors.Transparent;
            return;
        }

        BgColour = WorkColour(past);

        //A day already gone is only dimmed while it has nothing on it. Once
        //it is filled with a work colour the text has to be read against that
        //fill instead, and the grey above on the done green cannot be read at
        //all - a week of finished work was a row of green squares with
        //nothing legible in them.
        //
        //The two fills that are there to warn - the overdue red and the dark
        //orange red on work nobody has arranged - are dark enough to want
        //white on them. The working orange and the done green take black.
        TextColor = (past && !AllDone) || UnarrangedWork ? Colors.White : Colors.Black;

        //the work colour owns the fill, so today is marked with a ring instead
        //of losing the thing the fill is there to say
        if (today)
        {
            SelectedDayColor = ColourCurrentDay;
            SelectedDayBorderSize = 3;
        }
    }

    /// <summary>
    /// how a day with work on it is filled: orange while there is still work to
    /// do, green once it is all done, and a blend of the two part way through so
    /// a day reads as more done the greener it gets. Work still outstanding on a
    /// day that has already gone is overdue, and goes red instead.
    ///
    /// Work still to come that has no day arranged for it starts from a dark
    /// orange red rather than the orange, so a week ahead shows at a glance
    /// which of its days have actually been planned and which are just work
    /// falling due.
    /// </summary>
    private Color WorkColour(bool past)
    {
        if (AllDone)
            return CompletedColour;

        //cancelled jobs are dropped before they ever reach a day, so anything
        //left unfinished here is work that was genuinely missed
        if (past)
            return OverdueColour;

        int done = 0;
        foreach (Job j in Jobs)
            if (j.IsCompleted)
                done++;

        Color from = UnarrangedWork ? UnarrangedColour : BookedColour;

        return LerpColour(from, CompletedColour, (float)done / Jobs.Count);
    }

    /// <summary>
    /// this day has work still to come on it that nobody has said when they
    /// are doing.
    ///
    /// Only the days still ahead: today's work is being done rather than
    /// arranged, and a day already gone has its own colour for work that was
    /// missed. Booked work is shown on the day it is booked for rather than
    /// the day it fell due, so anything sat here unbooked really is work with
    /// no day against it.
    /// </summary>
    private bool UnarrangedWork
    {
        get
        {
            if (UsfulFuctions.DifferenceSigned(Date, DateNow) <= 0)
                return false;

            foreach (Job j in Jobs)
                if (!j.IsCompleted && !j.IsBookedIn)
                    return true;

            return false;
        }
    }

    /// <summary>
    /// everything on the day is done. a day with nothing on it is not work
    /// finished, so it does not count - both callers have already dealt with
    /// the empty days by the time they ask
    /// </summary>
    private bool AllDone
    {
        get
        {
            foreach (Job j in Jobs)
                if (!j.IsCompleted)
                    return false;

            return Jobs.Count > 0;
        }
    }

    /// <summary>straight blend between two colours, amount 0 gives from, 1 gives to</summary>
    private static Color LerpColour(Color from, Color to, float amount)
    {
        return new Color(
            from.Red + (to.Red - from.Red) * amount,
            from.Green + (to.Green - from.Green) * amount,
            from.Blue + (to.Blue - from.Blue) * amount);
    }
	public bool CalculateDay()
    {
		JobCount = 0;
		Amount = 0;
		EstimatedDuration = 0;
		foreach(Job j in Jobs)
        {
            JobCount++;
			//what the job is actually worth: a job cleaned at a front only
			//price counted as the full price here
			Amount += j.EffectivePrice;
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
        PopulateDays();

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
            ShowDaysWork();
            l_noJobs.IsVisible = false;
        }
        else
        {
            l_noJobs.IsVisible = true;
            _jobsToDisplay.Clear();
            
        }


    //    _selectedDay.ResetColor();
//_selectedDay = dayTapped;
        _selectedDay.SelectedDayColor = Colors.White;
        _selectedDay.SelectedDayBorderSize = 3;


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
            //the day is already picked, so the form opens on it
            BookJobFormcs.BookForDate = day.Date;
            await Navigation.PushAsync(new BookJobFormcs());
            RefreshCalenderData();
            RefreshPageDate();
        }
        else
            if (result == "Bookin All Jobs")
        {
            BookJobFormcs.jobs = day.Jobs.ToList();
            BookJobFormcs.BookForDate = day.Date;
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
        dayTapped.SelectedDayColor = Colors.White;
        dayTapped.SelectedDayBorderSize = 3;

        //picking a day is asking for that day, so any filter comes off
        _showingOwing = false;

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
        dayTapped.SelectedDayColor = Colors.White;
        dayTapped.SelectedDayBorderSize = 3;

        //picking a day is asking for that day, so any filter comes off
        _showingOwing = false;

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
  

    private void swip_ended(object sender, SwipeEndedEventArgs e)
    {

    }

    private async void On_Job_Compleated(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;

        //this slot says Clear once the job has been marked
        if (j.IsMarked)
            await WorkPlanner.ClearJob(j, this);
        else
            WorkPlanner.MarkJobDone(j, this);

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

        PopulateDays();
    }

    /// <summary>
    /// puts every job on the day it belongs to: work that is done on the day
    /// it was cleaned, work that is booked in on the day it is booked for,
    /// and everything else on the day it falls due.
    ///
    /// both building the calendar and refreshing it come through here, so a
    /// job cannot sit on one day when the page is built and another when it
    /// is refreshed
    /// </summary>
    private void PopulateDays()
    {
        List<Job> jobs = Job.Query();

        DateTime startFileter = _calenderDays[0].Date.Date;
        DateTime endFilter = _calenderDays[_calenderDays.Count - 1].Date.Date;

        //a cancelled job is not work to turn up for, so it only stays on the
        //calendar if it was actually cleaned - that day's takings still count
        jobs.RemoveAll(x => x.HaveCanceled && !x.IsCompleted);

        //each job is thrown away on the same date it would have been shown
        //against, so nothing is dropped for being due outside the month while
        //it is booked in for a day inside it
        jobs.RemoveAll(x => x.IsCompleted
            && (x.DateCompleated.Date < startFileter || x.DateCompleated.Date > endFilter));
        jobs.RemoveAll(x => !x.IsCompleted && x.IsBookedIn
            && (x.DateJobBookinFor.Date < startFileter || x.DateJobBookinFor.Date > endFilter));
        jobs.RemoveAll(x => !x.IsCompleted && !x.IsBookedIn
            && (x.DueDate.Date < startFileter || x.DueDate.Date > endFilter));

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
    }
    /// <summary>
    /// The day's work as it is shown: what is still to do at the top, the
    /// jobs already done pushed to the bottom, and the row striping worked
    /// out over that order rather than the day's own - otherwise the shading
    /// stops alternating as soon as anything is moved.
    /// </summary>
    private void ShowDaysWork()
    {
        List<Job> ordered = WorkPlanner.DoneAtTheBottom(_selectedDay.Jobs);

        bool dark = Application.Current.PlatformAppTheme == AppTheme.Dark;
        bool altColor = false;

        foreach (Job j in ordered)
        {
            if (dark)
                j.AltColour = altColor ? WorkPlanner.altColorDark : WorkPlanner.MainColorDark;
            else
                j.AltColour = altColor ? WorkPlanner.altColor : WorkPlanner.MainColor;

            altColor = !altColor;
        }

        _jobsToDisplay.Clear();
        foreach (Job j in ordered)
        {
            j.CollapsedInList = j.IsCompleted;
            _jobsToDisplay.Add(j);
        }
    }

    /// <summary>the list is showing everyone who owes rather than a day's work</summary>
    private bool _showingOwing = false;

    /// <summary>
    /// What has come in on a day begs the other question - who has not paid.
    /// Tapping the paid total answers it: everyone with money outstanding,
    /// wherever they are in the round and whenever they were last done.
    /// </summary>
    private void l_dayPaymentTotal_Tapped(object sender, EventArgs e)
    {
        //nothing to narrow down until a day has been picked
        if (_selectedDay == null)
            return;

        _showingOwing = !_showingOwing;
        RefreshPageDate();
    }

    private void bnt_clearFilter_Clicked(object sender, EventArgs e)
    {
        _showingOwing = false;
        RefreshPageDate();
    }

    /// <summary>
    /// The houses on this day that still owe something.
    ///
    /// This narrows the day down rather than going off across the round: you
    /// are looking at a day, the paid total says what came in on it, and this
    /// is the other half of that - who on this day has not paid.
    /// </summary>
    private void ShowOwingJobs()
    {
        List<Job> owing = new List<Job>();

        foreach (Job j in _selectedDay.Jobs)
        {
            if (j.HaveCanceled || j.CustomerId == -1)
                continue;

            Customer c = j.GetCustomer();
            if (c == null || c.Balance <= 0)
                continue;

            owing.Add(j);
        }

        owing = owing
            .OrderByDescending(x => x.GetCustomer().Balance)
            .ToList();

        float total = 0;
        foreach (Job j in owing)
            total += j.GetCustomer().Balance;

        bool dark = Application.Current.PlatformAppTheme == AppTheme.Dark;
        bool altColor = false;

        _jobsToDisplay.Clear();
        foreach (Job j in owing)
        {
            if (dark)
                j.AltColour = altColor ? WorkPlanner.altColorDark : WorkPlanner.MainColorDark;
            else
                j.AltColour = altColor ? WorkPlanner.altColor : WorkPlanner.MainColor;
            altColor = !altColor;

            //these are being looked at for what is owed, so they are opened up
            j.CollapsedInList = false;
            j.Refresh();
            j.RefreshColors();
            _jobsToDisplay.Add(j);
        }

        hsl_filter.IsVisible = true;
        l_filter.Text = owing.Count == 0
            ? "Nobody owes on this day"
            : $"{owing.Count} owing {Gloable.CurrenceSymbol}{total:0.00}";

        l_noJobs.IsVisible = owing.Count == 0;
        l_noJobs.Text = "Nobody owes on this day";

        //the day's own figures are about the whole day, not this part of it
        l_dayProgress.IsVisible = false;
        l_dayTimeLeft.IsVisible = false;
    }

    /// <summary>
    /// the work on the picked day that could still be booked in: not done,
    /// not cancelled, and not booked in already
    /// </summary>
    private List<Job> JobsToBookIn()
    {
        List<Job> jobs = new List<Job>();

        if (_selectedDay == null)
            return jobs;

        foreach (Job j in _selectedDay.Jobs)
            if (!j.IsCompleted && !j.HaveCanceled && !j.IsBookedIn)
                jobs.Add(j);

        return jobs;
    }

    /// <summary>
    /// Work due on a day that has not come round yet is exactly what wants
    /// booking in, and tapping the day is how somebody asks about it. Finding
    /// that took a double tap on the day, which nobody does on a phone, so a
    /// day still to come now offers it as a button as soon as it is tapped.
    ///
    /// Today is left out on purpose: today's work is being done, not arranged.
    /// A day already gone cannot be booked in at all.
    /// </summary>
    private void ShowBookInOption()
    {
        bnt_bookDayIn.IsVisible = false;

        if (_selectedDay == null)
            return;

        if (UsfulFuctions.DifferenceSigned(_selectedDay.Date, CalenderDay.DateNow) <= 0)
            return;

        int toBook = JobsToBookIn().Count;
        if (toBook == 0)
            return;

        //some of the day may already be booked in, and saying so is the
        //difference between the button looking wrong and looking right
        int alreadyBooked = 0;
        foreach (Job j in _selectedDay.Jobs)
            if (!j.IsCompleted && !j.HaveCanceled && j.IsBookedIn)
                alreadyBooked++;

        if (alreadyBooked == 0)
            bnt_bookDayIn.Text = toBook == 1 ? "Book It In" : $"Book All {toBook} In";
        else
            bnt_bookDayIn.Text = toBook == 1 ? "Book The Other One In" : $"Book The Other {toBook} In";

        bnt_bookDayIn.IsVisible = true;
    }

    private async void bnt_bookDayIn_Clicked(object sender, EventArgs e)
    {
        List<Job> jobs = JobsToBookIn();
        if (jobs.Count == 0)
            return;

        //the day is already picked, so the form opens on it rather than on
        //today with the date to put in again
        BookJobFormcs.jobs = jobs;
        BookJobFormcs.BookForDate = _selectedDay.Date;
        await Navigation.PushAsync(new BookJobFormcs());
    }

    public void RefreshPageDate()
    {
        hsl_filter.IsVisible = false;
        bnt_bookDayIn.IsVisible = false;

        if (_showingOwing && _selectedDay != null)
        {
            l_currentDayName.Text = $"Owing on {_selectedDay.Date:ddd dd MMM yyyy}";
            ShowOwingJobs();
            return;
        }

        //a day that is no longer picked cannot be narrowed down
        _showingOwing = false;

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
            l_dayProgress.IsVisible = false;
            l_dayTimeLeft.IsVisible = false;
            return;
        }

        l_noJobs.Text = "No Jobs To Do";
        l_currentDayName.Text = $"{_selectedDay.Date.DayOfWeek} {_selectedDay.Day}/{_selectedDay.Date.Month}/{_selectedDay.Date.Year}";

        ShowDaysWork();
        l_noJobs.IsVisible = _selectedDay.Jobs.Count == 0;

        float jobTotal = 0;
        foreach (Job j in _selectedDay.Jobs)
            jobTotal += j.EffectivePrice;

        float paymentsTotal = 0;
        foreach (Payment p in Payment.Query())
            if (UsfulFuctions.Difference(p.Date, _selectedDay.Date) == 0)
                paymentsTotal += p.Amount;

        float expensesTotal = Expense.TotalForDate(_selectedDay.Date);

        l_dayJobTotal.Text = $"Jobs {Gloable.CurrenceSymbol}{jobTotal}";
        l_dayPaymentTotal.Text = $"Paid {Gloable.CurrenceSymbol}{paymentsTotal}";
        l_dayExpenseTotal.Text = $"Spent {Gloable.CurrenceSymbol}{expensesTotal:0.00}";
        l_dayExpenseTotal.IsVisible = expensesTotal != 0;

        ShowDayProgress();
        ShowBookInOption();
    }

    /// <summary>
    /// How the day is going: how much of it is done, and roughly how long
    /// what is left will take.
    ///
    /// A job with no estimate of its own falls back to the default job time
    /// from the settings page, so the figure is not quietly optimistic on a
    /// round that has never had times filled in. Cancelled jobs are not work
    /// left and are not counted either way.
    /// </summary>
    private void ShowDayProgress()
    {
        int done = 0, left = 0, minutesLeft = 0;

        foreach (Job j in _selectedDay.Jobs)
        {
            if (j.HaveCanceled)
                continue;

            if (j.IsCompleted)
            {
                done++;
                continue;
            }

            left++;
            minutesLeft += j.EstimatedTime > 0 ? j.EstimatedTime : Settings.DefaultJobDuration;
        }

        int total = done + left;

        l_dayProgress.IsVisible = total > 0;
        if (left == 0)
            l_dayProgress.Text = total == 1 ? "Done" : $"All {total} done";
        else
            l_dayProgress.Text = $"{done} of {total} done, {left} left";

        //no times filled in anywhere - better to say nothing than "0m left"
        l_dayTimeLeft.IsVisible = minutesLeft > 0;
        l_dayTimeLeft.Text = $"About {FormatMinutes(minutesLeft)} left";
    }

    /// <summary>minutes as a person would say them - 2h 30m, 45m, 3h</summary>
    private static string FormatMinutes(int minutes)
    {
        int hours = minutes / 60;
        int rest = minutes % 60;

        if (hours == 0)
            return $"{rest}m";
        if (rest == 0)
            return $"{hours}h";
        return $"{hours}h {rest}m";
    }

    private void On_Job_More(object sender, EventArgs e)
    {
        WorkPlanner.ShowJobStatus(GetJobForSwipe(sender), this, RefreshPageDate);
    }

    private async void On_Job_DoAgain(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;

        if (await WorkPlanner.DoJobAgain(j, this))
            RefreshPageDate();
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