namespace UiInterface.Layouts;

using Kernel;
using System.Collections.ObjectModel;
using System.ComponentModel;

using Kernel.Fiilters;

public partial class PaperView : ContentPage
{

	
	public struct PaperViewLocationInfo
	{
		public string Street;
		public string City;
		public string Area;

		/// <summary>the round this street's work is on, blank for none</summary>
		public string Round;

	}
	public class JobInstance
	{
		public DateTime JobDate { get; set; }
		public bool Compleated { get; set; }
		public bool Paid { get; set; }
		public float Price { get; set; }
		public string Label { get; set; }
		public string Notes { get; set; }
		
		public bool IsPlaceHolder { get; set; } = false;

		
        public static JobInstance PlaceHolder()
        {
			return new JobInstance()
			{
				IsPlaceHolder = true
            };
        }
    }
	public class PaperItem: INotifyPropertyChanged
    {

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public static string StringPaid = "/", StringDone = "\\", StringDonePaid = "X", StringSkipped = "O", StringCanceld = "-";
		public string Title { get; set; } = " ";

		//the real address. rows are matched on these, streets are grouped by
		//them and a house added from a street heading is built out of them,
		//so they are never the made up ones - see Kernel/ScreenshotMode.cs
		public string PropertyStreet { get; set; }
		public string PropertyCity { get; set; }
		public string PropertyArea { get; set; }
		public string PropertyNumber { get; set; }

		/// <summary>the round this work is on, blank for none</summary>
		public string Round { get; set; } = string.Empty;

		/// <summary>
		/// the line naming a round, above its streets. it is not a street and
		/// not a house, so nothing that walks the round's work touches it
		/// </summary>
		public bool IsRoundHeading { get; set; } = false;

		/// <summary>the street as it should be shown, for the rows that show it</summary>
		public string DisplayStreet
		{
			get { return ScreenshotMode.Street(PropertyStreet); }
		}

		public int GroupId = 0;
		public DateTime StartDate { get; set; } = new DateTime(2000, 1, 1);
		public List<JobInstance> Instances { get; set; } = new List<JobInstance>();
		public int BaseJobId { get; set; } = -1;

		private bool _isCanceled;
		public bool IsCanceled
		{
			get
			{
				return _isCanceled;
			}
			set
			{
				_isCanceled = value;
                RaisePropertyChanged("IsCanceled");
                RaisePropertyChanged("ShowJobDetails");
                RaisePropertyChanged("ShowCancelledRow");
            }
		}

		/// <summary>
		/// a normal job row: the full set of columns. a cancelled job shows
		/// the collapsed line instead
		/// </summary>
		public bool ShowJobDetails
		{
			get { return ShowJobInformation && !IsCanceled; }
		}

		/// <summary>
		/// a cancelled job, shown as one faded struck through line
		/// </summary>
		public bool ShowCancelledRow
		{
			get { return ShowJobInformation && IsCanceled; }
		}

		/// <summary>
		/// work already promised to a day carries an orange tab down the very
		/// left of its row, so the bookings stand out while scanning the round.
		/// once the job is done the booking is spent, so the tab goes with it
		/// </summary>
		public bool ShowBookedTab
		{
			get { return ShowJobDetails && JobI3 != null && JobI3.IsBookedIn && !JobI3.IsCompleted; }
		}
		public Job BaseJob { get; set; }

		public Job _jobI3 { get; set; }
		public Job JobI3
		{
			get { return _jobI3; }
			set
			{
				_jobI3 = value;
				RaisePropertyChanged("JobI3");
				RaiseTagsChanged();
			}
		}

		/// <summary>
		/// the tags on the visit this row is writing up - the one whose mark
		/// is in the record column, which is the visit that is due or, once
		/// that is done and the next one is not due yet, the one just done.
		/// they are what was different about that time of doing it
		/// </summary>
		public string TagsText
		{
			get { return JobI3 == null ? string.Empty : JobI3.TagsText; }
		}

		public bool HaveTags
		{
			get { return ShowJobDetails && JobI3 != null && JobI3.HaveTags; }
		}

		public void RaiseTagsChanged()
		{
			RaisePropertyChanged("TagsText");
			RaisePropertyChanged("HaveTags");
		}

        public Job JobI4 { get; set; }

		public bool IsQuote { get; set; } = false;

		/// <summary>
		/// the row put in where a street's houses were last done at
		/// different times. it repeats the street so you can still see where
		/// you are, and can be tapped to book that part of the street
		/// </summary>
		public bool IsDateBreak { get; set; } = false;

		/// <summary>
		/// one of the rows in the booked work at the top of the list. they are
		/// a copy of work that is already further down the list, so they are
		/// left out of anything that works on the round itself
		/// </summary>
		public bool IsBookedRow { get; set; } = false;

		private string _jobNote = string.Empty;
        public string JobNote
		{
			get
			{
				return _jobNote;
			}
			set
			{
				_jobNote = value;
				RaisePropertyChanged("JobNote");
			}
		}
        public float TranslastionX { get; set; } = 0;


		public FontAttributes FontAttri { get; set; } = FontAttributes.None;

		public int RowSpan { get; set; } = 1;

		public int _i3RowSpan = 1;
		public int I3RowSpan
		{
			get
			{
				return _i3RowSpan;
			}
			set
			{
				_i3RowSpan = value;
				RaisePropertyChanged("I3RowSpan");			}
		}

        private bool _isSet = false;

		public float BasePice { get; set; } = 0;

		public string Notes { get; set; } = "";

		public bool TitleOnly;
		public string Price
		{
			get

			{
				if (BasePice - (int)BasePice == 0)
					return $"{Gloable.CurrenceSymbol}{BasePice}";
				else
					return $"{Gloable.CurrenceSymbol}{BasePice.ToString("n2")}";
			}
		}
		public bool ShowJobInformation { get; set; } = true;

        public bool NotShowJobInformation
        {
            get
            {
                return !ShowJobInformation;

            }
        }

		public Color _owingColour = Colors.White;

		public Color OwingColour
		{
			get { return _owingColour; }
			set { _owingColour = value;
				RaisePropertyChanged("OwingColour");
			}
		}

		public string _owing = string.Empty;
        public string Owing
		{
			get {
				return _owing;
			}
			set
			{
				_owing = value;
                RaisePropertyChanged("Owing");
            }
		}
        public string I2 { get; set; } = string.Empty;

		private string _i3 = string.Empty;
        public string I3
		{
			get { return _i3; }
			set
			{
				_i3 = value;
				RaisePropertyChanged("I3");
			}
		}

		public string _i4 = string.Empty;
        public string I4
		{
			get { return _i4; }
			set
			{
				_i4 = value;
				RaisePropertyChanged("I4");
			}
		}


		public static Color DueColour = Color.FromArgb("1E2E41");
        public static Color DueColourLight = Color.FromArgb("CCE5FF");
        public Color BgColour { get; set; } = Colors.Transparent;

		/// <summary>
		/// street header rows get extra top padding to act as a spacer between streets
		/// </summary>
		public Thickness TitlePadding { get; set; } = new Thickness(4);

