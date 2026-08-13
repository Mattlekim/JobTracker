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

        /// <summary>
        /// the job type to fall back on when none has been picked: the first
        /// one on the list, which is what the new job form starts on anyway.
        /// a job saved with no type at all shows as a blank wherever the type
        /// is listed, and there is nothing to group or filter it by.
        ///
        /// empty only if every job type has been deleted on the settings page,
        /// which is why this is asked for rather than JobNames[0]
        /// </summary>
        public static string DefaultJobName
        {
            get { return JobNames.Count > 0 ? JobNames[0] : string.Empty; }
        }

        /// <summary>
        /// gives a job the first job type when it has none at all. work added
        /// through Quick Add never had one - that form does not ask - so it is
        /// put right as the file is read rather than leaving every one of them
        /// blank until somebody opens it.
        ///
        /// a type that is simply not on the list any anymore is left alone: it
        /// says what the work is, and a job typed Gutter Clear must not turn
        /// into Windows because that entry was edited on the settings page.
        ///
        /// only what is in memory is changed. the file is written the next
        /// time something is saved, like every other tidy up done on load.
        /// </summary>
        public static void FillInJobType(Job job)
        {
            if (job == null || !string.IsNullOrWhiteSpace(job.Name))
                return;

            job.Name = DefaultJobName;
        }

        /// <summary>
        /// the tags that have been used before, offered when something is
        /// being tagged so the same thing is not typed three different ways.
        /// edited on the settings page and saved with the settings, like
        /// <see cref="JobNames"/>.
        ///
        /// this is only the list to pick from. what a visit was actually
        /// tagged with is on the job itself, so taking a tag off this list
        /// never changes what happened on a day already worked
        /// </summary>
        public static List<string> TagNames = new List<string>()
        {
            "Front Only",
            "Extra Dirty",
            "No Access",
            "Gate Locked",
            "Dog Out",
            "Customer In",

        };

        /// <summary>
        /// The tags every job picks up as it is marked done, set on the tag
        /// bar at the top of the pages work is written up from.
        ///
        /// A day is usually all the same: everything front only because of
        /// the weather, or every house on a street with nobody in. Saying so
        /// once and having it go on as the work is marked off is the only way
        /// it will actually get recorded on a round.
        ///
        /// Kept with the settings so it survives the app being closed
        /// mid-round. That is safe because the bar shows what it is set to
        /// whenever anything is set - it can only be folded away while it is
        /// empty, so it can never quietly tag a round nobody asked it to.
        /// </summary>
        public static List<string> AutoTags = new List<string>();

        public static bool AddAutoTag(string tag)
        {
            tag = TidyTag(tag);
            if (tag.Length == 0
                || AutoTags.Exists(x => string.Equals(x, tag, StringComparison.CurrentCultureIgnoreCase)))
                return false;

            AutoTags.Add(tag);
            RememberTag(tag);
            return true;
        }

        public static bool RemoveAutoTag(string tag)
        {
            tag = TidyTag(tag);
            return AutoTags.RemoveAll(x => string.Equals(x, tag, StringComparison.CurrentCultureIgnoreCase)) > 0;
        }

        /// <summary>
        /// tags are compared and stored trimmed, because a tag typed with a
        /// space on the end is the same tag
        /// </summary>
        private static string TidyTag(string tag)
        {
            return tag == null ? string.Empty : tag.Trim();
        }

        /// <summary>
        /// puts a tag on the list to pick from next time. matched without
        /// case, so typing "front only" does not add a second entry alongside
        /// "Front Only"
        /// </summary>
        public static bool RememberTag(string tag)
        {
            tag = TidyTag(tag);
            if (tag.Length == 0 || TagNames.Exists(x => string.Equals(x, tag, StringComparison.CurrentCultureIgnoreCase)))
                return false;

            TagNames.Add(tag);
            return true;
        }

        /// <summary>
        /// The rounds work can belong to - a patch, a village, a day of the
        /// week, whatever the work is actually split into.
        ///
        /// Empty to start with, because a round is a thing somebody names
        /// themselves: there is no sensible default and a made up one would
        /// only be in the way. Edited on the settings page and saved with the
        /// settings, like <see cref="JobNames"/> and <see cref="TagNames"/>.
        /// </summary>
        public static List<string> RoundNames = new List<string>();

        /// <summary>
        /// puts a round on the list to pick from. matched without case, so a
        /// round typed in twice does not become two rounds
        /// </summary>
        public static bool RememberRound(string round)
        {
            round = TidyTag(round);
            if (round.Length == 0
                || RoundNames.Exists(x => string.Equals(x, round, StringComparison.CurrentCultureIgnoreCase)))
                return false;

            RoundNames.Add(round);
            return true;
        }

        /// <summary>
        /// every round that has work on it, whether it is on the list to pick
        /// from or not - a round taken off that list still has its work
        /// </summary>
        public static List<string> RoundsInUse()
        {
            List<string> rounds = new List<string>();

            foreach (Job j in _Jobs)
                if (j.HaveRound && !rounds.Exists(x => string.Equals(x, j.Round, StringComparison.CurrentCultureIgnoreCase)))
                    rounds.Add(j.Round);

            rounds.Sort(StringComparer.CurrentCultureIgnoreCase);
            return rounds;
        }

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
        /// the quote was taken up: it comes off the quote list and goes on
        /// the round, first due on the day given.
        ///
        /// it keeps its id, its price and how often it is to be done, because
        /// it is the same piece of work - it has just stopped being a maybe.
        /// </summary>
        public static bool AcceptQuote(Job quote, DateTime firstDue)
        {
            if (quote == null || !_Quotes.Remove(quote))
                return false;

            quote.DueDate = firstDue;
            quote.IsCompleted = false;
            quote.DateCompleated = UsfulFuctions.DateBase;
            quote.IsPaidFor = false;
            quote.PaymentId = -1;
            quote.JobNextId = -1;
            quote.PreviousJobId = -1;
            quote.HaveCanceled = false;
            quote.HaveSkipped = false;
            quote.DisableSwipe = false;

            _Jobs.Add(quote);

            Save();
            return true;
        }

        /// <summary>the work was not wanted, or was quoted twice</summary>
        public static bool DeleteQuote(int id)
        {
            if (_Quotes.RemoveAll(x => x.Id == id) == 0)
                return false;

            Save();
            return true;
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
            //a list of its own rather than the buffer Query hands out. the
            //work list asks for the round and then the quotes, and sharing
            //one buffer left the round holding the quotes instead
            return new List<Job>(_Quotes);
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

            //the quotes went with the jobs before this was here, so deleting
            //everything or restoring a backup left the old ones behind
            _Quotes.Clear();
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
        /// The round this work belongs to, or blank for work that has not
        /// been put on one.
        ///
        /// Unlike a tag it belongs to the job rather than to one visit, so
        /// <see cref="DeepCopy"/> carries it over: the next visit to a house
        /// is on the same round as the last one.
        /// </summary>
        public string Round = string.Empty;

        [XmlIgnore]
        public bool HaveRound
        {
            get { return !string.IsNullOrWhiteSpace(Round); }
        }

        /// <summary>the round as a heading, for work that is not on one</summary>
        [XmlIgnore]
        public string RoundOrNone
        {
            get { return HaveRound ? Round : "No Round"; }
        }

        /// <summary>
        /// what the lists sort on. work with no round goes last rather than
        /// first: a blank sorts before everything, which would put the work
        /// nobody has organised at the top of every page
        /// </summary>
        [XmlIgnore]
        public int SortRoundFirst
        {
            get { return HaveRound ? 0 : 1; }
        }

        [XmlIgnore]
        public string SortRound
        {
            get { return HaveRound ? Round.Trim() : string.Empty; }
        }

        /// <summary>
        /// puts this job on a round, and remembers the round for next time.
        /// blank takes it off whatever round it was on
        /// </summary>
        public void SetRound(string round)
        {
            Round = TidyTag(round);
            RememberRound(Round);
            RaisePropertyChanged("Round");
            RaisePropertyChanged("HaveRound");
            RaisePropertyChanged("RoundOrNone");
        }

        private List<string> _tags = new List<string>();

        /// <summary>
        /// What was different about this visit - front only, no access, the
        /// customer was in. It belongs to this time of doing the job and
        /// nothing else: a completed visit keeps its tags for good, and the
        /// next visit it generates starts with none, which is what makes the
        /// customer's history say which times a tag was on.
        ///
        /// <see cref="DeepCopy"/> is deliberately not copying them for that
        /// reason - see the note there before changing it.
        /// </summary>
        public List<string> Tags
        {
            //never handed out as null: everything from the lists to the save
            //file walks it, and a job read from a file written before tags
            //existed has nothing in the element for it
            get { return _tags ?? (_tags = new List<string>()); }
            set { _tags = value ?? new List<string>(); }
        }

        /// <summary>is this visit tagged with this, whatever case it was typed in</summary>
        public bool HasTag(string tag)
        {
            tag = TidyTag(tag);
            return tag.Length > 0
                && Tags.Exists(x => string.Equals(x, tag, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// tag this visit. the tag is remembered for next time as well, so a
        /// tag only ever has to be typed once
        /// </summary>
        /// <returns>true when it was not already on</returns>
        public bool AddTag(string tag)
        {
            tag = TidyTag(tag);
            if (tag.Length == 0 || HasTag(tag))
                return false;

            Tags.Add(tag);
            RememberTag(tag);
            RefreshTags();
            return true;
        }

        /// <returns>true when the tag was on and has been taken off</returns>
        public bool RemoveTag(string tag)
        {
            tag = TidyTag(tag);
            if (Tags.RemoveAll(x => string.Equals(x, tag, StringComparison.CurrentCultureIgnoreCase)) == 0)
                return false;

            RefreshTags();
            return true;
        }

        [XmlIgnore]
        public bool HaveTags
        {
            get { return Tags.Count > 0; }
        }

        /// <summary>the tags as one line, for the job rows and the history</summary>
        [XmlIgnore]
        public string TagsText
        {
            get { return string.Join(" • ", Tags); }
        }

        private void RefreshTags()
        {
            RaisePropertyChanged("Tags");
            RaisePropertyChanged("HaveTags");
            RaisePropertyChanged("TagsText");
        }
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
        /// How long a job with no estimate of its own counts as taking.
        ///
        /// It is a setting (Settings.DefaultJobDuration is this, it does not
        /// keep a second copy) but it lives here because the jobs are what
        /// needs it: how long a house takes is asked on every page that shows
        /// work, and none of them should be spelling the fallback out for
        /// themselves.
        /// </summary>
        public static int DefaultDuration = 0;

        /// <summary>
        /// how long this job counts as taking, its own estimate or the
        /// round's usual. this is the one definition of it - the calendar,
        /// the booked work page, the booking form and the job rows all ask
        /// here, so a day's figures cannot disagree with the row above them
        /// </summary>
        [XmlIgnore]
        public int Minutes
        {
            get { return EstimatedTime > 0 ? EstimatedTime : DefaultDuration; }
        }

        /// <summary>true while there is a length worth putting on the row</summary>
        [XmlIgnore]
        public bool HaveLength
        {
            get { return Minutes > 0; }
        }

        /// <summary>the job's length as a tag, on the rows and the calendar</summary>
        [XmlIgnore]
        public string LengthText
        {
            get { return SpellMinutes(Minutes); }
        }

        /// <summary>minutes as somebody would say them</summary>
        public static string SpellMinutes(int minutes)
        {
            if (minutes <= 0)
                return string.Empty;

            if (minutes < 60)
                return $"{minutes} mins";

            int hours = minutes / 60;
            int rest = minutes % 60;

            string said = hours == 1 ? "1 hr" : $"{hours} hrs";

            return rest == 0 ? said : $"{said} {rest} mins";
        }
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

        /// <summary>
        /// The work list is picking jobs out rather than working through them.
        /// It is one switch for the whole round - either every row has a tick
        /// box or none of them do.
        ///
        /// It used to be set through a property on the job, which read a
        /// static behind the scenes but only told the one job it was set on
        /// that anything had changed. Rows built after that - the list is
        /// virtualised, so that is any row scrolled into view - read the
        /// static and drew a tick box while the rest of the list had none.
        /// That is where the boxes appearing on their own came from, and why
        /// they could not be got rid of: whichever rows were never told took
        /// no notice of being switched off either.
        ///
        /// So it is set through <see cref="SetSelectionMode"/>, which is the
        /// only thing that can change it and tells the whole round at once.
        /// </summary>
        public static bool SelectionMode { get; private set; }

        /// <summary>
        /// turns the tick boxes on or off for the whole list, clearing
        /// whatever was picked on the way out
        /// </summary>
        public static void SetSelectionMode(bool on)
        {
            SelectionMode = on;

            foreach (Job j in _Jobs)
            {
                if (!on)
                    j.IsSelected = false;

                j.RaisePropertyChanged("SelectionModeEnabled");
                j.RaisePropertyChanged("GridLengthCheckBoxWidth");
            }
        }

        /// <summary>nothing picked, with the tick boxes left as they are</summary>
        public static void ClearSelection()
        {
            foreach (Job j in _Jobs)
                if (j.IsSelected)
                    j.IsSelected = false;
        }

        /// <summary>everything with its tick in</summary>
        public static List<Job> Selected()
        {
            return _Jobs.FindAll(x => x.IsSelected);
        }

        /// <summary>
        /// worked out rather than stored, so a row built at any point reads
        /// the same answer as every other row.
        ///
        /// the booking summary rows are not work and cannot be picked, so
        /// they never show a box whatever the list is doing
        /// </summary>
        [XmlIgnore]
        public bool SelectionModeEnabled
        {
            get { return SelectionMode && CustomerId != -1; }
        }

        [XmlIgnore]
        public double GridLengthCheckBoxWidth
        {
            get { return SelectionModeEnabled ? 0.3 : 0; }
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
            DateTime due = DateCompleated;

            switch (Frequence_Type)
            {
                case FrequenceType.Day:
                    due = DateCompleated.AddDays(Frequence);
                    break;

                case FrequenceType.Week:
                    due = DateCompleated.AddDays(7 * Frequence);
                    break;

                case FrequenceType.Month:
                    due = DateCompleated.AddMonths(Frequence);
                    break;

                case FrequenceType.Year:
                    due = DateCompleated.AddYears(Frequence);
                    break;
            }

            return NextVisit(due).Id;
        }

        /// <summary>
        /// this job again, due on the given day, put on the round.
        ///
        /// the finished visit is left exactly as it is - its date, its price
        /// and its payment are the record of what was done and charged, so
        /// the next visit is a fresh copy rather than the same row moved on.
        /// </summary>
        private Job NextVisit(DateTime due)
        {
            Job j = this.DeepCopy();
            j.JobNextId = -1; //reset next id
            j.IsCompleted = false;
            j.DateCompleated = new DateTime(2000, 1, 1);
            j.PaymentId = -1;
            j.IsPaidFor = false;
            j.DueDate = due;

            j.GenerateId();
            j.PreviousJobId = this.Id; //set the id
            _Jobs.Add(j);
            return j;
        }

        /// <summary>
        /// a job with nothing to repeat - a gutter clear, a conservatory, a
        /// first clean for someone who is not going on the round. it is done
        /// once and that is the end of it.
        ///
        /// nothing else has to know about it: MarkJobDone only brings a job
        /// back round when Frequence is above zero, so a one off simply never
        /// generates a next visit.
        /// </summary>
        [XmlIgnore]
        public bool IsOneOff
        {
            get { return Frequence <= 0; }
        }

        /// <summary>
        /// the one off tag in the lists. the section headings are jobs with
        /// no customer behind them, and a job that has been done is already
        /// saying so - the tag is there to warn that it will not come back
        /// </summary>
        [XmlIgnore]
        public bool ShowOneOff
        {
            get { return IsOneOff && CustomerId >= 0 && !IsCompleted && !HaveCanceled; }
        }

        /// <summary>
        /// a finished one off can be put back on for another go - the same
        /// customer wants their gutters doing again a year later. only while
        /// it has not already been: JobNextId holds the go it was given, and
        /// a job that repeats brings itself back without being asked
        /// </summary>
        [XmlIgnore]
        public bool CanDoAgain
        {
            get { return IsOneOff && IsCompleted && !HaveCanceled && JobNextId == -1; }
        }

        /// <summary>
        /// put a finished one off back on the round, due on the given day.
        /// a one off has no frequency to work the day out from, so it is
        /// asked for rather than calculated.
        /// </summary>
        /// <returns>the new visit, or null if this job cannot have one</returns>
        public Job DoAgain(DateTime due)
        {
            if (!CanDoAgain)
                return null;

            Job j = NextVisit(due);
            JobNextId = j.Id;

            Job.Save();
            Refresh();
            return j;
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
            RaisePropertyChanged("IsOneOff");
            RaisePropertyChanged("ShowOneOff");
            RaisePropertyChanged("CanDoAgain");
            RaisePropertyChanged("Round");
            RaisePropertyChanged("HaveRound");
            RaisePropertyChanged("RoundOrNone");
            RaisePropertyChanged("Minutes");
            RaisePropertyChanged("HaveLength");
            RaisePropertyChanged("LengthText");
            RefreshTags();
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

            //whatever the tag bar is set to goes on as the work is written
            //up. it is done here rather than in each of the places work can
            //be marked done - the swipes, the paper view, the job's own
            //window - so none of them can be the one that forgets
            foreach (string tag in AutoTags)
                AddTag(tag);

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

            //the tags that came with the done mark go back off with it, so
            //clearing a job swiped by mistake really does put it back as it
            //was. marking it done again brings them straight back
            foreach (string tag in AutoTags)
                RemoveTag(tag);

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
            DueDate = DueDate.AddDays(SkipDays);
            HaveSkipped = true;
            //kept so the skip stays on the day it happened, alongside the
            //work done that day, rather than moving with the new due date
            DateSkipped = dateSkipped;
            Job.Save();
        }

        /// <summary>
        /// how far a skipped job is pushed out. a one off has no frequency to
        /// go by, and multiplying by nothing left it sitting on the same day
        /// still reading as due - so it goes to next week like anything else
        /// that was passed over
        /// </summary>
        private int SkipDays
        {
            get { return Frequence > 0 ? 7 * Frequence : 7; }
        }

        public void UnSkipJob()
        {
            if (!HaveSkipped)
                return;
            DueDate = DueDate.AddDays(-SkipDays);
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
                return $"{Address.PropertyNameNumber} {Address.DisplayStreet} {Address.DisplayCity} {Address.DisplayArea}";

            }
        }

        public string JobFormattedStreet
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedStreet");
                if (Address == null)
                    return string.Empty;
                return $"{Address.PropertyNameNumber} {Address.DisplayStreet}";

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
                return $"{Address.DisplayStreet}";

            }
        }

        /// <summary>
        /// Does this job answer to what has been typed into a search box.
        ///
        /// Matched on the things you would actually go looking by: the
        /// address, what the job is called, the tags on this visit, the
        /// customer's name, and their phone number. Nothing typed matches
        /// everything, so an empty box is the whole round rather than none
        /// of it.
        /// </summary>
        public bool MatchesSearch(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            search = search.Trim().ToLowerInvariant();

            if (Address != null)
            {
                //the whole address as one line, so "12 high" finds 12 High Street
                string whole = $"{Address.PropertyNameNumber} {Address.Street} {Address.City} {Address.Area} {Address.Postcode}";
                if (Has(whole, search))
                    return true;
            }

            if (Has(Name, search))
                return true;

            foreach (string tag in Tags)
                if (Has(tag, search))
                    return true;

            if (Has(Round, search))
                return true;

            MatchCustomer();
            if (_customer != null)
            {
                if (Has($"{_customer.FName} {_customer.SName}", search))
                    return true;

                //a number gets written down with spaces one time and without
                //them the next, so only the digits are compared.
                //
                //only when what was typed looks like a phone number though -
                //no letters in it and a few digits long. "12 high" is a house
                //on a street, and matching its 12 against every phone number
                //with a 12 somewhere in it turns up half the round
                if (!search.Any(char.IsLetter))
                {
                    string digits = OnlyDigits(search);
                    if (digits.Length >= 4 && OnlyDigits(_customer.Phone).Contains(digits))
                        return true;
                }
            }

            return false;
        }

        private static bool Has(string text, string search)
        {
            return !string.IsNullOrEmpty(text) && text.ToLowerInvariant().Contains(search);
        }

        private static string OnlyDigits(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            StringBuilder digits = new StringBuilder();
            foreach (char c in text)
                if (char.IsDigit(c))
                    digits.Append(c);

            return digits.ToString();
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
                return $"{Address.DisplayCity}";

            }
        }

        public string JobFormattedArea
        {
            get
            {
              //  RaisePropertyChanged("JobFormattedArea");
                if (Address == null)
                    return string.Empty;
                return $"{Address.DisplayArea}";

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
            //without this the copy fell back to the default of weeks, so the
            //second visit of a monthly job came round the next week
            job.Frequence_Type = Frequence_Type;
            job.EstimatedTime = EstimatedTime;
            job.Id = Id;
            job.IsCompleted = IsCompleted;
            job.IsPaidFor = IsPaidFor;
            job.JobNextId = JobNextId;
            job.Name = Name;
            job.PaymentId = PaymentId;
            job.Price = Price;
            job.TNB = TNB;
            job.HaveCanceled = HaveCanceled;
            //the round is the job's, not the visit's: the next clean at a
            //house is on the same round as the last one
            job.Round = Round;
            //Tags are deliberately left off: they say what happened on one
            //visit, so the next visit starts with a clean sheet
            if (this.AlternativePrices != null)
            {
                job.AlternativePrices = new List<AlternativePrice>();
                job.AlternativePrices.AddRange(this.AlternativePrices);
            }
            return job;
        }

    }
}
