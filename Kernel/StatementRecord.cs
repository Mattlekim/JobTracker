using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Kernel
{
    /// <summary>
    /// A bank statement that has been imported, and the copy of the file
    /// that was kept.
    ///
    /// The statement is the evidence behind the figures - if the taxman asks
    /// where a number came from, the answer is the statement it was read off.
    /// So the file itself is kept, filed under the tax year it covers, and
    /// goes into that year's backup and up to the cloud along with the
    /// receipts.
    ///
    /// A statement that straddles 5 April is kept in <em>both</em> tax years,
    /// one record and one copy of the file each. Backing up or handing over a
    /// single year then still has the whole of the evidence behind it, rather
    /// than half of it sitting in a year nobody asked for.
    /// </summary>
    public partial class StatementRecord
    {
        private static int _IdGenerator = 0;

        private static List<StatementRecord> _Records = new List<StatementRecord>();

        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        public int Id { get; set; }

        /// <summary>the tax year the statement is filed under</summary>
        public int TaxYear { get; set; }

        /// <summary>what the file was called when it was picked</summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>what the kept copy is called inside the tax year folder</summary>
        public string StoredFileName { get; set; } = string.Empty;

        public DateTime Imported { get; set; }

        /// <summary>
        /// the dates this tax year's part of the statement covers. for a
        /// statement that straddles 5 April these are the dates either side
        /// of it, not the whole file
        /// </summary>
        public DateTime FirstTransaction { get; set; }
        public DateTime LastTransaction { get; set; }

        /// <summary>the dates the whole file covers, however many years it runs across</summary>
        public DateTime FileFirstTransaction { get; set; }
        public DateTime FileLastTransaction { get; set; }

        /// <summary>how many rows fall in this tax year</summary>
        public int Transactions { get; set; }

        /// <summary>how many rows are in the file altogether</summary>
        public int FileTransactions { get; set; }

        /// <summary>true when the file runs across 5 April and is kept in another year as well</summary>
        [XmlIgnore]
        public bool Crossover
        {
            get { return FileTransactions > Transactions; }
        }

        /// <summary>size of the kept file, used to spot the same file being imported again</summary>
        public long FileSize { get; set; }

        /// <summary>
        /// the bank account the statement was imported against. -1 on a
        /// record from before accounts existed, and on a PayPal export,
        /// which has no account
        /// </summary>
        public int BankAccountId { get; set; } = -1;

        /// <summary>the account's name, for the kept statements list. empty when there is no account to name</summary>
        [XmlIgnore]
        public string AccountName
        {
            get
            {
                BankAccount account = BankAccount.Get(BankAccountId);
                return account == null ? string.Empty : account.Name;
            }
        }

        public const string StatementFolder = "statements";

        public const string FilePrefix = "statements";

        /// <summary>the top of the kept statement store, created on demand</summary>
        public static string GetStatementFolderPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StatementFolder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>where one tax year's statements are kept</summary>
        public static string GetStatementFolderPath(int taxYear)
        {
            string dir = Path.Combine(GetStatementFolderPath(), TaxCalendar.YearFolderName(taxYear));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        [XmlIgnore]
        public string StoredPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StoredFileName))
                    return string.Empty;
                return Path.Combine(GetStatementFolderPath(TaxYear), StoredFileName);
            }
        }

        [XmlIgnore]
        public bool FileKept
        {
            get
            {
                return !string.IsNullOrWhiteSpace(StoredFileName) && File.Exists(StoredPath);
            }
        }

        public static StatementRecord Add(StatementRecord record)
        {
            record.GenerateId();
            _Records.Add(record);
            return record;
        }

        public static StatementRecord Get(int id)
        {
            return _Records.FirstOrDefault(x => x.Id == id);
        }

        public static void Remove(int id)
        {
            StatementRecord record = Get(id);
            if (record != null)
                record.DeleteStoredFile();
            _Records.RemoveAll(x => x.Id == id);
        }

        public void DeleteStoredFile()
        {
            try
            {
                if (FileKept)
                    File.Delete(StoredPath);
            }
            catch
            {
            }
            StoredFileName = string.Empty;
        }

        public static void DeleteData()
        {
            _Records.Clear();
        }

        public static List<StatementRecord> Query()
        {
            List<StatementRecord> tmp = new List<StatementRecord>();
            tmp.AddRange(_Records);
            return tmp;
        }

        public static List<StatementRecord> QueryByYear(int taxYear)
        {
            return _Records.FindAll(x => x.TaxYear == taxYear);
        }

        /// <summary>
        /// the same statement already kept in a tax year. matched on the
        /// file's name and size, which is enough to recognise the file the
        /// bank hands out again without reading the whole thing back.
        /// a record from before accounts existed carries no account and
        /// counts as the same file whichever account is importing now -
        /// it is the same file, kept before there was anything to say so
        /// </summary>
        public static StatementRecord FindSameFile(string originalFileName, long fileSize, int taxYear, int bankAccountId = -1)
        {
            if (string.IsNullOrWhiteSpace(originalFileName))
                return null;

            return _Records.FirstOrDefault(x => x.FileSize == fileSize
                && x.TaxYear == taxYear
                && (x.BankAccountId == bankAccountId || x.BankAccountId == -1)
                && string.Equals(x.OriginalFileName, originalFileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// keeps a copy of a statement that has just been imported, in every
        /// tax year it covers. a statement running across 5 April is kept in
        /// both, so either year can be backed up or handed over on its own
        /// and still have all of its evidence with it.
        /// years it is already kept in are left as they are
        /// </summary>
        /// <param name="sourcePath">the file the user picked</param>
        /// <param name="dates">the transaction dates read off it</param>
        /// <param name="bankAccountId">the account it was imported against, -1 when there is none</param>
        /// <returns>the records added, one per tax year newly filed</returns>
        public static List<StatementRecord> Keep(string sourcePath, string originalFileName, List<DateTime> dates, int bankAccountId = -1)
        {
            List<StatementRecord> added = new List<StatementRecord>();

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return added;

            DateTime fileFirst = dates != null && dates.Count > 0 ? dates.Min() : UsfulFuctions.DateNow;
            DateTime fileLast = dates != null && dates.Count > 0 ? dates.Max() : UsfulFuctions.DateNow;

            string extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrEmpty(extension))
                extension = Path.GetExtension(sourcePath);

            //dated and made unique, so two statements from the same bank do
            //not land on top of one another. both years keep the same name,
            //because it is the same file
            string stored = $"statement_{fileFirst:yyyyMMdd}_{fileLast:yyyyMMdd}_{Guid.NewGuid().ToString("N").Substring(0, 6)}{extension}";

            long sourceSize;
            try
            {
                sourceSize = new FileInfo(sourcePath).Length;
            }
            catch
            {
                return added;
            }

            string name = originalFileName ?? Path.GetFileName(sourcePath);

            foreach (int taxYear in YearsFor(dates))
            {
                StatementRecord same = FindSameFile(name, sourceSize, taxYear, bankAccountId);
                if (same != null)
                {
                    //a record kept before accounts existed learns whose it is
                    //the first time the file comes past again. memory only -
                    //the file catches up on the next save
                    if (same.BankAccountId == -1)
                        same.BankAccountId = bankAccountId;
                    continue;
                }

                List<DateTime> inYear = DatesIn(dates, taxYear);

                string destination = Path.Combine(GetStatementFolderPath(taxYear), stored);
                try
                {
                    File.Copy(sourcePath, destination, true);
                }
                catch
                {
                    continue;
                }

                added.Add(Add(new StatementRecord()
                {
                    TaxYear = taxYear,
                    OriginalFileName = name,
                    StoredFileName = stored,
                    Imported = UsfulFuctions.DateNow,
                    FirstTransaction = inYear.Count > 0 ? inYear.Min() : fileFirst,
                    LastTransaction = inYear.Count > 0 ? inYear.Max() : fileLast,
                    FileFirstTransaction = fileFirst,
                    FileLastTransaction = fileLast,
                    Transactions = inYear.Count,
                    FileTransactions = dates == null ? 0 : dates.Count,
                    FileSize = sourceSize,
                    BankAccountId = bankAccountId,
                }));
            }

            return added;
        }

        /// <summary>
        /// every tax year a statement covers, oldest first. one for a normal
        /// statement, two for one that runs across 5 April
        /// </summary>
        public static List<int> YearsFor(List<DateTime> dates)
        {
            List<int> years = new List<int>();

            if (dates == null || dates.Count == 0)
            {
                years.Add(TaxCalendar.TaxYearOf(UsfulFuctions.DateNow));
                return years;
            }

            foreach (DateTime date in dates)
            {
                int year = TaxCalendar.TaxYearOf(date);
                if (!years.Contains(year))
                    years.Add(year);
            }

            years.Sort();
            return years;
        }

        private static List<DateTime> DatesIn(List<DateTime> dates, int taxYear)
        {
            List<DateTime> inYear = new List<DateTime>();
            if (dates == null)
                return inYear;

            foreach (DateTime date in dates)
                if (TaxCalendar.TaxYearOf(date) == taxYear)
                    inYear.Add(date);

            return inYear;
        }

        public string FormattedPeriod
        {
            get
            {
                if (FirstTransaction == DateTime.MinValue)
                    return "Dates unknown";
                return $"{FirstTransaction.ToShortDateString()} to {LastTransaction.ToShortDateString()}";
            }
        }

        public string FormattedImported
        {
            get
            {
                return $"Imported {Imported.ToShortDateString()}, {Transactions} transactions";
            }
        }

        /// <summary>
        /// says so when this is one half of a statement that runs across
        /// 5 April, and what the whole file covers
        /// </summary>
        [XmlIgnore]
        public string FormattedCrossover
        {
            get
            {
                if (!Crossover)
                    return string.Empty;

                return $"Runs across 5 April - the whole statement covers {FileFirstTransaction.ToShortDateString()} " +
                    $"to {FileLastTransaction.ToShortDateString()} ({FileTransactions} transactions) and is kept in both tax years.";
            }
        }

        [XmlIgnore]
        public bool ShowCrossover
        {
            get { return Crossover; }
        }

        [XmlIgnore]
        public string FormattedTaxYear
        {
            get { return $"Tax year {TaxCalendar.YearName(TaxYear)}"; }
        }
    }
}