		private bool _tnb;
		/// <summary>
		/// text night before flag shown as a red badge on the row
		/// </summary>
		public bool TNB
		{
			get { return _tnb; }
			set
			{
				_tnb = value;
				RaisePropertyChanged("TNB");
			}
		}

		private Color _rowColour = Colors.Transparent;
		/// <summary>
		/// alternating row background, used instead of ruled separator lines
		/// </summary>
		public Color RowColour
		{
			get { return _rowColour; }
			set
			{
				_rowColour = value;
				RaisePropertyChanged("RowColour");
			}
		}

		private bool _jobIsDue = false;


		public bool JobIsDue
		{
			get
			{
				return _jobIsDue;
			}
			set
			{
				_jobIsDue = value;

				if (IsCanceled)
				{
                    BgColour = Colors.Transparent;
                    RaisePropertyChanged("BgColour");
                   
				}
				else
				if (value)
				{
					if (Application.Current.PlatformAppTheme == AppTheme.Dark)
						BgColour = DueColour;
					else
						BgColour = DueColourLight;

				}
				else
					BgColour = Colors.Transparent;
				RaisePropertyChanged("BgColour");
				RaisePropertyChanged("JobIsDue");
			}
		}

        private int InstanceIndex = 0;

		public event PropertyChangedEventHandler PropertyChanged;

		public void UpdatePaperRecordI3(Job j)
		{

			if (j == null)
				return;

            string tmp = string.Empty;

            if (j.IsCompleted)
			{
				if (j.IsPaidFor)
					tmp = StringDonePaid;
				else
					tmp = StringDone;
			}
			else
				if (j.IsPaidFor)
				tmp = StringPaid;
			else
				tmp = ""; //blank ie is due but nothing to put here

			if (tmp == string.Empty && j.HaveSkipped)
				tmp = StringSkipped;

			if (j.UseAlterativePrice >= 0) //if there is an alternative price
			{
				try
				{
					JobNote = UsfulFuctions.GetFirstLettersFromWord(j.AlternativePrices[j.UseAlterativePrice].Description);
				}
				catch
				{
					JobNote = "?";
				}
			}
			else
				JobNote = string.Empty;

            tmp = $"{JobNote} {tmp}";

            JobIsDue = false;

            if (UsfulFuctions.DifferenceSigned(j.DueDate, UsfulFuctions.DateNow) <= 0) //if due
			{
				//I4 = tmp;

				if (tmp == String.Empty)
				{
					tmp = "_";
                   
                }

				if (!j.IsCompleted)
                    JobIsDue = true;
            }
                

            I3 = tmp;

			IsCanceled = j.HaveCanceled;
			TNB = j.TNB;
			//booking a job in, or clearing it, has to show on the row straight away
			RaisePropertyChanged("ShowBookedTab");

			//marking work done is what puts the tag bar's tags on it, so the
			//row has to be asked for them again every time it is written up
			RaiseTagsChanged();

			//a job whose customer has gone still has to draw rather than bring
			//the page down with it
			Customer customer = j.GetCustomer();
            float bal = customer == null ? 0 : customer.Balance;

            if (bal == 0)
                Owing = "Nothing Owed";
            else
                if (bal > 0)
                Owing = $"{Gloable.CurrenceSymbol}{Math.Abs(bal)} Owed";
            else
                Owing = $"{Gloable.CurrenceSymbol}{Math.Abs(bal)} In Credit";

			UpdateColors();
        }

		public void UpdateColors ()
		{
			if (Owing.Contains("Nothing Owed"))
			{
				if (Application.Current.PlatformAppTheme == AppTheme.Dark)
				{
					OwingColour = Colors.White;
				}
				else
				{

						OwingColour = Colors.Black;
					if (IsCanceled)
						OwingColour = new Color(OwingColour.Red, OwingColour.Green, OwingColour.Blue, 0.5f);
                }
            }
			else
				if (Owing.Contains("In Credit"))
				OwingColour = Colors.Green;
			else
				OwingColour = Colors.Red;
		}
        public void AddInstance(Job j)
        {
			

			if (BaseJobId == -1)
            {
				BaseJobId = j.BaseJobId;
				BaseJob = Job.Query(QueryType.JobId, j.BaseJobId).FirstOrDefault();
				float bal = j.GetCustomer().Balance;

				if (bal == 0)
					Owing = "Nothing Owed";
				else
					if (bal > 0)
						Owing = $"{Gloable.CurrenceSymbol}{Math.Abs(bal)} Owed";
					else
						Owing = $"{Gloable.CurrenceSymbol}{Math.Abs(bal)} In Credit";

				I3 = "0";

			

            }

			JobInstance ji = new JobInstance()
			{
				JobDate = j.DueDate,
				Compleated = j.IsCompleted,
				Notes = j.JobInstanceNotes,
				Paid = j.IsPaidFor,
				Price = j.Price,
			};

			string tmp = string.Empty;
			if (j.IsCompleted)
			{
				if (j.IsPaidFor)
					tmp = StringDonePaid;
				else
					tmp = StringDone;
			}
			else
				if (j.IsPaidFor)
				tmp = StringPaid;
			else
				tmp = ""; //blank ie is due but nothing to put here

			if (tmp == string.Empty && j.HaveSkipped)
				tmp = StringSkipped;

			JobNote = string.Empty;
			
			
			
			//rows
			//i4 is the last row to the right
			//i3 is the second to last row

			if (!j.IsCompleted) //so not compleated
			{
				if (UsfulFuctions.DifferenceSigned(j.DueDate, UsfulFuctions.DateNow) <= 0) //if due
				{
					//I4 = tmp;
					if (tmp == String.Empty)
						tmp = "_";
					I3 = tmp;
					JobI3 = j;
					JobI3.Data = this;
					JobIsDue = true;
				}
				else
				{
                    JobIsDue = false;
                    I4 = tmp;
                    JobI4 = j;
                    Job pj = Job.Query(QueryType.JobId, j.PreviousJobId).FirstOrDefault();
					
                    if (pj != null)
                    {
                        tmp = String.Empty;
                        if (pj.IsCompleted)
                        {
                            if (pj.IsPaidFor)
                                tmp = StringDonePaid;
                            else
                                tmp = StringDone;
                        }
                        else
                            if (pj.IsPaidFor)
                            tmp = StringPaid;

                        if (pj.UseAlterativePrice >= 0) //if there is an alternative price
                        {
                            try
                            {
                                JobNote = UsfulFuctions.GetFirstLettersFromWord(pj.AlternativePrices[pj.UseAlterativePrice].Description);
                            }
                            catch
                            {
                                JobNote = "?";
                            }
                        }

                        tmp = $"{JobNote} {tmp}";
                        I3 = tmp;
                        JobI3 = pj;
                        JobI3.Data = this;
                    }
                   
                }
			}

			InstanceIndex++;
			if (j.HasAlternativePrice)
			{
				ji.Price = j.ChosenAlternativePrice.Price;
				ji.Label = j.ChosenAlternativePrice.Description;
			}

			if (j.IsCompleted)
				ji.JobDate = j.DateCompleated;

			if (StartDate.Date < ji.JobDate)
				StartDate = ji.JobDate;

			Instances.Add(ji);

			if (!_isSet)
            {
				_isSet = true;
				if (j.Address != null)
				{
					Title = j.Address.DisplayStreet;
					PropertyNumber = j.Address.PropertyNameNumber;
					PropertyStreet = j.Address.Street;
					PropertyCity = j.Address.City;
					PropertyArea = j.Address.Area;
					
				}
				BasePice = j.Price;
				Notes = j.Notes;
				TNB = j.TNB;
				Round = j.Round ?? string.Empty;
            }
            if (j.JobNextId == -1) //ie no next job
            {
                //a job made this session can still carry a half filled
                //address, so nothing here is allowed to stay null
                PropertyStreet = j.Address.Street ?? string.Empty;
                PropertyCity = j.Address.City ?? string.Empty;
                PropertyArea = j.Address.Area ?? string.Empty;
            }

			if (j.HaveCanceled)
				IsCanceled = true;
            UpdateColors();
        }

