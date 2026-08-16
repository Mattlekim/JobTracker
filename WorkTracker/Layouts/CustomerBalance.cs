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

        //the amount on its own, then whether it is owed or credit - the same
        //way round as the Credit / Debt picker on the new job page, so there
        //is never a minus sign to find on a numeric keypad
        string typed = await page.DisplayPromptAsync("Change Balance",
            $"{who} {now}.\n\nHow much?",
            "Next", "Cancel",
            initialValue: Math.Abs(c.Balance).ToString("0.00"),
            keyboard: Keyboard.Numeric);

        if (typed == null)
            return false;

        float amount;
        if (!TryReadAmount(typed, out amount))
        {
            await page.DisplayAlert("Change Balance", $"'{typed.Trim()}' is not an amount.", "Ok");
            return false;
        }

        amount = Math.Abs(amount);

        //nothing owed is nothing owed - no point asking which way round
        if (amount == 0)
        {
            Apply(c, 0);
            return true;
        }

        string kind = await page.DisplayActionSheet(
            $"Is {Gloable.CurrenceSymbol}{amount:0.00} owed to you, or are they in credit?",
            "Cancel", null,
            DebtOption, CreditOption);

        if (kind == null || kind == "Cancel")
            return false;

        //debt is a balance owed, credit is the other way about
        Apply(c, kind == DebtOption ? amount : -amount);
        return true;
    }

    private const string DebtOption = "Debt - they owe you";
    private const string CreditOption = "Credit - they have paid ahead";

    /// <summary>
    /// reads an amount typed on this device, or pasted from something set to
    /// another country
    /// </summary>
    private static bool TryReadAmount(string typed, out float amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(typed))
            return false;

        typed = typed.Trim();

        return float.TryParse(typed, NumberStyles.Float, CultureInfo.CurrentCulture, out amount)
            || float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
    }

    /// <summary>
    /// puts the balance on the customer and makes sure everything showing it
    /// hears about it
    /// </summary>
    public static void Apply(Customer c, float balance)
    {
        if (c == null)
            return;

        //a figure typed in leaves a record - what it was, what it became and
        //when - so the customer's history can say the day it was agreed
        //rather than just showing a number nothing accounts for
        if (c.Balance != balance)
            BalanceAdjustment.AddSetByHand(c.Id, balance, c.Balance, null);

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
