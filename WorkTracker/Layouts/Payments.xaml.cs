namespace UiInterface.Layouts;
using Kernel;
public partial class Payments : ContentPage
{
	public Payments()
	{
	
		InitializeComponent();
		
        NavigatedTo += RefreshPage;
	}

    private void RefreshPage(object sender, NavigatedToEventArgs e)
    {
        lv_Payments.ItemsSource = null;
        lv_Payments.ItemsSource = Payment.Query();
    }

    private void list_child_added(object sender, ElementEventArgs e)
    {

    }

    private async void selectFile()
    {
        FileResult fr = await FilePicker.Default.PickAsync(PickOptions.Default);
        if (fr == null)
            return;

        try
        {
            CSVFile file = CSV.Import(fr.FullPath);
            StatmentViewer.CsvFile = file;
            await Navigation.PushAsync(new StatmentViewer());
        }
        catch
        {
            await DisplayAlert("Error", "There was a problem importing the file. Make sure the file type is supported", "Ok");
        }

        
    }
    private void bnt_ImportBank(object sender, EventArgs e)
    {
        selectFile();
        
    }
}