		public void SyncRecordsToDate(DateTime date)
        {
			//search through all instances
			//start at due date and work backwards
			//due date should be the last in the record

			//first of flip the array
			Instances.Reverse();

			if (Instances[0].JobDate < DateTime.Now)
            {
			//	Instances.Insert(0, JobInstance.PlaceHolder());
            }
        }
    }

	/// <summary>
	/// The line over a round's work.
	///
	/// It is a heading like a street's, only bolder and with a rule under it,
	/// so a page split into rounds reads as rounds rather than as one long
	/// list. Work that is not on a round gets one too - that is the pile to
	/// go through and put on one.
	/// </summary>
	private static PaperItem RoundHeading(string round)
	{
		return new PaperItem()
		{
			Title = string.IsNullOrWhiteSpace(round) ? "- No Round -" : $"{round.ToUpper()}",
			FontAttri = FontAttributes.Bold,
			RowSpan = 5,
			ShowJobInformation = false,
			IsRoundHeading = true,
			Round = round ?? string.Empty,
			TitlePadding = new Thickness(4, 22, 4, 2),
		};
	}

	/// <summary>
	/// the words over a street: the street, the town and the area as they are
	/// shown. the same shape as the key the rows were grouped by, so with
	/// screenshot mode off it comes out exactly as it always did
	/// </summary>
	private static string HeadingFor(PaperViewLocationInfo here)
	{
		return $"{ScreenshotMode.Street(here.Street)} {ScreenshotMode.Town(here.City)} {ScreenshotMode.Area(here.Area)}".ToLower();
	}

	public ObservableCollection<PaperItem> PaperItems = new ObservableCollection<PaperItem>();

	public JobFilterBase PaperViewFilter;

	public PaperView()
	{
		InitializeComponent();
		//return;
		NavigatedTo += PaperView_NavigatedTo;
		DataRefreshNotifier.DataChanged += () => _fullRefresh = true;

		g_filters.BindingContext = PaperViewFilter;

		//set before the list is built, and without setting it off again
		cb_showCanceledJobs.CheckedChanged -= cb_showCanceledJobs_Changed;
		cb_showCanceledJobs.IsChecked = ShowCancelledJobs;
		cb_showCanceledJobs.CheckedChanged += cb_showCanceledJobs_Changed;

		cb_showAllJobs.CheckedChanged -= cb_showAllJobs_Changed;
		cb_showAllJobs.IsChecked = ShowAllJobs;
		cb_showAllJobs.CheckedChanged += cb_showAllJobs_Changed;







      //  SetFilter(cf);
        try
        {
            FullPageLoad();
        }
        catch (Exception ex)
        {
            // Never let a data problem stop the app from opening -
            // record it so the crash log page can surface it.
            WorkTracker.CrashLogger.Log("PaperView.FullPageLoad", ex);
        }


	}

	public void SetFilter(JobFilterBase jfb)
	{
        PaperViewFilter = jfb;
		p_filter_selection.Items.Clear();
	//	l_filterName.BindingContext = PaperViewFilter;
		//p_filter_selection.SelectedItem = "All";
		foreach (string s in PaperViewFilter.FilterOptions)
			p_filter_selection.Items.Add(s);

        p_filter_selection.SelectedIndex = 0;
        g_filters.BindingContext = PaperViewFilter;

		g_filters.IsVisible = true;
    }

