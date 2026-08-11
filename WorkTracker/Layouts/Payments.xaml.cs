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

    private void list_child_added(object sender, ElementEventArgs e)
    {

    }

    private async void selectFile()
    {
        FileResult fr = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a bank statement (.csv or .pdf)",
        });

        if (fr == null)
            return;

        bool isPdf = fr.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        try
        {
            CSVFile file = isPdf ? await ImportPdf(fr.FullPath) : CSV.Import(fr.FullPath);
            if (file == null) //the password was needed and not given
                return;

            StatmentViewer.SourceIsPdf = isPdf;
            StatmentViewer.CsvFile = file;
            await Navigation.PushAsync(new StatmentViewer());
        }
        catch (InvalidDataException ex)
        {
            await DisplayAlert("Nothing to import", ex.Message, "Ok");
        }
        catch
        {
            await DisplayAlert("Error", "There was a problem importing the file. Make sure the file type is supported", "Ok");
        }


    }

    /// <summary>
    /// Reads the statement off the UI thread - a long pdf takes a moment - asking for the password
    /// if the bank locked the file.
    /// </summary>
    private async Task<CSVFile> ImportPdf(string path)
    {
        string password = null;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                string pw = password;
                return await Task.Run(() => PdfStatementImporter.Import(path, pw));
            }
            catch (PdfStatementPasswordException ex)
            {
                password = await DisplayPromptAsync("Password needed", $"{ex.Message} Enter the password to open it.",
                    "Open", "Cancel");

                if (string.IsNullOrEmpty(password))
                    return null;
            }
        }

        await DisplayAlert("Error", "The statement could not be opened with that password", "Ok");
        return null;
    }
    private void bnt_ImportBank(object sender, EventArgs e)
    {
        selectFile();
        
    }
}