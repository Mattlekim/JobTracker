using System.Globalization;
using System.Text.RegularExpressions;
using Kernel;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;
using PdfPage = UglyToad.PdfPig.Content.Page; //maui has a Page of its own

namespace UiInterface.ImportExport
{
    /// <summary>the pdf is locked and the password we were given (if any) did not open it</summary>
    public class PdfStatementPasswordException : Exception
    {
        public PdfStatementPasswordException(string message) : base(message) { }
    }

    /// <summary>
    /// Turns a bank statement PDF into the same header + rows shape <see cref="CSV"/> produces for a
    /// csv, so <c>StatmentViewer</c> can show and import either the same way.
    ///
    /// A PDF has no columns - only words with a position on the page - so the columns have to be
    /// rebuilt. Where the statement has a recognisable table header ("Date ... Description ... Paid in")
    /// the header words say where each column sits and every later word drops into the column it lines
    /// up with. Statements with no such header fall back to reading each line as a date, a description
    /// and whatever money amounts follow it.
    /// </summary>
    public static class PdfStatementImporter
    {
        /// <summary>words that make a line look like the header row of the transaction table</summary>
        private static readonly string[] DetailHeaders =
            { "description", "details", "reference", "transaction", "narrative", "payee", "particulars", "type" };

        /// <summary>a money amount - decimals are required so account numbers and dates are not mistaken for one</summary>
        private static readonly Regex MoneyPattern = new Regex(
            @"(?<![\w.])[-+]?[£$€]?\s?\d{1,3}(?:,\d{3})*\.\d{2}\s?(?:CR|DR)?(?![\w.])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>a whole cell that is nothing but a date - 01/04/2024, 1 Apr 24, 2024-04-01</summary>
        private static readonly Regex DateCellPattern = new Regex(
            @"^\s*(\d{1,2}(st|nd|rd|th)?[\s\-/\.]+[A-Za-z]{3,9}([\s\-/\.]+\d{2,4})?|\d{1,2}[\-/\.]\d{1,2}([\-/\.]\d{2,4})?|\d{4}[\-/\.]\d{1,2}[\-/\.]\d{1,2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>the same thing but at the start of a line, for statements with no table header</summary>
        private static readonly Regex DateStartPattern = new Regex(
            @"^\s*(\d{1,2}(st|nd|rd|th)?[\s\-/\.]+[A-Za-z]{3,9}([\s\-/\.]+\d{2,4})?|\d{1,2}[\-/\.]\d{1,2}([\-/\.]\d{2,4})?|\d{4}[\-/\.]\d{1,2}[\-/\.]\d{1,2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex YearPattern = new Regex(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);

        private static readonly string[] DateFormatsWithYear =
        {
            "d/M/yyyy", "d/M/yy", "d-M-yyyy", "d-M-yy", "d.M.yyyy", "d.M.yy",
            "yyyy-M-d", "yyyy/M/d", "yyyy.M.d",
            "d MMM yyyy", "d MMM yy", "d-MMM-yyyy", "d-MMM-yy", "d.MMM.yyyy",
            "d MMMM yyyy", "d MMMM yy", "d-MMMM-yyyy",
            "MMM d yyyy", "MMMM d yyyy",
        };

        private static readonly string[] DateFormatsWithoutYear =
        {
            "d/M", "d-M", "d.M", "d MMM", "d-MMM", "d MMMM", "MMM d", "MMMM d",
        };

        /// <summary>
        /// Reads a statement PDF. Throws <see cref="PdfStatementPasswordException"/> when the file is
        /// locked and <see cref="InvalidDataException"/> when there is nothing in it we can import.
        /// </summary>
        public static CSVFile Import(string filePath, string password = null)
        {
            List<Line> lines = ReadLines(filePath, password);

            if (lines.Count == 0)
                throw new InvalidDataException(
                    "No text could be read from this PDF. Scans and photos of a statement will not work - download the statement as a PDF from your bank instead.");

            int fallbackYear = GuessStatementYear(lines);

            CSVFile csv = TableFromHeader(lines, fallbackYear);
            if (csv == null)
                csv = TableFromLineShape(lines, fallbackYear);

            if (csv == null)
                throw new InvalidDataException(
                    "No transactions could be found in this PDF. If your bank can export the statement as a csv, that will import more reliably.");

            return csv;
        }

        #region reading the page

        /// <summary>a word and where it sits on the page</summary>
        private class PositionedWord
        {
            public string Text;
            public double Left, Right, Middle, Height;
            public double Centre => (Left + Right) / 2;
        }

        /// <summary>the words that share a baseline, left to right</summary>
        private class Line
        {
            public List<PositionedWord> Words = new List<PositionedWord>();
            public double Middle;
            public int Page;
            public double Height => Words.Average(w => w.Height);
            public string Text => string.Join(" ", Words.Select(w => w.Text));
        }

        private static List<Line> ReadLines(string filePath, string password)
        {
            List<Line> lines = new List<Line>();

            using (PdfDocument doc = OpenDocument(filePath, password))
            {
                foreach (PdfPage page in doc.GetPages())
                {
                    List<PositionedWord> words = new List<PositionedWord>();
                    foreach (Word w in page.GetWords())
                    {
                        string text = w.Text == null ? null : w.Text.Trim();
                        if (string.IsNullOrEmpty(text))
                            continue;

                        words.Add(new PositionedWord
                        {
                            Text = text,
                            Left = Math.Min(w.BoundingBox.Left, w.BoundingBox.Right),
                            Right = Math.Max(w.BoundingBox.Left, w.BoundingBox.Right),
                            Middle = (w.BoundingBox.Top + w.BoundingBox.Bottom) / 2,
                            Height = Math.Max(Math.Abs(w.BoundingBox.Height), 1),
                        });
                    }

                    List<Line> pageLines = GroupIntoLines(words);
                    foreach (Line l in pageLines)
                        l.Page = page.Number;

                    lines.AddRange(pageLines);
                }
            }

            return lines;
        }

        private static PdfDocument OpenDocument(string filePath, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(password))
                    return PdfDocument.Open(filePath);

                return PdfDocument.Open(filePath, new ParsingOptions { Password = password, UseLenientParsing = true });
            }
            catch (PdfDocumentEncryptedException)
            {
                throw new PdfStatementPasswordException(string.IsNullOrEmpty(password)
                    ? "This statement is password protected."
                    : "That password did not open the statement.");
            }
        }

        /// <summary>groups words onto the line they were printed on, top of the page first</summary>
        private static List<Line> GroupIntoLines(List<PositionedWord> words)
        {
            List<Line> lines = new List<Line>();

            foreach (PositionedWord w in words.OrderByDescending(x => x.Middle))
            {
                Line line = lines.Count > 0 ? lines[lines.Count - 1] : null;
                double tolerance = Math.Max(1.5, w.Height * 0.5);

                if (line != null && Math.Abs(line.Middle - w.Middle) <= tolerance)
                    line.Words.Add(w);
                else
                {
                    line = new Line { Middle = w.Middle };
                    line.Words.Add(w);
                    lines.Add(line);
                }
            }

            foreach (Line l in lines)
                l.Words.Sort((a, b) => a.Left.CompareTo(b.Left));

            return lines;
        }

        #endregion

        #region statements with a table header

        /// <summary>a run of words with a gap either side of it - one heading, or one column of one row</summary>
        private class Cell
        {
            public string Text;
            public double Left, Right;
        }

        private static CSVFile TableFromHeader(List<Line> lines, int fallbackYear)
        {
            int headerIndex = -1;
            List<Cell> headings = null;

            for (int i = 0; i < lines.Count; i++)
            {
                string text = lines[i].Text.ToLowerInvariant();
                if (!text.Contains("date"))
                    continue;

                if (!DetailHeaders.Any(h => text.Contains(h)))
                    continue;

                List<Cell> cells = SplitIntoCells(lines[i]);
                if (cells.Count < 3) //not a table, just a sentence with the word date in it
                    continue;

                headerIndex = i;
                headings = cells;
                break;
            }

            if (headerIndex == -1)
                return null;

            double[] boundaries = new double[headings.Count - 1];
            for (int i = 0; i < boundaries.Length; i++)
                boundaries[i] = (headings[i].Right + headings[i + 1].Left) / 2;

            //split everything below the header, then work out which column holds the dates
            List<string[]> split = new List<string[]>();
            List<Line> bodyLines = new List<Line>();
            string headerText = Normalise(lines[headerIndex].Text);

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (Normalise(lines[i].Text) == headerText) //the header repeats on every page
                    continue;

                split.Add(SplitByColumns(lines[i], boundaries));
                bodyLines.Add(lines[i]);
            }

            int dateColumn = -1, dateHits = 0;
            for (int c = 0; c < headings.Count; c++)
            {
                int hits = split.Count(cells => DateCellPattern.IsMatch(cells[c]));
                if (hits > dateHits)
                {
                    dateHits = hits;
                    dateColumn = c;
                }
            }

            if (dateColumn == -1) //nothing under the header reads as a date, so the header was a red herring
                return null;

            int detailColumn = DetailColumn(headings, dateColumn);
            bool[] moneyColumns = MoneyColumns(split, headings);

            List<string[]> rows = new List<string[]>();
            List<DateTime> dates = new List<DateTime>();
            List<bool> datesHadYear = new List<bool>();
            int lastRowLine = -1;

            for (int i = 0; i < split.Count; i++)
            {
                string[] cells = split[i];
                DateTime date;
                bool hadYear;

                if (TryParseDate(cells[dateColumn], fallbackYear, out date, out hadYear))
                {
                    TidyOverflow(cells, moneyColumns, detailColumn);
                    rows.Add(cells);
                    dates.Add(date);
                    datesHadYear.Add(hadYear);
                    lastRowLine = i;
                    continue;
                }

                //a description too long for one line carries straight on underneath, with nothing but
                //description on it - anything else is a page heading or a footer, not part of the row
                if (rows.Count > 0 && detailColumn != -1 && i == lastRowLine + 1
                    && cells[detailColumn].Length > 0 && OnlyFilledColumn(cells) == detailColumn
                    && bodyLines[i].Page == bodyLines[lastRowLine].Page
                    && bodyLines[lastRowLine].Middle - bodyLines[i].Middle < bodyLines[i].Height * 2.5)
                {
                    string[] last = rows[rows.Count - 1];
                    last[detailColumn] = (last[detailColumn] + " " + cells[detailColumn]).Trim();
                    lastRowLine = i; //a description can wrap over more than one line
                }
            }

            if (rows.Count == 0)
                return null;

            ResolveMissingYears(dates, datesHadYear);
            for (int i = 0; i < rows.Count; i++)
                rows[i][dateColumn] = dates[i].ToString("dd/MM/yyyy");

            string[] header = new string[headings.Count];
            for (int i = 0; i < headings.Count; i++)
                header[i] = headings[i].Text.Length > 0 ? headings[i].Text : "Column " + (i + 1);

            return Build(header, rows);
        }

