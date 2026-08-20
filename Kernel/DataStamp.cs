using System;
using System.Collections.Generic;
using System.IO;

namespace Kernel
{
    /// <summary>one part of the data, and when it was last written</summary>
    public struct DataStampEntry
    {
        public string Area;
        public DateTime When;
    }

    public struct DataStampSaveData
    {
        public List<DataStampEntry> Areas;
    }

    /// <summary>
    /// When the data was last changed, written down with the data itself.
    ///
    /// A file's own timestamp cannot answer this. The moment the round is
    /// copied anywhere - into a backup, out of a zip, down from the cloud, off
    /// one phone on to another - every file is stamped with the day the copy
    /// was taken, and the day the copy was taken says nothing about how old
    /// the work in it is. A backup made this morning out of a round nobody has
    /// touched since March is a March round, and putting it back over a round
    /// worked all summer is the one mistake that cannot be undone.
    ///
    /// So the date is kept *in* the data (<c>datastamp.rjt</c>) and travels
    /// with it. It is written a part at a time - the jobs, the customers, the
    /// payments - because "the jobs were last changed in March" is a more
    /// useful thing to be told than one date for the lot, and the newest of
    /// them is <see cref="LastModified"/>.
    ///
    /// **A save into another folder does not move the date.** That is a copy
    /// of the round being made rather than the round changing, so the copy is
    /// given the date the round already had - which is the whole point, and
    /// the thing to check first if a backup ever starts claiming it is newer
    /// than it is.
    /// </summary>
    public static class DataStamp
    {
        /// <summary>what the dates are kept in. it sits with the data and is backed up with it</summary>
        public const string FilePath = "datastamp.rjt";

        //  the parts of the data. these strings are written into the file, so
        //  renaming one loses the date already recorded against it
        public const string Jobs = "jobs";
        public const string Customers = "customers";
        public const string Payments = "payments";
        public const string Expenses = "expenses";
        public const string ExpenseRules = "expense rules";
        public const string BankAccounts = "bank accounts";
        public const string Statements = "statements";
        public const string DirectDebits = "direct debits";
        public const string BalanceAdjustments = "balance adjustments";
        public const string DayNotes = "day notes";
        public const string Settings = "settings";

        /// <summary>
        /// the folder the round itself lives in - null, the data folder, for
        /// the app. It is here so the self test can work in a folder of its
        /// own without stamping the real data folder of whatever machine it is
        /// run on.
        /// </summary>
        public static string HomeFolder = null;

        private static readonly Dictionary<string, DateTime> _when = new Dictionary<string, DateTime>();

        /// <summary>
        /// saving is mostly done on the UI thread, but not all of it - the
        /// cloud writes what it pulls down on a thread of its own, and a
        /// dictionary being written while it is being walked throws
        /// </summary>
        private static readonly object _lock = new object();

        private static bool _read;

        /// <summary>when this part of the data was last written, or DateTime.MinValue</summary>
        public static DateTime WhenChanged(string area)
        {
            lock (_lock)
            {
                Read();
                DateTime when;
                return _when.TryGetValue(area ?? string.Empty, out when) ? when : DateTime.MinValue;
            }
        }

        /// <summary>
        /// when anything at all was last written. DateTime.MinValue when
        /// nothing has been - a phone the app was only just installed on
        /// </summary>
        public static DateTime LastModified
        {
            get
            {
                lock (_lock)
                {
                    Read();
                    DateTime newest = DateTime.MinValue;
                    foreach (KeyValuePair<string, DateTime> entry in _when)
                        if (entry.Value > newest)
                            newest = entry.Value;
                    return newest;
                }
            }
        }

        /// <summary>is there a date at all - there is none on a fresh install</summary>
        public static bool Known
        {
            get { return LastModified > DateTime.MinValue; }
        }

        /// <summary>which part of the data was the last to change</summary>
        public static string LastChanged
        {
            get
            {
                lock (_lock)
                {
                    Read();
                    string area = string.Empty;
                    DateTime newest = DateTime.MinValue;
                    foreach (KeyValuePair<string, DateTime> entry in _when)
                        if (entry.Value > newest)
                        {
                            newest = entry.Value;
                            area = entry.Key;
                        }
                    return area;
                }
            }
        }

        /// <summary>
        /// something has just been written. Called from the kernel's own Save
        /// methods rather than from the pages that set the work off, so none
        /// of the places data can be changed from can be the one that forgets.
        /// </summary>
        public static void Touch(string area, string dir = null)
        {
            try
            {
                if (!IsHome(dir))
                {
                    //a save into the data folder while the round has been
                    //pointed somewhere else is the self test running, not the
                    //round changing. it is not ours to stamp
                    if (string.IsNullOrEmpty(dir))
                        return;

                    //a save into another folder is a copy of the round - a
                    //backup being built. the round has not changed, so the
                    //copy carries the date the round already had, not today's
                    CopyInto(dir);
                    return;
                }

                lock (_lock)
                {
                    Read();
                    _when[area ?? string.Empty] = DateTime.Now;
                    Write(HomeFolder);
                }
            }
            catch
            {
                //the date is a note about the data, not the data. losing it
                //must never cost the save it was written alongside
            }
        }