    private void p_FilterSelected(object sender, EventArgs e)
    {
		PaperViewFilter.SelectedIndex = p_filter_selection.SelectedIndex;
        FullPageLoad();
    }
	/// <summary>
	/// compares two bits of an address ignoring case, treating a missing one
	/// as blank rather than throwing
	/// </summary>
	private static bool SameText(string a, string b)
	{
		return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
			FullPageLoad();
		}
	}

	private void e_search_Changed(object sender, TextChangedEventArgs e)
	{
		_searchText = e.NewTextValue ?? string.Empty;
		FullPageLoad();
	}

	private void bnt_clearSearch_Clicked(object sender, EventArgs e)
	{
		e_search.Text = string.Empty;
	}

    private void FullPageLoad()
	{
        List<Job> jobs = Job.Query();

		//what is typed in the search box narrows the round down. done first,
		//so the street headings only come out for streets with something left
		//in them
		if (!string.IsNullOrWhiteSpace(_searchText))
			jobs.RemoveAll(x => !x.MatchesSearch(_searchText));
		jobs.RemoveAll(x => x.JobNextId == -1 && x.IsCompleted && UsfulFuctions.DifferenceSigned(DateTime.Now, x.DateCompleated) > 30);

		//cancelled work is out of the way unless it is asked for
		if (!ShowCancelledJobs)
			jobs.RemoveAll(x => x.HaveCanceled);

		if (PaperViewFilter != null)
			PaperViewFilter.Filter(ref jobs);

		//picked out before the list is thinned down, so a day booked further
		//ahead than the list reaches still shows at the top
		List<Job> bookedWork = jobs.FindAll(x => x.IsBookedIn && !x.IsCompleted && !x.HaveCanceled);

		//and taken out of the round: booked work belongs in the block at the top
		//and nowhere else. it used to be copied up rather than moved, so every
		//booked house was drawn a second time against its own street
		jobs.RemoveAll(x => x.IsBookedIn && !x.IsCompleted && !x.HaveCanceled);

		//the list is the work in hand: a job stays on it while it is due,
		//while it is coming up in the next few days, and for the rest of the
		//day it was written up so it can still be corrected. everything else
		//is out of the way until all jobs are asked for
		if (!ShowAllJobs)
		{
			HashSet<int> keep = new HashSet<int>();
			foreach (Job j in jobs)
				if (IsWorkInHand(j))
					keep.Add(j.BaseJobId);

			jobs.RemoveAll(x => !keep.Contains(x.BaseJobId));
		}

        jobs = jobs.OrderByDescending(x => x.OrderByDate).ToList();
        
        PaperItem pi;

        List<PaperItem> tmpPaperwork = new List<PaperItem>();


        foreach (Job j in jobs)
        {

            pi = tmpPaperwork.FirstOrDefault(x => x.BaseJobId == j.BaseJobId);
            if (pi != null)
            {
                pi.AddInstance(j);
            }
            else
            {
                pi = new PaperItem();
                pi.AddInstance(j);
                tmpPaperwork.Add(pi);
            }

        }


        foreach (Job j in jobs)
        {

            pi = tmpPaperwork.FirstOrDefault(x => x.BaseJobId == j.BaseJobId);
            if (pi != null)
            {
                pi.AddInstance(j);
            }
            else
            {
                pi = new PaperItem();
                pi.AddInstance(j);
                tmpPaperwork.Add(pi);
            }

        }

        //now lets group them by location
        List<string> location = new List<string>();
        List<PaperViewLocationInfo> locationData = new List<PaperViewLocationInfo>();


        string tmpString = string.Empty;
        foreach (Job j in jobs)
        {
            //the round is part of what makes a group: a street worked on two
            //rounds is two lots of work, and they are not done together
            tmpString = $"{j.Round}|{j.Address.Street} {j.Address.City} {j.Address.Area}";
            tmpString = tmpString.ToLower();
			if (!location.Contains(tmpString))
            {
                location.Add(tmpString);
				locationData.Add(new PaperViewLocationInfo()
				{
					Street = j.Address.Street ?? string.Empty,
					City = j.Address.City ?? string.Empty,
					Area = j.Address.Area ?? string.Empty,
					Round = j.Round ?? string.Empty,
				});
            }
        }

        //round first, then street. work on no round goes last rather than
        //first, so what nobody has put on a round is not the top of the page
        locationData = locationData
            .OrderBy(x => string.IsNullOrWhiteSpace(x.Round) ? 1 : 0)
            .ThenBy(x => x.Round, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Street, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        //headings only when there are rounds to name. a round nobody has set
        //up would otherwise put "No Round" over every page and say nothing
        bool showRounds = locationData.Exists(x => !string.IsNullOrWhiteSpace(x.Round));

        PaperItems.Clear();
        string roundShowing = null;
        bool firstGroup = true;

        foreach (PaperViewLocationInfo here in locationData)
        {
            //a heading for each round as it comes up, so the page reads as one
            //round after another rather than as a list that happens to be in
            //an order
            if (showRounds && (firstGroup || !SameText(roundShowing, here.Round)))
                PaperItems.Add(RoundHeading(here.Round));

            roundShowing = here.Round;
            firstGroup = false;

            //compared without case on both sides and safe against a half
            //filled address: area used to be compared with its case left on,
            //so anything with a capital in it never matched
            //the heading over a street is read, so it is the shown address.
            //everything under it keeps the real one
            List<PaperItem> jobsToAdd = tmpPaperwork.FindAll(x =>
                SameText(x.PropertyStreet, here.Street)
                && SameText(x.PropertyArea, here.Area)
                && SameText(x.PropertyCity, here.City)
                && SameText(x.Round, here.Round));
            char[] tmp = HeadingFor(here).ToCharArray();
            tmp[0] = char.ToUpper(tmp[0]);

            for (int i = 1; i < tmp.Length; i++)
                if (tmp[i - 1] == ' ')
                    tmp[i] = char.ToUpper(tmp[i]);

            PaperItems.Add(new PaperItem()
            {
                Title = new string(tmp),
                FontAttri = FontAttributes.Bold,
                RowSpan = 5,
                ShowJobInformation = false,
				PropertyStreet = here.Street,
                PropertyArea = here.Area,
                PropertyCity = here.City,
                Round = here.Round,
                TitlePadding = new Thickness(4, 18, 4, 4), //spacer between streets
            });
            if (jobsToAdd != null && jobsToAdd.Count > 0)
            {
                PaperItem paperItem = null;
                foreach (PaperItem p in jobsToAdd)
                {
                    paperItem = p;
                    p.Title = p.PropertyNumber;
                    p.TranslastionX = 10;
                    p.SyncRecordsToDate(DateTime.Now);
                    PaperItems.Add(paperItem);
                }
            }
        }

        //now check the dates
        PaperItem currentItem;
        PaperItem nextItem;
		int groupId = 0;
        for (int i = 0; i < PaperItems.Count - 1; i++)
        {
            currentItem = PaperItems[i];
            nextItem = PaperItems[i + 1];

            if (!currentItem.ShowJobInformation) //if this is just showing the street etc
            {
                groupId++;

                if (nextItem.JobI3 != null)
                {
                    currentItem.I3 = nextItem.JobI3.OrderByDate.ToString("dd/MM/yy");
                    currentItem.I3RowSpan = 2;
                }
                else
                    currentItem.I3RowSpan = 1;
				currentItem.GroupId = groupId;
                
            }
            else
            {
                currentItem.GroupId = groupId;
                if (currentItem.JobI3 != null && nextItem.JobI3 != null)
					if (currentItem.JobI3.OrderByDate != nextItem.JobI3.OrderByDate)
					{
						//the street is repeated on the break so you can still
						//see which one you are looking at further down, and it
						//can be tapped to book that part of it in.
						//
						//the words are the shown street; PropertyStreet below
						//stays real, because that is what a house added from
						//here is built out of
						string streetName = string.IsNullOrWhiteSpace(currentItem.PropertyStreet)
							? "- - - - -"
							: currentItem.DisplayStreet;

						PaperItem pai = new PaperItem()
						{
							ShowJobInformation = false,
							IsDateBreak = true,
							GroupId = groupId,
							Title = streetName,
							PropertyStreet = currentItem.PropertyStreet,
							PropertyCity = currentItem.PropertyCity,
							PropertyArea = currentItem.PropertyArea,
							FontAttri = FontAttributes.Italic,
							TitlePadding = new Thickness(4, 12, 4, 2),
						};
						PaperItems.Insert(i + 1, pai);

					}
					
						
            }

			
        }
		if (PaperItems.Count > 0)
			PaperItems[PaperItems.Count - 1].GroupId = groupId;

		//the round that is booked in goes above the street list
		AddBookedWorkToTop(bookedWork);

		//alternating row backgrounds instead of ruled lines; restart at each street
		bool darkTheme = Application.Current.PlatformAppTheme == AppTheme.Dark;
		bool alt = false;
		foreach (PaperItem p in PaperItems)
		{
			if (!p.ShowJobInformation) //street headers and spacer rows stay plain
			{
				p.RowColour = Colors.Transparent;
				alt = false;
				continue;
			}

			if (darkTheme)
				p.RowColour = alt ? WorkPlanner.MainColorDark : WorkPlanner.altColorDark;
			else
				p.RowColour = alt ? WorkPlanner.MainColor : WorkPlanner.altColor;
			alt = !alt;
		}

        c_jobList.ItemsSource = PaperItems;
    }

	/// <summary>
	/// how many days ahead a job can be due and still be worth having on the
	/// list
	/// </summary>
	public const int DueSoonDays = 3;

	/// <summary>
	/// whether this record keeps its house on the job list. work that is due,
	/// due in the next few days or booked in is in hand; work written up
	/// today stays until the end of the day so it can still be corrected
	/// </summary>
	private static bool IsWorkInHand(Job j)
	{
		if (j == null)
			return false;

		if (!j.IsCompleted)
		{
			if (j.IsBookedIn)
				return true;

			//skipped work has been dealt with for this round, so it only
			//holds its place for the rest of the day it was skipped on
			//(whole days, so it still counts right over the new year)
			if (!j.HaveSkipped)
				return (j.DueDate.Date - DateTime.Now.Date).TotalDays <= DueSoonDays;
		}

		DateTime markedOn = j.IsCompleted ? j.DateCompleated : j.DateSkipped;
		return markedOn.Date == DateTime.Now.Date;
	}

	/// <summary>
	/// puts the work booked in at the top of the list, a day at a time and
	/// grouped by street the same way as the round below it. the job is moved
	/// here rather than copied, so a booked house is listed once and once only
	/// </summary>
	private void AddBookedWorkToTop(List<Job> bookedWork)
	{
		if (bookedWork == null || bookedWork.Count == 0)
			return;

		//one row per house, even if an older record of it was left booked in
		List<Job> booked = new List<Job>();
		foreach (Job j in bookedWork.OrderByDescending(x => x.DueDate))
			if (!booked.Any(x => x.BaseJobId == j.BaseJobId))
				booked.Add(j);

		int insertAt = 0;
		foreach (IGrouping<DateTime, Job> day in booked
			.GroupBy(x => x.DateJobBookinFor.Date)
			.OrderBy(x => x.Key))
		{
			List<Job> daysWork = day.ToList();

			float total = 0;
			foreach (Job j in daysWork)
				total += j.EffectivePrice;

			PaperItems.Insert(insertAt++, new PaperItem()
			{
				ShowJobInformation = false,
				IsBookedRow = true,
				Title = $"Booked In {day.Key.ToString("ddd dd MMM")} - {daysWork.Count} job(s) {Gloable.CurrenceSymbol}{total.ToString("n2")}",
				FontAttri = FontAttributes.Bold,
				TitlePadding = new Thickness(4, 12, 4, 4),
				I3 = day.Key.ToString("dd/MM/yy"),
			});

			foreach (IGrouping<string, Job> street in daysWork
				.GroupBy(x => x.Address == null ? string.Empty : x.Address.Street ?? string.Empty)
				.OrderBy(x => x.Key))
			{
				//the address is carried on the heading so a house can be
				//added to this street from here, the same as further down
				Job first = street.FirstOrDefault();

				PaperItems.Insert(insertAt++, new PaperItem()
				{
					ShowJobInformation = false,
					IsBookedRow = true,
					//the shown street. the real one is carried below it, which
					//is what a house added from this heading is built out of
					Title = string.IsNullOrWhiteSpace(street.Key) ? "- - - - -" : ScreenshotMode.Street(street.Key),
					PropertyStreet = first == null || first.Address == null ? string.Empty : first.Address.Street,
					PropertyCity = first == null || first.Address == null ? string.Empty : first.Address.City,
					PropertyArea = first == null || first.Address == null ? string.Empty : first.Address.Area,
					FontAttri = FontAttributes.Italic,
					TitlePadding = new Thickness(14, 4, 4, 2),
				});

				//by house number as a number, so 2 comes before 10
				foreach (Job j in street
					.OrderBy(x => x.SortHouseNumber)
					.ThenBy(x => x.SortHouseSuffix, StringComparer.CurrentCultureIgnoreCase))
					PaperItems.Insert(insertAt++, BookedRow(j));
			}
		}

		//a plain gap so the round below does not read as part of the booking
		PaperItems.Insert(insertAt, new PaperItem()
		{
			ShowJobInformation = false,
			IsBookedRow = true,
			Title = " ",
		});
	}

	/// <summary>one house in the booked work at the top of the list</summary>
	private PaperItem BookedRow(Job j)
	{
		PaperItem row = new PaperItem()
		{
			IsBookedRow = true,
			Title = j.Address == null ? string.Empty : j.Address.PropertyNameNumber,
			PropertyNumber = j.Address == null ? string.Empty : j.Address.PropertyNameNumber,
			BasePice = j.EffectivePrice,
			Notes = j.Notes,
			BaseJob = j,
			JobI3 = j,
			TranslastionX = 10,
		};

		row.UpdatePaperRecordI3(j);

		//the balance is written back through the job, so a booked house that
		//is not on the round below still has a row to write to
		if (j.Data == null)
			j.Data = row;

		return row;
	}

	/// <summary>
	/// brings every row showing this job back in line. booked work is on the
	/// list twice, and a job with no row at all is left alone rather than
	/// bringing the page down
	/// </summary>
	private void RefreshRowsForJob(Job j)
	{
		if (j == null)
			return;

		foreach (PaperItem p in PaperItems)
			if (p.JobI3 == j || p.BaseJob == j)
				p.UpdatePaperRecordI3(j);
	}

	private bool SkipNavigatTo = true;
	private void PaperView_NavigatedTo(object sender, NavigatedToEventArgs e)
	{
		if (SkipNavigatTo)
		{
			SkipNavigatTo = false;
			return;
		}

		if (_fullRefresh)
		{
			_fullRefresh = false;
			FullPageLoad();
			return;

		}
		foreach (PaperItem pi in PaperItems)
			pi.UpdatePaperRecordI3(pi.JobI3);
	}

	private void grid_Job_Tapped(object sender, EventArgs e)
    {
		TappedEventArgs args = e as TappedEventArgs;
		Job j = args.Parameter as Job;
		if (j == null)
			return;


        //get the last instance of the job
        Job nextJob;
		nextJob = j;
		//visited guard: a corrupt JobNextId cycle would otherwise loop forever
		HashSet<int> seenJobs = new HashSet<int>() { nextJob.Id };
		while (nextJob.JobNextId != -1)
		{
			nextJob = Job.Query(QueryType.JobId, nextJob.JobNextId).FirstOrDefault();
			if (nextJob == null || !seenJobs.Add(nextJob.Id))
				break;
		}

		if (nextJob == null)
			ViewCustomerDetails.CurrentJob = j;
		else
			ViewCustomerDetails.CurrentJob = nextJob;

		ViewCustomerDetails vcd = new ViewCustomerDetails();
		vcd.OnJobDetialsUpdated += (job) =>
		{
			FullPageLoad();
		};

        Navigation.PushAsync(vcd);
    }

	private async void tap_i3_lable(object sender, EventArgs e)
	{
		TappedEventArgs args = e as TappedEventArgs;
		Job j = args.Parameter as Job;
		PaperItem tappedItem = (sender as Element)?.BindingContext as PaperItem;

		//jobs with no history yet (new or not due) have no I3 record - fall
		//back to the row's upcoming job so they can still be actioned
		if (j == null && tappedItem != null)
			j = tappedItem.JobI3 ?? tappedItem.JobI4 ?? tappedItem.BaseJob;

		if (j == null)
			return;

		if (tappedItem == null)
			tappedItem = j.Data as PaperItem;

		List<string> options = new List<string>();
		if (!j.IsCompleted)
		{
			options.Add($"Done {PaperItem.StringDone}");

			foreach (AlternativePrice ap in j.AlternativePrices ?? new List<AlternativePrice>())
				options.Add($"Done - {ap.Description} {Gloable.CurrenceSymbol}{ap.Price}");
		}

		if (!j.IsPaidFor)
			options.Add($"Paid {PaperItem.StringPaid}");

		if (!j.IsCompleted && !j.IsPaidFor)
			options.Add($"Done & Paid  {PaperItem.StringDonePaid}");

		if (!j.IsCompleted)
			options.Add($"Skip  {PaperItem.StringSkipped}");

		if (!j.IsCompleted)
			options.Add(j.IsBookedIn ? "Unbook" : "Book In");

		if (j.IsCompleted || j.IsPaidFor || j.HaveSkipped)
			options.Add("Clear");

		options.Add(j.HaveCanceled ? "Resume Job" : "Cancel Job");
		options.Add("Custom");
		string result = await DisplayActionSheet($"Update Record", "Cancel", null, options.ToArray());
		if (result == null)
			return;

		if (result == "Cancel Job")
		{
			j.CancelJob();
			//while cancelled work is hidden it has to leave the list, not
			//just collapse in place
			if (!ShowCancelledJobs)
				FullPageLoad();
			else
				tappedItem?.UpdatePaperRecordI3(j);
			return;
		}

		if (result == "Resume Job")
		{
			j.UnCancelJob();
			tappedItem?.UpdatePaperRecordI3(j);
			return;
		}

		if (result == "Book In")
		{
			BookJobFormcs.jobs = new List<Job> { j };
			//the booked work at the top has to be built again with this in it
			_fullRefresh = true;
			await Navigation.PushAsync(new BookJobFormcs());
			return;
		}

		if (result == "Unbook")
		{
			j.UnBookInJob();
			Job.Save();
			//and taken back out of the booked work at the top
			FullPageLoad();
			return;
		}

		//a direct debit already on its way marks the job paid itself when
		//the money arrives, so it must not be marked paid here as well
		if (result.Contains("Paid") && !j.IsPaidFor)
		{
			GoCardlessRequest pendingDD = GoCardlessRequest.PendingForJob(j.Id);
			if (pendingDD != null)
			{
				await DisplayAlert("Payment Pending",
					$"A direct debit is already on its way for this job ({pendingDD.FormattedSummary}). " +
					"It will be marked paid automatically once the money comes through.", "Ok");
				return;
			}
		}

		if (result.Contains("Done"))
		{
			if (result.Contains("-"))//marker for alternative price
			{
				int i = 0;
				foreach (AlternativePrice ap in j.AlternativePrices ?? new List<AlternativePrice>())
				{
					if (result.Contains(ap.Description) && result.Contains($"{ap.Price}"))
					{
						j.UseAlterativePrice = i;
						j.MarkJobDone();
						break;
					}
					i++;
				}
			}
			else
			if (CustomeMarkDate)
				j.MarkJobDone(DateToMarkWorkDone);
			else
				j.MarkJobDone();
		}

		if (result.Contains("Paid"))
		{
        /*    float ball = 0;
            if (j.GetCustomer() != null)
                ball = j.GetCustomer().Balance;

            if (ball > 0)
            {
                await DisplayAlert("Waring", $"{j.JobFormattedStreet} is in dept by {Gloable.CurrenceSymbol}{ball}", "Mark Paid Up", "Cancel");

            }
		*/
            j.MarkJobPaid();
		}

		if (result.Contains("Done & Paid"))
		{
			if (CustomeMarkDate)
				j.MarkJobDone(DateToMarkWorkDone);
			else
                j.MarkJobDone();


            j.MarkJobPaid();
        }

		if (result.Contains("Skip"))
		{
			bool wasBooked = j.IsBookedIn;

			//written up on the day the round was actually done, same as
			//marking work done. through WorkPlanner rather than the job so
			//the day it was booked for lets go of it as well
			if (CustomeMarkDate)
				WorkPlanner.MarkJobSkipped(j, DateToMarkWorkDone);
			else
				WorkPlanner.MarkJobSkipped(j);

			if (wasBooked)
			{
				//the same as Unbook: the house has come out of the booked work
				//at the top and belongs back against its own street
				FullPageLoad();
				return;
			}
		}

		if (result.Contains("Clear"))
		{
			if (j.IsCompleted)
			{
				j.UnMarkJobDone();
			}
			if (j.IsPaidFor)
			{
				j.UnMarkJobPaid();
			}
			if (j.HaveSkipped)
			{
				j.UnSkipJob();
			}
		}

		if (result.Contains("Custom"))
		{
			UpdateJobInstance.CurrentJob = j;
			UpdateJobInstance uJi = new UpdateJobInstance();
			Job customJob = j;
			uJi.OnConfirmed = (() => {
                RefreshRowsForJob(customJob);
            });
			await Navigation.PushAsync(uJi);
			return;
		}

		tappedItem?.UpdatePaperRecordI3(j);
		RefreshRowsForJob(j);
	}

	private async void grid_Ballence_Tapped(object sender, EventArgs e)
	{
		TappedEventArgs args = e as TappedEventArgs;
		Job j = args.Parameter as Job;

		if (j == null)
			return;

		List<string> options = new List<string>();

		Customer c = j.GetCustomer();
		if (c == null)
			return;

		if (c.Balance > 0)
			options.Add("Clear Job Dept");
		else
			options.Add("Clear Job Credit");

		if (c.Balance > 0)
            options.Add("Mark Job As Fully Paid Up");

        options.Add("Change Customer Balance");

		string bal = $"{Gloable.CurrenceSymbol}0.00";

		float owed = 0;

		owed = c.Balance;
		bal = $"{Gloable.CurrenceSymbol}{Math.Abs(owed)}";
		if (owed > 0)
			bal = $"{bal} owing.";
		else
			bal = $"{bal} in credit.";



		string result = await DisplayActionSheet($"Balance {bal}", "Cancel", null, options.ToArray());
		if (result == null)
			return;

		if (result.Contains("Change"))
		{
			if (await CustomerBalance.ChangeAsync(c, this))
				RefreshRowsForJob(j);
			return;
		}

		if (result.Contains("Clear"))
		{
			string msg = string.Empty;
			if (owed > 0)
			{
				msg = $"The customers owes you {Gloable.CurrenceSymbol}{Math.Abs(owed)}. Are you sure you want to clear all the customer dept?";
				bal = "Clear Customer Dept?";
			}
			else
			{
				msg = $"The customers is {Gloable.CurrenceSymbol}{Math.Abs(owed)} in credit with you. Are you sure you want to clear all the customer credit?";
				bal = "Clear Customer Credit?";
			}



			if (await DisplayAlert(bal, msg, "Clear Balance", "Cancel"))
			{

				if (c != null)
					c.Balance = 0;
				RefreshRowsForJob(j);

				Customer.Save();
			}
		}

		if (result.Contains("Mark"))
		{
			string msg = string.Empty;

			msg = $"The customers owes you {Gloable.CurrenceSymbol}{Math.Abs(owed)}. Mark them as fully paid up?";
			bal = "Customer Paid?";

			if (await DisplayAlert(bal, msg, "Yes Paid", "Cancel"))
			{

				//find every instance of job and mark it as paid
				Job jInstance = null;
				jInstance = j;
				
				while (jInstance != null)
				{
					if (jInstance.IsCompleted)
						jInstance.IsPaidFor = true;
                   

                    jInstance = Job.Query(QueryType.CustomerId, jInstance.PreviousJobId).FirstOrDefault();
                }

                jInstance = Job.Query(QueryType.CustomerId, j.JobNextId).FirstOrDefault();

                while (jInstance != null)
                {
                    if (jInstance.IsCompleted)
                        jInstance.IsPaidFor = true;


                    jInstance = Job.Query(QueryType.CustomerId, jInstance.JobNextId).FirstOrDefault();
                }

                Payment.Add(j.CustomerId, c.Balance, PaymentMethod.Cash, string.Empty);
                if (c != null)
					c.Balance = 0;
				RefreshRowsForJob(j);

                Customer.Save();
				Job.Save();

            
            }
        }
		
    }

	private bool _fullRefresh = false;
	private void bnt_newJob_Clicked(object sender, EventArgs e)
	{
		NewJobForm(false);
    }

	/// <summary>
	/// New Quote used to be wired to the new job handler, so anything put in
	/// through it went straight on to the round as work and never reached the
	/// quote list - which is why the quotes section had nothing in it
	/// </summary>
	private void bnt_newQuote_Clicked(object sender, EventArgs e)
	{
		NewJobForm(true);
	}

	private void NewJobForm(bool asQuote)
	{
		NewJob.AddNewJob = true;
		NewJob.AddAsQuote = asQuote;
		NewJob.JobToAdd = null;
		NewJob nj = new NewJob();
		nj.OnJobAdded+=	(Job j) => {
			_fullRefresh = true;
		};
        Navigation.PushAsync(nj);
    }

	private async void grid_street_Tapped(object sender, EventArgs e)
	{
        TappedEventArgs args = e as TappedEventArgs;
		PaperItem pi = args.Parameter as PaperItem;

		if (pi.Title == " ")
			return;

		//the line naming a round is not a street: there is no address on it to
		//add a house to, and everything else on this sheet belongs to a street
		if (pi.IsRoundHeading)
			return;

		List<string> options = new List<string>();
		options.Add("Quick Add");
		options.Add("Add Range");
        options.Add("Add Customer");
        options.Add("Quick Quote");

		int count = 0;
		List<Job> streetJobs = new List<Job>();
		List<Job> runJobs = new List<Job>();

		//The headings over the booked work are a copy of the round further
		//down the list, so booking that work again or marking it off from up
		//here would be acting on the copy rather than the round. Adding a
		//house to the street still makes sense though, and that is what this
		//heading is most useful for - so those are offered and the rest are
		//left to the street's own heading below.
		if (!pi.IsBookedRow)
		{
			foreach(PaperItem paperi in PaperItems)
				if (paperi.GroupId == pi.GroupId)
				{
					if (paperi.JobI3 != null)
						if (!paperi.JobI3.IsCompleted)
							count++;
				}

			//what could be booked in for a day from here: the whole street, or
			//just the run of houses under this heading
			streetJobs = BookableJobs(pi, !pi.IsDateBreak);
			runJobs = pi.IsDateBreak ? streetJobs : BookableJobs(pi, false);

			if (streetJobs.Count > 0 || count > 1)
				options.Add("-----------");

			if (streetJobs.Count > 0)
				options.Add(pi.IsDateBreak
					? $"Book In {runJobs.Count} From Here"
					: $"Book In {streetJobs.Count} On This Street");

			//only worth offering when the street is split by a break, otherwise
			//it would be the same jobs twice
			if (!pi.IsDateBreak && runJobs.Count > 0 && runJobs.Count != streetJobs.Count)
				options.Add($"Book In {runJobs.Count} To The First Break");

			if (count > 1)
				options.Add($"Mark {count} below as compleated");
		}
        string result = await DisplayActionSheet($"{pi.Title}", "Cancel", null, options.ToArray());
        if (result == null)
            return;
        if (result.Contains("Quick Add"))
		{
			//return;
            Location address = new Location()
            {
                Street = pi.PropertyStreet,
                City = pi.PropertyCity,
                Area = pi.PropertyArea,
            };

            QuickAddCustomer.IsQuote = false;
            QuickAddCustomer.TheAddress = address;

            QuickAddCustomer qac = new QuickAddCustomer();
			qac.OnJobCreated = (Job j) =>
			{
				FullPageLoad();
			};
			await Navigation.PushAsync(qac);
			return;
		}

        if (result.Contains("Quick Quote"))
        {
            //return;
            Location address = new Location()
            {
                Street = pi.PropertyStreet,
                City = pi.PropertyCity,
                Area = pi.PropertyArea,
            };
			QuickAddCustomer.IsQuote = true;
            QuickAddCustomer.TheAddress = address;

            QuickAddCustomer qac = new QuickAddCustomer();
            qac.OnJobCreated = (Job j) =>
            {
                FullPageLoad();
            };
            await Navigation.PushAsync(qac);
            return;
        }

        if (result == "Add Range")
        {
            await AddAllHouses(pi);
            return;
        }

        if (result.Contains("Customer"))
		{
            NewJob.AddNewJob = true;
            NewJob.JobToAdd = null;
			
            NewJob nj = new NewJob();
            nj.OnJobAdded += (Job j) => {
                _fullRefresh = true;
            };
            await Navigation.PushAsync(nj);
            return;
		}

		if (result.StartsWith("Book In"))
		{
			BookJobFormcs.jobs = result.Contains("First Break") ? runJobs : streetJobs;
			await Navigation.PushAsync(new BookJobFormcs());
			_fullRefresh = true;
			return;
		}

		if (result.Contains("Mark"))
		{
            foreach (PaperItem paperi in PaperItems)
                if (paperi.GroupId == pi.GroupId)
                {
					if (paperi.JobI3 != null)
						if (!paperi.JobI3.IsCompleted)
						{
							paperi.JobI3.MarkJobDone();
							paperi.UpdatePaperRecordI3(paperi.JobI3);
						}
                }
        }
    }

	/// <summary>
	/// the jobs worth booking in from a heading: still to be done, not
	/// cancelled and not already booked for a day.
	/// </summary>
	/// <param name="wholeStreet">
	/// true for every house on the street; false for just the run of houses
	/// under this heading, which stops at the next break where the houses
	/// were last done at a different time
	/// </param>
	private List<Job> BookableJobs(PaperItem heading, bool wholeStreet)
	{
		List<Job> jobs = new List<Job>();
		foreach (PaperItem p in PaperItems)
		{
			if (p.JobI3 == null)
				continue;

			bool wanted = wholeStreet
				? SameText(p.PropertyStreet, heading.PropertyStreet)
					&& SameText(p.PropertyCity, heading.PropertyCity)
					&& SameText(p.PropertyArea, heading.PropertyArea)
				: p.GroupId == heading.GroupId;

			if (!wanted)
				continue;
			if (p.JobI3.IsCompleted || p.JobI3.HaveCanceled || p.JobI3.IsBookedIn)
				continue;
			if (!jobs.Contains(p.JobI3))
				jobs.Add(p.JobI3);
		}
		return jobs;
	}

	
	private async Task AddAllHouses(PaperItem pi)
	{
		string numbers = await DisplayPromptAsync("Add Range",
			$"House numbers for {pi.DisplayStreet} (e.g. 1,3,5-11 - a range keeps to one side of the street):",
			"Next", "Cancel");
		if (string.IsNullOrWhiteSpace(numbers))
			return;

		string priceText = await DisplayPromptAsync("Add Range", "Price for each job:",
			"Next", "Cancel", keyboard: Keyboard.Numeric);
		if (priceText == null)
			return;
		float.TryParse(priceText, out float price);

		string freqText = await DisplayPromptAsync("Add Range", "Frequency (weeks):",
			"Add", "Cancel", initialValue: "4", keyboard: Keyboard.Numeric);
		if (freqText == null)
			return;
		if (!int.TryParse(freqText, out int weeks) || weeks <= 0)
			weeks = 4;

		int added = 0, skipped = 0;
		foreach (string house in ParseHouseNumbers(numbers))
		{
			bool exists = Customer.Query().Any(c => c.Address != null
				&& string.Equals(c.Address.PropertyNameNumber?.Trim(), house, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(c.Address.Street?.Trim(), pi.PropertyStreet?.Trim(), StringComparison.OrdinalIgnoreCase));
			if (exists)
			{
				skipped++;
				continue;
			}

			Location address = new Location()
			{
				PropertyNameNumber = house,
				Street = pi.PropertyStreet,
				City = pi.PropertyCity ?? string.Empty,
				Area = pi.PropertyArea ?? string.Empty,
				Postcode = string.Empty,
			};

			Customer customer = new Customer { Address = address };
			Customer.Add(customer);

			Job job = new Job
			{
				CustomerId = customer.Id,
				Address = address,
				Price = price,
			};
			job.SetFrequence(weeks, FrequenceType.Week);
			job.DueDate = UsfulFuctions.DateNow;
			Job.Add(job);
			added++;
		}

		Customer.Save();
		Job.Save();
		FullPageLoad();

		string summary = $"Added {added} house(s) on {pi.DisplayStreet}.";
		if (skipped > 0)
			summary += $"\nSkipped {skipped} that already exist.";
		await DisplayAlert("Add Range", summary, "Ok");
	}

	/// <summary>"1,3,5-11" -> 1,3,5,7,9,11. A range whose ends share odd/even
	/// steps by two (one side of the street), otherwise by one.</summary>
	public static List<string> ParseHouseNumbers(string input)
	{
		List<string> result = new List<string>();
		foreach (string token in input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			System.Text.RegularExpressions.Match range =
				System.Text.RegularExpressions.Regex.Match(token.Trim(), @"^(\d+)\s*-\s*(\d+)$");
			if (range.Success)
			{
				int from = int.Parse(range.Groups[1].Value);
				int to = int.Parse(range.Groups[2].Value);
				if (to < from)
					(from, to) = (to, from);
				int step = (from % 2 == to % 2) ? 2 : 1;
				for (int n = from; n <= to && result.Count < 500; n += step)
					result.Add(n.ToString());
			}
			else
				result.Add(token.Trim());
		}
		return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	private void tbi_DoneDate_Clicked(object sender, EventArgs e)
	{
		dp_DoneDate.IsEnabled = true;
		dp_DoneDate.IsVisible = true;
        dp_DoneDate.Focused += Dp_DoneDate_Focused;
        if (!dp_DoneDate.Focus())
		{

		}
		
    }

	private void Dp_DoneDate_Focused(object sender, FocusEventArgs e)
	{
		throw new NotImplementedException();
	}

	private async void bnt_View_Clicked(object sender, EventArgs e)
	{
		List<string> options = new List<string>();
		options.Add("All Jobs");
        options.Add("City");
        options.Add("Area");
        options.Add("Round");

        //Group was on this list with nothing behind it, so picking it put the
        //sheet away and changed nothing. under a button labelled Filters that
        //reads as a filter that does not work
        string result = await DisplayActionSheet("Filter The List", "Cancel", null, options.ToArray());
        if (result == null || result == "Cancel")
            return;
        switch (result)
		{
			case "All Jobs":
				PaperViewFilter = null;
				g_filters.IsVisible = false;
				FullPageLoad();
				break;
			case "City":
				SetFilter(new CityFilter(Job.Query()));
				break;
			case "Area":
				SetFilter(new AreaFilter(Job.Query()));
				break;
			case "Round":
				SetFilter(new RoundFilter(Job.Query()));
				break;
		}


    }

	private DateTime DateToMarkWorkDone = DateTime.Now;
	private bool CustomeMarkDate = false;
	private void dp_MarkDate_Selected(object sender, DateChangedEventArgs e)
	{
        DatePicker dp = sender as DatePicker;
		DateTime dt = dp.Date;
		if (dt.DayOfYear == DateTime.Now.DayOfYear &&  dt.Year == DateTime.Now.Year)
		{
			CustomeMarkDate = false;
		}
		else
		{
			CustomeMarkDate = true;
			DateToMarkWorkDone = new DateTime(dt.Year, dt.Month, dt.Day);
		}	
    }

	/// <summary>
	/// whether cancelled jobs appear in the list at all. off to begin with -
	/// cancelled work is not work to do
	/// </summary>
	public static bool ShowCancelledJobs
	{
		get { return Preferences.Get("PaperView_ShowCancelled", false); }
		set { Preferences.Set("PaperView_ShowCancelled", value); }
	}

	private void cb_showCanceledJobs_Changed(object sender, CheckedChangedEventArgs e)
	{
		if (ShowCancelledJobs == e.Value)
			return;

		ShowCancelledJobs = e.Value;
		FullPageLoad();
	}

	/// <summary>
	/// whether every job is listed, or just the work in hand. off to begin
	/// with - a round that is not due for weeks is not work to do today
	/// </summary>
	public static bool ShowAllJobs
	{
		get { return Preferences.Get("PaperView_ShowAllJobs", false); }
		set { Preferences.Set("PaperView_ShowAllJobs", value); }
	}

	private void cb_showAllJobs_Changed(object sender, CheckedChangedEventArgs e)
	{
		if (ShowAllJobs == e.Value)
			return;

		ShowAllJobs = e.Value;
		FullPageLoad();
	}

	private void bnt_hideOptions_Clicked(object sender, EventArgs e)
	{
		g_options.IsVisible = false;
    }

	private void tbi_ShowOptions_Clicked(object sender, EventArgs e)
	{
        g_options.IsVisible = true;
    }
}