namespace UiInterface.Layouts;

using Microsoft.Maui.Controls.Shapes;
using Kernel;
using UiInterface.Controles;
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
    private bool _showNote;

    /// <summary>
    /// true when something is written against this day. A note nobody can see
    /// without tapping the day is a note nobody reads, so the day carries a
    /// mark on the grid - the day panel is where the note itself is said
    /// </summary>
    public bool ShowNote
    {
        get
        {
            return _showNote;
        }
        set
        {
            _showNote = value;
            RaisePropertyChanged("ShowNote");
        }
    }

    /// <summary>
    /// the mark a day with a note carries. A pencil rather than a dot: the
    /// day is already coloured by its work and a coloured dot on top of that
    /// would read as more of the same
    /// </summary>
    public static string NoteMark = "✎";

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

    /// <summary>
    /// the weekend, shaded a little differently from the working week so a
    /// month can be counted without reading the headings.
    ///
    /// It is a wash rather than a colour - the same slate over whatever the
    /// page is behind it, so the day numbers still read. It is only ever what
    /// a day with nothing on it is filled with: a day carrying work keeps its
    /// work colour, because that fill is saying something and the weekend is
    /// not worth losing it over - the same reason today is marked with a ring
    /// rather than a fill.
    ///
    /// Two of them, because one alpha cannot serve both themes: the wash that
    /// is barely there on a dark page is a blue-grey block on a near white
    /// one. The theme is asked at the time the colour is set - the same
    /// question the rest of this page asks - so a page built after the phone
    /// changes theme comes out right, and one already on screen does not.
    /// </summary>
    private static Color WeekendColour
    {
        get
        {
            return Application.Current != null && Application.Current.PlatformAppTheme == AppTheme.Dark
                ? Color.FromArgb("#33607D8B")
                : Color.FromArgb("#26607D8B");
        }
    }

    /// <summary>the same wash over the grey the months either side are dimmed
    /// with, so the weekend columns do not break at the edge of the month</summary>
    private static Color WeekendOutsideMonth = Color.FromArgb("2A2E30");

    /// <summary>Sa and Su on the headings, which is the half of this that
    /// survives a weekend covered in work colours</summary>
    public static Color WeekendHeading = Color.FromArgb("8FA8B4");

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsBookedIn = false;

    public CalenderDay(int day, DateTime date)
    {
		Day = day;
		Date = date;
    }

    /// <summary>
    /// Moves this day's work on to another day.
    ///
    /// Work booked in is put on the calendar by the day it is booked for
    /// rather than by the day it is due, so the booking has to be moved as
    /// well as the due date. Moving the due date on its own looked right
    /// until the page was built from the jobs again - the day dropped on
    /// showed the work, and the next start put it back where it had been.
    ///
    /// A job already done stays where it is: it was done on the day it was
    /// done, and that is what the month's takings are worked out from.
    /// </summary>
    /// <returns>how many jobs actually moved</returns>
    public int MoveDay(CalenderDay newDay)
    {
        List<Job> moving = new List<Job>();

        foreach (Job j in Jobs)
            if (!j.IsCompleted)
                moving.Add(j);

        foreach (Job j in moving)
        {
            j.DueDate = newDay.Date;

            if (j.IsBookedIn)
                j.DateJobBookinFor = newDay.Date;
            else
                IsBookedIn = false;

            Jobs.Remove(j);
            newDay.Jobs.Add(j);
        }

        newDay.CalculateDay();
        CalculateDay();

        return moving.Count;
    }

    /// <summary>saturday or sunday. off the date rather than off which column
    /// the cell landed in, so nothing depends on the grid starting on a
    /// monday</summary>
    public bool IsWeekend
    {
        get { return Date.DayOfWeek == DayOfWeek.Saturday || Date.DayOfWeek == DayOfWeek.Sunday; }
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
                BgColour = IsWeekend ? WeekendOutsideMonth : MyGray;
            else
                BgColour = IsWeekend ? WeekendColour : Colors.Transparent;
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

			//what the job counts as taking, the round's usual included, so the
			//day says the same as the rows on it
			EstimatedDuration += j.Minutes;
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

        ShowNote = DayNote.Has(Date);

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

    /// <summary>the rows the day list draws: street headings with the
    /// day's houses under them, or a flat run of jobs on the owed view</summary>
    private ObservableCollection<object> _jobsToDisplay = new ObservableCollection<object>();
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

            //Sa and Su named in the weekend's own colour. the wash under the
            //days is lost on any day carrying work, so the heading is what
            //says where the weekend is on a busy month. read off the date the
            //column really holds rather than off the position in the list
            DayOfWeek dayOfWeek = startDate.AddDays(i).DayOfWeek;
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                dayHeader.TextColor = CalenderDay.WeekendHeading;

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

            //a day with something written against it says so on the grid.
            //the note itself is far too long for a cell this size - this is
            //only what makes somebody tap the day and read it
            l = new Label() { FontSize = 12, Text = CalenderDay.NoteMark };
            l.BindingContext = _calenderDays[i];
            l.SetBinding(Label.TextColorProperty, "TextColor");
            l.SetBinding(Label.IsVisibleProperty, "ShowNote");
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
      

        //the day's list and whether anything is on it are RefreshPageDate's
        //to say, at the bottom of this method: it is the one place that knows
        //what is being left off the day - see ShowDaysWork

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

        //asked for once and compared against, rather than worked out twice:
        //the two would have to be changed together, and the one that was not
        //would quietly fall through to whatever is below it
        string noteOption = DayNoteEditor.ButtonText(day.Date);

        options.Add(noteOption);
        options.Add("Add Expense");

        if (day.Jobs.Count > 0)
        {
            if (numberOfJobsBookedIn > 0)
            {
                if (numberOfJobsNotBookedIn > 0)
                    options.Add($"Bookin Remaining {numberOfJobsNotBookedIn} Jobs");
                options.Add($"Cancel {numberOfJobsBookedIn} Jobs Booked In");
                //the booked day handed to somebody else's copy of the app -
                //the same Send Work the work list and the booked work page
                //use, and opt-in on the settings page like them. a day
                //already out with somebody is not offered again
                if (Settings.EnableWorkSharing
                    && day.Jobs.Any(x => x != null && x.IsBookedIn && x.CustomerId != -1
                        && !x.IsCompleted && !x.HaveCanceled && !WorkShare.IsOut(x)))
                    options.Add("Send Booked In Jobs To Someone");
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
        if (result == noteOption)
        {
            if (await DayNoteEditor.ChangeAsync(day.Date, this))
            {
                day.ShowNote = DayNote.Has(day.Date);
                ShowDayNote();
            }
            return;
        }
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
            //the day being cancelled, not today: it is the day whose booking
            //is taken off, and the day the question names
            await WorkPlanner.CancelBooking(day.Jobs, this, day.Date);
            RefreshCalenderData();
            RefreshPageDate();
        }
        else
            if (result == "Send Booked In Jobs To Someone")
        {
            //what is already done, cancelled or already out with somebody
            //stays home - it is not work to hand over. sending changes
            //nothing about the booking
            List<Job> toSend = day.Jobs.Where(x => x != null && x.IsBookedIn
                && !x.IsCompleted && !x.HaveCanceled && x.CustomerId != -1
                && !WorkShare.IsOut(x)).ToList();

            if (toSend.Count == 0)
                await DisplayAlert("Send To Someone",
                    "Everything booked for this day is already done or already out with somebody - there is nothing left to send.", "Ok");
            else
                await Navigation.PushAsync(new SendWork(toSend));
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

            if (!await DisplayAlert("Merge?", "This day already has work scheduled. Would you like to merge the days?", "Yes", "No"))
                return;
        }

        await NotifyIfWanted(jobsText, jobsEmail, endDay);
        await MoveTheWork(startday, endDay);
    }

    /// <summary>
    /// offers to tell the customers booked in for the day that it is moving.
    ///
    /// Saying yes to this used to be the one way to move a day and have
    /// nothing happen: the messages went out and the work was left where it
    /// was, because the answer was taken as a reason to stop rather than as
    /// something to do on the way.
    /// </summary>
    private async Task NotifyIfWanted(List<Job> toText, List<Job> toEmail, int endDay)
    {
        if (toText.Count == 0 && toEmail.Count == 0)
            return;

        if (!await DisplayAlert("Notify Customers?", "Jobs for this day have been booked in. Would you like to message customers to inform them of the change of date?", "Yes", "No"))
            return;

        if (toText.Count > 0)
            await WorkPlanner.TextCustomers(toText, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);

        if (toEmail.Count > 0)
            await WorkPlanner.EmailCustomers(toEmail, _calenderDays[endDay].Date, WorkPlanner.DefaultRearangeMessage, this);
    }

    /// <summary>
    /// puts one day's work on another day and writes it down.
    ///
    /// The booking cache is built from the jobs, so it has to be told: a day
    /// moved here with the work list left holding a booking row for the day
    /// it came off.
    /// </summary>
    private async Task MoveTheWork(int startday, int endDay)
    {
        int moved = _calenderDays[startday].MoveDay(_calenderDays[endDay]);

        if (moved == 0)
        {
            await DisplayAlert("Nothing To Move",
                "There is no work left on that day to move. Work that has already been done stays on the day it was done.", "Ok");
            return;
        }

        _selectedDay = _calenderDays[endDay];
        RefreshAfterWorkChanged();

        Job.Save();
        DataRefreshNotifier.NotifyDataChanged();
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

        //marking it done makes the next visit, which can land on a day in
        //this same month; clearing it takes that visit away again
        RefreshAfterWorkChanged();
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
        RefreshAfterWorkChanged();
    }

    /// <summary>the classic pull down: the month built again from the jobs</summary>
    private void rv_calendar_Refreshing(object sender, EventArgs e)
    {
        try
        {
            RefreshCalenderData();
            RefreshPageDate();
        }
        finally
        {
            rv_calendar.IsRefreshing = false;
        }
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
        }

        RebuildDays();
    }

    /// <summary>
    /// The day cells built again from the jobs.
    ///
    /// The days are a cache the jobs are the truth for, and
    /// <see cref="PopulateDays"/> is the only thing allowed to fill them - so
    /// anything that changes the work has to come back through here.
    ///
    /// Rebuilding puts every day's colour back to what its work says
    /// (<see cref="CalenderDay.CalculateDay"/> ends in ResetColor), and that
    /// takes the ring off the day being looked at - so the ring goes back on
    /// afterwards. Rebuilding a day is no reason to lose which one is picked.
    /// </summary>
    private void RebuildDays()
    {
        //which day was picked is read before the rebuild, because PopulateDays
        //picks today when nothing is picked - and a day that has only just
        //been chosen for you is not one to draw a ring round. Moving to
        //another month deliberately leaves nothing picked
        CalenderDay picked = _selectedDay;

        foreach (CalenderDay cd in _calenderDays)
            cd.Jobs.Clear();

        PopulateDays();

        if (picked != null)
        {
            picked.SelectedDayColor = Colors.White;
            picked.SelectedDayBorderSize = 3;
        }
    }

    /// <summary>
    /// The work has changed - marked done, cleared, skipped, cancelled, paid,
    /// moved to another day.
    ///
    /// Which day a job belongs on can change with it, so the days are built
    /// again from the jobs before the panel under the calendar is drawn.
    /// Skipping is the plain case: it pushes the due date out, so the house is
    /// on another day now - but the panel is drawn from the day's own cached
    /// list, and redrawing that list without rebuilding it left the house
    /// sitting on a day it was no longer due on, and the day's totals counting
    /// it, until the page was pulled down by hand.
    ///
    /// Every swipe and menu on this page that touches the work goes through
    /// here, so none of them can be the one that forgets.
    /// </summary>
    private void RefreshAfterWorkChanged()
    {
        RebuildDays();
        RefreshPageDate();
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
    /// whether the work that only falls due on a day is left off that day's
    /// list, leaving what was actually booked in for it.
    ///
    /// Off to begin with: until somebody says otherwise a day's work is
    /// everything that lands on it. It is kept like the paper view's view
    /// options - how the page is being read is not something about the round,
    /// so it does not belong in the data files - and it is kept rather than
    /// asked again every time, because a round that is worked to what was
    /// booked in is worked that way every day.
    /// </summary>
    public static bool HideDueWork
    {
        get { return Preferences.Get("Calendar_HideDueWork", false); }
        set { Preferences.Set("Calendar_HideDueWork", value); }
    }

    /// <summary>how much of the picked day's work the last build left off it,
    /// which is what the bar above the list has to say out loud</summary>
    private int _dueHidden = 0;

    /// <summary>
    /// The day's work as it is shown: the work booked in for the day first,
    /// then the work that merely falls due on it, each under a title of its
    /// own and each split into street headings with the houses under them, in
    /// street order and up each street by house number - the same street
    /// format All Jobs reads the round in (Controles/StreetSplit).
    ///
    /// The two are separated because they are two different things: a booked
    /// day is what you have arranged to turn up for, and the rest is work the
    /// round says is ready. Mixed into one list, a day planned out read the
    /// same as a day nobody had touched.
    ///
    /// Work already done stays on its street, folded to the faded line, so a
    /// street is one run of houses however far through it the day is.
    ///
    /// Hands back the jobs actually drawn, because the day's figures under the
    /// calendar are about what is on screen - see <see cref="RefreshPageDate"/>.
    /// </summary>
    private List<Job> ShowDaysWork()
    {
        List<Job> booked = new List<Job>();
        List<Job> due = new List<Job>();

        foreach (Job j in _selectedDay.Jobs)
        {
            if (j == null)
                continue;

            if (j.IsBookedIn)
                booked.Add(j);
            else
                due.Add(j);
        }

        _dueHidden = 0;
        if (HideDueWork)
        {
            //a clean that was done on the day is what the day was, booked in
            //or not - it is not work waiting to be arranged, so hiding the due
            //work never hides it. What comes off is the outstanding work that
            //nobody has booked
            _dueHidden = due.RemoveAll(x => !x.IsCompleted);
        }

        //a title is only drawn when there is something to tell apart: a day
        //that is all booked in, or all due, is one list and reads as one
        bool titles = booked.Count > 0 && due.Count > 0;

        _jobsToDisplay.Clear();

        List<Job> shown = new List<Job>();
        AddDaySection(booked, titles ? "Booked In" : null, BookedSectionColour, shown);
        AddDaySection(due, titles ? "Due" : null, DueSectionColour, shown);

        return shown;
    }

    /// <summary>the booked half is in the page's booking orange, which reads
    /// on either theme</summary>
    private static readonly Color BookedSectionColour = Color.FromArgb("#EF6C00");

    private static readonly Color DueSectionSlate = Color.FromArgb("#546E7A");

    private static readonly Color DueSectionSlateDark = Color.FromArgb("#90A4AE");

    /// <summary>
    /// the due half is written in slate, and it takes the lighter one on the
    /// dark page - the same slate that reads as quiet on a white page is too
    /// dark to read on a near black one. Like the rest of the colours here the
    /// theme is asked at build time and not watched
    /// </summary>
    private static Color DueSectionColour
    {
        get
        {
            return Application.Current != null
                && Application.Current.PlatformAppTheme == AppTheme.Dark
                ? DueSectionSlateDark : DueSectionSlate;
        }
    }

    /// <summary>
    /// one part of the day on to the list: its title, then its streets with
    /// their houses under them
    /// </summary>
    private void AddDaySection(List<Job> jobs, string title, Color colour, List<Job> shown)
    {
        if (jobs.Count == 0)
            return;

        if (title != null)
            _jobsToDisplay.Add(new SectionHeading()
            {
                Title = title,
                Detail = SectionDetail(jobs),
                Colour = colour,
            });

        foreach (object row in StreetSplit.WithHeadings(jobs))
        {
            if (row is Job j)
            {
                j.CollapsedInList = j.IsCompleted;
                shown.Add(j);
            }

            _jobsToDisplay.Add(row);
        }
    }

    /// <summary>
    /// what a section comes to, said under its title - how many houses and
    /// what they are worth, worded by <see cref="DayProgress"/> so a part of a
    /// day and the whole day cannot be worded differently
    /// </summary>
    private static string SectionDetail(List<Job> jobs)
    {
        DayProgress part = DayProgress.For(jobs);

        string houses = jobs.Count == 1 ? "1 house" : $"{jobs.Count} houses";

        return part.ShowValue ? $"{houses} - {part.ValueText}" : houses;
    }

    /// <summary>
    /// The way the due work is taken off the day and put back, and - while it
    /// is off - what is not being shown.
    ///
    /// A filter that is on with nothing on screen saying so is the one thing
    /// this must not do, which is why the bar comes up as soon as anything is
    /// actually hidden. A day with nothing due has nothing to say either way.
    /// </summary>
    private void ShowDueWorkOption()
    {
        hsl_dueHidden.IsVisible = false;
        bnt_hideDue.IsVisible = false;

        if (_selectedDay == null)
            return;

        if (HideDueWork)
        {
            if (_dueHidden == 0)
                return;

            l_dueHidden.Text = _dueHidden == 1
                ? "1 job just due not shown"
                : $"{_dueHidden} jobs just due not shown";
            hsl_dueHidden.IsVisible = true;
            return;
        }

        int due = 0;
        foreach (Job j in _selectedDay.Jobs)
            if (j != null && !j.IsBookedIn && !j.IsCompleted && !j.HaveCanceled)
                due++;

        if (due == 0)
            return;

        bnt_hideDue.Text = due == 1 ? "Hide The One Just Due" : $"Hide The {due} Just Due";
        bnt_hideDue.IsVisible = true;
    }

    private void bnt_hideDue_Clicked(object sender, EventArgs e)
    {
        HideDueWork = true;
        RefreshPageDate();
    }

    private void bnt_showDue_Clicked(object sender, EventArgs e)
    {
        HideDueWork = false;
        RefreshPageDate();
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

        //the day itself is not what is on screen, so nothing of it is hidden
        _dueHidden = 0;

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
        hsl_dueHidden.IsVisible = false;
        bnt_hideDue.IsVisible = false;

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
            l_dayNote.IsVisible = false;
            bnt_dayNote.IsVisible = false;
            return;
        }

        l_currentDayName.Text = $"{_selectedDay.Date.DayOfWeek} {_selectedDay.Day}/{_selectedDay.Date.Month}/{_selectedDay.Date.Year}";

        List<Job> shown = ShowDaysWork();

        //a day emptied by the due work being hidden is not a day with nothing
        //on it, and saying "No Jobs To Do" over a day with five houses due on
        //it would be a plain lie. The bar above says how many are hidden
        l_noJobs.Text = _dueHidden > 0 ? "Nothing Booked In For This Day" : "No Jobs To Do";
        l_noJobs.IsVisible = shown.Count == 0;

        //how the day stands, worked out in the kernel so this page and the
        //booked work page cannot say the same day two different ways.
        //
        //It is asked of the work on screen rather than of the whole day: with
        //the due work hidden the chips are about the day as it is being read,
        //and the day as a whole is still what colours the cell on the grid
        DayProgress day = DayProgress.For(shown);

        float paymentsTotal = 0;
        foreach (Payment p in Payment.Query())
            if (UsfulFuctions.Difference(p.Date, _selectedDay.Date) == 0)
                paymentsTotal += p.Amount;

        float expensesTotal = Expense.TotalForDate(_selectedDay.Date);

        //the day's work was one figure - what the lot came to. It says how
        //much of that has actually been done now, because that is the
        //question being asked of it: eight houses of twelve is not two thirds
        //of the money when the four left are the expensive ones. Said in the
        //one chip rather than in a second beside it, which would only carry
        //the same total round again
        l_dayJobTotal.Text = day.ShowValue
            ? $"Jobs {day.ValueText}"
            : $"Jobs {Gloable.CurrenceSymbol}0";
        l_dayPaymentTotal.Text = $"Paid {Gloable.CurrenceSymbol}{paymentsTotal}";
        l_dayExpenseTotal.Text = $"Spent {Gloable.CurrenceSymbol}{expensesTotal:0.00}";
        l_dayExpenseTotal.IsVisible = expensesTotal != 0;

        ShowDayProgress(day);
        ShowDayNote();
        ShowDueWorkOption();
        ShowBookInOption();
    }

    /// <summary>
    /// What is written against the day being looked at, and the way in to
    /// writing it. The button is there whether or not there is a note -
    /// a note nobody can find out how to add is no feature at all - and it
    /// says which of the two it is about to do.
    /// </summary>
    private void ShowDayNote()
    {
        string note = _selectedDay == null ? string.Empty : DayNote.TextFor(_selectedDay.Date);

        l_dayNote.Text = note;
        l_dayNote.IsVisible = note.Length > 0;

        bnt_dayNote.IsVisible = _selectedDay != null;
        bnt_dayNote.Text = _selectedDay == null
            ? "Add A Note" : DayNoteEditor.ButtonText(_selectedDay.Date);
    }

    private void l_dayNote_Tapped(object sender, EventArgs e)
    {
        WriteTheDayNote();
    }

    private void bnt_dayNote_Clicked(object sender, EventArgs e)
    {
        WriteTheDayNote();
    }

    /// <summary>
    /// asks for the note and puts the day right afterwards - the day panel
    /// says it and the grid carries the mark, so both have to be told
    /// </summary>
    private async void WriteTheDayNote()
    {
        if (_selectedDay == null)
            return;

        DateTime day = _selectedDay.Date;

        try
        {
            if (!await DayNoteEditor.ChangeAsync(day, this))
                return;

            //the mark on the grid is worked out with the day's figures, so
            //the day is put right rather than only the panel under it
            foreach (CalenderDay cd in _calenderDays)
                if (cd.Date.Date == day.Date)
                    cd.ShowNote = DayNote.Has(cd.Date);

            ShowDayNote();
        }
        catch (Exception ex)
        {
            //an alert on a page that has gone never comes back, and this is
            //an async void - it would take the app down rather than the page
            WorkTracker.CrashLogger.Log("CalenderView.WriteTheDayNote", ex);
        }
    }

    /// <summary>
    /// How the day is going: how much of it is done and roughly how long what
    /// is left will take. <see cref="DayProgress"/> counts it and words it -
    /// the booked work page shows a day the same way, and the two must not be
    /// able to disagree about how far through it you are.
    /// </summary>
    private void ShowDayProgress(DayProgress day)
    {
        l_dayProgress.IsVisible = day.HaveWork;
        l_dayProgress.Text = day.CountText;

        l_dayTimeLeft.IsVisible = day.ShowTimeLeft;
        l_dayTimeLeft.Text = day.TimeLeftText;
    }

    private void On_Job_More(object sender, EventArgs e)
    {
        WorkPlanner.ShowJobStatus(GetJobForSwipe(sender), this, RefreshAfterWorkChanged);
    }

    private async void On_Job_DoAgain(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j == null)
            return;

        if (await WorkPlanner.DoJobAgain(j, this))
            RefreshAfterWorkChanged();
    }

    private void On_Job_Skipped(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        WorkPlanner.MarkJobSkipped(j);

        //a skip pushes the job out to its next visit, so it is not this day's
        //work any more - the day it is on now is worked out from the jobs
        RefreshAfterWorkChanged();
    }

    private async void On_Job_Canceled(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        await WorkPlanner.MarkJobCancled(j, this);

        //a visit cancelled before it was done is not work to turn up for, so
        //it comes off the calendar altogether
        RefreshAfterWorkChanged();
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

    

    private void Card_Info(object sender, JobCardEventArgs e)
    {
        WorkPlanner.ShowJobInfo(e.Job, this);
    }

}
