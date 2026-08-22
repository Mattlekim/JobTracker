namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The invoices that have been made, newest first. It is the record side of
/// the feature: the editor writes them, this lists them, and a tap opens what
/// to do with one - edit it, build it again to share, or throw it away.
///
/// Reached from the Invoices section on the settings page and from a customer's
/// own details. The list is built again on the way back so an invoice made or
/// changed elsewhere shows without a manual refresh.
/// </summary>
public partial class Invoices : ContentPage
{
    public Invoices()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Show();
    }

    private void Show()
    {
        List<Invoice> invoices = Invoice.Query();
        cv_invoices.ItemsSource = invoices;
        l_empty.IsVisible = invoices.Count == 0;
    }

    private void rv_invoices_Refreshing(object sender, EventArgs e)
    {
        try
        {
            Show();
        }
        finally
        {
            rv_invoices.IsRefreshing = false;
        }
    }

    private void tbi_New_Clicked(object sender, EventArgs e)
    {
        Navigation.PushAsync(new InvoiceEditor());
    }

    private async void Row_Tapped(object sender, EventArgs e)
    {
        //the gesture's sender is the row (or its recogniser, whose context
        //propagates from the row) - either way its BindingContext is the row's
        //invoice
        Invoice invoice = (sender as BindableObject)?.BindingContext as Invoice;
        if (invoice == null)
            return;

        string action = await DisplayActionSheet(
            $"{invoice.FormattedNumber} - {invoice.FormattedTotal}", "Cancel", null,
            "Edit", "Share", "Delete");

        if (action == "Edit")
        {
            await Navigation.PushAsync(new InvoiceEditor(invoice, false));
        }
        else if (action == "Share")
        {
            await ShareInvoice(invoice);
        }
        else if (action == "Delete")
        {
            if (!await DisplayAlert("Delete Invoice",
                    $"Delete {invoice.FormattedNumber}? This cannot be undone. The invoice number is not reused.",
                    "Delete", "Keep"))
                return;

            Invoice.Delete(invoice.Id);
            Show();
        }
    }

    /// <summary>
    /// builds the invoice as a self-contained web page again and offers to
    /// share or save it. an invoice is a record, so it is not changed here -
    /// this only re-renders what is already saved
    /// </summary>
    private async Task ShareInvoice(Invoice invoice)
    {
        try
        {
            string html = InvoiceHtml.Build(invoice);
            string fileName = DeviceFileSaver.SafeName($"Invoice {invoice.FormattedNumber}.html");
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);
            File.WriteAllText(path, html);

            await DeviceFileSaver.OfferAsync(this, $"Invoice {invoice.FormattedNumber}",
                path, fileName, "text/html");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could Not Build Invoice", ex.Message, "Ok");
        }
    }
}
