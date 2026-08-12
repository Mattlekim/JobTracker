using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Kernel
{
    /// <summary>
    /// tidies up the payee text a bank prints against a transaction so the
    /// same shop or bill can be recognised again next month, when the
    /// reference number and the date stamped on the end of it have changed
    /// </summary>
    public static class StatementText
    {
        /// <summary>
        /// words banks put around the payee that say how the money moved
        /// rather than who it went to
        /// </summary>
        private static readonly HashSet<string> NoiseWords = new HashSet<string>
        {
            "DIRECT", "DEBIT", "DD", "SO", "STO", "STANDING", "ORDER", "CARD", "PAYMENT", "PAYMENTS",
            "TO", "FROM", "BP", "BGC", "CHQ", "POS", "VIS", "VISA", "DEB", "ONLINE",
            "BANK", "TRANSFER", "TFR", "FPO", "FPI", "FASTER", "REF", "REFERENCE", "MANDATE", "NO",
            "VIA", "MOBILE", "INTERNET", "PURCHASE", "WITHDRAWAL", "ATM", "CONTACTLESS", "CSH",
            "CALL", "RECURRING", "ON",
        };

        /// <summary>
        /// upper cased, with punctuation, numbers and card digits taken out,
        /// so "TESCO STORES 3456 12AUG" and "TESCO STORES 8891 09SEP" both
        /// come out as "TESCO STORES"
        /// </summary>
        public static string Normalise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            StringBuilder letters = new StringBuilder();
            foreach (char c in text.ToUpperInvariant())
                letters.Append(char.IsLetterOrDigit(c) ? c : ' ');

            List<string> kept = new List<string>();
            foreach (string word in letters.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (IsNumberish(word))
                    continue;
                kept.Add(word);
            }

            return string.Join(" ", kept);
        }

        /// <summary>
        /// a reference number, card number, sort code or a date stamp like
        /// "12AUG" - none of which say who was paid
        /// </summary>
        private static bool IsNumberish(string word)
        {
            int digits = word.Count(char.IsDigit);
            if (digits == 0)
                return false;

            //anything that is mostly digits is a number the bank made up
            return digits * 2 >= word.Length;
        }

        /// <summary>
        /// the short name a rule is remembered under: the payee with the
        /// "how it was paid" words dropped, cut to the first few words so a
        /// branch or town on the end does not stop it matching
        /// </summary>
        public static string PayeeKey(string text)
        {
            string normalised = Normalise(text);
            if (normalised.Length == 0)
                return string.Empty;

            List<string> words = new List<string>();
            foreach (string word in normalised.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (NoiseWords.Contains(word))
                    continue;
                words.Add(word);
                if (words.Count == 3)
                    break;
            }

            //all the bank gave us was how it was paid - better to keep that
            //than to end up with an empty key that matches everything
            if (words.Count == 0)
                return normalised;

            return string.Join(" ", words);
        }

        /// <summary>
        /// the date off a statement line. banks write these every way there
        /// is - 12/08/2026, 12-08-26, 2026-08-12, 12 Aug 2026 - and a csv
        /// exported on a machine set to another country will not match this
        /// one either, so the format is worked out rather than assumed
        /// </summary>
        public static bool TryParseDate(string text, out DateTime date)
        {
            date = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            Match m = NumericDate.Match(text);
            if (m.Success)
            {
                int a = int.Parse(m.Groups[1].Value);
                int b = int.Parse(m.Groups[2].Value);
                int c = int.Parse(m.Groups[3].Value);

                int day, month, year;
                if (a > 31) //yyyy-mm-dd
                {
                    year = a; month = b; day = c;
                }
                else //dd/mm/yyyy, what uk banks print
                {
                    day = a; month = b; year = c;
                }

                if (year < 100)
                    year += 2000;

                //the american way round, when the day slot cannot be a day
                if (month > 12 && day <= 12)
                {
                    int t = day; day = month; month = t;
                }

                return TryMakeDate(year, month, day, out date);
            }

            m = WordDate.Match(text);
            if (m.Success)
            {
                int day = int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[4].Value);
                string monthName = m.Groups[1].Success ? m.Groups[2].Value : m.Groups[3].Value;
                int year = int.Parse(m.Groups[5].Value);
                if (year < 100)
                    year += 2000;

                int month = MonthFromName(monthName);
                if (month == 0)
                    return false;

                return TryMakeDate(year, month, day, out date);
            }

            return false;
        }

        private static readonly Regex NumericDate = new Regex(@"\b(\d{1,4})[/\-.](\d{1,2})[/\-.](\d{1,4})\b", RegexOptions.Compiled);

        private static readonly Regex WordDate = new Regex(
            @"\b(?:(\d{1,2})\s*(?:st|nd|rd|th)?\s+([A-Za-z]{3,9})|([A-Za-z]{3,9})\s+(\d{1,2})(?:st|nd|rd|th)?,?)\s+(\d{2,4})\b",
            RegexOptions.Compiled);

        private static int MonthFromName(string name)
        {
            string[] months = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
            string lower = name.ToLowerInvariant();
            for (int i = 0; i < months.Length; i++)
                if (lower.StartsWith(months[i]))
                    return i + 1;
            return 0;
        }

        private static bool TryMakeDate(int year, int month, int day, out DateTime date)
        {
            date = DateTime.MinValue;
            if (year < 1990 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31)
                return false;
            try
            {
                date = new DateTime(year, month, day);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// does <paramref name="text"/> contain <paramref name="key"/> as
        /// whole words rather than as part of a longer word
        /// </summary>
        public static bool ContainsKey(string text, string key)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key))
                return false;

            return $" {text} ".Contains($" {key} ", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// a decision about an outgoing on a bank statement, remembered so the
    /// same one never has to be dealt with twice. either "this is a business
    /// expense, file it like this" or "this is not, leave it alone".
    /// a recurring bill - insurance, car tax, a phone contract - is logged by
    /// itself the next time a statement is imported
    /// </summary>
    public partial class ExpenseRule
    {
        private static int _IdGenerator = 0;

        private static List<ExpenseRule> _Rules = new List<ExpenseRule>();

        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        public int Id { get; set; }

        /// <summary>
        /// the payee text this rule matches, run through
        /// <see cref="StatementText.PayeeKey"/> when the rule was made
        /// </summary>
        public string Match { get; set; } = string.Empty;

        /// <summary>the payee as the bank printed it, for showing to the user</summary>
        public string Merchant { get; set; } = string.Empty;

        /// <summary>true when this outgoing is not a business expense at all</summary>
        public bool Ignore { get; set; }

        public ExpenseCategory Category { get; set; } = ExpenseCategory.General;

        /// <summary>note copied onto every expense this rule creates</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>how many statement lines this rule has dealt with</summary>
        public int TimesUsed { get; set; }

        public DateTime LastUsed { get; set; }

        public static ExpenseRule Add(ExpenseRule rule)
        {
            rule.GenerateId();
            _Rules.Add(rule);
            return rule;
        }

        /// <summary>
        /// makes the rule for a payee, or updates the one already there so a
        /// payee never ends up with two rules disagreeing about it
        /// </summary>
        public static ExpenseRule Remember(string reference, bool ignore, ExpenseCategory category, string notes)
        {
            string key = StatementText.PayeeKey(reference);
            if (key.Length == 0)
                return null;

            ExpenseRule rule = _Rules.FirstOrDefault(x => x.Match == key);
            if (rule == null)
            {
                rule = new ExpenseRule() { Match = key };
                Add(rule);
            }

            rule.Merchant = FriendlyMerchant(reference);
            rule.Ignore = ignore;
            rule.Category = category;
            rule.Notes = notes ?? string.Empty;
            return rule;
        }

        /// <summary>
        /// the payee written the way a person would: the bank's shouting cut
        /// down to the words that name the shop
        /// </summary>
        public static string FriendlyMerchant(string reference)
        {
            string key = StatementText.PayeeKey(reference);
            if (key.Length == 0)
                return reference ?? string.Empty;

            //title case reads better on the expense list than TESCO STORES
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.ToLowerInvariant());
        }

        public static ExpenseRule Get(int id)
        {
            return _Rules.FirstOrDefault(x => x.Id == id);
        }

        public static void Remove(int id)
        {
            _Rules.RemoveAll(x => x.Id == id);
        }

        public static void DeleteData()
        {
            _Rules.Clear();
        }

        public static List<ExpenseRule> Query()
        {
            List<ExpenseRule> tmp = new List<ExpenseRule>();
            tmp.AddRange(_Rules);
            return tmp;
        }

        /// <summary>
        /// the rule for a statement payee, or null when this payee has never
        /// been dealt with. the longest match wins, so a rule for
        /// "SHELL BROMLEY" beats a broader one for "SHELL"
        /// </summary>
        public static ExpenseRule FindMatch(string reference)
        {
            string text = StatementText.Normalise(reference);
            if (text.Length == 0)
                return null;

            string key = StatementText.PayeeKey(reference);

            ExpenseRule best = null;
            foreach (ExpenseRule rule in _Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Match))
                    continue;

                if (rule.Match != key && !StatementText.ContainsKey(text, rule.Match))
                    continue;

                if (best == null || rule.Match.Length > best.Match.Length)
                    best = rule;
            }
            return best;
        }

        /// <summary>records that this rule dealt with another statement line</summary>
        public void MarkUsed(DateTime date)
        {
            TimesUsed++;
            if (date > LastUsed)
                LastUsed = date;
        }

        [XmlIgnore]
        public string FormattedRule
        {
            get
            {
                if (Ignore)
                    return "Never an expense";
                return $"Expense - {Category}";
            }
        }

        [XmlIgnore]
        public string FormattedMerchant
        {
            get
            {
                return string.IsNullOrWhiteSpace(Merchant) ? Match : Merchant;
            }
        }

        [XmlIgnore]
        public string FormattedUse
        {
            get
            {
                string used = TimesUsed == 1 ? "1 transaction" : $"{TimesUsed} transactions";
                if (TimesUsed == 0)
                    return "Not used yet";
                if (LastUsed == DateTime.MinValue)
                    return used;
                return $"{used}, last {LastUsed.ToShortDateString()}";
            }
        }

        [XmlIgnore]
        public string FormattedNotes
        {
            get
            {
                return string.IsNullOrWhiteSpace(Notes) ? string.Empty : $"Note: {Notes}";
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
    }
}
