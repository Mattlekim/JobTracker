namespace UiInterface.Controles;

using Kernel;

/// <summary>
/// The one place a picker is asked "how was this paid for", so the job's own
/// window and the paper view's record window cannot word the same list two
/// ways or read an answer back two ways.
///
/// It is here because of a hole that made the option look like it was not
/// there at all. The pickers were filled through <c>Picker.Items</c> and then
/// pointed at a method with <c>SelectedItem</c> - and MAUI works that
/// property's index out off <c>ItemsSource</c>, which is null on a picker
/// filled that way. So the selection never landed: the box opened **blank**
/// with no method named on it, on a page where the method is one of the three
/// things being set. The list is an <c>ItemsSource</c> now and the answer is
/// carried by <see cref="Select"/>/<see cref="Picked"/> instead.
///
/// The wording is <see cref="Payment.NameFor"/> - the same reading names the
/// payments page and the customer's history use, so a cheque is a Cheque here
/// as well as there. **What a picker shows is not what is saved**: the method
/// is carried by position through <see cref="Methods"/> and the enum value is
/// what comes back out, so the wording can be changed without touching a
/// single stored payment. Do not go back to parsing the picked text.
/// </summary>
public static class PaymentMethodChoice
{
    /// <summary>the methods offered, in the order they are offered</summary>
    public static List<PaymentMethod> Methods()
    {
        List<PaymentMethod> methods = new List<PaymentMethod>();

        foreach (PaymentMethod m in Enum.GetValues(typeof(PaymentMethod)))
            methods.Add(m);

        return methods;
    }

    /// <summary>
    /// the method a visit with no payment on it yet opens on: the customer's
    /// usual one (Layouts/NewCustomer is what asks for it) rather than cash
    /// for everybody, because a round where half the houses pay by transfer
    /// is half a round of payments filed as cash by somebody who never
    /// looked at the picker.
    ///
    /// Two methods are never opened on. Other is what a customer nobody has
    /// answered for carries and is not a method anybody means; and a direct
    /// debit is **requested rather than taken** - picked here it asks
    /// GoCardless for the money instead of writing the payment down, which
    /// is a thing to choose on purpose and not to be handed by a preference
    /// set months ago on the customer's form.
    /// </summary>
    public static PaymentMethod Usual(Job job)
    {
        Customer c = job == null ? null : job.GetCustomer();

        if (c == null)
            return PaymentMethod.Cash;

        if (c.NormalPaymentMethord == PaymentMethod.Other || c.NormalPaymentMethord == PaymentMethod.GoCardless)
            return PaymentMethod.Cash;

        return c.NormalPaymentMethord;
    }

    /// <summary>fills the picker in and gives it something to say while nothing is picked</summary>
    public static void Fill(Picker picker)
    {
        if (picker == null)
            return;

        List<string> names = new List<string>();

        foreach (PaymentMethod m in Methods())
            names.Add(Payment.NameFor(m));

        picker.ItemsSource = names;

        if (string.IsNullOrWhiteSpace(picker.Title))
            picker.Title = "Paid by";
    }

    /// <summary>points the picker at a method</summary>
    public static void Select(Picker picker, PaymentMethod method)
    {
        if (picker == null)
            return;

        int index = Methods().IndexOf(method);
        picker.SelectedIndex = index < 0 ? 0 : index;
    }

    /// <summary>
    /// the method that is picked. <paramref name="fallback"/> is what an
    /// untouched picker answers, so a page that never showed the list still
    /// saves a sensible method rather than the first one on it
    /// </summary>
    public static PaymentMethod Picked(Picker picker, PaymentMethod fallback)
    {
        if (picker == null)
            return fallback;

        List<PaymentMethod> methods = Methods();

        if (picker.SelectedIndex < 0 || picker.SelectedIndex >= methods.Count)
            return fallback;

        return methods[picker.SelectedIndex];
    }
}
