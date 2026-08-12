using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Serialization;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Kernel
{
    /// <summary>
    /// the main job class
    /// </summary>
    public partial class Job: INotifyPropertyChanged
    {

        public static List<string> JobNames = new List<string>()
        {
            "Windows",
            "Gutter Clear",
            "Fascias and Soffits",
            "Conservatory Roof",
            "Solar Pannels",
            "PVC Whiting",
            "Grass Cutting",
            
        };

        public GridLength Gr { get; set; } = new GridLength(0.3, GridUnitType.Star);

        /// <summary>
        /// the master id number
        /// </summary>
        private static int _IdGenerator = 0;

        /// <summary>
        /// the master list of jobs
        /// </summary>
        private static List<Job> _Jobs = new List<Job>();

        /// <summary>
        /// all the quotes we have
        /// </summary>
        private static List<Job> _Quotes = new List<Job>();

        /// <summary>
        /// add a new job
        /// </summary>
        /// <param name="customerId">the customer the job belongs too</param>
        /// <param name="price">the price of the job</param>
        /// <param name="frequence">the frequence of the job</param>
        /// <returns></returns>
        public static ResultType Add(int customerId, float price, int frequence)
        {
            return Add(customerId, price, frequence, UsfulFuctions.DateNow);
        }

        public static ResultType Add(Job job)
        {
            job.GenerateId();
            job.BaseJobId = job.Id;
            _Jobs.Add(job);
            return ResultType.Success;
        }

        /// <summary>
        /// add a new quote to the system
        /// </summary>
        /// <param name="Quote"></param>
        /// <returns></returns>
        public static ResultType AddQuote(Job Quote)
        {
            Quote.GenerateId();
            Quote.BaseJobId = Quote.Id;
            _Quotes.Add(Quote);
            return ResultType.Success;
        }

        /// <summary>
        /// add a new job
        /// </summary>
        /// <param name="customerId">the customer the job belongs too</param>
        /// <param name="price">the price of the job</param>
        /// <param name="frequence">the frequence of the job</param>
        /// <param name="dueDate">the date the job is due</param>
        /// <returns></returns>
        public static ResultType Add(int customerId, float price, int frequence, DateTime dueDate)
        {

            Job job = new Job()
            {
                CustomerId = customerId,
                Price = price,
                Frequence = frequence,
            };
            job.GenerateId(); //generate the id
            job.SetDueDate(dueDate);
            _Jobs.Add(job); //add the job

            return ResultType.Success;
        }


        public static void RefreshJobs()
        {
            string s;
            foreach (Job j in _Jobs)
            {
                j.Refresh();
                j.RefreshColors();
                //s = j.JobFormattedOwed;
                //s = j.JobFormattedDueTime;
              
            }
        }
        /// <summary>
        /// mark a job as compleated
        /// </summary>
        /// <param name="id">the id of the job to make compleated</param>
        public static void MarkCompleate(int id)
        {
            Job j = FindJobs(id).FirstOrDefault();
            if (j != null)
            {
                j.MarkJobDone();
            }

        }

        /// <summary>
        /// mark a job as compleated
        /// </summary>
        /// <param name="id">the id of the job to compleate</param>
        /// <param name="date">the date the job was compleated</param>
        public static void MarkCompleate(int id, DateTime date)
        {
            Job j = FindJobs(id).FirstOrDefault();
            if (j != null)
            {
                j.MarkJobDone(date);
            }

        }

        private static List<Job> _tmpJobs = new List<Job>();
        /// <summary>
        /// query jobs
        /// </summary>
        /// <returns>returns all jobs</returns>
        public static List<Job> Query()
        {
            _tmpJobs.Clear();
            _tmpJobs.AddRange(_Jobs);
            return _tmpJobs;
        }

        /// <summary>
        /// query jobs
        /// </summary>
        /// <returns>returns all jobs</returns>
        public static List<Job> QueryQuotes()
        {
            _tmpJobs.Clear();
            _tmpJobs.AddRange(_Quotes);
            return _tmpJobs;
        }

        /// <summary>
        /// query jobs with a input query
        /// </summary>
        /// <param name="type">the query type</param>
        /// <param name="id">the id</param>
        /// <returns></returns>
        public static List<Job> Query(QueryType type, int id)
        {
            switch(type)
            {
                case QueryType.CustomerId:
                    return FindJobsForCustomer(id);

                case QueryType.JobId:
                    return FindJobs(id);
            }

            return null;
        }


        public static List<Job> FindJobs(QueryType type, DateTime date, JobFilter jobFilter)
        {
            if (type == QueryType.AfterDate || type == QueryType.BeforeDate)
                return FindJobsDate(type, date, jobFilter);

            return new List<Job>();
        }
        private static List<Job> FindJobsForCustomer(int customerId)
        {
            return _Jobs.FindAll(x => x.CustomerId == customerId);
        }

        private static List<Job> FindJobs(int jobId)
        {
            return _Jobs.FindAll(x => x.Id == jobId);
        }

        private static List<Job> FindJobsDate(QueryType type, DateTime date, JobFilter jobFilter)
        {
            switch(type)
            {
                case QueryType.AfterDate:
                    if (jobFilter == JobFilter.Compleated)
                        return _Jobs.FindAll(x => x.DueDate >= date && x.IsCompleted == true);

                    if (jobFilter == JobFilter.NotCompleated)
                        return _Jobs.FindAll(x => x.DueDate >= date && x.IsCompleted == false);

                    return _Jobs.FindAll(x => x.DueDate >= date);

                case QueryType.BeforeDate:
                    if (jobFilter == JobFilter.Compleated)
                        return _Jobs.FindAll(x => x.DueDate <= date && x.IsCompleted == true);

                    if (jobFilter == JobFilter.NotCompleated)
                        return _Jobs.FindAll(x => x.DueDate <= date && x.IsCompleted == false);

                    return _Jobs.FindAll(x => x.DueDate <= date);
            }

            return new List<Job>();
        }

        public static void SortJobsByDateDue()
        {
            _Jobs = _Jobs.OrderBy(x => x.DueDate).ThenBy(x=>x.DateCompleated).ToList();
        }

        /// <summary>
        /// generate the id number for the current customer
        /// </summary>
        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        private Customer _customer;

        public void MatchCustomer()
        {
            if (_customer == null)
            {

                try
                {
                    List<Customer> c = Customer.Query("id", CustomerId.ToString());
                    if (c.Count > 0)
                        _customer = c[0];
                }
                catch
                {
                    return;
                }
            }
        }

        public static void DeleteData()
        {
            _Jobs.Clear();
        }

        public List<AlternativePrice> AlternativePrices;

        [XmlIgnore]
        public bool DisableSwipe = false;

        public bool EnabledSwipe { get { return !DisableSwipe; } }
        public int UseAlterativePrice = -1;
        /// <summary>
        /// the first job this is based off
        /// for example if a job repeates then we will have a reference
        /// to the first job
        /// if there is a price incress it will be reflected and be detectable
        /// </summary>
        public int BaseJobId;

        /// <summary>
        /// the uniuqe id for this job
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// the customer id to link this job to
        /// </summary>
        public int CustomerId;
        /// <summary>
        /// the name of the job can be left blank
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// the description of the job. Can be left blank
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// any notes for the job
        /// </summary>
        public string Notes = String.Empty;

        /// <summary>
        /// any notes for this instance of job
        /// </summary>
        public string JobInstanceNotes = string.Empty;
        /// <summary>
        /// the address of the job
        /// </summary>
        public Location Address;

        /// <summary>
        /// 
        /// </summary>
        public int DayId;

        /// <summary>
        /// the next instance of the job
        /// </summary>
        public int JobNextId = -1;

        /// <summary>
        /// the previous instance of the job
        /// </summary>
        public int PreviousJobId = -1;

        /// <summary>
        /// estimated time in minutes
        /// </summary>
        public int EstimatedTime = 0;
        /// <summary>
        /// if the job is booked in or not
        /// </summary>
        public bool IsBookedIn { get; set; } = false;

        public DateTime DateJobBookinFor;

        [XmlIgnore]
        public DateTime OrderByDate
        {
            get
            {
                if (IsCompleted)
                    return DateCompleated;

                //a skipped job belongs on the day it was skipped, with the
                //rest of the work done that day, not on the due date it was
                //pushed out to
                if (HaveSkipped && DateSkipped > UsfulFuctions.DateBase)
                    return DateSkipped;

                return DueDate;
            }
        }

        /// <summary>
        /// this is where we can put temp data
        /// this is not saved
        /// </summary>
        [XmlIgnore]
        public object Data;

        private static bool _selectionMode = false;

        private static double _gridLengthCheckBoxWidth = 0;
        public double GridLengthCheckBoxWidth
        {
            get
            {
                return _gridLengthCheckBoxWidth;
            }
            set
            {
                _gridLengthCheckBoxWidth = value;
                RaisePropertyChanged("GridLengthCheckBoxWidth");
            }
            
        }
        public bool SelectionModeEnabled 
        {
            get { return _selectionMode; }
            set
            {
                _selectionMode = value;
                if (_selectionMode)
                    GridLengthCheckBoxWidth =0.3;
                else
                    GridLengthCheckBoxWidth = 0;
                RaisePropertyChanged("SelectionModeEnabled");
            }
        }


        [XmlIgnore]
        public Color AltColour { get; set; } = Colors.Transparent;
        [XmlIgnore]
        private bool _isSelected;
        [XmlIgnore]
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                RaisePropertyChanged("IsSelected");
            }
        }

        [XmlIgnore]
        private bool _collapsedInList;
        /// <summary>
        /// ui state: completed jobs show as a narrow faded row in day lists until tapped
        /// </summary>
        [XmlIgnore]
        public bool CollapsedInList
        {
            get { return _collapsedInList; }
            set
            {
                _collapsedInList = value;
                RaisePropertyChanged("CollapsedInList");
                RaisePropertyChanged("ExpandedInList");
            }
        }
        [XmlIgnore]
        public bool ExpandedInList { get { return !_collapsedInList; } }

        [XmlIgnore]
        public DateTime tmpDate;

        public void UnBookInJob()
        {
            IsBookedIn = false;
            DateJobBookinFor = new DateTime(2000, 1, 1);
        }
        public void BookInJob(DateTime date)
        {
            DateJobBookinFor = date;
            IsBookedIn = true;
        }

        /// <summary>
        /// set the frequcne of the job
        /// </summary>
        /// <param name="i"></param>
        


        public void SetFrequence(int i, FrequenceType type)
        {
            if (i < 0)
                return;

            Frequence = i;
            Frequence_Type = type;
        }

        private int GenerateNextDueDate()
        {
            Job j = this.DeepCopy();
            j.JobNextId = -1; //reset next id
            j.IsCompleted = false;
            j.DateCompleated = new DateTime(2000, 1, 1);
            j.PaymentId = -1;
            j.IsPaidFor = false;
            j.PreviousJobId = Id;

            switch (Frequence_Type)
            {
                case FrequenceType.Day:
                    j.DueDate = DateCompleated.AddDays(Frequence);
                    break;

                case FrequenceType.Week:
                    j.DueDate = DateCompleated.AddDays(7 * Frequence);
                    break;

                case FrequenceType.Month:
                    j.DueDate = DateCompleated.AddMonths(Frequence);
                    break;

                case FrequenceType.Year:
                    j.DueDate = DateCompleated.AddYears(Frequence);
                    break;
            }
            
            j.GenerateId();
            j.PreviousJobId = this.Id; //set the id
            _Jobs.Add(j);
            return j.Id;
        }

        private static string tmp;
        private static int tmpInt;
        public void Refresh()
        {

            //tmp = JobFormattedDueTime;
            //tmp = JobFormattedOwed;
            RaisePropertyChanged("JobFormattedOwed");
            RaisePropertyChanged("JobFormattedDueTime");
            RaisePropertyChanged("ShowOwed");
            RaisePropertyChanged("PaymentPending");
            RaisePropertyChanged("PaymentPendingText");
            RaisePropertyChanged("IsMarked");
            RaisePropertyChanged("DoneActionText");
            RaisePropertyChanged("ShowPaidAction");
        }

        /// <summary>
        /// the job has already been marked done or paid, so the swipe offers
        /// Clear and More rather than Done and Done &amp; Paid again
        /// </summary>
        [XmlIgnore]
        public bool IsMarked
        {
            get { return IsCompleted || IsPaidFor; }
        }

        /// <summary>what the first swipe action does to this job as it stands</summary>
        [XmlIgnore]
        public string DoneActionText
        {
            get { return IsMarked ? "Clear" : "Done"; }
        }

        /// <summary>
        /// Done &amp; Paid is only worth offering on a job that is neither yet
        /// </summary>
        [XmlIgnore]
        public bool ShowPaidAction
        {
            get { return !IsMarked; }
        }

        /// <summary>the payment this job was marked paid with, or null</summary>
        [XmlIgnore]
        public Payment JobPayment
        {
            get
            {
                if (!IsPaidFor)
                    return null;

                Payment p = Payment.Get(PaymentId);
                return p == null || p.Id == -1 ? null : p;
            }
        }

        /// <summary>
        /// Clearing a job takes its payment back off the customer's balance,
        /// which is only right for cash taken at the door. Money that came in
        /// through the bank was read off a statement and has to be dealt with
        /// there, or the books stop matching the bank.
        /// </summary>
        [XmlIgnore]
        public bool CanClearPayment
        {
            get
            {
                Payment p = JobPayment;
                return p == null || p.PaymentMethod == PaymentMethod.Cash;
            }
        }

        /// <summary>everything this customer owes, as it stands right now</summary>
        [XmlIgnore]
        public float CustomerOwes
        {
            get
            {
                MatchCustomer();
                return _customer == null ? 0 : _customer.Balance;
            }
        }
        public void MarkJobPaid()
        {
            if (IsPaidFor)
                return;

            //a direct debit is already on its way, marking it paid here as
            //well would take the money twice
            if (PaymentPending)
                return;

            IsPaidFor = true;

            MatchCustomer();
            if (_customer!= null)
            {
               //c[0].Balance -= Price;
               PaymentId = Payment.Add(_customer.Id, EffectivePrice, PaymentMethod.Cash, string.Empty).Id;
                Payment.Save();
                
            }
            Job.Save();
        }

        public void MarkJobPaid(float amount, PaymentMethod paymentMethod)
        {
            if (IsPaidFor)
                return;

            //see MarkJobPaid() above - never pay a job twice
            if (PaymentPending)
                return;

            IsPaidFor = true;

            MatchCustomer();
            if (_customer != null)
            {

                PaymentId = Payment.Add(_customer.Id, amount, paymentMethod, string.Empty).Id;
                Payment.Save();
            }
            Job.Save();
        }

        /// <summary>
        /// clear whatever is still owing and mark the job paid, for when the
        /// money received will never exactly match what was charged - a
        /// customer rounding down, or a processing fee.
        ///
        /// the shortfall is written off, not recorded as income, because it
        /// was never actually received
        /// </summary>
        /// <returns>the amount written off</returns>
        public float SettleBalance()
        {
            MatchCustomer();

            float writtenOff = 0;
            if (_customer != null)
            {
                writtenOff = _customer.Balance;
                _customer.Balance = 0;
                Customer.Save();
            }

            //a direct debit still on its way marks the job paid itself when
            //it lands, so it is left alone here
            if (!IsPaidFor && !PaymentPending)
                IsPaidFor = true;

            Refresh();
            RefreshColors();
            Job.Save();
            return writtenOff;
        }

        /// <summary>
        /// mark paid against a payment that has already been recorded (and
        /// has therefore already come off the customer's balance). used when
        /// a direct debit finally clears
        /// </summary>
        public void MarkJobPaidByRecordedPayment(int paymentId)
        {
            if (IsPaidFor)
                return;

            IsPaidFor = true;
            PaymentId = paymentId;
            Job.Save();
        }

        /// <summary>
        /// true while a direct debit payment request for this job is waiting
        /// to clear. the job is not paid yet and cannot be charged again
        /// </summary>
        [XmlIgnore]
        public bool PaymentPending
        {
            get { return GoCardlessRequest.HasPendingForJob(Id); }
        }

        /// <summary>
        /// the pending direct debit as a short line for the job lists
        /// </summary>
        [XmlIgnore]
        public string PaymentPendingText
        {
            get
            {
                GoCardlessRequest r = GoCardlessRequest.PendingForJob(Id);
                if (r == null)
                    return string.Empty;
                if (r.ChargeDate > UsfulFuctions.DateBase)
                    return $"DD {r.FormattedAmount} due {r.ChargeDate.ToShortDateString()}";
                return $"DD {r.FormattedAmount} pending";
            }
        }


        public void UnMarkJobPaid()
        {
            if (!IsPaidFor)
                return;

            IsPaidFor = false;

            MatchCustomer();
            if (_customer != null)
            {
                //  c[0].Balance += Price;
                _customer.Balance += Payment.Remove(PaymentId);
                Customer.Save();
                Payment.Save();
            }
            Job.Save();
        }


        public void MarkJobDone(bool forceNotSave = false)
        {
            MarkJobDone(UsfulFuctions.DateNow, forceNotSave);
            this.Refresh();
        }

        public void MarkJobDone(DateTime date, bool forceNotSave = false)
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            HaveSkipped = false;
            DateSkipped = UsfulFuctions.DateBase;
            DateCompleated = date;
            if (Frequence > 0)
                JobNextId = GenerateNextDueDate();

            MatchCustomer();
            if (_customer != null)
            {
                _customer.Balance += EffectivePrice;
                if (!forceNotSave)
                {
                    Payment.Save();
                    Customer.Save();
                }
            }
            if (!forceNotSave)
                Job.Save();
        }

        public bool UnMarkJobDone(bool forceNotSave = false)
        {
            if (!IsCompleted)
                return false;

            

            List<Job> jobChecks = _Jobs.FindAll(x => x.Id == JobNextId);
            if (jobChecks.Count > 0)
            {
                if (jobChecks[0].IsCompleted) //if the next instance is already done we cannot uncompleate this job
                    return false;
            }

            IsCompleted = false;
            DateCompleated = new DateTime();
            _Jobs.RemoveAll(x => x.Id == JobNextId); //remove the next instance of the job

            MatchCustomer();
            if (_customer != null)
            {
                //has to be the same figure MarkJobDone added, or the balance
                //is left out by the difference
                _customer.Balance -= EffectivePrice;
                if (!forceNotSave)
                {
                    Payment.Save();
                    Customer.Save();
                }
            }

            this.Refresh();
            if (!forceNotSave)
                Job.Save();
            UseAlterativePrice = -1;
            return true;
        }


        public void SkipJob()
        {
            SkipJob(UsfulFuctions.DateNow);
        }

        /// <summary>
        /// skip the job, noting the day it was skipped on
        /// </summary>
        /// <param name="dateSkipped">
        /// the day you were there and skipped it, which is not necessarily
        /// today when a round is being written up afterwards
        /// </param>
        public void SkipJob(DateTime dateSkipped)
        {
            DueDate = DueDate.AddDays(7 * Frequence);
            HaveSkipped = true;
            //kept so the skip stays on the day it happened, alongside the
            //work done that day, rather than moving with the new due date
            DateSkipped = dateSkipped;
            Job.Save();
        }

        public void UnSkipJob()
        {
            if (!HaveSkipped)
                return;
            DueDate = DueDate.AddDays(-7 * Frequence);
            HaveSkipped = false;
            DateSkipped = UsfulFuctions.DateBase;
            Job.Save();
        }

        public void CancelJob()
        {
            HaveCanceled = true;
            DateCanceled = UsfulFuctions.DateNow;
            Job.Save();
        }

        public void UnCancelJob()
        {
            HaveCanceled = false;
            DateCanceled = UsfulFuctions.DateBase;
            Job.Save();
        }
        /// <summary>
        /// how often this repeates 0 is never
        /// time is in weeks
        /// -1 will represent 1 calendar months
        /// -2 will be 2 calendar months
        /// - up to -12 for the year
        /// </summary>
        public int Frequence;


        public FrequenceType Frequence_Type = FrequenceType.Week;

        /// <summary>
        /// this price of the job
        /// </summary>
        public float Price;

        /// <summary>
        /// what the job is actually worth: the alternative price when one
        /// was used (front only and so on), otherwise the normal price
        /// </summary>
        /// <summary>
        /// true when an alternative price is in use and really is there. the
        /// list is empty until a price is added to it, and can be cleared
        /// again while the choice of price is left behind
        /// </summary>
        [XmlIgnore]
        public bool HasAlternativePrice
        {
            get
            {
                return UseAlterativePrice >= 0
                    && AlternativePrices != null
                    && UseAlterativePrice < AlternativePrices.Count;
            }
        }

        [XmlIgnore]
        public AlternativePrice ChosenAlternativePrice
        {
            get { return HasAlternativePrice ? AlternativePrices[UseAlterativePrice] : null; }
        }

        [XmlIgnore]
        public float EffectivePrice
        {
            get
            {
                if (HasAlternativePrice)
                    return AlternativePrices[UseAlterativePrice].Price;
                return Price;
            }
        }

        public string SubChargeReason;
        public float SubCharge;

        public bool CustomerAddressDifferentToJob = false;

        public string JobFormattedStringPrice
        {
            get
            {
          //      RaisePropertyChanged("JobFormattedStringPrice");
                return $"Price {Gloable.CurrenceSymbol}{Price}";
            }
        }
        public string JobFormattedStringNotes
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedStringNotes");
                return $"{Notes}";
            }
        }
        public string JobFormattedString
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedString");
                if (Address == null)
                    return string.Empty;
                return $"{Address.PropertyNameNumber} {Address.Street} {Address.City} {Address.Area}";

            }
        }

        public string JobFormattedStreet
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedStreet");
                if (Address == null)
                    return string.Empty;
                return $"{Address.PropertyNameNumber} {Address.Street}";

            }
        }


        public string JobFormattedHouseNumber
        {
            get
            {
           //     RaisePropertyChanged("JobFormattedHouseNumber");
                if (Address == null)
                    return string.Empty;
                return $"{Address.PropertyNameNumber}";

            }
        }
        public string JobFormattedStreetOnly
        {
            get
            {
              //  RaisePropertyChanged("JobFormattedStreetOnly");
                if (Address == null)
                    return string.Empty;
                return $"{Address.Street}";

            }
        }

        /// <summary>
        /// Putting work in the order it would be walked or driven: street
        /// first, then up the street by house number.
        ///
        /// Sorting on the formatted address does not do this - it reads
        /// "12 High Street", so the house number is what gets sorted and it
        /// gets sorted as text, which puts 10 before 2 and scatters a street
        /// across the list. These three are the sort key instead:
        /// <see cref="SortStreet"/>, then <see cref="SortHouseNumber"/>, then
        /// <see cref="SortHouseSuffix"/>.
        /// </summary>
        [XmlIgnore]
        public string SortStreet
        {
            get { return Address == null || Address.Street == null ? string.Empty : Address.Street.Trim(); }
        }

        /// <summary>
        /// the house number as a number, so 2 comes before 10. a house with a
        /// name rather than a number sorts after the numbered ones
        /// </summary>
        [XmlIgnore]
        public int SortHouseNumber
        {
            get
            {
                string digits = string.Empty;
                foreach (char c in HouseNumberText())
                {
                    if (!char.IsDigit(c))
                        break;
                    digits += c;
                }

                if (digits.Length == 0 || !int.TryParse(digits, out int number))
                    return int.MaxValue;

                return number;
            }
        }

        /// <summary>
        /// whatever is left of the house number once the digits are off the
        /// front, so 12a comes before 12b - and the whole name for a house
        /// that has one instead of a number
        /// </summary>
        [XmlIgnore]
        public string SortHouseSuffix
        {
            get
            {
                string text = HouseNumberText();

                int digits = 0;
                while (digits < text.Length && char.IsDigit(text[digits]))
                    digits++;

                return text.Substring(digits).Trim();
            }
        }

        private string HouseNumberText()
        {
            if (Address == null || Address.PropertyNameNumber == null)
                return string.Empty;
            return Address.PropertyNameNumber.Trim();
        }

        public string JobFormattedCity
        {
            get
            {
            //    RaisePropertyChanged("JobFormattedCity");
                if (Address == null)
                    return string.Empty;
                return $"{Address.City}";

            }
        }

        public string JobFormattedArea
        {
            get
            {
              //  RaisePropertyChanged("JobFormattedArea");
                if (Address == null)
                    return string.Empty;
                return $"{Address.Area}";

            }
        }

        public string JobFormattedSubString
        {
            get
            {
                RaisePropertyChanged("JobFormattedSubString");
                return $"Frequence {Frequence} Weekly {Gloable.CurrenceSymbol}{Price}";

            }
        }

        public string FormattedData
        {
            get
            {
             //   RaisePropertyChanged("FormattedData");
                if (IsCompleted)
                    return DateCompleated.ToShortDateString();
                else
                    return DueDate.ToShortDateString();
            }
        }
        
        public string JobFormattedDetails
        {
            get {
               // RaisePropertyChanged("JobFormattedDetails");
                if (IsCompleted)
                {
                    tmp = $"Completed on {DateCompleated.ToShortDateString()}.";
                    AlternativePrice chosen = ChosenAlternativePrice;
                    if (chosen == null)
                        tmp += $"Price {Gloable.CurrenceSymbol}{Price}";
                    else
                        tmp += $"Price {Gloable.CurrenceSymbol}{chosen.Price} for {chosen.Description}";
                }
                else
                    tmp = $"Job next due on {DueDate.ToShortDateString()}";

                return tmp;
            }

        }

        [XmlIgnore]
        public Color DueColorCode
        {
            get
            {
                return _dueColorCode;
            }
            set
            {
                _dueColorCode = value;
                RaisePropertyChanged("DueColorCode");
            }
        } 
        private Color _dueColorCode = Colors.LightGray;
        [XmlIgnore]
        public Color DueColorTextCode
        {
            get
            {
                return _dueColorTextCode;
            }
            set
            {
                _dueColorTextCode = value;
                RaisePropertyChanged("DueColorTextCode");
            }
        } 
        private Color _dueColorTextCode = Colors.LightGray;


        public string JobFormattedDueTime
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedDueTime");
                if (IsCompleted)
                {
                    DueColorCode = Colors.LightGray;
                    DueColorTextCode = Colors.Black;
                    //counted from the day the work was actually done, not the day
                    //it fell due. this used to measure from DueDate, so a job
                    //cleaned months after it was due reported the whole overdue
                    //stretch as though that was how long ago it was cleaned -
                    //work finished this morning came up as "526 Days Ago"
                    int d = UsfulFuctions.Difference(DateCompleated, UsfulFuctions.DateNow);
                    switch (d)
                    {
                        case 0:
                            return $"Completed Today";

                        //Difference has no sign to it, so yesterday counts as
                        //one. the old -1 here could never be matched
                        case 1:
                            return $"Completed Yesterday";

                    }
                    return $"Completed {d} Days Ago";
                }

                DueColorTextCode = Colors.White;

                if (HaveCanceled)
                {
                    DueColorCode = Colors.Red;
                    return  "Canceled";
                }

             
                if (DueDate.DayOfYear == DateTime.Now.DayOfYear && DueDate.Year == DateTime.Now.Year) //if not due
                {
                    DueColorCode = Colors.Orange;
                    return "Due Today";
                    
                }

                if (DueDate.Ticks > UsfulFuctions.DateNow.Ticks) //if not due
                {
                    DueColorCode = Colors.Blue;
                    tmpInt = UsfulFuctions.Difference(DueDate, UsfulFuctions.DateNow);
                    switch (tmpInt)
                    {
                        case 0:
                            return $"Due Today";

                        case 1:
                            return $"Due Tomorrow";

                        default:
                            return $"Due in {UsfulFuctions.Difference(DueDate, UsfulFuctions.DateNow)} Days";
                    }
                    
                    
                }

                DueColorCode = Colors.Red;
                return $"{UsfulFuctions.Difference(DueDate, UsfulFuctions.DateNow)} Days Late";
                
            }
        }

        [XmlIgnore]

        public Color OwedColorCode
        {
            get
            {
                return _owedColorCode;
            }
            set
            {
                _owedColorCode = value;
                RaisePropertyChanged("OwedColorCode");
            }
        }
        private Color _owedColorCode;
        public Customer GetCustomer()
        {
            MatchCustomer();
            return _customer;
        }

        public void TmpSetCustomer(Customer c)
        {
            _customer = c;
        }

        public void AddToBalenceOwed(float amount)
        {
            MatchCustomer();
            if (_customer == null)
                return;
            _customer.Balance += amount;
        }

        public void AddToBalenceCredit(float amount)
        {
            MatchCustomer();
            if (_customer == null)
                return;
            _customer.Balance -= amount;
        }
        public bool HaveJobNotes
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Notes);
            }
        }

        [XmlIgnore]
        public bool HaveJobName
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Name);
            }
        }

        public string JobFormattedOwedShort
        {
            get
            {
                MatchCustomer();
            //    RaisePropertyChanged("JobFormattedOwedShort");
                if (_customer == null)
                {
                    return string.Empty;
                }

                if (_customer.Balance >= 0)
                    return $"{_customer.Balance}";

                return "0";
            }
        }


        public void RefreshColors()
        {
            OwedColorCode = Colors.Yellow;

            MatchCustomer();

            if (_customer == null)
            {
                OwedColorCode = Colors.Transparent;
                return;
            }

            if (_customer.Balance == 0)
            {
                OwedColorCode = Colors.LightBlue;
                return;

            }

            if (_customer.Balance > 0)
            {
                OwedColorCode = Colors.Red;
                return;

            }

            OwedColorCode = Colors.Green;
            return;
        }

        /// <summary>
        /// hide the owed tag when there is no customer or nothing owed
        /// </summary>
        [XmlIgnore]
        public bool ShowOwed
        {
            get
            {
                MatchCustomer();
                return _customer != null && _customer.Balance != 0;
            }
        }

        public string JobFormattedOwed
        {
            get
            {
                MatchCustomer();


      //         RaisePropertyChanged("JobFormattedOwed");

                if (_customer == null)
                {
               
                    return string.Empty;
                }

                if (_customer.Balance == 0)
                {
                    return  "Nothing Owed";
                     
                }

                if (_customer.Balance > 0)
                {
                    return $"Owes {Gloable.CurrenceSymbol}{_customer.Balance}";
                    
                }

                return $"{Gloable.CurrenceSymbol}{Math.Abs(_customer.Balance)} In Credit";
                
            }
        }

        /// <summary>
        /// date the job is due
        /// </summary>
        public DateTime DueDate;

        /// <summary>
        /// date the job was compleated
        /// </summary>
        public DateTime DateCompleated;

        /// <summary>
        /// if the job has been compleated
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// if the job has been paid for
        /// </summary>
        public bool IsPaidFor { get; set; }

        /// <summary>
        /// the payment id
        /// </summary>
        public int PaymentId { get; set; }  
        
        /// <summary>
        /// if you want to text night before or not
        /// </summary>
        public bool TNB { get; set; }


        /// <summary>
        /// if you want to send email night before or not
        /// </summary>
        public bool ENB { get; set; }

        /// <summary>
        /// if the customer has been text or not
        /// </summary>
        public bool HaveBeenText { get; set; }

        /// <summary>
        /// if the customer has been emailed or not
        /// </summary>
        public bool HaveBeenEmailed { get; set; }

        /// <summary>
        /// if to text after completion
        /// </summary>
        public bool TAC { get; set; }

        /// <summary>
        /// if to email after completion
        /// </summary>
        public bool EAC { get; set; }

        /// <summary>
        /// if the job has been canceld or not
        /// </summary>
        private bool _haveCanceled = false;
        public bool HaveCanceled
        {
            get { return _haveCanceled; }
            set
            {
                _haveCanceled = value;
                RaisePropertyChanged("HaveCanceled");
                RaisePropertyChanged("NotCanceled");
            }
        }
        [XmlIgnore]
        public bool NotCanceled { get { return !_haveCanceled; } }

        public bool HaveSkipped { get; set; } = false;

        /// <summary>
        /// the day the job was skipped on. jobs saved before this was kept
        /// have nothing here, and fall back to the due date
        /// </summary>
        public DateTime DateSkipped { get; set; }

        public DateTime DateCanceled { get; set; }
        public static void Delete(string id)
        {
            _Jobs.RemoveAll(x => x.Id.ToString() == id);
        }
        public void SetDueDate(DateTime date)
        {
            DueDate = date;
        }


       

        public void SetDueDate(int day, int month, int year)
        {
            DueDate = new DateTime(year, month, day);
        }

        public void SetDueDateInFuture(int days)
        {
            DueDate = UsfulFuctions.DateNow;
            DueDate.AddDays(days);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string propertyName)
        {
          
            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public Job DeepCopy()
        {
            Job job = new Job();
            job.TAC = TAC;
            job.Notes = Notes;
            job.Frequence = Frequence;
            job.Address = Address.DeepCopy();
            job.BaseJobId = BaseJobId;
            job.CustomerAddressDifferentToJob = CustomerAddressDifferentToJob;
            job.CustomerId = CustomerId;
            job.DateCompleated = DateCompleated;
            job.DayId = DayId;
            job.Description = Description;
            job.DueDate = DueDate;
            job.Frequence = Frequence;
            job.Id = Id;
            job.IsCompleted = IsCompleted;
            job.IsPaidFor = IsPaidFor;
            job.JobNextId = JobNextId;
            job.Name = Name;
            job.PaymentId = PaymentId;
            job.Price = Price;
            job.TNB = TNB;
            job.HaveCanceled = HaveCanceled;
            if (this.AlternativePrices != null)
            {
                job.AlternativePrices = new List<AlternativePrice>();
                job.AlternativePrices.AddRange(this.AlternativePrices);
            }
            return job;
        }

    }
}
