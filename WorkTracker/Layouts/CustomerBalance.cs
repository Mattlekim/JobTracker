namespace UiInterface.Layouts;

using System.Globalization;
using Kernel;

/// <summary>
/// Setting what a customer owes by hand.
///
/// The balance normally looks after itself - doing a job puts its price on,
/// taking a payment takes it off - but a round taken over from somebody else,
/// or work done before the app was being used, has to be typed in from
/// whatever it was written on before.
/// </summary>
public static class CustomerBalance
{
    /// <summary>
    /// asks for the new balance and puts it on the customer. returns true
    /// when it was changed
    /// </summary>
    public static async Task<bool> ChangeAsync(Customer c, Page page)
    {
        if (c == null)
            return false;

        string who = $"{c.FName} {c.SName}".Trim();
        if (who.Length == 0)
            who = "this customer";

        string now = c.Balance > 0
            ? $"owes {Gloable.CurrenceSymbol}{c.Balance:0.00}"
            : c.Balance < 0
                ? $"is {Gloable.CurrenceSymbol}{Math.Abs(c.Balance):0.00} in credit"
                : "owes nothing";

        //the plain keyboard rather than the numeric one, because a customer in
        //credit is a minus and the numeric pad has no minus on it
        string typed = await page.DisplayPromptAsync("Change Balance",
            $"{who} {now}.\n\nWhat should it be? A minus figure is credit.",
            "Save", "Cancel", initialValue: c.Balance.ToString("0.00"));

        if (typed == null)
            return false;

        typed = typed.Trim();

        //typed on this device, or pasted from something set to another country
        float balance;
        if (!float.TryParse(typed, NumberStyles.Float, CultureInfo.CurrentCulture, out balance)
            && !float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out balance))
        {
            await page.DisplayAlert("Change Balance", $"'{typed}' is not an amount.", "Ok");
            return false;
        }

        Apply(c, balance);
        return true;
    }

    /// <summary>
    /// puts the balance on the customer and makes sure everything showing it
    /// hears about it
    /// </summary>
    public static void Apply(Customer c, float balance)
    {
        if (c == null)
            return;

        c.Balance = balance;
        c.DateBalanceLastUpdate = DateTime.Now;
        Customer.Save();

        //what a customer owes shows against every job they have, and those
        //rows are only redrawn when the job itself says something changed
        foreach (Job j in Job.Query(QueryType.CustomerId, c.Id))
        {
            j.Refresh();
            j.RefreshColors();
        }

        DataRefreshNotifier.NotifyDataChanged();
    }
}
