using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Serialization;
using Microsoft.Maui.Graphics;

namespace Kernel
{
    /// <summary>
    /// the main job class
    /// </summary>
    public partial class Job
    {
        /// <summary>
        /// the master id number
        /// </summary>
        private static int _IdGenerator = 0;

        /// <summary>
        /// the master list of jobs
        /// </summary>
        private static List<Job> _Jobs = new List<Job>();

        /// <summary>
        /// add a new job
        /// </summary>
        /// <param name="customerId">the customer the job belongs too</param>
        /// <param name="price">the price of the job</param>
        /// <param name="frequence">the frequence of the job</param>
        /// <returns></returns>
        public static ResultType Add(int customerId, float price, int frequence)
        {
            return Add(customerId, price, frequence, DateTime.Now);
        }

        public static ResultType Add(Job job)
        {
            job.GenerateId();
            _Jobs.Add(job);
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
                s = j.JobFormattedOwed;
                s = j.JobFormattedDueTime;
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

        /// <summary>
        /// query jobs
        /// </summary>
        /// <returns>returns all jobs</returns>
        public static List<Job> Query()
        {
            return _Jobs;
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
        public int JobNextId;

        /// <summary>
        /// set the frequcne of the job
        /// </summary>
        /// <param name="i"></param>
        public void SetFrequence(int i)
        {
            Frequence = i;
        }

        private int GenerateNextDueDate()
        {
            Job j = this.DeepCopy();
            j.JobNextId = -1; //reset next id
            j.IsCompleted = false;
            j.DateCompleated = new DateTime(2000, 1, 1);
            j.PaymentId = -1;
            j.IsPaidFor = false;
            
            j.DueDate = DateCompleated.AddDays(7 * Frequence);
            j.GenerateId();
            _Jobs.Add(j);

            return j.Id;
        }

        private static string tmp;
        public void Refresh()
        {
            tmp = JobFormattedDueTime;
            tmp = JobFormattedOwed;
        }
        public void MarkJobPaid()
        {
            if (IsPaidFor)
                return;

            IsPaidFor = true;

            List<Customer> c = Customer.Query("id", CustomerId.ToString());
            if (c.Count > 0)
            {
                c[0].Balance -= Price;
            }
        }

        public void UnMarkJobPaid()
        {
            if (!IsPaidFor)
                return;

            IsPaidFor = false;

            List<Customer> c = Customer.Query("id", CustomerId.ToString());
            if (c.Count > 0)
            {
                c[0].Balance += Price;
            }
        }


        public void MarkJobDone()
        {
            MarkJobDone(DateTime.Now);
        }

        public void MarkJobDone(DateTime date)
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            DateCompleated = date;
            if (Frequence > 0)
                JobNextId = GenerateNextDueDate();

            List<Customer> c = Customer.Query("id", CustomerId.ToString());
            if (c.Count > 0)
            {
                c[0].Balance += Price;
            }
        }

        public void UnMarkJobDone()
        {
            if (!IsCompleted)
                return;

            

            List<Job> jobChecks = _Jobs.FindAll(x => x.Id == JobNextId);
            if (jobChecks.Count > 0)
            {
                if (jobChecks[0].IsCompleted) //if the next instance is already done we cannot uncompleate this job
                    return;
            }

            IsCompleted = false;
            DateCompleated = new DateTime();
            _Jobs.RemoveAll(x => x.Id == JobNextId); //remove the next instance of the job

            List<Customer> c = Customer.Query("id", CustomerId.ToString());
            if (c.Count > 0)
            {
                c[0].Balance -= Price; 
            }
        }

        public void SkipJob()
        {
            DueDate = DueDate.AddDays(7 * Frequence);
        }

        public void CancelJob()
        {
            HaveCanceled = true;
            DateCanceled = DateTime.Now;
        }

        public void UnCancelJob()
        {
            HaveCanceled = false;
            DateCanceled = new DateTime();
        }
        /// <summary>
        /// how often this repeates 0 is never
        /// time is in weeks
        /// -1 will represent 1 calendar months
        /// -2 will be 2 calendar months
        /// - up to -12 for the year
        /// </summary>
        public int Frequence;

        /// <summary>
        /// this price of the job
        /// </summary>
        public float Price;

        public bool CustomerAddressDifferentToJob = false;

        public string JobFormattedStringNotes
        {
            get
            {
                return $"{Notes}";
            }
        }
        public string JobFormattedString
        {
            get
            {
                return $"{Address.PropertyNameNumber} {Address.Street} {Address.City} {Address.Area} Job: {Name} {Description}";

            }
        }

        public string JobFormattedSubString
        {
            get
            {
                return $"Frequence {Frequence} Weekly {Gloable.CurrenceSymbol}{Price}";

            }
        }

        [XmlIgnore]
        public Color DueColorCode { get; private set; } = Colors.LightGray;
        [XmlIgnore]
        public Color DueColorTextCode { get; private set; } = Colors.LightGray;
        public string JobFormattedDueTime
        {
            get
            {
                if (IsCompleted)
                {
                    DueColorCode = Colors.LightGray;
                    DueColorTextCode = Colors.Black;
                    TimeSpan ts = DateTime.Now - DateCompleated;
                    switch (ts.Days)
                    {
                        case 0:
                            return $"completed Today";

                        case 1:
                            return $"completed Yesterday";
                                                    
                    }
                    return $"Compleated {ts.Days} Ago";
                }

                DueColorTextCode = Colors.White;

                if (HaveCanceled)
                {
                    DueColorCode = Colors.Red;
                    return "Canceled";
                }

             
                if (DueDate.DayOfYear == DateTime.Now.DayOfYear && DueDate.Year == DateTime.Now.Year) //if not due
                {
                    DueColorCode = Colors.Orange;
                    return "Due Today";
                }

                if (DueDate.Ticks > DateTime.Now.Ticks) //if not due
                {
                    DueColorCode = Colors.Blue;
                    return $"Due in {(DueDate - DateTime.Now).Days + 1} Days";
                }

                DueColorCode = Colors.Red;
                return $"{(DateTime.Now - DueDate).Days} Days Late";
            }
        }

        [XmlIgnore]
        public Color OwedColorCode { get; private set; } = Colors.LightGray;
        public string JobFormattedOwed
        {
            get
            {
                Customer c = GetCustomer();
                if (c == null)
                {
                    return string.Empty;
                }

                if (c.Balance == 0)
                {
                    OwedColorCode = Colors.LightBlue;
                    return "Nothing Owed";
                }

                if (c.Balance > 0)
                {
                    OwedColorCode = Colors.Red;
                    return $"Owes {Gloable.CurrenceSymbol}{c.Balance}";
                }

                OwedColorCode = Colors.Green;
                return $"{Gloable.CurrenceSymbol}{Math.Abs(c.Balance)} In Credit";
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
        /// if to text after completion
        /// </summary>
        public bool TAC { get; set; }

        /// <summary>
        /// if the job has been canceld or not
        /// </summary>
        public bool HaveCanceled { get; set; } = false;

        public DateTime DateCanceled { get; set; }
        public static void Delete(string id)
        {
            _Jobs.RemoveAll(x => x.Id.ToString() == id);
        }
        public void SetDueDate(DateTime date)
        {
            DueDate = date;
        }


        public Customer GetCustomer()
        {
            List<Customer> c = Customer.Query("id", CustomerId.ToString());
            if (c.Count > 0)
            {
                return c[0];
            }
            return null;
        }

        public void SetDueDate(int day, int month, int year)
        {
            DueDate = new DateTime(year, month, day);
        }

        public void SetDueDateInFuture(int days)
        {
            DueDate = DateTime.Now;
            DueDate.AddDays(days);
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

            return job;
        }

    }
}
