namespace UiInterface.Layouts;

using Kernel;
using System.Collections.ObjectModel;
using System.ComponentModel;

public partial class PaperView : ContentPage
{
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
		public string PropertyTown { get; set; }
		public string PropertyArea { get; set; }
		public string PropertyNumber { get; set; }
		public DateTime StartDate { get; set; } = new DateTime(2000, 1, 1);
		public List<JobInstance> Instances { get; set; } = new List<JobInstance>();
		public int BaseJobId { get; set; } = -1;

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
				if (value)
					BgColour = DueColour;
				else
                    BgColour = Colors.Transparent;
                RaisePropertyChanged("BgColour");
                RaisePropertyChanged("JobIdDue");
			}
		}

        private int InstanceIndex = 0;

		public event PropertyChangedEventHandler PropertyChanged;

		public void UpdatePaperRecordI3(Job j)
		{
			string tmp = string.Empty;
		
			//AT
			//need to do job is due color

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
				OwingColour = Colors.White;
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
					PropertyTown = j.Address.City;
					PropertyArea = j.Address.Area;
					
				}
				BasePice = j.Price;
				Notes = j.Notes;
            }

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
	public PaperView()
	{
		InitializeComponent();

		List<Job> jobs = Job.Query();
		jobs = jobs.OrderByDescending(x => x.OrderByDate).ToList();
		PaperItem pi;

		List<PaperItem> tmpPaperwork = new List<PaperItem>();


		foreach(Job j in jobs)
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

		//now lets group them by streets
		List<string> streets = new List<string>();

		

		foreach(Job j in jobs)
        {
			if (!streets.Contains(j.JobFormattedStreetOnly.ToLower()))
            {
				streets.Add(j.JobFormattedStreetOnly.ToLower());
            }
        }

		PaperItems.Clear();
		foreach(string street in streets)
        {
			List<PaperItem> jobsToAdd = tmpPaperwork.FindAll(x => x.Title.ToLower() == street);
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
		for (int i = 0; i < PaperItems.Count - 1; i++)
		{
			currentItem = PaperItems[i];
            nextItem = PaperItems[i + 1];

			if (!currentItem.ShowJobInformation) //if this is just showing the street etc
			{
				if (nextItem.JobI3 != null)
				{
					currentItem.I3 = nextItem.JobI3.OrderByDate.ToString("dd MMM yy");
					currentItem.I3RowSpan = 2;
				}
				else
					currentItem.I3RowSpan = 1;
			}
			else
			{
				if (currentItem.JobI3 != null && nextItem.JobI3!= null)
					if (currentItem.JobI3.OrderByDate != nextItem.JobI3.OrderByDate)
					{
						PaperItems.Insert(i + 1, new PaperItem() { ShowJobInformation = false }); 
					}
			}
        }

		c_jobList.ItemsSource = PaperItems;

	}

    private void grid_Job_Tapped(object sender, EventArgs e)
    {
		TappedEventArgs args = e as TappedEventArgs;
		Job j = args.Parameter as Job;
		if (j == null)
			return;

		ViewCustomerDetails.CurrentJob = j;
		Navigation.PushAsync(new ViewCustomerDetails());
    }

	private async void tap_i3_lable(object sender, EventArgs e)
	{
		TappedEventArgs args = e as TappedEventArgs;
		Job j = args.Parameter as Job;

		if (j == null)
			return;

		List<string> options = new List<string>();
		if (!j.IsCompleted)
			options.Add($"Done {PaperItem.StringDone}");

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

		if (result.Contains("Done"))
		{
			j.MarkJobDone();
		}

		if (result.Contains("Paid"))
		{
			j.MarkJobPaid();
		}

		if (result.Contains("Done & Paid"))
		{
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
		options.Add("Clear Customer Balance");
		options.Add("Change Customer Balance");


		string bal = $"{Gloable.CurrenceSymbol}0.00";

        float owed = 0;

        if (j.GetCustomer != null)
		{
            owed = j.GetCustomer().Balance;
            bal = $"{Gloable.CurrenceSymbol}{Math.Abs(owed)}";
			if (owed > 0)
				bal = $"{bal} owing.";
			else
                bal = $"{bal} in credit.";

        }
		

        string result = await DisplayActionSheet($"Balance {bal}", "Cancel", "", options.ToArray());

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

			
			
			if (await DisplayAlert(bal, msg, "Confirm", "Cancel"))
			{
				Customer c = j.GetCustomer();
				if (c != null)
					c.Balance = 0;
				PaperItem pi = j.Data as PaperItem;
				pi.Owing = $"Nothing Owed";
				pi.UpdateColors();

                Customer.Save();
			}
		}
    }
}