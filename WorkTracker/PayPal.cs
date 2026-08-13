namespace UiInterface;

using System.Globalization;
using Kernel;

/// <summary>
/// Asking a customer for money through PayPal.
///
/// This is a paypal.me link and nothing more: no account to connect, no keys
/// to paste in, nothing to go wrong at somebody else's end. The round's own
/// paypal.me name goes in on the settings page once, and every customer can
/// then be sent a link with the amount already filled in - they tap it, they
/// pay, and it turns up on the PayPal statement to be imported like the bank
/// one.
///
/// The money is **not** marked as received here. It has not been: the link
/// has only been sent. Marking the job paid before the money has landed is
/// how a round ends up chasing people who have paid and not chasing people
/// who have not.
/// </summary>
public static class PayPal
{
    /// <summary>
    /// the paypal.me name, without the paypal.me/ in front of it. empty until
    /// it has been filled in on the settings page
    /// </summary>
    public static string Handle = string.Empty;

    public static bool IsSetUp
    {
        get { return !string.IsNullOrWhiteSpace(Handle); }
    }

    /// <summary>
    /// A link that opens PayPal with the amount already in it.
    ///
    /// The currency goes on the end because paypal.me reads a bare number as
    /// whatever the *payer's* account is in, which is not necessarily what
    /// the round charges in. It is taken off the phone's own region, so a
    /// round in Ireland asks for euros without being told to.
    /// </summary>
    public static string LinkFor(float amount)
    {
        if (!IsSetUp)
            return string.Empty;

        string name = Handle.Trim().Trim('/');

        //somebody will paste the whole link in rather than just their name
        int slash = name.LastIndexOf('/');
        if (slash >= 0)
            name = name.Substring(slash + 1);

        if (amount <= 0)
            return $"https://paypal.me/{name}";

        return $"https://paypal.me/{name}/{amount.ToString("0.00", CultureInfo.InvariantCulture)}{Currency()}";
    }

    /// <summary>
    /// the three letter currency code for wherever the phone is. blank if it
    /// cannot be worked out, which leaves paypal.me to its own default rather
    /// than asking for the wrong money
    /// </summary>
    private static string Currency()
    {
        try
        {
            return RegionInfo.CurrentRegion.ISOCurrencySymbol;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// what to send the customer. the link is on a line of its own so it is
    /// tappable in every messaging app rather than swallowed by the sentence
    /// around it
    /// </summary>
    public static string MessageFor(float amount)
    {
        string link = LinkFor(amount);

        if (amount <= 0)
            return $"Hi, you can pay for your window cleaning here:\n{link}\nMany thanks";

        return $"Hi, that comes to {Gloable.CurrenceSymbol}{amount:0.00} for your window cleaning. You can pay here:\n{link}\nMany thanks";
    }
}
