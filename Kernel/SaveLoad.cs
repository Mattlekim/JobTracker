using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Serialization;

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Kernel
{
    public struct CustomerSaveData
    {
        public List<Customer> Customers;
        public int NextCustomerId;
    }


    public partial class Customer
    {

        private static string _FilePath = "customers.rjt";


        public static void Save(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            CustomerSaveData csd = new CustomerSaveData()
            {

            };
            csd.Customers = new List<Customer>();
            csd.Customers.AddRange(_Customers);
            csd.NextCustomerId = _IdGenerator;
            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(CustomerSaveData));
                xs.Serialize(fs, csd);

            }
            SyncNotifier.NotifySaved();
        }
        public static void Load(string dir = null)
        {
            try
            {
                string fileLocation = string.Empty;
                if (dir != null && dir != string.Empty)
                {
                    fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                    fileLocation = Path.Combine(fileLocation, _FilePath);
                }
                else
                    fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);


                CustomerSaveData csd = new CustomerSaveData()
                {

                };

                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(CustomerSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    csd = (CustomerSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.


                    _Customers.Clear();
                    _Customers.AddRange(csd.Customers);
                    _IdGenerator = csd.NextCustomerId;

                    //these are different objects to the ones the index was
                    //built from, and a restore can easily bring back the same
                    //number of customers, so the count check cannot see it
                    InvalidateIndex();
                }
            }
            catch
            {
            }
        }

    }

    public struct JobSaveData
    {
        public List<Job> Jobs;
        public int NextJobId;
    }

    public partial class Job: INotifyPropertyChanged
    {
        private static string _FilePath = "jobs.rjt";

        private static string _FilePathQuotes = "quotes.rjt";

        /// <summary>
        /// true when the jobs file was there and could not be read. nothing
        /// is written while this is set: an empty list saved over a file that
        /// would not parse is the round gone for good
        /// </summary>
        public static bool LoadFailed = false;

        /// <summary>why the load failed, for telling somebody</summary>
        public static string LoadError = string.Empty;

        public static void Save(string dir = null)
        {
            //the file was there and unreadable, so what is in memory is not
            //the round - it is what was left after failing to read it
            if (LoadFailed)
                return;

            JobSaveData csd = new JobSaveData()
            {

            };

            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            csd.Jobs = new List<Job>();
            csd.Jobs.AddRange(_Jobs);
            csd.NextJobId = _IdGenerator;

            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
                xs.Serialize(fs, csd);

            }


            //written every time, empty or not. this used to be skipped when
            //there were no quotes left, which left the last file on disk -
            //so a quote taken up or thrown out came back the next time the
            //app was started
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePathQuotes);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePathQuotes);

            csd.Jobs = new List<Job>();
            csd.Jobs.AddRange(_Quotes);

            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
                xs.Serialize(fs, csd);
            }
            SyncNotifier.NotifySaved();
        }

        public static bool _Loaded = false;

        /// <summary>
        /// the file on disk is not the one that was read - read it again.
        ///
        /// A failed read is forgotten here as well. LoadFailed stops anything
        /// being written, which is right while the unreadable file is still
        /// the one on disk - but a restore has just put a different file
        /// there, and left set it would have quietly blocked every save that
        /// followed the restore.
        /// </summary>
        public static void Reset()
        {
            _Loaded = false;
            LoadFailed = false;
            LoadError = string.Empty;
        }

        private static void FixBaseIdBug()
        {
            foreach (Job j in _Jobs)
                if (j.BaseJobId == 0)
                {
                    if (j.JobNextId == -1)
                    {
                        List<Job> linkedJobs = new List<Job>();
                        linkedJobs.Add(j);
                        int jobId = j.Id;

                        while (true)
                        {
                            Job job = _Jobs.FirstOrDefault(x => x.JobNextId == jobId);
                            if (job == null) //no matching job ie the first job in the list
                            {
                                foreach (Job jj in linkedJobs)
                                {
                                    jj.BaseJobId = jobId;
                                }
                                break;
                            }

                            linkedJobs.Add(job);
                            jobId = job.Id;
                        }

                    }
                }

        }
        /// <summary>
        /// A round belongs to the job rather than to one visit of it, so
        /// every visit of a job carries the same one.
        ///
        /// Rounds were set on the visit in front of whoever set them for a
        /// while, which left a house on a round up to the clean it was set on
        /// and on no round from the next one - and the next one is the one
        /// every list and every figure is worked out from. This fills the
        /// round down the whole job from the last visit that has one, so a
        /// round already put together does not have to be put together again.
        ///
        /// Only what is in memory is changed. The file catches up on the next
        /// save, like the other tidy ups done on load.
        /// </summary>
        private static void FillRoundsDownTheJob()
        {
            //the round off the newest visit of each job that has one: ids go
            //up with every visit, so the highest is the last thing said about
            //where that house is
            Dictionary<int, KeyValuePair<int, string>> latest = new Dictionary<int, KeyValuePair<int, string>>();

            foreach (Job j in _Jobs)
            {
                if (j.BaseJobId <= 0 || string.IsNullOrWhiteSpace(j.Round))
                    continue;

                KeyValuePair<int, string> found;
                if (latest.TryGetValue(j.BaseJobId, out found) && found.Key >= j.Id)
                    continue;

                latest[j.BaseJobId] = new KeyValuePair<int, string>(j.Id, j.Round);
            }

            foreach (Job j in _Jobs)
            {
                KeyValuePair<int, string> round;
                if (j.BaseJobId <= 0 || !latest.TryGetValue(j.BaseJobId, out round))
                    continue;

                if (!string.Equals(j.Round ?? string.Empty, round.Value, StringComparison.CurrentCulture))
                    j.Round = round.Value;
            }
        }

        public static void Load(string dir = null)
        {
            if (_Loaded)
                return;

            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            JobSaveData csd = new JobSaveData()
            {

            };

            try
            {
                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    csd = (JobSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.

                    foreach (Job j in csd.Jobs)
                    {
                        j.DateCompleated = new DateTime(j.DateCompleated.Year, j.DateCompleated.Month, j.DateCompleated.Day);
                        j.DueDate = new DateTime(j.DueDate.Year, j.DueDate.Month, j.DueDate.Day);
                        if (j.Address.Street == null)
                            j.Address.Street = String.Empty;
                        else
                            j.Address.Street = j.Address.Street.Trim();

                        if (j.Address.City == null)
                            j.Address.City = String.Empty;
                        else
                            j.Address.City = j.Address.City.Trim();

                        if (j.Address.Area == null)
                            j.Address.Area = String.Empty;
                        else
                            j.Address.Area = j.Address.Area.Trim();

                        //work saved without a type shows as a blank wherever
                        //the type is listed, and there is nothing to group or
                        //filter it by
                        FillInJobType(j);

                        //work cancelled while it was booked in used to stay
                        //booked: nothing unbooked it, so the work list kept a
                        //booking row counting work every list filters out - a
                        //day with nothing behind it. CancelJob unbooks now;
                        //this puts right what older files still carry. only
                        //what is in memory is changed - the file catches up
                        //on the next save, like the other tidy ups here
                        if (j.HaveCanceled && !j.IsCompleted && j.IsBookedIn)
                            j.UnBookInJob();
                    }

                    _Jobs.Clear();
                    _Jobs.AddRange(csd.Jobs);
                    _IdGenerator = csd.NextJobId;
                }
                FixBaseIdBug();
                FillRoundsDownTheJob();

                //a rise agreed before the app was last closed, on a day that
                //has since come round. it is worked off the due date rather
                //than off today, so a skip can push a visit over the day
                //while nothing is looking
                Job.ApplyPriceRises();

                _Loaded = true;
            }
            catch (FileNotFoundException)
            {
                //no jobs file yet: a new install, and an empty round is right
                _Loaded = true;
            }
            catch (DirectoryNotFoundException)
            {
                _Loaded = true;
            }
            catch (Exception ex)
            {
                //  The file is there and could not be read.
                //
                //  This used to be swallowed whole, which is the worst thing
                //  it could do: the jobs list is left empty, the app opens
                //  looking like every job has gone, and the first save writes
                //  that emptiness over the file that still had them all in
                //  it. A round could be lost to a single bad character.
                //
                //  So the failure is remembered, and nothing is written until
                //  somebody has seen it.
                LoadFailed = true;
                LoadError = ex.Message;
                _Jobs.Clear();
            }


            
            try
            {
                if (dir != null && dir != string.Empty)
                {
                    fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                    fileLocation = Path.Combine(fileLocation, _FilePathQuotes);
                }
                else
                    fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePathQuotes);


                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    csd = (JobSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.

                    foreach (Job j in csd.Jobs)
                    {
                        j.DateCompleated = new DateTime(j.DateCompleated.Year, j.DateCompleated.Month, j.DateCompleated.Day);
                        j.DueDate = new DateTime(j.DueDate.Year, j.DueDate.Month, j.DueDate.Day);
                        if (j.Address.Street == null)
                            j.Address.Street = String.Empty;
                        else
                            j.Address.Street = j.Address.Street.Trim();

                        if (j.Address.City == null)
                            j.Address.City = String.Empty;
                        else
                            j.Address.City = j.Address.City.Trim();

                        if (j.Address.Area == null)
                            j.Address.Area = String.Empty;
                        else
                            j.Address.Area = j.Address.Area.Trim();

                        FillInJobType(j);
                    }

                    _Quotes.Clear();
                    _Quotes.AddRange(csd.Jobs);
                    
                }

            }
            catch
                {

            }

        }
    }

    public struct ExpenseSaveData
    {
        public List<Expense> Expenses;
        public int NextExpenseId;
    }

    public partial class Expense
    {
        /// <summary>the one file everything used to go in, before the years were split up</summary>
        private const string _LegacyFilePath = "expenses.rjt";

        /// <summary>expenses-2026.rjt holds the 2026/27 tax year</summary>
        public const string FilePrefix = "expenses";

        public static void Save(string dir = null)
        {
            Save(dir, null);
        }

        /// <summary>
        /// writes one file per tax year. only the years that have actually
        /// changed get written, so a finished year keeps its timestamp and
        /// the cloud has nothing to send for it.
        /// <paramref name="onlyYears"/> limits it to the years asked for,
        /// which is how a backup of one tax year is made
        /// </summary>
        public static void Save(string dir, HashSet<int> onlyYears)
        {
            Dictionary<int, List<Expense>> byYear = new Dictionary<int, List<Expense>>();
            foreach (Expense e in _Expenses)
            {
                int year = TaxCalendar.TaxYearOf(e.Date);
                if (onlyYears != null && !onlyYears.Contains(year))
                    continue;

                if (!byYear.TryGetValue(year, out List<Expense> list))
                {
                    list = new List<Expense>();
                    byYear[year] = list;
                }
                list.Add(e);
            }

            //the id counter only goes in the tax year we are in, so adding an
            //expense today cannot change a finished year's file
            int currentYear = TaxCalendar.TaxYearOf(UsfulFuctions.DateNow);

            foreach (KeyValuePair<int, List<Expense>> year in byYear)
            {
                ExpenseSaveData esd = new ExpenseSaveData();
                esd.Expenses = year.Value;
                esd.NextExpenseId = year.Key == currentYear ? _IdGenerator : 0;

                YearlyStore.WriteIfChanged(YearlyStore.PathFor(FilePrefix, year.Key, dir),
                    YearlyStore.Serialise(esd));
            }

            //a year whose last expense has been deleted should not be left
            //behind looking like it still holds something
            if (onlyYears == null)
                foreach (int year in YearlyStore.YearsOnDisk(FilePrefix, dir))
                    if (!byYear.ContainsKey(year))
                        YearlyStore.DeleteYear(FilePrefix, year, dir);

            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            _Expenses.Clear();
            _IdGenerator = 0;

            HashSet<int> ids = new HashSet<int>();
            int nextId = 0;

            foreach (int year in YearlyStore.YearsOnDisk(FilePrefix, dir))
            {
                try
                {
                    ExpenseSaveData esd = YearlyStore.Deserialise<ExpenseSaveData>(YearlyStore.PathFor(FilePrefix, year, dir));
                    if (esd.Expenses != null)
                        foreach (Expense e in esd.Expenses)
                            if (ids.Add(e.Id))
                                _Expenses.Add(e);

                    if (esd.NextExpenseId > nextId)
                        nextId = esd.NextExpenseId;
                }
                catch
                {
                    //one unreadable year must not lose the others
                }
            }

            bool migrated = LoadLegacyFile(dir, ids, ref nextId);

            foreach (Expense e in _Expenses)
                if (e.Id >= nextId)
                    nextId = e.Id + 1;

            _IdGenerator = nextId;

            if (migrated)
            {
                Save(dir);
                YearlyStore.RetireLegacyFile(_LegacyFilePath, dir);
            }
        }

        /// <summary>
        /// picks up the single file expenses used to be kept in, so it can be
        /// split into years. the caller takes it away once the year files are
        /// written, so anything deleted since cannot come back the next time
        /// the app starts
        /// </summary>
        private static bool LoadLegacyFile(string dir, HashSet<int> ids, ref int nextId)
        {
            string path = YearlyStore.LegacyPath(_LegacyFilePath, dir);
            if (!File.Exists(path))
                return false;

            try
            {
                ExpenseSaveData esd = YearlyStore.Deserialise<ExpenseSaveData>(path);
                if (esd.Expenses != null)
                    foreach (Expense e in esd.Expenses)
                        if (ids.Add(e.Id))
                            _Expenses.Add(e);

                if (esd.NextExpenseId > nextId)
                    nextId = esd.NextExpenseId;

                return true;
            }
            catch
            {
                //leave a file that will not read alone rather than lose it
                return false;
            }
        }
    }

    public struct ExpenseRuleSaveData
    {
        public List<ExpenseRule> Rules;
        public int NextRuleId;
    }

    public partial class ExpenseRule
    {
        private static string _FilePath = "expenserules.rjt";

        public static void Save(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            ExpenseRuleSaveData ersd = new ExpenseRuleSaveData();
            ersd.Rules = new List<ExpenseRule>();
            ersd.Rules.AddRange(_Rules);
            ersd.NextRuleId = _IdGenerator;

            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(ExpenseRuleSaveData));
                xs.Serialize(fs, ersd);
            }
            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            ExpenseRuleSaveData ersd = new ExpenseRuleSaveData();
            try
            {
                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(ExpenseRuleSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    ersd = (ExpenseRuleSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.

                    _Rules.Clear();
                    if (ersd.Rules != null)
                        _Rules.AddRange(ersd.Rules);
                    _IdGenerator = ersd.NextRuleId;
                }
            }
            catch
            {
            }
        }
    }

    public struct BankAccountSaveData
    {
        public List<BankAccount> Accounts;
        public int NextAccountId;
    }

    public partial class BankAccount
    {
        private static string _FilePath = "bankaccounts.rjt";

        public static void Save(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            BankAccountSaveData basd = new BankAccountSaveData();
            basd.Accounts = new List<BankAccount>();
            basd.Accounts.AddRange(_Accounts);
            basd.NextAccountId = _IdGenerator;

            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(BankAccountSaveData));
                xs.Serialize(fs, basd);
            }
            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            BankAccountSaveData basd = new BankAccountSaveData();
            try
            {
                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(BankAccountSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    basd = (BankAccountSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.

                    _Accounts.Clear();
                    if (basd.Accounts != null)
                        _Accounts.AddRange(basd.Accounts);
                    _IdGenerator = basd.NextAccountId;
                }
            }
            catch
            {
            }

            //an id handed out twice would muddle two accounts' statements
            //together, so a file whose counter is behind its accounts is
            //put right rather than trusted
            foreach (BankAccount account in _Accounts)
                if (account.Id >= _IdGenerator)
                    _IdGenerator = account.Id + 1;

            //the layout out of an old settings file becomes the first
            //account, so columns already taught are not asked for again
            EnsureLegacyAccount(dir);
        }
    }

    public struct StatementRecordSaveData
    {
        public List<StatementRecord> Records;
        public int NextRecordId;
    }

    public partial class StatementRecord
    {
        public static void Save(string dir = null)
        {
            Save(dir, null);
        }

        /// <summary>
        /// one file per tax year, alongside that year's expenses and income,
        /// so the statements the figures came off travel with them
        /// </summary>
        public static void Save(string dir, HashSet<int> onlyYears)
        {
            Dictionary<int, List<StatementRecord>> byYear = new Dictionary<int, List<StatementRecord>>();
            foreach (StatementRecord record in _Records)
            {
                if (onlyYears != null && !onlyYears.Contains(record.TaxYear))
                    continue;

                if (!byYear.TryGetValue(record.TaxYear, out List<StatementRecord> list))
                {
                    list = new List<StatementRecord>();
                    byYear[record.TaxYear] = list;
                }
                list.Add(record);
            }

            int currentYear = TaxCalendar.TaxYearOf(UsfulFuctions.DateNow);

            foreach (KeyValuePair<int, List<StatementRecord>> year in byYear)
            {
                StatementRecordSaveData srsd = new StatementRecordSaveData();
                srsd.Records = year.Value;
                srsd.NextRecordId = year.Key == currentYear ? _IdGenerator : 0;

                YearlyStore.WriteIfChanged(YearlyStore.PathFor(FilePrefix, year.Key, dir),
                    YearlyStore.Serialise(srsd));
            }

            if (onlyYears == null)
                foreach (int year in YearlyStore.YearsOnDisk(FilePrefix, dir))
                    if (!byYear.ContainsKey(year))
                        YearlyStore.DeleteYear(FilePrefix, year, dir);

            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            _Records.Clear();
            _IdGenerator = 0;

            HashSet<int> ids = new HashSet<int>();
            int nextId = 0;

            foreach (int year in YearlyStore.YearsOnDisk(FilePrefix, dir))
            {
                try
                {
                    StatementRecordSaveData srsd = YearlyStore.Deserialise<StatementRecordSaveData>(YearlyStore.PathFor(FilePrefix, year, dir));
                    if (srsd.Records != null)
                        foreach (StatementRecord record in srsd.Records)
                            if (ids.Add(record.Id))
                                _Records.Add(record);

                    if (srsd.NextRecordId > nextId)
                        nextId = srsd.NextRecordId;
                }
                catch
                {
                }
            }

            foreach (StatementRecord record in _Records)
                if (record.Id >= nextId)
                    nextId = record.Id + 1;

            _IdGenerator = nextId;
        }
    }

    public struct GoCardlessRequestSaveData
    {
        public List<GoCardlessRequest> Requests;
        public int NextRequestId;
    }

    public partial class GoCardlessRequest
    {
        private static string _FilePath = "directdebits.rjt";

        public static void Save(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            GoCardlessRequestSaveData gsd = new GoCardlessRequestSaveData();
            gsd.Requests = new List<GoCardlessRequest>();
            gsd.Requests.AddRange(_Requests);
            gsd.NextRequestId = _IdGenerator;

            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(GoCardlessRequestSaveData));
                xs.Serialize(fs, gsd);
            }
            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            GoCardlessRequestSaveData gsd = new GoCardlessRequestSaveData();
            try
            {
                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(GoCardlessRequestSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    gsd = (GoCardlessRequestSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.

                    _Requests.Clear();
                    _Requests.AddRange(gsd.Requests);
                    _IdGenerator = gsd.NextRequestId;
                }
            }
            catch
            {
            }
        }
    }

    public struct PaymentSaveData
    {
        public List<Payment> Payments;
        public int NextPaymentId;
        public List<string> paymentsToIgnore;
    }

    /// <summary>
    /// the references told to stay out of the payment import. nothing to do
    /// with any one tax year, so it keeps its own small file rather than
    /// being copied into every year
    /// </summary>
    public struct PaymentIgnoreSaveData
    {
        public List<string> paymentsToIgnore;
    }

    public partial class Payment
    {
        /// <summary>the one file everything used to go in, before the years were split up</summary>
        private const string _LegacyFilePath = "payment.rjt";

        /// <summary>payments-2026.rjt holds the money taken in the 2026/27 tax year</summary>
        public const string FilePrefix = "payments";

        public const string IgnoreFilePath = "paymentignore.rjt";

        public static void Save(string dir = null)
        {
            Save(dir, null);
        }

        /// <summary>
        /// writes one file per tax year, and only the years whose payments
        /// have actually changed. <paramref name="onlyYears"/> limits it to
        /// the years asked for, which is how one tax year is backed up on
        /// its own
        /// </summary>
        public static void Save(string dir, HashSet<int> onlyYears)
        {
            Dictionary<int, List<Payment>> byYear = new Dictionary<int, List<Payment>>();
            foreach (Payment p in _Payments)
            {
                int year = TaxCalendar.TaxYearOf(p.Date);
                if (onlyYears != null && !onlyYears.Contains(year))
                    continue;

                if (!byYear.TryGetValue(year, out List<Payment> list))
                {
                    list = new List<Payment>();
                    byYear[year] = list;
                }
                list.Add(p);
            }

            //the id counter only goes in the tax year we are in, so taking a
            //payment today cannot change a finished year's file
            int currentYear = TaxCalendar.TaxYearOf(UsfulFuctions.DateNow);

            foreach (KeyValuePair<int, List<Payment>> year in byYear)
            {
                PaymentSaveData psd = new PaymentSaveData();
                psd.Payments = year.Value;
                psd.NextPaymentId = year.Key == currentYear ? _IdGenerator : 0;
                psd.paymentsToIgnore = new List<string>();

                YearlyStore.WriteIfChanged(YearlyStore.PathFor(FilePrefix, year.Key, dir),
                    YearlyStore.Serialise(psd));
            }

            if (onlyYears == null)
                foreach (int year in YearlyStore.YearsOnDisk(FilePrefix, dir))
                    if (!byYear.ContainsKey(year))
                        YearlyStore.DeleteYear(FilePrefix, year, dir);

            SaveIgnoreList(dir);

            SyncNotifier.NotifySaved();
        }

        private static void SaveIgnoreList(string dir)
        {
            PaymentIgnoreSaveData pisd = new PaymentIgnoreSaveData();
            pisd.paymentsToIgnore = new List<string>();
            if (IgnorePaymentList != null)
                pisd.paymentsToIgnore.AddRange(IgnorePaymentList);

            YearlyStore.WriteIfChanged(Path.Combine(YearlyStore.Folder(dir), IgnoreFilePath),
                YearlyStore.Serialise(pisd));
        }

        public static void Load(string dir = null)
        {
            _Payments.Clear();
            _IdGenerator = 0;

            HashSet<int> ids = new HashSet<int>();
            int nextId = 0;

            foreach (int year in YearlyStore.YearsOnDisk(FilePrefix, dir))
            {
                try
                {
                    PaymentSaveData psd = YearlyStore.Deserialise<PaymentSaveData>(YearlyStore.PathFor(FilePrefix, year, dir));
                    if (psd.Payments != null)
                        foreach (Payment p in psd.Payments)
                            if (ids.Add(p.Id))
                                _Payments.Add(p);

                    if (psd.NextPaymentId > nextId)
                        nextId = psd.NextPaymentId;
                }
                catch
                {
                    //one unreadable year must not lose the others
                }
            }

            LoadIgnoreList(dir);

            bool migrated = LoadLegacyFile(dir, ids, ref nextId);

            foreach (Payment p in _Payments)
                if (p.Id >= nextId)
                    nextId = p.Id + 1;

            _IdGenerator = nextId;

            if (migrated)
            {
                Save(dir);
                YearlyStore.RetireLegacyFile(_LegacyFilePath, dir);
            }
        }

        private static void LoadIgnoreList(string dir)
        {
            IgnorePaymentList = new List<string>();
            try
            {
                string path = Path.Combine(YearlyStore.Folder(dir), IgnoreFilePath);
                if (!File.Exists(path))
                    return;

                PaymentIgnoreSaveData pisd = YearlyStore.Deserialise<PaymentIgnoreSaveData>(path);
                if (pisd.paymentsToIgnore != null)
                    IgnorePaymentList.AddRange(pisd.paymentsToIgnore);
            }
            catch
            {
            }
        }

        /// <summary>
        /// picks up the single file payments used to be kept in, so it can be
        /// split into years. the ignore list rode along in the same file, so
        /// it comes out of here too
        /// </summary>
        private static bool LoadLegacyFile(string dir, HashSet<int> ids, ref int nextId)
        {
            string path = YearlyStore.LegacyPath(_LegacyFilePath, dir);
            if (!File.Exists(path))
                return false;

            try
            {
                PaymentSaveData psd = YearlyStore.Deserialise<PaymentSaveData>(path);
                if (psd.Payments != null)
                    foreach (Payment p in psd.Payments)
                        if (ids.Add(p.Id))
                            _Payments.Add(p);

                if (psd.NextPaymentId > nextId)
                    nextId = psd.NextPaymentId;

                if (psd.paymentsToIgnore != null)
                    foreach (string reference in psd.paymentsToIgnore)
                        if (!IgnorePaymentList.Contains(reference))
                            IgnorePaymentList.Add(reference);

                return true;
            }
            catch
            {
                //leave a file that will not read alone rather than lose it
                return false;
            }
        }
    }

}