        /// <summary>headings that only ever sit over a column of amounts</summary>
        private static readonly string[] MoneyHeaders =
            { "paid in", "paid out", "money in", "money out", "credit", "debit", "amount", "balance",
              "withdraw", "deposit", "receipt" };

        /// <summary>
        /// The columns that hold amounts rather than words, going on both the heading and what is
        /// actually printed underneath it.
        /// </summary>
        private static bool[] MoneyColumns(List<string[]> split, List<Cell> headings)
        {
            bool[] money = new bool[headings.Count];

            for (int c = 0; c < headings.Count; c++)
            {
                string heading = headings[c].Text.ToLowerInvariant();
                if (MoneyHeaders.Any(h => heading.Contains(h)))
                {
                    money[c] = true;
                    continue;
                }

                int filled = split.Count(cells => cells[c].Length > 0);
                if (filled == 0)
                    continue;

                money[c] = split.Count(cells => MoneyPattern.IsMatch(cells[c])) * 2 >= filled;
            }

            return money;
        }

        /// <summary>
        /// A description wider than its column runs on into the one beside it, which puts words in a
        /// money column. Anything in a money column that is not an amount belongs to the description.
        /// </summary>
        private static void TidyOverflow(string[] cells, bool[] moneyColumns, int detailColumn)
        {
            if (detailColumn == -1)
                return;

            for (int c = 0; c < cells.Length; c++)
            {
                if (!moneyColumns[c] || cells[c].Length == 0 || MoneyPattern.IsMatch(cells[c]))
                    continue;

                cells[detailColumn] = (cells[detailColumn] + " " + cells[c]).Trim();
                cells[c] = string.Empty;
            }
        }

