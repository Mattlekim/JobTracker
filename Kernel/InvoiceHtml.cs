using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel
{
    /// <summary>
    /// Turns an invoice into a self-contained HTML page - the whole thing in
    /// one file, logo and all, nothing fetched from anywhere. That is what
    /// makes it work everywhere the app runs and everywhere it is sent: it
    /// opens in any browser, prints to PDF, and can be emailed or saved
    /// without a pdf library the app deliberately does without (it hand-writes
    /// its spreadsheets over the zip for the same reason).
    ///
    /// It is kept in the kernel, and reads its bytes rather than a file path,
    /// so the self test can build an invoice and check the figures come out on
    /// it.
    /// </summary>
    public static class InvoiceHtml
    {
        /// <summary>
        /// the invoice as a full HTML document. the business header is read off
        /// <see cref="BusinessInfo"/>; the logo is embedded from its bytes when
        /// there are any
        /// </summary>
        public static string Build(Invoice invoice)
        {
            if (invoice == null)
                return string.Empty;

            byte[] logo = BusinessInfo.LogoBytes();
            return Build(invoice, logo);
        }

        /// <summary>
        /// the same, with the logo bytes passed in rather than read off disk -
        /// the self test has no logo file and the app can pass what it likes
        /// </summary>
        public static string Build(Invoice invoice, byte[] logoBytes)
        {
            if (invoice == null)
                return string.Empty;

            StringBuilder s = new StringBuilder();

            s.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
            s.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            s.Append("<title>Invoice ").Append(Escape(invoice.FormattedNumber)).Append("</title>");
            s.Append("<style>").Append(Css()).Append("</style>");
            s.Append("</head><body><div class=\"page\">");

            //  header: the business on the left, INVOICE and its number on the right
            s.Append("<div class=\"head\">");
            s.Append("<div class=\"from\">");
            if (logoBytes != null && logoBytes.Length > 0)
                s.Append("<img class=\"logo\" alt=\"logo\" src=\"data:image/jpeg;base64,")
                 .Append(Convert.ToBase64String(logoBytes)).Append("\">");
            if (!string.IsNullOrWhiteSpace(BusinessInfo.Name))
                s.Append("<div class=\"bizname\">").Append(Escape(BusinessInfo.Name)).Append("</div>");
            s.Append("<div class=\"bizlines\">");
            AppendMultiline(s, BusinessInfo.Address);
            AppendContactLine(s, "Tel", BusinessInfo.Phone);
            AppendContactLine(s, "Email", BusinessInfo.Email);
            AppendContactLine(s, "Web", BusinessInfo.Website);
            AppendContactLine(s, "VAT/UTR", BusinessInfo.TaxNumber);
            s.Append("</div>");
            s.Append("</div>");

            s.Append("<div class=\"title\"><div class=\"word\">INVOICE</div>");
            s.Append("<table class=\"meta\"><tr><td class=\"k\">Invoice no.</td><td class=\"v\">")
             .Append(Escape(invoice.FormattedNumber)).Append("</td></tr>");
            s.Append("<tr><td class=\"k\">Date</td><td class=\"v\">")
             .Append(Escape(invoice.FormattedDate)).Append("</td></tr>");
            if (invoice.DueDate > DateTime.MinValue)
                s.Append("<tr><td class=\"k\">Due</td><td class=\"v\">")
                 .Append(Escape(invoice.DueDate.ToString("d MMM yyyy"))).Append("</td></tr>");
            s.Append("</table>");
            //a plain PAID stamp when it is settled, so the state is obvious on
            //the page as well as in the app
            if (invoice.Paid)
                s.Append("<div class=\"paid\">PAID</div>");
            s.Append("</div>");
            s.Append("</div>");

            //  bill to
            s.Append("<div class=\"billto\"><div class=\"label\">Bill to</div>");
            if (!string.IsNullOrWhiteSpace(invoice.BillToName))
                s.Append("<div class=\"name\">").Append(Escape(invoice.BillToName)).Append("</div>");
            s.Append("<div class=\"addr\">");
            AppendMultiline(s, invoice.BillToAddress);
            s.Append("</div></div>");

            //  the lines. the date column only appears when a line has a date -
            //  an invoice for several cleans wants it, one for a single job
            //  does not
            bool anyDate = false;
            if (invoice.Lines != null)
                foreach (InvoiceLine line in invoice.Lines)
                    if (line.HasDate)
                    {
                        anyDate = true;
                        break;
                    }

            s.Append("<table class=\"lines\"><thead><tr>");
            if (anyDate)
                s.Append("<th class=\"date\">Date</th>");
            s.Append("<th class=\"desc\">Description</th>");
            s.Append("<th class=\"num\">Qty</th>");
            s.Append("<th class=\"num\">Unit</th>");
            s.Append("<th class=\"num\">Amount</th>");
            s.Append("</tr></thead><tbody>");

            if (invoice.Lines != null)
                foreach (InvoiceLine line in invoice.Lines)
                {
                    s.Append("<tr>");
                    if (anyDate)
                        s.Append("<td class=\"date\">")
                         .Append(line.HasDate ? Escape(line.Date.ToString("d MMM yyyy")) : string.Empty)
                         .Append("</td>");
                    s.Append("<td class=\"desc\">").Append(Escape(line.Description)).Append("</td>");
                    s.Append("<td class=\"num\">").Append(Number(line.Quantity)).Append("</td>");
                    s.Append("<td class=\"num\">").Append(Money(line.UnitPrice)).Append("</td>");
                    s.Append("<td class=\"num\">").Append(Money(line.LineTotal)).Append("</td>");
                    s.Append("</tr>");
                }

            //the total sits under the amount column, so it spans everything to
            //its left - one more column when the date is showing
            int spanToTotal = anyDate ? 4 : 3;
            s.Append("</tbody><tfoot><tr>");
            s.Append("<td class=\"desc total-l\" colspan=\"").Append(spanToTotal).Append("\">Total</td>");
            s.Append("<td class=\"num total-v\">").Append(Money(invoice.Total)).Append("</td>");
            s.Append("</tr></tfoot></table>");

            //  notes, how to pay, the footer line
            AppendBlock(s, "Notes", invoice.Notes);
            AppendBlock(s, "Payment", BusinessInfo.PaymentDetails);

            if (!string.IsNullOrWhiteSpace(BusinessInfo.FooterNote))
            {
                s.Append("<div class=\"footer\">");
                AppendMultiline(s, BusinessInfo.FooterNote);
                s.Append("</div>");
            }

            s.Append("</div></body></html>");
            return s.ToString();
        }

        /// <summary>a "Label: value" contact line, left off when there is no value</summary>
        private static void AppendContactLine(StringBuilder s, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            s.Append("<div>").Append(Escape(label)).Append(": ").Append(Escape(value.Trim())).Append("</div>");
        }

        /// <summary>a headed block of text, left off entirely when the text is blank</summary>
        private static void AppendBlock(StringBuilder s, string label, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            s.Append("<div class=\"block\"><div class=\"label\">").Append(Escape(label)).Append("</div><div>");
            AppendMultiline(s, text);
            s.Append("</div></div>");
        }

        /// <summary>text with its line breaks kept, each line escaped</summary>
        private static void AppendMultiline(StringBuilder s, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool first = true;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (!first)
                    s.Append("<br>");
                s.Append(Escape(line.Trim()));
                first = false;
            }
        }

        private static string Money(float amount)
        {
            return $"{Gloable.CurrenceSymbol}{amount:0.00}";
        }

        /// <summary>a quantity - a whole number shows without a decimal point</summary>
        private static string Number(float quantity)
        {
            if (quantity == (float)Math.Truncate(quantity))
                return ((long)quantity).ToString();
            return quantity.ToString("0.##");
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string Css()
        {
            //a plain, printable sheet - black on white, the way an invoice is
            //expected to look on paper and in an email. kept small and inline
            //so the file is self-contained
            return
                "*{box-sizing:border-box;}" +
                "body{margin:0;background:#f4f4f4;color:#222;font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:14px;}" +
                ".page{max-width:800px;margin:20px auto;background:#fff;padding:36px;box-shadow:0 1px 4px rgba(0,0,0,.15);}" +
                ".head{display:flex;justify-content:space-between;align-items:flex-start;gap:24px;}" +
                ".logo{max-width:180px;max-height:90px;display:block;margin-bottom:10px;}" +
                ".bizname{font-size:22px;font-weight:700;margin-bottom:4px;}" +
                ".bizlines div{color:#555;line-height:1.4;}" +
                ".title{text-align:right;}" +
                ".title .word{font-size:30px;font-weight:700;letter-spacing:2px;color:#1a9d68;margin-bottom:8px;}" +
                ".meta{margin-left:auto;border-collapse:collapse;}" +
                ".meta td{padding:2px 0 2px 12px;}" +
                ".meta .k{color:#777;text-align:right;}" +
                ".meta .v{font-weight:600;text-align:right;}" +
                ".paid{display:inline-block;margin-top:10px;padding:4px 14px;border:2px solid #1a9d68;color:#1a9d68;font-weight:700;letter-spacing:2px;border-radius:6px;}" +
                ".billto{margin:28px 0 20px;}" +
                ".label{text-transform:uppercase;font-size:11px;letter-spacing:1px;color:#999;margin-bottom:4px;}" +
                ".billto .name{font-weight:700;font-size:16px;}" +
                ".billto .addr div,.billto .addr{color:#444;line-height:1.4;}" +
                "table.lines{width:100%;border-collapse:collapse;margin-top:8px;}" +
                "table.lines th{text-align:left;border-bottom:2px solid #1a9d68;padding:8px 6px;font-size:12px;text-transform:uppercase;letter-spacing:.5px;color:#555;}" +
                "table.lines td{padding:8px 6px;border-bottom:1px solid #eee;vertical-align:top;}" +
                "table.lines .num{text-align:right;white-space:nowrap;}" +
                "table.lines .date{white-space:nowrap;color:#555;}" +
                "table.lines tfoot td{border-bottom:none;padding-top:14px;font-size:16px;}" +
                ".total-l{text-align:right;font-weight:600;}" +
                ".total-v{font-weight:700;border-top:2px solid #1a9d68;}" +
                ".block{margin-top:22px;}" +
                ".block div{line-height:1.5;color:#444;}" +
                ".footer{margin-top:34px;padding-top:14px;border-top:1px solid #eee;text-align:center;color:#888;font-size:12px;line-height:1.5;}" +
                "@media print{body{background:#fff;}.page{box-shadow:none;margin:0;max-width:100%;}}";
        }
    }
}
