namespace UiInterface.Layouts;

using Kernel;
using UiInterface.Controles;

/// <summary>
/// Records money in that is not off the round - a day's work for somebody, a
/// one-off. It writes an "other income" payment (`Payment.AddOtherIncome`),
/// which counts as income for tax but is tied to no customer and moves no
/// balance.
///
/// Reached from the payments page toolbar and from a day's action sheet on the
/// calendar, where <see cref="DateToUse"/> opens it on the day that was tapped.
/// </summary>
public partial class AddIncome : ContentPage
{
    /// <summary>
    /// the day the income is for. set by a caller that already has a day in
    /// mind - the calendar - and reset to today afterwards, like
    /// BookJobFormcs.BookForDate, so the next caller does not pick it up
    /// </summary>
    public static DateTime DateToUse = DateTime.MinValue;

    public AddIncome()
    {
        InitializeComponent();

        dp_date.Date = DateToUse > DateTime.MinValue ? DateToUse : DateTime.Now.Date;
        DateToUse = DateTime.MinValue;

        PaymentMethodChoice.Fill(p_method);
        PaymentMethodChoice.Select(p_method, PaymentMethod.Cash);
    }

    private async void bnt_save_Clicked(object sender, EventArgs e)
    {
        float amount;
        if (!float.TryParse(e_amount.Text, out amount) || amount <= 0)
        {
            await DisplayAlert("Add Income", "Put in how much came in first.", "Ok");
            return;
        }

        PaymentMethod method = PaymentMethodChoice.Picked(p_method, PaymentMethod.Cash);

        Payment.AddOtherIncome(amount, method, e_description.Text, dp_date.Date);

        //the payments page and the tax figures both read off the payments, so
        //tell the app the data has changed
        DataRefreshNotifier.NotifyDataChanged();

        await Navigation.PopAsync();
    }
}