        /// <summary>the only column with anything in it, or -1 when it is none or more than one</summary>
        private static int OnlyFilledColumn(string[] cells)
        {
            int filled = -1;

            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c].Length == 0)
                    continue;

                if (filled != -1)
                    return -1;

                filled = c;
            }

            return filled;
        }

        /// <summary>the column holding the payment reference - the one the customer is matched on</summary>
        private static int DetailColumn(List<Cell> headings, int dateColumn)
        {
            for (int i = 0; i < headings.Count; i++)
            {
                if (i == dateColumn)
                    continue;

                string text = headings[i].Text.ToLowerInvariant();
                if (DetailHeaders.Any(h => text.Contains(h)))
                    return i;
            }

            return -1;
        }

        /// <summary>breaks a line where the gap between words is wide enough to be a column gap</summary>
        private static List<Cell> SplitIntoCells(Line line)
        {
            List<Cell> cells = new List<Cell>();
            double gapLimit = Math.Max(3.0, line.Words.Average(w => w.Height) * 0.9);

            foreach (PositionedWord w in line.Words)
            {
                Cell current = cells.Count > 0 ? cells[cells.Count - 1] : null;

                if (current != null && w.Left - current.Right <= gapLimit)
                {
                    current.Text += " " + w.Text;
                    current.Right = Math.Max(current.Right, w.Right);
                }
                else
                    cells.Add(new Cell { Text = w.Text, Left = w.Left, Right = w.Right });
            }

            return cells;
        }

        /// <summary>drops each word of a line into the column its centre falls in</summary>
        private static string[] SplitByColumns(Line line, double[] boundaries)
        {
            string[] cells = new string[boundaries.Length + 1];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = string.Empty;

            foreach (PositionedWord w in line.Words)
            {
                int c = 0;
                while (c < boundaries.Length && w.Centre > boundaries[c])
                    c++;

                cells[c] = cells[c].Length == 0 ? w.Text : cells[c] + " " + w.Text;
            }

            return cells;
        }

        #endregion

        #region statements with no table header

        /// <summary>
        /// Last resort: read every line that starts with a date as date, description and then one
        /// column per money amount on the line. Which of those amounts is the credit varies by bank,
        /// so they are all handed over and the user picks the right one in the viewer.
        /// </summary>
        private static CSVFile TableFromLineShape(List<Line> lines, int fallbackYear)
        {
            List<List<string>> rows = new List<List<string>>();
            List<DateTime> dates = new List<DateTime>();
            List<bool> datesHadYear = new List<bool>();
            int mostAmounts = 0;

            foreach (Line line in lines)
            {
                string text = Normalise(line.Text);
                Match dateMatch = DateStartPattern.Match(text);
                if (!dateMatch.Success)
                    continue;

                DateTime date;
                bool hadYear;
                if (!TryParseDate(dateMatch.Value, fallbackYear, out date, out hadYear))
                    continue;

                dates.Add(date);
                datesHadYear.Add(hadYear);

                string rest = text.Substring(dateMatch.Length).Trim();
                MatchCollection amounts = MoneyPattern.Matches(rest);

                List<string> row = new List<string>();
                row.Add(date.ToString("dd/MM/yyyy"));
                row.Add(amounts.Count > 0 ? rest.Substring(0, amounts[0].Index).Trim() : rest);

                foreach (Match m in amounts)
                    row.Add(m.Value.Trim());

                if (amounts.Count > mostAmounts)
                    mostAmounts = amounts.Count;

                rows.Add(row);
            }

            if (rows.Count == 0)
                return null;

            ResolveMissingYears(dates, datesHadYear);
            for (int i = 0; i < rows.Count; i++)
                rows[i][0] = dates[i].ToString("dd/MM/yyyy");

            List<string> header = new List<string> { "Date", "Reference" };
            for (int i = 0; i < mostAmounts; i++)
                header.Add(mostAmounts == 1 ? "Amount" : "Amount " + (i + 1));

            List<string[]> padded = new List<string[]>();
            foreach (List<string> row in rows)
            {
                while (row.Count < header.Count)
                    row.Add(string.Empty);

                padded.Add(row.ToArray());
            }

            return Build(header.ToArray(), padded);
        }

        #endregion

        #region dates and general tidying

        /// <summary>
        /// Statements print dates every way there is, and rows often leave the year off it entirely.
        /// Everything imported is written back out as dd/MM/yyyy, which is the only shape
        /// <see cref="UsfulFuctions.StringToDateTime"/> understands.
        /// </summary>
        public static bool TryParseDate(string text, int fallbackYear, out DateTime date)
        {
            bool hadYear;
            return TryParseDate(text, fallbackYear, out date, out hadYear);
        }

        public static bool TryParseDate(string text, int fallbackYear, out DateTime date, out bool hadYear)
        {
            date = DateTime.MinValue;
            hadYear = false;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            //"1st April" -> "1 April", and any double spacing left by the word grouping
            text = Regex.Replace(Normalise(text), @"(\d)(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);
            text = text.Replace(",", " ").Trim();
            text = Regex.Replace(text, @"\s+", " ");

            if (DateTime.TryParseExact(text, DateFormatsWithYear, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
            {
                hadYear = true;
                return true;
            }

            if (DateTime.TryParseExact(text, DateFormatsWithoutYear, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
            {
                try
                {
                    date = new DateTime(fallbackYear, date.Month, date.Day);
                    return true;
                }
                catch //29 february in a year that has none
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Rows that print no year take it from the statement itself. The latest year printed anywhere
        /// on it is the one the statement ends in - earlier rows are walked back from there.
        /// </summary>
        private static int GuessStatementYear(List<Line> lines)
        {
            int latest = 0;

            foreach (Line line in lines)
                foreach (Match m in YearPattern.Matches(line.Text))
                {
                    int year = Convert.ToInt32(m.Value);
                    if (year < 1990 || year > DateTime.Today.Year) //a statement is never about the future
                        continue;

                    if (year > latest)
                        latest = year;
                }

            return latest == 0 ? DateTime.Today.Year : latest;
        }

        /// <summary>
        /// A statement running over a new year prints "28 Dec" and then "04 Jan" with no year on either.
        /// Rows are in date order, so working back from the last one, a date that would land after the
        /// row below it belongs to the year before.
        /// </summary>
        private static void ResolveMissingYears(List<DateTime> dates, List<bool> hadYear)
        {
            DateTime? next = null;

            for (int i = dates.Count - 1; i >= 0; i--)
            {
                if (!hadYear[i])
                {
                    DateTime date = dates[i];

                    if (next.HasValue)
                    {
                        date = SafeDate(next.Value.Year, date.Month, date.Day, date);
                        if (date > next.Value)
                            date = SafeDate(date.Year - 1, date.Month, date.Day, date);
                    }

                    if (date > DateTime.Today)
                        date = SafeDate(date.Year - 1, date.Month, date.Day, date);

                    dates[i] = date;
                }

                next = dates[i];
            }
        }

        private static DateTime SafeDate(int year, int month, int day, DateTime fallback)
        {
            try
            {
                return new DateTime(year, month, day);
            }
            catch //29 february in a year that has none
            {
                return fallback;
            }
        }

        private static string Normalise(string text)
        {
            return Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
        }

        private static CSVFile Build(string[] header, List<string[]> rows)
        {
            CSVFile csv = new CSVFile();
            csv.Header = header;
            csv.data = rows.ToArray();
            return csv;
        }

        #endregion
    }
}
