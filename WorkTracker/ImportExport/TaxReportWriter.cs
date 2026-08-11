using System.Globalization;
using System.IO.Compression;
using System.Text;
using Kernel;

namespace UiInterface.ImportExport
{
    /// <summary>
    /// Writes a tax year's figures to an .xlsx: the quarterly update totals
    /// in the boxes HMRC asks for, then every income and expense entry
    /// behind them. Made to be handed to an accountant or typed into
    /// whichever software files the quarterly update.
    ///
    /// Written directly as the xlsx zip/XML so no extra NuGet packages are
    /// needed on Android.
    /// </summary>
    public static class TaxReportWriter
    {
        public static void Write(Stream output, int taxYear, AccountingBasis basis, bool calendarQuarters)
        {
            List<TaxSummary> summaries = TaxSummary.BuildYear(taxYear, basis, calendarQuarters);

            using ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            AddEntry(zip, "[Content_Types].xml", ContentTypesXml);
            AddEntry(zip, "_rels/.rels", RelsXml);
            AddEntry(zip, "xl/workbook.xml", WorkbookXml);
            AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            AddEntry(zip, "xl/styles.xml", StylesXml);
            AddEntry(zip, "xl/worksheets/sheet1.xml", BuildSheet(taxYear, basis, calendarQuarters, summaries));
        }

        const int StyleBold = 1;
        const int StyleDate = 2;
        const int StyleMoney = 3;

        static string BuildSheet(int taxYear, AccountingBasis basis, bool calendarQuarters, List<TaxSummary> summaries)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            int row = 1;

            row = TextRow(sb, row, $"Tax year {TaxCalendar.YearName(taxYear)}", StyleBold);
            row = TextRow(sb, row, basis == AccountingBasis.Cash
                ? "Cash basis - income counted when the money was received"
                : "Accruals basis - income counted when the work was done");
            row = TextRow(sb, row, calendarQuarters
                ? "Calendar quarterly periods (elected with HMRC)"
                : "Standard quarterly periods");
            row = TextRow(sb, row, $"Produced {DateTime.Now.ToShortDateString()} by Work Tracker");
            row++;

            //---- the quarterly figures, one column per period ----
            List<HmrcExpenseCategory> boxes = summaries
                .SelectMany(s => s.ExpensesByCategory.Keys)
                .Distinct()
                .OrderBy(b => (int)b)
                .ToList();

            sb.Append($"<row r=\"{row}\">");
            AppendText(sb, row, 1, "Period", StyleBold);
            for (int i = 0; i < summaries.Count; i++)
                AppendText(sb, row, 2 + i, summaries[i].Period.Name, StyleBold);
            sb.Append("</row>");
            row++;

            row = PeriodRow(sb, row, "From", summaries, s => s.Period.Start.ToShortDateString());
            row = PeriodRow(sb, row, "To", summaries, s => s.Period.End.ToShortDateString());
            row = PeriodRow(sb, row, "Due with HMRC", summaries, s => s.Period.Due.ToShortDateString());
            row++;

            row = MoneyRow(sb, row, "Income (turnover)", summaries, s => s.Income, StyleBold);
            row++;

            row = TextRow(sb, row, "Expenses", StyleBold);
            foreach (HmrcExpenseCategory box in boxes)
                row = MoneyRow(sb, row, TaxCalendar.HmrcCategoryName(box), summaries,
                    s => s.ExpensesByCategory.TryGetValue(box, out float v) ? v : 0);

            row = MoneyRow(sb, row, "Total expenses", summaries, s => s.TotalExpenses, StyleBold);
            row++;
            row = MoneyRow(sb, row, "Profit", summaries, s => s.Profit, StyleBold);
            row++;

            row = TextRow(sb, row, "Expense categories are grouped into HMRC boxes as a starting point - check with your accountant before filing.");
            row++;

            //---- the entries behind the totals ----
            TaxPeriod year = TaxCalendar.WholeYear(taxYear);

            row = TextRow(sb, row, "Income", StyleBold);
            sb.Append($"<row r=\"{row}\">");
            AppendText(sb, row, 1, "Date", StyleBold);
            AppendText(sb, row, 2, "Customer", StyleBold);
            AppendText(sb, row, 3, "Address", StyleBold);
            AppendText(sb, row, 4, "Method / job", StyleBold);
            AppendText(sb, row, 5, "Reference", StyleBold);
            AppendText(sb, row, 6, "Amount", StyleBold);
            sb.Append("</row>");
            row++;

