using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Kernel
{
    /// <summary>
    /// How much of everything a set of the app's data holds, and when it was
    /// last changed.
    ///
    /// It exists for one question: a backup is about to be put back over the
    /// round, and "everything on this device is replaced" is not something
    /// anybody can weigh up without knowing what the difference actually is.
    /// Two snapshots - one of what is on the phone, one read out of the backup
    /// - answer it in figures: how many houses, how many cleans written up,
    /// how much money in and out.
    ///
    /// The backup is read out of the zip as it stands rather than unpacked, so
    /// looking costs nothing and changes nothing. Nothing here loads anything
    /// into the app: the lists it counts are its own.
    /// </summary>
    public class DataSnapshot
    {
        /// <summary>when the data was last changed. DateTime.MinValue when there is nothing to say</summary>
        public DateTime LastModified = DateTime.MinValue;

        /// <summary>
        /// true when the date was worked out from the files rather than read
        /// off a stamp - a backup made before the app kept one. It is the day
        /// the backup was written, so it is the best guess and not the answer
        /// </summary>
        public bool DateIsGuessed;

        /// <summary>false when the backup could not be looked into at all</summary>
        public bool Readable = true;

        public int Jobs;
        public int JobsDone;
        public int Quotes;
        public int Customers;
        public int Payments;
        public int Expenses;

        public float MoneyIn;
        public float MoneyOut;

        /// <summary>
        /// the tax years the payment and expense figures cover. A backup can
        /// hold one tax year on its own, and restoring it leaves the other
        /// years on the device alone - so counting all of them against one
        /// year's worth would say a thousand payments were about to vanish
        /// when not one of them is going anywhere
        /// </summary>
        public List<int> TaxYears = new List<int>();

        public bool KnowsWhenItChanged
        {
            get { return LastModified > DateTime.MinValue; }
        }

        /// <summary>the date said the way the rest of the app says dates</summary>
        public string WhenText
        {
            get
            {
                if (!KnowsWhenItChanged)
                    return "not known";
                return LastModified.ToString("d MMM yyyy") + " at " + LastModified.ToString("HH:mm");
            }
        }

        /// <summary>what is in it, in one line</summary>
        public string Summary
        {
            get
            {
                return $"{Jobs} job(s), {Customers} customer(s), {Payments} payment(s), {Expenses} expense(s)";
            }
        }

        private void NoteYear(int year)
        {
            if (!TaxYears.Contains(year))
                TaxYears.Add(year);
        }

        /// <summary>
        /// what is on the device now.
        ///
        /// <paramref name="onlyYears"/> keeps the money figures to the tax
        /// years a backup actually holds, so the two sides are counting the
        /// same thing.
        /// </summary>
        public static DataSnapshot Current(List<int> onlyYears = null)
        {
            DataSnapshot snapshot = new DataSnapshot();
            snapshot.LastModified = DataStamp.LastModified;

            foreach (Job job in Job.Query())
            {
                snapshot.Jobs++;
                if (job.IsCompleted)
                    snapshot.JobsDone++;
            }

            snapshot.Quotes = Job.QueryQuotes().Count;
            snapshot.Customers = Customer.Query().Count;

            HashSet<int> wanted = onlyYears == null || onlyYears.Count == 0
                ? null : new HashSet<int>(onlyYears);

            foreach (Payment payment in Payment.Query())
            {
                int year = TaxCalendar.TaxYearOf(payment.Date);
                if (wanted != null && !wanted.Contains(year))
                    continue;

                snapshot.Payments++;
                snapshot.MoneyIn += payment.Amount;
                snapshot.NoteYear(year);
            }

            foreach (Expense expense in Expense.Query())
            {
                int year = TaxCalendar.TaxYearOf(expense.Date);
                if (wanted != null && !wanted.Contains(year))
                    continue;

                snapshot.Expenses++;
                snapshot.MoneyOut += expense.Amount;
                snapshot.NoteYear(year);
            }

            if (wanted != null)
            {
                snapshot.TaxYears = new List<int>(onlyYears);
            }

            snapshot.TaxYears.Sort();
            return snapshot;
        }

        /// <summary>
        /// what is inside a backup, read straight out of the zip. Nothing is
        /// unpacked and nothing on the device is touched
        /// </summary>
        public static DataSnapshot FromBackup(string path)
        {
            DataSnapshot snapshot = new DataSnapshot();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                snapshot.Readable = false;
                return snapshot;
            }

            //the newest of the data files in it, which is the date to fall
            //back on for a backup made before the app wrote one down
            DateTime newestFile = DateTime.MinValue;

            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(path))
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string name = Path.GetFileName((entry.FullName ?? string.Empty).Replace('\\', '/'));
                        if (name.Length == 0)
                            continue;

                        if (string.Equals(name, DataStamp.FilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            using (Stream stream = entry.Open())
                                snapshot.LastModified = DataStamp.ReadFrom(stream);
                            continue;
                        }

                        if (DataStamp.AreaOfFile(name).Length > 0)
                        {
                            DateTime written = entry.LastWriteTime.LocalDateTime;
                            if (written > newestFile)
                                newestFile = written;
                        }

                        Count(snapshot, entry, name);
                    }
            }
            catch
            {
                //not a zip, or one that cannot be opened. the restore itself
                //will say so - all this can do is not pretend to know
                snapshot.Readable = false;
                return snapshot;
            }

            if (!snapshot.KnowsWhenItChanged && newestFile > DateTime.MinValue)
            {
                snapshot.LastModified = newestFile;
                snapshot.DateIsGuessed = true;
            }

            snapshot.TaxYears.Sort();
            return snapshot;
        }

        /// <summary>
        /// one file out of the backup added to the figures. A file that will
        /// not read is passed over rather than thrown: this is a look at a
        /// backup, and half a look is worth more than none
        /// </summary>
        private static void Count(DataSnapshot snapshot, ZipArchiveEntry entry, string name)
        {
            try
            {
                if (string.Equals(name, "jobs.rjt", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = entry.Open())
                    {
                        JobSaveData data = YearlyStore.Deserialise<JobSaveData>(stream);
                        if (data.Jobs != null)
                            foreach (Job job in data.Jobs)
                            {
                                snapshot.Jobs++;
                                if (job.IsCompleted)
                                    snapshot.JobsDone++;
                            }
                    }
                    return;
                }

                if (string.Equals(name, "quotes.rjt", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = entry.Open())
                    {
                        JobSaveData data = YearlyStore.Deserialise<JobSaveData>(stream);
                        snapshot.Quotes += data.Jobs == null ? 0 : data.Jobs.Count;
                    }
                    return;
                }

                if (string.Equals(name, "customers.rjt", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = entry.Open())
                    {
                        CustomerSaveData data = YearlyStore.Deserialise<CustomerSaveData>(stream);
                        snapshot.Customers += data.Customers == null ? 0 : data.Customers.Count;
                    }
                    return;
                }

                if (YearlyStore.IsYearFile(name, Payment.FilePrefix)
                    || string.Equals(name, "payment.rjt", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = entry.Open())
                    {
                        PaymentSaveData data = YearlyStore.Deserialise<PaymentSaveData>(stream);
                        if (data.Payments != null)
                            foreach (Payment payment in data.Payments)
                            {
                                snapshot.Payments++;
                                snapshot.MoneyIn += payment.Amount;
                                snapshot.NoteYear(TaxCalendar.TaxYearOf(payment.Date));
                            }
                    }
                    return;
                }

                if (YearlyStore.IsYearFile(name, Expense.FilePrefix)
                    || string.Equals(name, "expenses.rjt", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = entry.Open())
                    {
                        ExpenseSaveData data = YearlyStore.Deserialise<ExpenseSaveData>(stream);
                        if (data.Expenses != null)
                            foreach (Expense expense in data.Expenses)
                            {
                                snapshot.Expenses++;
                                snapshot.MoneyOut += expense.Amount;
                                snapshot.NoteYear(TaxCalendar.TaxYearOf(expense.Date));
                            }
                    }
                    return;
                }
            }
            catch
            {
                //one file that will not read must not lose the rest of them
            }
        }

        //  ----------------------------------------------------  saying it

        /// <summary>
        /// What putting <paramref name="backup"/> back over <paramref name="here"/>
        /// would change, house by house and pound by pound.
        ///
        /// Everything is said as "here" against "in the backup" rather than as
        /// a bare plus or minus, because which way round a difference goes is
        /// the whole of what somebody is trying to work out.
        /// </summary>
        public static string Difference(DataSnapshot backup, DataSnapshot here)
        {
            if (backup == null || here == null)
                return "There is nothing to compare.";

            if (!backup.Readable)
                return "This backup could not be looked into, so there is no telling what is in it.";

            StringBuilder text = new StringBuilder();

            text.AppendLine(CountLine("Jobs", here.Jobs, backup.Jobs));
            text.AppendLine(CountLine("Jobs done", here.JobsDone, backup.JobsDone));
            text.AppendLine(CountLine("Quotes", here.Quotes, backup.Quotes));
            text.AppendLine(CountLine("Customers", here.Customers, backup.Customers));

            if (backup.TaxYears.Count == 0)
            {
                text.AppendLine();
                text.AppendLine("There are no payments or expenses in this backup, so what is on this device is left as it is.");
            }
            else
            {
                text.AppendLine();
                text.AppendLine(CountLine("Payments", here.Payments, backup.Payments));
                text.AppendLine(MoneyLine("Money in", here.MoneyIn, backup.MoneyIn));
                text.AppendLine(CountLine("Expenses", here.Expenses, backup.Expenses));
                text.AppendLine(MoneyLine("Money out", here.MoneyOut, backup.MoneyOut));

                text.AppendLine();
                text.AppendLine($"Money is counted for {YearsText(backup.TaxYears)} - the tax year(s) in this backup. Any other year on this device is left alone.");
            }

            text.AppendLine();
            text.AppendLine(backup.KnowsWhenItChanged
                ? $"The backup was last changed {backup.WhenText}{(backup.DateIsGuessed ? " (going by the files in it - it does not say)" : string.Empty)}."
                : "The backup does not say when it was last changed.");

            text.Append(here.KnowsWhenItChanged
                ? $"This device was last changed {here.WhenText}."
                : "This device has nothing recorded yet.");

            return text.ToString();
        }

        /// <summary>a line of the comparison, or that there is nothing in it</summary>
        private static string CountLine(string what, int here, int backup)
        {
            if (here == backup)
                return $"{what}: {here} - the same either way";

            int difference = backup - here;
            return $"{what}: {here} here, {backup} in the backup ({Math.Abs(difference)} {(difference < 0 ? "fewer" : "more")})";
        }

        private static string MoneyLine(string what, float here, float backup)
        {
            if (Math.Abs(here - backup) < 0.005f)
                return $"{what}: {Money(here)} - the same either way";

            float difference = backup - here;
            return $"{what}: {Money(here)} here, {Money(backup)} in the backup ({Money(Math.Abs(difference))} {(difference < 0 ? "less" : "more")})";
        }

        private static string Money(float amount)
        {
            return $"{Gloable.CurrenceSymbol}{amount:0.00}";
        }

        private static string YearsText(List<int> years)
        {
            if (years == null || years.Count == 0)
                return "no tax year";

            List<string> named = new List<string>();
            foreach (int year in years)
                named.Add(TaxCalendar.YearName(year));

            return string.Join(", ", named);
        }

        /// <summary>
        /// is the backup older than what is on the device - the one thing
        /// worth stopping somebody over. Neither side knowing when it changed
        /// is not older, it is unknown, and a warning nobody can act on is
        /// worse than none
        /// </summary>
        public static bool BackupIsOlder(DataSnapshot backup, DataSnapshot here)
        {
            if (backup == null || here == null)
                return false;

            if (!backup.KnowsWhenItChanged || !here.KnowsWhenItChanged)
                return false;

            return backup.LastModified < here.LastModified;
        }

        /// <summary>how far apart two dates are, said the way somebody would say it</summary>
        public static string HowLong(TimeSpan gap)
        {
            if (gap.TotalDays >= 60)
                return $"{(int)(gap.TotalDays / 30)} months";
            if (gap.TotalDays >= 2)
                return $"{(int)gap.TotalDays} days";
            if (gap.TotalHours >= 2)
                return $"{(int)gap.TotalHours} hours";
            if (gap.TotalMinutes >= 2)
                return $"{(int)gap.TotalMinutes} minutes";

            return "moments";
        }
    }
}
