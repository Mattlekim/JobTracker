namespace UiInterface.Layouts;

using Kernel;
using System.Collections.ObjectModel;
using System.ComponentModel;
public partial class QuickAddCustomer : ContentPage
{
	public static Location TheAddress;

    /// <summary>
    /// set before the page is pushed to make this a quote rather than a job.
    /// it is read once, in the constructor, and put back to false: a static
    /// left set would turn the next ordinary job into a quote as well
    /// </summary>
    public static bool IsQuote = false;

    private readonly bool _isQuote;

    public Action<Job> OnJobCreated;
	/// <summary>
	/// fills the name, phone and email in from the phone's own contacts. a
	/// field the contact has nothing for is left as it stands rather than
	/// wiped, so this can be used to top up what has already been typed
	/// </summary>
	private async void bnt_FromContacts(object sender, EventArgs e)
	{
		ContactFill.Details picked = await ContactFill.PickAsync(this);
		if (picked == null)
			return;

		if (!string.IsNullOrWhiteSpace(picked.Name))
			e_name.Text = picked.Name;

		if (!string.IsNullOrWhiteSpace(picked.Phone))
			e_phone.Text = picked.Phone;

		if (!string.IsNullOrWhiteSpace(picked.Email))
			e_email.Text = picked.Email;
	}

	/// <summary>
	/// fills the address in from where the phone is standing. the property
	/// number is left alone - a phone is not accurate enough to be trusted
	/// with which house it is outside
	/// </summary>
	private async void bnt_useLocation_Clicked(object sender, EventArgs e)
	{
		AddressFromLocation.Found found = await AddressFromLocation.AskAsync(this);
		if (found == null)
			return;

		//a part that came back empty leaves what is already typed alone
		if (!string.IsNullOrWhiteSpace(found.Street))
			e_street.Text = found.Street;

		if (!string.IsNullOrWhiteSpace(found.City))
			e_city.Text = found.City;

		if (!string.IsNullOrWhiteSpace(found.Area))
			e_area.Text = found.Area;

		if (!string.IsNullOrWhiteSpace(found.Postcode))
			e_postcode.Text = found.Postcode;
	}

	public QuickAddCustomer()
	{
		_isQuote = IsQuote;
		IsQuote = false;

		InitializeComponent();
		vsl_main.BindingContext = TheAddress;

		WireAddressSuggestions();
        if (_isQuote)
        {
            this.Title = "Add New Quote";
            bnt_Add.Text = "Add Quote";
        }
        else
        {
            this.Title = "Add New Job";
            bnt_Add.Text = "Add Job";
        }

        e_frequcney.Text = $"{Settings.DefaultFrequence}";
        e_duration.Text = $"{Settings.DefaultJobDuration}";

 
        p_frequencyType.SelectedItem = Settings.DefaultFrequenceType.ToString();
    }

    private void cb_oneOff_Changed(object sender, CheckedChangedEventArgs e)
    {
        //how often it comes round is only worth asking about when it comes
        //round at all
        hsl_frequency.IsVisible = !cb_oneOff.IsChecked;
    }

	private async void bnt_SaveJob_Clicked(object sender, EventArgs e)
	{
		//so we do validation
		if (e_number.Text == null || e_number.Text == string.Empty)
		{
			await DisplayAlert("Error", "You must enter a property number / name", "Ok");
			return;
		}

        if (e_street.Text == null || e_street.Text == string.Empty)
        {
            await DisplayAlert("Error", "You must enter a street", "Ok");
            return;
        }

        if (e_price.Text == null || e_price.Text == string.Empty)
        {
            if (!_isQuote)
            {
                await DisplayAlert("Error", "Price can not be empty!", "Ok");
                return;
            }
            
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
                await DisplayAlert("Error", "Duration not valid. Please enter price again", "Ok");
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
            if (_isQuote)
                price = 0;
            else
            {
                await DisplayAlert("Error", "Price not valid. Please enter price again", "Ok");
                return;
            }
        }

        //a one off has nothing to repeat, so how often is not asked for and
        //the frequency stays at nothing - which is what stops it coming back
        //round once it is marked done
        if (!cb_oneOff.IsChecked)
        {
            if (e_frequcney.Text == null || e_frequcney.Text == string.Empty)
            {
                await DisplayAlert("Error", "Frequency must be 0 or bigger.'", "Ok");
                return;
            }

            try
            {
                freq = Convert.ToInt32(e_frequcney.Text);
            }
            catch
            {
                await DisplayAlert("Error", "Frequency not valid. Please Enter a number 0 or bigger.", "Ok");
                return;
            }
        }

        if (cb_tnb.IsChecked || cb_tac.IsChecked)
			if (e_phone.Text == null || e_phone.Text == string.Empty)
			{
                await DisplayAlert("Error", "You must enter a phone number to use 'Text Night Before' or 'Text After Completion'", "Ok");
                return;
            }

        if (cb_enb.IsChecked || cb_er.IsChecked)
            if (e_email.Text == null || e_email.Text == string.Empty)
            {
                await DisplayAlert("Error", "You must enter a email to use 'Email Night Before' or 'Email Recipt'", "Ok");
                return;
            }



        //an entry that was never typed in gives null, which must not reach
        //the address - empty is what the rest of the app expects
        Location address = new Location()
        {
            PropertyNameNumber = e_number.Text ?? string.Empty,
            Street = e_street.Text ?? string.Empty
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

        j.DueDate = dp_StartDate.Date;

        if (_isQuote)
            Job.AddQuote(j);
        else
            Job.Add(j);

        Customer.Save();
        Job.Save();


        if (OnJobCreated != null)
            OnJobCreated(j);

        //a quote is not put on the round, so say where it has gone - looking
        //for it among the work is looking in the wrong place
        if (_isQuote)
            await DisplayAlert("Quote Created",
                "The quote is in the Quotes section at the bottom of Work > List.", "Ok");

        await Navigation.PopAsync();
    }
    /// <summary>
    /// offers the streets, towns and areas already on the round as the
    /// address is typed. picking a street fills the town and area in with
    /// wherever that street is, because a street only sits in one town
    /// </summary>
    private void WireAddressSuggestions()
    {
        Controles.SuggestionBox.Attach(e_street, hsl_streetSuggestions, Customer.KnownStreets, picked =>
        {
            Location where = Customer.AddressForStreet(picked);
            if (where == null)
                return;

            //only fills in what has been left empty - never types over
            //something already put in by hand
            if (string.IsNullOrWhiteSpace(e_city.Text) && !string.IsNullOrWhiteSpace(where.City))
                e_city.Text = where.City;

            if (string.IsNullOrWhiteSpace(e_area.Text) && !string.IsNullOrWhiteSpace(where.Area))
                e_area.Text = where.Area;
        });

        Controles.SuggestionBox.Attach(e_city, hsl_citySuggestions, Customer.KnownCities);
        Controles.SuggestionBox.Attach(e_area, hsl_areaSuggestions, Customer.KnownAreas);
    }

}