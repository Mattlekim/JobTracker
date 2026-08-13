namespace UiInterface.ImportExport;

using Kernel;
using UiInterface.Layouts;

/// <summary>
/// Reading a PayPal activity export.
///
/// A bank names its columns whatever it likes, which is why a bank statement
/// has to have its columns pointed out once and remembered. PayPal does not:
/// every export has the same headings on the front row, so the columns are
/// worked out from those and never asked for at all.
///
/// That also means they are never saved. The bank's columns are remembered
/// because they were typed in; these are read off the file every time, so a
/// PayPal export can never overwrite the layout that was set up for a bank.
/// </summary>
public static class PayPalStatement
{
    /// <summary>
    /// what is taken from the file. the money is the gross - what the
    /// customer actually sent - so a job paid for by PayPal clears the
    /// balance to the penny.
    ///
    /// PayPal's fee is in a column of its own and is not brought in: it is
    /// not money leaving the account as a transaction of its own, and
    /// counting the net instead would leave every customer a few pence short
    /// for ever. The fees are worth putting in as an expense once a quarter
    /// off the PayPal statement itself.
    /// </summary>
    private static readonly string[] DateNames = { "date" };
    private static readonly string[] NameNames = { "name", "from email address", "payer name" };
    private static readonly string[] AmountNames = { "gross" };

    /// <summary>columns nothing but PayPal puts next to the three above</summary>
    private static readonly string[] MarkerNames = { "fee", "net", "transaction id" };

    /// <summary>
    /// Is this a PayPal export rather than a bank's csv?
    ///
    /// All three of the columns the import runs off have to be there, and one
    /// of PayPal's own alongside them. A bank getting mistaken for PayPal
    /// would have its statement read with the wrong columns, so it is worth
    /// asking for more than a heading called Date.
    /// </summary>
    public static bool Looks(CSVFile file)
    {
        return Column(file, DateNames) >= 0
            && Column(file, NameNames) >= 0
            && Column(file, AmountNames) >= 0
            && Column(file, MarkerNames) >= 0;
    }

    /// <summary>
    /// points the statement pages at the right columns for a PayPal export.
    /// </summary>
    /// <returns>true when the file was a PayPal one and has been set up</returns>
    public static bool Apply(CSVFile file)
    {
        if (!Looks(file))
            return false;

        StatmentViewer.PayPalDate = Column(file, DateNames);
        StatmentViewer.PayPalRef = Column(file, NameNames);
        StatmentViewer.PayPalAmount = Column(file, AmountNames);

        return true;
    }

    /// <summary>
    /// the column with one of these headings, or -1. matched without case and
    /// with the quotes and spaces PayPal wraps its headings in taken off
    /// </summary>
    private static int Column(CSVFile file, string[] names)
    {
        if (file == null || file.Header == null)
            return -1;

        for (int i = 0; i < file.Header.Length; i++)
        {
            string heading = Tidy(file.Header[i]);

            foreach (string name in names)
                if (heading == name)
                    return i;
        }

        return -1;
    }

    private static string Tidy(string heading)
    {
        if (heading == null)
            return string.Empty;

        return heading.Trim().Trim('"').Trim().ToLowerInvariant();
    }
}
