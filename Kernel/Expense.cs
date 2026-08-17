using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Kernel
{
    public enum ExpenseCategory
    {
        General,
        Fuel,
        Materials,
        Equipment,
        Vehicle,
        Insurance,
        Food,
        /// <summary>card/direct debit processing fees, bank charges</summary>
        BankCharges,
        Other
    }

    /// <summary>
    /// a business expense. can be linked to a job (JobId) or just to a day
    /// (Date) when added from the calendar. a receipt photo can be attached
    /// and read with OCR to fill in the details
    /// </summary>
    public partial class Expense
    {
        /// <summary>
        /// the master id number
        /// </summary>
        private static int _IdGenerator = 0;

        /// <summary>
        /// the master list of expenses
        /// </summary>
        private static List<Expense> _Expenses = new List<Expense>();

        /// <summary>
        /// generate the id number for the current expense
        /// </summary>
        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        public static Expense Add(Expense expense)
        {
            expense.GenerateId();
            _Expenses.Add(expense);
            return expense;
        }

        public static Expense Get(int id)
        {
            return _Expenses.FirstOrDefault(x => x.Id == id);
        }

        public static void Remove(int id)
        {
            Expense e = Get(id);
            if (e != null)
                e.DeleteReceiptPhoto();
            _Expenses.RemoveAll(x => x.Id == id);
        }

        public static void DeleteData()
        {
            _Expenses.Clear();
        }

        public static List<Expense> Query()
        {
            List<Expense> tmp = new List<Expense>();
            tmp.AddRange(_Expenses);
            return tmp;
        }

        /// <summary>
        /// all expenses linked to a job
        /// </summary>
        public static List<Expense> QueryByJob(int jobId)
        {
            return _Expenses.FindAll(x => x.JobId == jobId);
        }

        /// <summary>
        /// all expenses on a given day (linked to the day or to a job on that day)
        /// </summary>
        public static List<Expense> QueryByDate(DateTime date)
        {
            return _Expenses.FindAll(x => UsfulFuctions.Difference(x.Date, date) == 0);
        }

        /// <summary>
        /// total spent on a given day
        /// </summary>
        public static float TotalForDate(DateTime date)
        {
            float total = 0;
            foreach (Expense e in QueryByDate(date))
                total += e.Amount;
            return total;
        }

        public int Id { get; set; }

        /// <summary>
        /// the job this expense belongs to. -1 when the expense is only
        /// attached to a day from the calendar
        /// </summary>
        public int JobId { get; set; } = -1;

        /// <summary>
        /// the day the expense happened
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// how much was spent
        /// </summary>
        public float Amount { get; set; }

        /// <summary>
        /// who the money was paid to (shop/supplier)
        /// </summary>
        public string Merchant { get; set; } = string.Empty;

        public ExpenseCategory Category { get; set; } = ExpenseCategory.General;

        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// file name of the receipt photo inside the receipts folder.
        /// empty when there is no receipt attached
        /// </summary>
        public string ReceiptFileName { get; set; } = string.Empty;

        /// <summary>
        /// id of whatever this expense was created from (a GoCardless payout
        /// for example) so the same thing is never recorded twice
        /// </summary>
        public string ExternalReference { get; set; } = string.Empty;

        /// <summary>
        /// find an expense already recorded from an outside source
        /// </summary>
        public static Expense FindByReference(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return null;
            return _Expenses.FirstOrDefault(x => x.ExternalReference == reference);
        }

        /// <summary>
        /// the id given to an expense taken off a bank statement. it is built
        /// only from what a statement always prints the same way - the date,
        /// the payee and the amount - so re-importing the same statement, or
        /// a later one that overlaps it, lands on the same id and the line is
        /// skipped instead of being recorded a second time.
        /// <paramref name="occurrence"/> counts identical transactions on the
        /// same day, so two £5 fuel stops on the same forecourt are still two
        /// expenses
        /// </summary>
        public static string StatementReference(DateTime date, string payee, float amount, int occurrence)
        {
            string who = StatementText.Normalise(payee);
            return $"stmt:{date:yyyyMMdd}:{who}:{amount.ToString("0.00", CultureInfo.InvariantCulture)}#{occurrence}";
        }

        /// <summary>
        /// the same id with the account it came off worked into it. with more
        /// than one account, the same amount to the same payee on the same
        /// day out of two accounts is two transactions, not a re-import -
        /// which the account-blind id above would have swallowed. a PayPal
        /// export has no account and keeps the plain id, exactly as before
        /// </summary>
        public static string StatementReference(BankAccount account, DateTime date, string payee, float amount, int occurrence)
        {
            if (account == null)
                return StatementReference(date, payee, amount, occurrence);

            string who = StatementText.Normalise(payee);
            return $"stmt:a{account.Id}:{date:yyyyMMdd}:{who}:{amount.ToString("0.00", CultureInfo.InvariantCulture)}#{occurrence}";
        }

        /// <summary>
        /// the expense a statement line was recorded as, if it ever was.
        /// tries the account-tagged reference and, on the account that
        /// inherited them, the plain reference from before accounts existed -
        /// an expense recorded back then must not come in again as new
        /// </summary>
        public static Expense FindFromStatement(BankAccount account, DateTime date, string payee, float amount, int occurrence)
        {
            Expense expense = FindByReference(StatementReference(account, date, payee, amount, occurrence));

            if (expense == null && account != null && account.InheritsLegacyReferences)
                expense = FindByReference(StatementReference(date, payee, amount, occurrence));

            return expense;
        }

        /// <summary>true when this statement line has already been recorded</summary>
        public static bool AlreadyImported(string statementReference)
        {
            return FindByReference(statementReference) != null;
        }

        /// <summary>every expense that came off a bank statement</summary>
        public static List<Expense> QueryFromStatements()
        {
            return _Expenses.FindAll(x => x.ExternalReference != null
                && x.ExternalReference.StartsWith("stmt:", StringComparison.Ordinal));
        }

        public const string ReceiptFolder = "receipts";

        /// <summary>
        /// the top of the receipt photo store, created on demand. photos
        /// themselves live in a tax year folder underneath it
        /// </summary>
        public static string GetReceiptFolderPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ReceiptFolder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// where one tax year's receipts are kept, so a year can be backed up
        /// or handed over with the paperwork that goes with it
        /// </summary>
        public static string GetReceiptFolderPath(int taxYear)
        {
            string dir = Path.Combine(GetReceiptFolderPath(), TaxCalendar.YearFolderName(taxYear));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>the tax year this expense - and so its receipt - belongs to</summary>
        [XmlIgnore]
        public int TaxYear
        {
            get { return TaxCalendar.TaxYearOf(Date); }
        }

        [XmlIgnore]
        public bool HasReceipt
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ReceiptFileName) && File.Exists(ReceiptPhotoPath);
            }
        }

        /// <summary>
        /// full path of the receipt photo on this device: in its tax year
        /// folder, or loose in the receipts folder for a photo taken before
        /// the years were split up
        /// </summary>
        [XmlIgnore]
        public string ReceiptPhotoPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ReceiptFileName))
                    return string.Empty;

                string filed = Path.Combine(GetReceiptFolderPath(TaxYear), ReceiptFileName);
                if (File.Exists(filed))
                    return filed;

                string loose = Path.Combine(GetReceiptFolderPath(), ReceiptFileName);
                if (File.Exists(loose))
                    return loose;

                //nothing there yet - a photo about to be written goes in the
                //tax year folder
                return filed;
            }
        }

        /// <summary>
        /// puts the receipt in the folder for the tax year the expense is in.
        /// called after saving, because changing an expense's date can move
        /// it into another tax year and the paperwork has to follow it
        /// </summary>
        public void FileReceiptWithItsYear()
        {
            if (string.IsNullOrWhiteSpace(ReceiptFileName))
                return;

            try
            {
                string wanted = Path.Combine(GetReceiptFolderPath(TaxYear), ReceiptFileName);
                string current = ReceiptPhotoPath;

                if (current == wanted || !File.Exists(current))
                    return;

                File.Move(current, wanted, true);
            }
            catch
            {
                //a photo that will not move stays where it is and is still
                //found by the fallback in ReceiptPhotoPath
            }
        }

        /// <summary>
        /// moves photos taken before receipts were filed by tax year into the
        /// right folder. anything nobody claims is left where it is
        /// </summary>
        public static int FileLooseReceipts()
        {
            int moved = 0;
            try
            {
                Dictionary<string, Expense> owners = new Dictionary<string, Expense>();
                foreach (Expense e in _Expenses)
                    if (!string.IsNullOrWhiteSpace(e.ReceiptFileName) && !owners.ContainsKey(e.ReceiptFileName))
                        owners[e.ReceiptFileName] = e;

                foreach (string path in Directory.GetFiles(GetReceiptFolderPath()))
                {
                    string name = Path.GetFileName(path);
                    if (!owners.TryGetValue(name, out Expense owner))
                        continue;

                    File.Move(path, Path.Combine(GetReceiptFolderPath(owner.TaxYear), name), true);
                    moved++;
                }
            }
            catch
            {
            }
            return moved;
        }

        public void DeleteReceiptPhoto()
        {
            try
            {
                if (HasReceipt)
                    File.Delete(ReceiptPhotoPath);
            }
            catch
            {
            }
            ReceiptFileName = string.Empty;
        }

        private Job _job;

        [XmlIgnore]
        public Job LinkedJob
        {
            get
            {
                if (_job == null && JobId != -1)
                    _job = Job.Query(QueryType.JobId, JobId).FirstOrDefault();
                return _job;
            }
        }

        public string FormattedAmount
        {
            get
            {
                return $"{Gloable.CurrenceSymbol}{Amount:0.00}";
            }
        }

        public string FormattedCategory
        {
            get
            {
                return Category.ToString();
            }
        }

        public string FormattedDate
        {
            get
            {
                int days = UsfulFuctions.DifferenceSigned(Date, UsfulFuctions.DateNow);
                if (days == 0)
                    return "Today";
                if (days == -1)
                    return "Yesterday";
                return Date.ToShortDateString();
            }
        }

        public string FormattedMerchant
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Merchant))
                    return "Expense";
                return Merchant;
            }
        }

        [XmlIgnore]
        public bool HaveNotes
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Notes);
            }
        }

        /// <summary>
        /// where the expense is attached: the job address or just the day
        /// </summary>
        public string FormattedLinkedTo
        {
            get
            {
                Job j = LinkedJob;
                if (j != null)
                    return $"Job: {j.JobFormattedStreet}";
                return $"Day: {Date.ToShortDateString()}";
            }
        }
    }

    /// <summary>
    /// pulls the useful details (total, date, merchant) out of the raw text
    /// that OCR reads off a receipt photo
    /// </summary>
    public static class ReceiptReader
    {
        /// <summary>
        /// result of reading a receipt. any field the reader could not work
        /// out is left at its default
        /// </summary>
        public class ReceiptData
        {
            public float Amount = -1;
            public DateTime Date = DateTime.MinValue;
            public string Merchant = string.Empty;

            public bool FoundAmount { get { return Amount >= 0; } }
            public bool FoundDate { get { return Date != DateTime.MinValue; } }
            public bool FoundMerchant { get { return !string.IsNullOrWhiteSpace(Merchant); } }
            public bool FoundAnything { get { return FoundAmount || FoundDate || FoundMerchant; } }
        }

        //a money value like 12.34, £12.34, 12,34
        private static readonly Regex MoneyRegex = new Regex(@"(?:[£$€]\s*)?(\d{1,6})[.,](\d{2})\b", RegexOptions.Compiled);

        //dates like 12/08/2026, 12-08-26, 2026-08-12
        private static readonly Regex NumericDateRegex = new Regex(@"\b(\d{1,4})[/\-.](\d{1,2})[/\-.](\d{1,4})\b", RegexOptions.Compiled);

        //dates like 12 Aug 2026 or Aug 12, 2026
        private static readonly Regex WordDateRegex = new Regex(
            @"\b(?:(\d{1,2})\s*(?:st|nd|rd|th)?\s+([A-Za-z]{3,9})|([A-Za-z]{3,9})\s+(\d{1,2})(?:st|nd|rd|th)?,?)\s+(\d{2,4})\b",
            RegexOptions.Compiled);

        private static readonly string[] TotalKeywords = { "grand total", "amount due", "total due", "amount paid", "card payment", "total to pay", "balance due", "to pay", "total" };
        private static readonly string[] NotTotalKeywords = { "subtotal", "sub total", "sub-total", "vat", "tax", "change", "cash", "tend", "saving", "discount", "points", "balance before" };

        public static ReceiptData Read(string ocrText)
        {
            ReceiptData data = new ReceiptData();
            if (string.IsNullOrWhiteSpace(ocrText))
                return data;

            List<string> lines = ocrText
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            data.Amount = FindTotal(lines);
            data.Date = FindDate(lines);
            data.Merchant = FindMerchant(lines);
            return data;
        }

        private static float FindTotal(List<string> lines)
        {
            //first choice: a money value on a line with a "total" style keyword
            //(taking the last such line, receipts list the grand total at the end)
            float best = -1;
            foreach (string line in lines)
            {
                string lower = line.ToLowerInvariant();
                if (!TotalKeywords.Any(k => lower.Contains(k)))
                    continue;
                if (NotTotalKeywords.Any(k => lower.Contains(k)))
                    continue;

                Match m = MoneyRegex.Match(line);
                if (m.Success)
                    best = ParseMoney(m);
            }
            if (best >= 0)
                return best;

            //fall back to the largest money value on the receipt
            foreach (string line in lines)
            {
                string lower = line.ToLowerInvariant();
                if (NotTotalKeywords.Any(k => lower.Contains(k)))
                    continue;
                foreach (Match m in MoneyRegex.Matches(line))
                {
                    float v = ParseMoney(m);
                    if (v > best)
                        best = v;
                }
            }
            return best;
        }

        private static float ParseMoney(Match m)
        {
            string s = $"{m.Groups[1].Value}.{m.Groups[2].Value}";
            float v;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return -1;
        }

        private static DateTime FindDate(List<string> lines)
        {
            foreach (string line in lines)
            {
                Match m = NumericDateRegex.Match(line);
                if (m.Success)
                {
                    DateTime d = ParseNumericDate(m);
                    if (d != DateTime.MinValue)
                        return d;
                }

                m = WordDateRegex.Match(line);
                if (m.Success)
                {
                    DateTime d = ParseWordDate(m);
                    if (d != DateTime.MinValue)
                        return d;
                }
            }
            return DateTime.MinValue;
        }

        private static DateTime ParseNumericDate(Match m)
        {
            int a, b, c;
            if (!int.TryParse(m.Groups[1].Value, out a)) return DateTime.MinValue;
            if (!int.TryParse(m.Groups[2].Value, out b)) return DateTime.MinValue;
            if (!int.TryParse(m.Groups[3].Value, out c)) return DateTime.MinValue;

            int day, month, year;
            if (a > 31) //yyyy-mm-dd
            {
                year = a; month = b; day = c;
            }
            else //dd/mm/yyyy (uk receipts)
            {
                day = a; month = b; year = c;
            }

            if (year < 100)
                year += 2000;

            //some receipts are mm/dd - swap when the day slot cannot be a day
            if (day > 31 && month <= 31)
            {
                int t = day; day = month; month = t;
            }
            if (month > 12 && day <= 12)
            {
                int t = day; day = month; month = t;
            }

            return MakeDate(year, month, day);
        }

        private static DateTime ParseWordDate(Match m)
        {
            string monthName;
            string dayStr;
            if (m.Groups[1].Success) //12 Aug 2026
            {
                dayStr = m.Groups[1].Value;
                monthName = m.Groups[2].Value;
            }
            else //Aug 12, 2026
            {
                monthName = m.Groups[3].Value;
                dayStr = m.Groups[4].Value;
            }

            int day, year;
            if (!int.TryParse(dayStr, out day)) return DateTime.MinValue;
            if (!int.TryParse(m.Groups[5].Value, out year)) return DateTime.MinValue;
            if (year < 100)
                year += 2000;

            int month = MonthFromName(monthName);
            if (month == 0)
                return DateTime.MinValue;

            return MakeDate(year, month, day);
        }

        private static int MonthFromName(string name)
        {
            string[] months = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
            string lower = name.ToLowerInvariant();
            for (int i = 0; i < months.Length; i++)
                if (lower.StartsWith(months[i]))
                    return i + 1;
            return 0;
        }

        private static DateTime MakeDate(int year, int month, int day)
        {
            if (year < 2000 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31)
                return DateTime.MinValue;
            try
            {
                return new DateTime(year, month, day);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static readonly string[] MerchantSkipWords = { "receipt", "invoice", "vat", "tel", "phone", "www", ".com", ".co.uk", "@", "welcome", "thank" };

        private static string FindMerchant(List<string> lines)
        {
            //the shop name is normally one of the first lines: short-ish text
            //without numbers or contact details
            foreach (string line in lines.Take(5))
            {
                string lower = line.ToLowerInvariant();
                if (line.Length < 3 || line.Length > 40)
                    continue;
                if (line.Any(char.IsDigit))
                    continue;
                if (MerchantSkipWords.Any(k => lower.Contains(k)))
                    continue;
                return line;
            }
            return string.Empty;
        }
    }
}