        /// <summary>
        /// writes the round's dates into another folder, unchanged. this is
        /// what puts the date the data was last touched into a backup instead
        /// of the date the backup was taken
        /// </summary>
        public static void CopyInto(string dir)
        {
            try
            {
                lock (_lock)
                {
                    Read();
                    Write(dir);
                }
            }
            catch
            {
                //a copy with no date in it still reads - the date is worked
                //out from the files instead, and said to be a guess. Losing
                //the whole backup over it would be the worse answer
            }
        }

        /// <summary>read the dates again - after a restore, they are the backup's</summary>
        public static void Load(string dir = null)
        {
            lock (_lock)
            {
                _read = false;
                HomeFolder = dir;
                Read();
            }
        }

        /// <summary>forget the lot, for Delete All Data and the self test</summary>
        public static void DeleteData()
        {
            lock (_lock)
            {
                _when.Clear();
                _read = true;
            }
        }

        /// <summary>the date out of a stamp file that is not on disk - one inside a backup</summary>
        public static DateTime ReadFrom(Stream stream)
        {
            DateTime newest = DateTime.MinValue;
            try
            {
                DataStampSaveData data = YearlyStore.Deserialise<DataStampSaveData>(stream);
                if (data.Areas != null)
                    foreach (DataStampEntry entry in data.Areas)
                        if (entry.When > newest)
                            newest = entry.When;
            }
            catch
            {
                //an unreadable stamp is the same as no stamp
            }
            return newest;
        }

        /// <summary>
        /// which part of the data a file belongs to, or blank when the file is
        /// none of ours. Used to read a date off a folder - or a backup - that
        /// has no stamp in it yet.
        /// </summary>
        public static string AreaOfFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            string name = Path.GetFileName(fileName.Replace('\\', '/'));

            if (Is(name, "jobs.rjt") || Is(name, "quotes.rjt"))
                return Jobs;
            if (Is(name, "customers.rjt"))
                return Customers;
            if (Is(name, Payment.IgnoreFilePath) || Is(name, "payment.rjt")
                || YearlyStore.IsYearFile(name, Payment.FilePrefix))
                return Payments;
            if (Is(name, "expenses.rjt") || YearlyStore.IsYearFile(name, Expense.FilePrefix))
                return Expenses;
            if (YearlyStore.IsYearFile(name, StatementRecord.FilePrefix))
                return Statements;
            if (Is(name, "expenserules.rjt"))
                return ExpenseRules;
            if (Is(name, "bankaccounts.rjt"))
                return BankAccounts;
            if (Is(name, "directdebits.rjt"))
                return DirectDebits;
            if (Is(name, "balanceadjustments.rjt"))
                return BalanceAdjustments;
            if (Is(name, "daynotes.rjt"))
                return DayNotes;
            if (Is(name, "settings.txt"))
                return Settings;

            return string.Empty;
        }

        private static bool Is(string name, string other)
        {
            return string.Equals(name, other, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHome(string dir)
        {
            return string.Equals(dir ?? string.Empty, HomeFolder ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string PathFor(string dir)
        {
            return Path.Combine(YearlyStore.Folder(dir), FilePath);
        }

        private static void Read()
        {
            if (_read)
                return;

            _read = true;
            _when.Clear();

            try
            {
                string path = PathFor(HomeFolder);
                if (File.Exists(path))
                {
                    DataStampSaveData data = YearlyStore.Deserialise<DataStampSaveData>(path);
                    if (data.Areas != null)
                    {
                        foreach (DataStampEntry entry in data.Areas)
                            if (!string.IsNullOrEmpty(entry.Area))
                                _when[entry.Area] = entry.When;
                        return;
                    }
                }
            }
            catch
            {
                //unreadable - fall through and work it out from the files
            }

            SeedFromTheFiles(HomeFolder);
        }

        /// <summary>
        /// a round that has been worked for years and has no stamp in it yet,
        /// because the app only just started keeping one. The files are the
        /// best answer there is, so each part of the data is dated from the
        /// newest file it is kept in
        /// </summary>
        private static void SeedFromTheFiles(string dir)
        {
            try
            {
                foreach (string path in Directory.GetFiles(YearlyStore.Folder(dir)))
                {
                    string area = AreaOfFile(path);
                    if (area.Length == 0)
                        continue;

                    DateTime when = File.GetLastWriteTime(path);

                    DateTime have;
                    if (!_when.TryGetValue(area, out have) || when > have)
                        _when[area] = when;
                }
            }
            catch
            {
                //no folder yet - a phone the app was only just installed on
            }
        }

        private static void Write(string dir)
        {
            DataStampSaveData data = new DataStampSaveData();
            data.Areas = new List<DataStampEntry>();

            foreach (KeyValuePair<string, DateTime> entry in _when)
                data.Areas.Add(new DataStampEntry() { Area = entry.Key, When = entry.Value });

            YearlyStore.WriteIfChanged(PathFor(dir), YearlyStore.Serialise(data));
        }
    }
}
