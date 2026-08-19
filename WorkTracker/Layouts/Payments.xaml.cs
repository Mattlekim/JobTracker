namespace UiInterface.Layouts;
using Kernel;
using UiInterface.ImportExport;
public partial class Payments : ContentPage
{
	public Payments()
	{
	
		InitializeComponent();
		
        NavigatedTo += RefreshPage;
	}

    private async void RefreshPage(object sender, NavigatedToEventArgs e)
    {
        lv_Payments.ItemsSource = null;
        lv_Payments.ItemsSource = Payment.Query();

        //direct debits that cleared since last time show up as payments
        if (GoCardless.IsConnected && GoCardlessRequest.QueryPending().Count > 0)
        {
            await GoCardless.RefreshPendingAsync();
            lv_Payments.ItemsSource = null;
            lv_Payments.ItemsSource = Payment.Query();
        }
    }

    private async void selectFile()
    {
        CSVFile file = await StatementFile.PickAsync(this);
        if (file == null)
            return;

        await Navigation.PushAsync(new StatmentViewer());
    }

    private void bnt_ImportBank(object sender, EventArgs e)
    {
        selectFile();

    }

    /// <summary>
    /// where a reference ignored by mistake gets put back, without having to
    /// find the statement and import it again
    /// </summary>
    private void bnt_ignoredPayments(object sender, EventArgs e)
    {
        Navigation.PushAsync(new IgnoredPayments());
    }
}