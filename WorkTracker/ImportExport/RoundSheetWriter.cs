using System.Globalization;
using System.IO.Compression;
using System.Text;
using Kernel;

namespace UiInterface.ImportExport
{
    /// <summary>
    /// Writes the round to an .xlsx in the same layout RoundSheetParser
    /// reads (street header rows, house number in column A - grey filled
    /// when TNB - then PRICE / Name / PT / Freq / Notes and dated Cleaned
    /// columns), so an exported sheet can be imported again.
    ///
    /// Written directly as the xlsx zip/XML so no extra NuGet packages are
    /// needed on Android.
    /// </summary>
    public static class RoundSheetWriter
    {
        public static void Write(Stream output, List<Job> allJobs)
        {
            //one row per active job chain (the newest instance)
            List<Job> current = allJobs.FindAll(x => x.JobNextId == -1 && !x.HaveCanceled);

            //most recent completed date per customer, giving the Cleaned columns
            var lastCleaned = new Dictionary<int, DateTime>();
            foreach (Job j in allJobs)
                if (j.IsCompleted && j.DateCompleated.Year > 2001)
                    if (!lastCleaned.TryGetValue(j.CustomerId, out DateTime d) || j.DateCompleated.Date > d)
                        lastCleaned[j.CustomerId] = j.DateCompleated.Date;

            List<DateTime> cleanedDates = lastCleaned.Values.Distinct().OrderBy(x => x).ToList();
            if (cleanedDates.Count > 20) //keep the sheet a sane width
                cleanedDates = cleanedDates.Skip(cleanedDates.Count - 20).ToList();

            using ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            AddEntry(zip, "[Content_Types].xml", ContentTypesXml);
            AddEntry(zip, "_rels/.rels", RelsXml);
            AddEntry(zip, "xl/workbook.xml", WorkbookXml);
            AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            AddEntry(zip, "xl/styles.xml", StylesXml);
            AddEntry(zip, "xl/worksheets/sheet1.xml", BuildSheet(current, lastCleaned, cleanedDates));
        }

        static string BuildSheet(List<Job> jobs, Dictionary<int, DateTime> lastCleaned, List<DateTime> cleanedDates)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            int row = 1;

            //row 1 - headers (the importer looks these up by name)
            sb.Append($"<row r=\"{row}\">");
            string[] headers = { "Street", "PRICE", "Front", "Name", "PT", "Freq", "Notes" };
            for (int i = 0; i < headers.Length; i++)
                AppendText(sb, row, i + 1, headers[i], 0);
            for (int i = 0; i < cleanedDates.Count; i++)
                AppendText(sb, row, headers.Length + 1 + i, "Cleaned", 0);
            sb.Append("</row>");
            row++;

            //row 2 - the date of each Cleaned column
            sb.Append($"<row r=\"{row}\">");
            for (int i = 0; i < cleanedDates.Count; i++)
                AppendNumber(sb, row, 8 + i, ToExcelSerial(cleanedDates[i]), 2);
            sb.Append("</row>");
            row++;

            foreach (var street in jobs
                .Where(x => x.Address != null && !string.IsNullOrWhiteSpace(x.Address.Street))
                .GroupBy(x => $"{x.Address.Street}|{x.Address.City}|{x.Address.Area}", StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.First().Address.Street, StringComparer.OrdinalIgnoreCase))
            {
                //street header row - column A with no digits
                sb.Append($"<row r=\"{row}\">");
                AppendText(sb, row, 1, street.First().Address.Street, 0);
                sb.Append("</row>");
                row++;

                foreach (Job j in street.OrderBy(x => HouseSortKey(x.Address.PropertyNameNumber))
                                        .ThenBy(x => x.Address.PropertyNameNumber, StringComparer.OrdinalIgnoreCase))
                {
                    Customer c = j.GetCustomer();
                    sb.Append($"<row r=\"{row}\">");
                    AppendText(sb, row, 1, j.Address.PropertyNameNumber, j.TNB ? 1 : 0);
                    AppendNumber(sb, row, 2, j.Price, 0);
                    //column 3 (Front) intentionally empty
                    AppendText(sb, row, 4, $"{c?.FName} {c?.SName}".Trim(), 0);
                    AppendText(sb, row, 5, PtLetter(c), 0);
                    AppendText(sb, row, 6, FreqText(j), 0);
                    AppendText(sb, row, 7, (j.Notes ?? string.Empty).Replace("\r", " ").Replace("\n", " "), 0);

                    if (lastCleaned.TryGetValue(j.CustomerId, out DateTime cleaned))
                    {
                        int col = cleanedDates.IndexOf(cleaned);
                        if (col >= 0)
                            AppendText(sb, row, 8 + col, "x", 0);
                    }
                    sb.Append("</row>");
                    row++;
                }
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        static long HouseSortKey(string houseNumber)
        {
            string digits = new string((houseNumber ?? string.Empty).TakeWhile(char.IsDigit).ToArray());
            return long.TryParse(digits, out long n) ? n : long.MaxValue;
        }

        static string PtLetter(Customer c)
        {
            if (c == null)
                return string.Empty;
            return c.NormalPaymentMethord switch
            {
                PaymentMethod.Bank => "B",
                PaymentMethod.Cash => "C",
                PaymentMethod.Paypal => "PP",
                _ => string.Empty,
            };
        }

        static string FreqText(Job j)
        {
            if (j.Frequence > 0)
                return $"{j.Frequence} weekly";
            if (j.Frequence < 0)
                return $"{-j.Frequence} monthly";
            return string.Empty;
        }

        static void AppendText(StringBuilder sb, int row, int col, string text, int style)
        {
            if (string.IsNullOrEmpty(text))
                return;
            sb.Append($"<c r=\"{ColLetter(col)}{row}\" t=\"inlineStr\"{StyleAttr(style)}><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>");
        }

        static void AppendNumber(StringBuilder sb, int row, int col, double value, int style)
        {
            sb.Append($"<c r=\"{ColLetter(col)}{row}\"{StyleAttr(style)}><v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>");
        }

        static string StyleAttr(int style) => style > 0 ? $" s=\"{style}\"" : string.Empty;

        static string ColLetter(int col)
        {
            string s = string.Empty;
            while (col > 0)
            {
                col--;
                s = (char)('A' + col % 26) + s;
                col /= 26;
            }
            return s;
        }

        static string Escape(string text)
            => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        static double ToExcelSerial(DateTime date)
            => (date - new DateTime(1899, 12, 30)).TotalDays;

        static void AddEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using Stream s = entry.Open();
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            s.Write(bytes, 0, bytes.Length);
        }

        const string ContentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        const string RelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        const string WorkbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Round\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        const string WorkbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        //style 1 = grey fill (the importer's TNB marker), style 2 = date format
        const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"3\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFBFBFBF\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"3\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFill=\"1\"/>" +
            "<xf numFmtId=\"14\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "</cellXfs>" +
            "</styleSheet>";
    }
}