            if (basis == AccountingBasis.Cash)
            {
                foreach (Payment p in Payment.Query().Where(x => year.Contains(x.Date)).OrderBy(x => x.Date))
                {
                    Customer c = p.GetCustomer();
                    sb.Append($"<row r=\"{row}\">");
                    AppendNumber(sb, row, 1, ToExcelSerial(p.Date), StyleDate);
                    AppendText(sb, row, 2, c == null ? "Unidentified" : $"{c.FName} {c.SName}".Trim(), 0);
                    AppendText(sb, row, 3, c?.Address?.ToString() ?? string.Empty, 0);
                    AppendText(sb, row, 4, p.PaymentMethod.ToString(), 0);
                    AppendText(sb, row, 5, p.CustomerReference ?? string.Empty, 0);
                    AppendNumber(sb, row, 6, p.Amount, StyleMoney);
                    sb.Append("</row>");
                    row++;
                }
            }
            else
            {
                foreach (Job j in Job.Query()
                    .Where(x => x.IsCompleted && !x.HaveCanceled && year.Contains(x.DateCompleated))
                    .OrderBy(x => x.DateCompleated))
                {
                    Customer c = j.GetCustomer();
                    sb.Append($"<row r=\"{row}\">");
                    AppendNumber(sb, row, 1, ToExcelSerial(j.DateCompleated), StyleDate);
                    AppendText(sb, row, 2, c == null ? string.Empty : $"{c.FName} {c.SName}".Trim(), 0);
                    AppendText(sb, row, 3, j.JobFormattedString, 0);
                    AppendText(sb, row, 4, string.IsNullOrWhiteSpace(j.Name) ? "Job" : j.Name, 0);
                    AppendText(sb, row, 5, j.IsPaidFor ? "Paid" : "Unpaid", 0);
                    AppendNumber(sb, row, 6, j.EffectivePrice, StyleMoney);
                    sb.Append("</row>");
                    row++;
                }
            }

            row++;
            row = TextRow(sb, row, "Expenses", StyleBold);
            sb.Append($"<row r=\"{row}\">");
            AppendText(sb, row, 1, "Date", StyleBold);
            AppendText(sb, row, 2, "Shop / supplier", StyleBold);
            AppendText(sb, row, 3, "Category", StyleBold);
            AppendText(sb, row, 4, "HMRC box", StyleBold);
            AppendText(sb, row, 5, "Receipt", StyleBold);
            AppendText(sb, row, 6, "Amount", StyleBold);
            AppendText(sb, row, 7, "Notes", StyleBold);
            sb.Append("</row>");
            row++;

            foreach (Expense e in Expense.Query().Where(x => year.Contains(x.Date)).OrderBy(x => x.Date))
            {
                sb.Append($"<row r=\"{row}\">");
                AppendNumber(sb, row, 1, ToExcelSerial(e.Date), StyleDate);
                AppendText(sb, row, 2, e.FormattedMerchant, 0);
                AppendText(sb, row, 3, e.Category.ToString(), 0);
                AppendText(sb, row, 4, TaxCalendar.HmrcCategoryName(TaxCalendar.HmrcCategoryFor(e.Category)), 0);
                AppendText(sb, row, 5, e.HasReceipt ? "Yes" : "No", 0);
                AppendNumber(sb, row, 6, e.Amount, StyleMoney);
                AppendText(sb, row, 7, (e.Notes ?? string.Empty).Replace("\r", " ").Replace("\n", " "), 0);
                sb.Append("</row>");
                row++;
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        static int TextRow(StringBuilder sb, int row, string text, int style = 0)
        {
            sb.Append($"<row r=\"{row}\">");
            AppendText(sb, row, 1, text, style);
            sb.Append("</row>");
            return row + 1;
        }

        static int PeriodRow(StringBuilder sb, int row, string label, List<TaxSummary> summaries, Func<TaxSummary, string> value)
        {
            sb.Append($"<row r=\"{row}\">");
            AppendText(sb, row, 1, label, 0);
            for (int i = 0; i < summaries.Count; i++)
                AppendText(sb, row, 2 + i, value(summaries[i]), 0);
            sb.Append("</row>");
            return row + 1;
        }

        static int MoneyRow(StringBuilder sb, int row, string label, List<TaxSummary> summaries, Func<TaxSummary, float> value, int labelStyle = 0)
        {
            sb.Append($"<row r=\"{row}\">");
            AppendText(sb, row, 1, label, labelStyle);
            for (int i = 0; i < summaries.Count; i++)
                AppendNumber(sb, row, 2 + i, value(summaries[i]), StyleMoney);
            sb.Append("</row>");
            return row + 1;
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
            "<sheets><sheet name=\"Tax\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        const string WorkbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        //style 1 = bold, 2 = date, 3 = money to 2dp
        const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"0.00\"/></numFmts>" +
            "<fonts count=\"2\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"4\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            "<xf numFmtId=\"14\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "</cellXfs>" +
            "</styleSheet>";
    }
}
