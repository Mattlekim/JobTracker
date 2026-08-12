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
    /// So the file itself is kept, filed under the tax year most of it falls
    /// in, and goes into that year's backup and up to the cloud along with
    /// the receipts.
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

        /// <summary>the dates the statement covers, as read off it</summary>
        public DateTime FirstTransaction { get; set; }
        public DateTime LastTransaction { get; set; }

        /// <summary>how many rows were read off it</summary>
        public int Transactions { get; set; }

        /// <summary>size of the kept file, used to spot the same file being imported again</summary>
        public long FileSize { get; set; }

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
        /// the same statement imported a second time. matched on the file's
        /// name and size, which is enough to recognise the file the bank
        /// hands out again without reading the whole thing back
        /// </summary>
        public static StatementRecord FindSameFile(string originalFileName, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(originalFileName))
                return null;

            return _Records.FirstOrDefault(x => x.FileSize == fileSize
                && string.Equals(x.OriginalFileName, originalFileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// keeps a copy of a statement that has just been imported, filed
        /// under the tax year most of its transactions fall in
        /// </summary>
        /// <param name="sourcePath">the file the user picked</param>
        /// <param name="dates">the transaction dates read off it</param>
        public static StatementRecord Keep(string sourcePath, string originalFileName, List<DateTime> dates)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return null;

            int taxYear = YearFor(dates);

            DateTime first = dates != null && dates.Count > 0 ? dates.Min() : UsfulFuctions.DateNow;
            DateTime last = dates != null && dates.Count > 0 ? dates.Max() : UsfulFuctions.DateNow;

            string extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrEmpty(extension))
                extension = Path.GetExtension(sourcePath);

            //dated and made unique, so two statements from the same bank do
            //not land on top of one another
            string stored = $"statement_{first:yyyyMMdd}_{last:yyyyMMdd}_{Guid.NewGuid().ToString("N").Substring(0, 6)}{extension}";
            string destination = Path.Combine(GetStatementFolderPath(taxYear), stored);

            try
            {
                File.Copy(sourcePath, destination, true);
            }
            catch
            {
                return null;
            }

            StatementRecord record = new StatementRecord()
            {
                TaxYear = taxYear,
                OriginalFileName = originalFileName ?? Path.GetFileName(sourcePath),
                StoredFileName = stored,
                Imported = UsfulFuctions.DateNow,
                FirstTransaction = first,
                LastTransaction = last,
                Transactions = dates == null ? 0 : dates.Count,
                FileSize = new FileInfo(destination).Length,
            };

            return Add(record);
        }

        /// <summary>
        /// which tax year a statement belongs in. a statement that straddles
        /// 5 April goes in whichever year most of it falls in
        /// </summary>
        public static int YearFor(List<DateTime> dates)
        {
            if (dates == null || dates.Count == 0)
                return TaxCalendar.TaxYearOf(UsfulFuctions.DateNow);

            Dictionary<int, int> counts = new Dictionary<int, int>();
            foreach (DateTime date in dates)
            {
                int year = TaxCalendar.TaxYearOf(date);
                counts[year] = counts.TryGetValue(year, out int count) ? count + 1 : 1;
            }

            int best = TaxCalendar.TaxYearOf(dates[0]);
            int bestCount = 0;
            foreach (KeyValuePair<int, int> pair in counts)
                if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key > best))
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }

            return best;
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

        [XmlIgnore]
        public string FormattedTaxYear
        {
            get { return $"Tax year {TaxCalendar.YearName(TaxYear)}"; }
        }
    }
}
