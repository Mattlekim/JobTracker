using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UiInterface.ImportExport
{
    /// <summary>
    /// One customer row parsed from a round sheet. Neutral of any app
    /// types so the parser can be tested standalone.
    /// </summary>
    public class ImportedCustomerRow
    {
        public string Street = string.Empty;
        public string HouseNumber = string.Empty;
        public string Name = string.Empty;
        public string PriceText = string.Empty;   // raw PRICE cell text
        public float? Price;                      // parsed when numeric
        public string FrontPriceText = string.Empty;
        public string PaymentType = string.Empty; // raw PT cell (B / C / ...)
        public string FrequencyText = string.Empty;
        public string Notes = string.Empty;
        public bool Tnb;                          // grey house number cell
        public DateTime? LastCleaned;             // most recent cleaned column
        public int SourceRow;
    }

    /// <summary>
    /// Reads a window-cleaning round spreadsheet (.xlsx). Expected layout:
    /// row 1 headers (Street/PRICE/Front/Name/PT/Freq/Notes then repeated
    /// 'Cleaned' columns), row 2 holds the date of each Cleaned column,
    /// rows below are either a street-name header row or a customer row
    /// whose column A is the house number. A grey fill on the house number
    /// means the customer is TNB (text night before).
    ///
    /// Implemented directly over the xlsx zip/XML so no extra NuGet
    /// packages are needed on Android.
    /// </summary>
    public static class RoundSheetParser
    {
        static readonly XNamespace NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public static List<ImportedCustomerRow> Parse(Stream xlsxStream)
        {
            using ZipArchive zip = new ZipArchive(xlsxStream, ZipArchiveMode.Read, leaveOpen: true);

            List<string> sharedStrings = ReadSharedStrings(zip);
            List<bool> styleIsGrey = ReadGreyStyles(zip);
            XDocument sheet = LoadFirstSheet(zip);

            // cells[row][col] -> (text, styleIndex, isNumeric, numericValue)
            var rows = new SortedDictionary<int, Dictionary<int, ParsedCell>>();
            foreach (XElement rowEl in sheet.Descendants(NsMain + "row"))
            {
                foreach (XElement cellEl in rowEl.Elements(NsMain + "c"))
                {
                    ParsedCell cell = ParseCell(cellEl, sharedStrings);
                    if (cell == null)
                        continue;
                    (int r, int c) = CellRef(cellEl.Attribute("r")?.Value);
                    if (r <= 0 || c <= 0)
                        continue;
                    if (!rows.TryGetValue(r, out var cols))
                        rows[r] = cols = new Dictionary<int, ParsedCell>();
                    cols[c] = cell;
                }
            }

            return BuildRows(rows, styleIsGrey);
        }

        class ParsedCell
        {
            public string Text = string.Empty;
            public int StyleIndex = -1;
            public bool IsNumeric;
            public double Numeric;
        }

        static List<ImportedCustomerRow> BuildRows(
            SortedDictionary<int, Dictionary<int, ParsedCell>> rows, List<bool> styleIsGrey)
        {
            var result = new List<ImportedCustomerRow>();
            if (!rows.TryGetValue(1, out var headerRow))
                return result;

            int colPrice = -1, colFront = -1, colName = -1, colPt = -1, colFreq = -1, colNotes = -1;
            var cleanedCols = new List<int>();
            foreach (var kv in headerRow)
            {
                string h = kv.Value.Text.Trim().ToLowerInvariant();
                if (h == "price") colPrice = kv.Key;
                else if (h == "front") colFront = kv.Key;
                else if (h == "name") colName = kv.Key;
                else if (h == "pt") colPt = kv.Key;
                else if (h.StartsWith("freq")) colFreq = kv.Key;
                else if (h == "notes") colNotes = kv.Key;
                else if (h.Contains("clean")) cleanedCols.Add(kv.Key);
            }

            // row 2 carries the date of each cleaned column
            var cleanedDates = new Dictionary<int, DateTime>();
            if (rows.TryGetValue(2, out var dateRow))
                foreach (int col in cleanedCols)
                    if (dateRow.TryGetValue(col, out ParsedCell c) && c.IsNumeric)
                        cleanedDates[col] = FromExcelSerial(c.Numeric);

            string currentStreet = string.Empty;
            foreach (var kv in rows)
            {
                int rowNum = kv.Key;
                if (rowNum <= 2)
                    continue;
                var cols = kv.Value;
                if (!cols.TryGetValue(1, out ParsedCell colA) || colA.Text.Trim().Length == 0)
                    continue;

                string a = colA.Text.Trim();
                bool hasDigit = a.Any(char.IsDigit);
                if (!hasDigit)
                {
                    currentStreet = a;
                    continue;
                }
                if (currentStreet.Length == 0)
                    continue;

                var row = new ImportedCustomerRow
                {
                    SourceRow = rowNum,
                    Street = currentStreet,
                    HouseNumber = a,
                    Tnb = colA.StyleIndex >= 0 && colA.StyleIndex < styleIsGrey.Count && styleIsGrey[colA.StyleIndex],
                    Name = TextAt(cols, colName),
                    PriceText = TextAt(cols, colPrice),
                    FrontPriceText = TextAt(cols, colFront),
                    PaymentType = TextAt(cols, colPt),
                    FrequencyText = TextAt(cols, colFreq),
                    Notes = TextAt(cols, colNotes),
                };

                if (colPrice > 0 && cols.TryGetValue(colPrice, out ParsedCell priceCell) && priceCell.IsNumeric)
                    row.Price = (float)priceCell.Numeric;

                foreach (int col in cleanedCols)
                    if (cleanedDates.TryGetValue(col, out DateTime date)
                        && cols.TryGetValue(col, out ParsedCell mark)
                        && IsCleanedMarker(mark.Text)
                        && (row.LastCleaned == null || date > row.LastCleaned))
                        row.LastCleaned = date;

                result.Add(row);
            }
            return result;
        }

        static string TextAt(Dictionary<int, ParsedCell> cols, int col)
        {
            if (col > 0 && cols.TryGetValue(col, out ParsedCell c))
                return c.Text.Trim();
            return string.Empty;
        }

        /// <summary>'x' = cleaned, '/' = cleaned not paid, 'F' = front only;
        /// 'O' = missed. A bare payment ('£6') is not a clean.</summary>
        public static bool IsCleanedMarker(string text)
        {
            string t = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (t.Length == 0)
                return false;
            if (t.Contains('x') || t.Contains('/'))
                return true;
            return t.Contains('f') && !t.Contains('£');
        }

        public static string ExtractPhone(string notes)
        {
            if (string.IsNullOrEmpty(notes))
                return string.Empty;
            Match m = Regex.Match(notes, @"\b07\d{3}\s?\d{3}\s?\d{3}\b|\b07\d{3}\s?\d{6}\b");
            return m.Success ? m.Value : string.Empty;
        }

        public static (int amount, string unit) ParseFrequency(string text)
        {
            string t = (text ?? string.Empty).ToLowerInvariant();
            Match m = Regex.Match(t, @"\d+");
            int amount = m.Success ? int.Parse(m.Value) : 4;
            string unit = t.Contains("month") ? "month" : t.Contains("day") ? "day" : "week";
            return (amount, unit);
        }

        static DateTime FromExcelSerial(double serial)
            => new DateTime(1899, 12, 30).AddDays(serial);

        static ParsedCell ParseCell(XElement c, List<string> sharedStrings)
        {
            string type = c.Attribute("t")?.Value ?? "n";
            var cell = new ParsedCell();
            string styleAttr = c.Attribute("s")?.Value;
            if (styleAttr != null && int.TryParse(styleAttr, out int s))
                cell.StyleIndex = s;

            if (type == "inlineStr")
            {
                XElement isEl = c.Element(NsMain + "is");
                cell.Text = isEl == null ? string.Empty
                    : string.Concat(isEl.Descendants(NsMain + "t").Select(t => t.Value));
                return cell;
            }

            string v = c.Element(NsMain + "v")?.Value;
            if (v == null)
                return null;

            if (type == "s")
            {
                if (int.TryParse(v, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                    cell.Text = sharedStrings[idx];
            }
            else if (type == "str" || type == "b" || type == "e")
            {
                cell.Text = v;
            }
            else if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
            {
                cell.IsNumeric = true;
                cell.Numeric = num;
                cell.Text = num == Math.Floor(num) && Math.Abs(num) < 1e10
                    ? ((long)num).ToString(CultureInfo.InvariantCulture)
                    : num.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                cell.Text = v;
            }
            return cell;
        }

        static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            ZipArchiveEntry entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return list;
            using Stream s = entry.Open();
            XDocument doc = XDocument.Load(s);
            foreach (XElement si in doc.Root.Elements(NsMain + "si"))
                list.Add(string.Concat(si.Descendants(NsMain + "t").Select(t => t.Value)));
            return list;
        }

        /// <summary>For each cell style (cellXfs index), whether its fill is a
        /// grey/shaded solid - the sheet's marker for TNB customers.</summary>
        static List<bool> ReadGreyStyles(ZipArchive zip)
        {
            var result = new List<bool>();
            ZipArchiveEntry entry = zip.GetEntry("xl/styles.xml");
            if (entry == null)
                return result;
            using Stream s = entry.Open();
            XDocument doc = XDocument.Load(s);

            var fillIsGrey = new List<bool>();
            XElement fills = doc.Root.Element(NsMain + "fills");
            if (fills != null)
            {
                foreach (XElement fill in fills.Elements(NsMain + "fill"))
                {
                    XElement pattern = fill.Element(NsMain + "patternFill");
                    fillIsGrey.Add(pattern != null
                        && (pattern.Attribute("patternType")?.Value == "solid")
                        && IsGreyColor(pattern.Element(NsMain + "fgColor")));
                }
            }

            XElement cellXfs = doc.Root.Element(NsMain + "cellXfs");
            if (cellXfs != null)
            {
                foreach (XElement xf in cellXfs.Elements(NsMain + "xf"))
                {
                    string fillId = xf.Attribute("fillId")?.Value;
                    bool grey = fillId != null
                        && int.TryParse(fillId, out int f)
                        && f >= 0 && f < fillIsGrey.Count && fillIsGrey[f];
                    result.Add(grey);
                }
            }
            return result;
        }

        static bool IsGreyColor(XElement color)
        {
            if (color == null)
                return false;

            double tint = 0;
            string tintAttr = color.Attribute("tint")?.Value;
            if (tintAttr != null)
                double.TryParse(tintAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out tint);

            string theme = color.Attribute("theme")?.Value;
            if (theme != null)
            {
                // theme 0 is the white background, theme 1 the black text
                // colour; a tint towards the middle of either is a grey
                double lum = theme == "0" ? 255 * (1 + tint)
                           : theme == "1" ? 255 * tint
                           : -1;
                return lum >= 20 && lum <= 242;
            }

            string rgb = color.Attribute("rgb")?.Value;
            if (rgb != null && rgb.Length == 8
                && int.TryParse(rgb, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
            {
                int r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
                int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                int lum = (r + g + b) / 3;
                return max - min < 30 && lum >= 20 && lum <= 242;
            }

            if (color.Attribute("indexed")?.Value == "22") // classic grey 25%
                return true;

            return false;
        }

        static XDocument LoadFirstSheet(ZipArchive zip)
        {
            // resolve the first sheet listed in the workbook via its rels
            ZipArchiveEntry wbEntry = zip.GetEntry("xl/workbook.xml")
                ?? throw new InvalidDataException("Not a valid .xlsx file (no workbook).");
            XNamespace nsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

            string relId;
            using (Stream s = wbEntry.Open())
            {
                XDocument wb = XDocument.Load(s);
                XElement sheet = wb.Root.Element(NsMain + "sheets")?.Elements(NsMain + "sheet").FirstOrDefault()
                    ?? throw new InvalidDataException("Workbook contains no sheets.");
                relId = sheet.Attribute(nsRel + "id")?.Value;
            }

            string target = "worksheets/sheet1.xml";
            ZipArchiveEntry relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (relsEntry != null && relId != null)
            {
                using Stream s = relsEntry.Open();
                XDocument rels = XDocument.Load(s);
                XNamespace nsPkg = "http://schemas.openxmlformats.org/package/2006/relationships";
                foreach (XElement rel in rels.Root.Elements(nsPkg + "Relationship"))
                    if (rel.Attribute("Id")?.Value == relId)
                        target = rel.Attribute("Target")?.Value ?? target;
            }

            target = target.TrimStart('/');
            if (!target.StartsWith("xl/"))
                target = "xl/" + target;
            ZipArchiveEntry sheetEntry = zip.GetEntry(target)
                ?? throw new InvalidDataException($"Worksheet '{target}' not found in file.");
            using Stream sheetStream = sheetEntry.Open();
            return XDocument.Load(sheetStream);
        }

        static (int row, int col) CellRef(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef))
                return (0, 0);
            int col = 0, i = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
                col = col * 26 + (char.ToUpperInvariant(cellRef[i++]) - 'A' + 1);
            int.TryParse(cellRef.Substring(i), out int row);
            return (row, col);
        }
    }
}
