namespace UiInterface.Layouts;

using Kernel;
using System.Collections.ObjectModel;
using System.ComponentModel;
public partial class QuickAddCustomer : ContentPage
{
	public static Location TheAddress;

    public static bool IsQuote = false;

    public Action<Job> OnJobCreated;
	public QuickAddCustomer()
	{
		InitializeComponent();
		vsl_main.BindingContext = TheAddress;
        if (IsQuote)
            this.Title = "Add New Quote";
        else
            this.Title = "Add New Job";

        e_frequcney.Text = $"{Settings.DefaultFrequence}";
        e_duration.Text = $"{Settings.DefaultJobDuration}";

 
        p_frequencyType.SelectedItem = Settings.DefaultFrequenceType.ToString();
    }

	private void bnt_SaveJob_Clicked(object sender, EventArgs e)
	{
		//so we do validation
		if (e_number.Text == null || e_number.Text == string.Empty)
		{
			DisplayAlert("Error", "You must enter a property number / name", "Ok");
			return;
		}

        if (e_street.Text == null || e_street.Text == string.Empty)
        {
            DisplayAlert("Error", "You must enter a street", "Ok");
            return;
        }

        if (e_price.Text == null || e_price.Text == string.Empty)
        {
            DisplayAlert("Error", "Price can not be empty!", "Ok");
            return;
        }
        int duration = 0;
        if (e_duration.Text != null && e_duration.Text != String.Empty)
        {
            
            try
            {
                duration = Convert.ToInt32(e_duration.Text);
            }
            catch
            {
                DisplayAlert("Error", "Duration not valid. Please enter price again", "Ok");
                return;
            }
        }

        float price = 0;
        int freq = 0;
        
        try
        {
            price = (float)Convert.ToDouble(e_price.Text);
        }
        catch
        {
            DisplayAlert("Error", "Price not valid. Please enter price again", "Ok");
            return;
        }

        if (e_frequcney.Text == null || e_frequcney.Text == string.Empty)
        {
            DisplayAlert("Error", "Frequency must be 0 or bigger.'", "Ok");
            return;
        }

        try
        {
            freq = Convert.ToInt32(e_frequcney.Text);
        }
        catch
        {
            DisplayAlert("Error", "Frequency not valid. Please Enter a number 0 or bigger.", "Ok");
            return;
        }

        if (cb_tnb.IsChecked || cb_tac.IsChecked)
			if (e_phone.Text == null || e_phone.Text == string.Empty)
			{
                DisplayAlert("Error", "You must enter a phone number to use 'Text Night Before' or 'Text After Completion'", "Ok");
                return;
            }

        if (cb_enb.IsChecked || cb_er.IsChecked)
            if (e_email.Text == null || e_email.Text == string.Empty)
            {
                DisplayAlert("Error", "You must enter a email to use 'Email Night Before' or 'Email Recipt'", "Ok");
                return;
            }



        Location address = new Location()
        {
            PropertyNameNumber = e_number.Text,
            Street = e_street.Text
        };

        if (e_area.Text != null && e_area.Text != string.Empty)
            address.Area = e_area.Text;

        if (e_city.Text != null && e_city.Text != string.Empty)
            address.City = e_city.Text;

        if (e_postcode.Text != null && e_postcode.Text != string.Empty)
            address.Postcode = e_postcode.Text;

        

        Customer c = new Customer()
        {
            Address = address,
        };

        if (e_name.Text != null && e_name.Text != string.Empty)
            c.FName = e_name.Text;

        if (e_phone.Text != null && e_phone.Text != string.Empty)
            c.Phone = e_phone.Text;

        Customer.Add(c);

        Job j = new Job()
        {
            CustomerId = c.Id,
            Address = address,
            Price = price,
            EstimatedTime = duration,
        };

        if (e_notes.Text != null && e_notes.Text != string.Empty)
            j.Notes = e_notes.Text;

        j.TNB = cb_tnb.IsChecked;
        j.TAC = cb_tac.IsChecked;
        j.EAC = cb_er.IsChecked;
        j.ENB = cb_enb.IsChecked;

        j.SetFrequence(freq, (FrequenceType)p_frequencyType.SelectedIndex);

        if (IsQuote)
            Job.AddQuote(j);
        else
            Job.Add(j);

        Customer.Save();
        Job.Save();


        if (OnJobCreated != null)
            OnJobCreated(j);

        Navigation.PopAsync();
    }
}