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
		public string PropertyStreet { get; set; }
		public string PropertyCity { get; set; }
		public string PropertyArea { get; set; }
		public string PropertyNumber { get; set; }

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
            }
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
			}
		}

        public Job JobI4 { get; set; }

		public bool IsQuote { get; set; } = false;

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
            float bal = j.GetCustomer().Balance;

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
			if (j.UseAlterativePrice >= 0)
				if (j.AlternativePrices.Count > 0)
				{
					ji.Price = j.AlternativePrices[j.UseAlterativePrice].Price;
					ji.Label = j.AlternativePrices[j.UseAlterativePrice].Description;
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
					Title = j.Address.Street;
					PropertyNumber = j.Address.PropertyNameNumber;
					PropertyStreet = j.Address.Street;
					PropertyCity = j.Address.City;
					PropertyArea = j.Address.Area;
					
				}
				BasePice = j.Price;
				Notes = j.Notes;
            }
            if (j.JobNextId == -1) //ie no next job
            {
                PropertyStreet = j.Address.Street;
                PropertyCity = j.Address.City;
                PropertyArea = j.Address.Area;
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

	public ObservableCollection<PaperItem> PaperItems = new ObservableCollection<PaperItem>();

	public JobFilterBase PaperViewFilter;

	private CityFilter Filter_City;

	public static void ForceRefresh()
	{
		_fullRefresh = true;	
	}
	
	public PaperView()
	{
		InitializeComponent();
		//return;
		NavigatedTo += PaperView_NavigatedTo;

        Filter_City = new CityFilter(Job.Query());

		g_filters.BindingContext = PaperViewFilter;






		if (Job.Query().Count <= 0)
		{
			l_showNoJobs.IsEnabled = true;
			l_showNoJobs.IsVisible = true;

			bnt_ViewTutorial.IsVisible = true;
			bnt_ViewTutorial.IsEnabled = true;
		}
		else
		{
			l_showNoJobs.IsEnabled = false;
			l_showNoJobs.IsVisible = false;

            bnt_ViewTutorial.IsVisible = false;
			bnt_ViewTutorial.IsEnabled = false;
        }

        //  SetFilter(cf);
        FullPageLoad();

		
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
    private void FullPageLoad()
	{
        List<Job> jobs = Job.Query();
		jobs.RemoveAll(x => x.JobNextId == -1 && x.IsCompleted && UsfulFuctions.DifferenceSigned(DateTime.Now, x.DateCompleated) > 30);

		if (PaperViewFilter != null)
			PaperViewFilter.Filter(ref jobs);

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
            tmpString = $"{j.Address.Street} {j.Address.City} {j.Address.Area}";
            tmpString = tmpString.ToLower();
			if (!location.Contains(tmpString))
            {
                location.Add(tmpString);
				locationData.Add(new PaperViewLocationInfo()
				{
					Street = j.Address.Street,
					City = j.Address.City,
					Area = j.Address.Area,
				});
            }
        }

        PaperItems.Clear();
		int count = 0;
        foreach (string street in location)
        {
            List<PaperItem> jobsToAdd = tmpPaperwork.FindAll(x => x.PropertyStreet.ToLower() == locationData[count].Street.ToLower() && x.PropertyArea == locationData[count].Area.ToLower() && x.PropertyCity.ToLower() == locationData[count].City.ToLower());
            char[] tmp = street.ToCharArray();
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
				PropertyStreet = locationData[count].Street,
                PropertyArea = locationData[count].Area,
                PropertyCity = locationData[count].City,
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
			count++;
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
                    currentItem.I3 = nextItem.JobI3.OrderByDate.ToString("dd MMM yy");
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
						PaperItem pai = new PaperItem();
						pai = new PaperItem() { ShowJobInformation = false };
						pai.GroupId = groupId;
						PaperItems.Insert(i + 1, pai);

					}
					
						
            }

			
        }
		if (PaperItems.Count > 0)
			PaperItems[PaperItems.Count - 1].GroupId = groupId;
	/*	foreach (PaperItem p in PaperItems)
		{
			p.Title = $":: {p.GroupId}";
			p.RaisePropertyChanged("PropertyStreet");
		}*/
        c_jobList.ItemsSource = PaperItems;
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
            if (Job.Query().Count <= 0)
            {
                l_showNoJobs.IsEnabled = true;
                l_showNoJobs.IsVisible = true;

                bnt_ViewTutorial.IsVisible = true;
                bnt_ViewTutorial.IsEnabled = true;
            }
            else
            {
                l_showNoJobs.IsEnabled = false;
                l_showNoJobs.IsVisible = false;

                bnt_ViewTutorial.IsVisible = false;
                bnt_ViewTutorial.IsEnabled = false;
            }
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
		while (nextJob.JobNextId != -1)
		{
			nextJob = Job.Query(QueryType.JobId, nextJob.JobNextId).FirstOrDefault();
			if (nextJob == null)
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

		if (j == null)
			return;

		List<string> options = new List<string>();
		if (!j.IsCompleted)
		{
			options.Add($"Done {PaperItem.StringDone}");

			foreach (AlternativePrice ap in j.AlternativePrices)
				options.Add($"Done - {ap.Description} {Gloable.CurrenceSymbol}{ap.Price}");
		}

		if (!j.IsPaidFor)
			options.Add($"Paid {PaperItem.StringPaid}");

		if (!j.IsCompleted && !j.IsPaidFor)
			options.Add($"Done & Paid  {PaperItem.StringDonePaid}");

		if (!j.IsCompleted)
			options.Add($"Skip  {PaperItem.StringSkipped}");

		if (j.IsCompleted || j.IsPaidFor)
			options.Add("Clear");

		options.Add("Custom");
		string result = await DisplayActionSheet($"Update Record", "Cancel", "", options.ToArray());
		if (result == null)
			return;
		if (result.Contains("Done"))
		{
			if (result.Contains("-"))//marker for alternative price
			{
				int i = 0;
				foreach (AlternativePrice ap in j.AlternativePrices)
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
			j.SkipJob();
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
		}

		if (result.Contains("Custom"))
		{
			UpdateJobInstance.CurrentJob = j;
			UpdateJobInstance uJi = new UpdateJobInstance();
			uJi.OnConfirmed = (() => {
                PaperItem pi = j.Data as PaperItem;
                pi.UpdatePaperRecordI3(j);
            });
			await Navigation.PushAsync(uJi);
			return;
		}

		PaperItem pi = j.Data as PaperItem;
		pi.UpdatePaperRecordI3(j);
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



		string result = await DisplayActionSheet($"Balance {bal}", "Cancel", "", options.ToArray());
		if (result == null)
			return;
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
				PaperItem pi = j.Data as PaperItem;
				pi.Owing = $"Nothing Owed";
				pi.UpdateColors();

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
				PaperItem pi = j.Data as PaperItem;
				pi.Owing = $"Nothing Owed";
				pi.UpdateColors();
                pi.UpdatePaperRecordI3(j);

                Customer.Save();
				Job.Save();

            
            }
        }
		
    }

	private static bool _fullRefresh = false;
	private void bnt_newJob_Clicked(object sender, EventArgs e)
	{
		NewJob.AddNewJob = true;
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

		List<string> options = new List<string>();
		options.Add("Quick Add");
        options.Add("Add Customer");
        options.Add("Quick Quote");

		int count = 0;
		foreach(PaperItem paperi in PaperItems)
			if (paperi.GroupId == pi.GroupId)
			{
				if (paperi.JobI3 != null)
					if (!paperi.JobI3.IsCompleted)
						count++;
			}

		if (count > 1)
		{
			options.Add("-----------");
			options.Add($"Mark {count} below as compleated");
		}
        string result = await DisplayActionSheet($"{pi.Title}", "Cancel", "", options.ToArray());
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
        options.Add("Group");
        string result = await DisplayActionSheet("Select View", null, null, options.ToArray());
        if (result == null)
            return;
        switch (result)
		{
			case "City":
				SetFilter(Filter_City);
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

	private void bnt_hideOptions_Clicked(object sender, EventArgs e)
	{
		g_options.IsVisible = false;
    }

	private void tbi_ShowOptions_Clicked(object sender, EventArgs e)
	{
        g_options.IsVisible = true;
    }

	private void bnt_ViewTutorial_Clicked(object sender, EventArgs e)
	{

    }
}