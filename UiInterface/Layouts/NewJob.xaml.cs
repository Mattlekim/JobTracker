namespace UiInterface.Layouts;
using Kernel;
public partial class NewJob : ContentPage
{

    public static Job JobToAdd = new Job();

    public static bool AddNewJob = false;
	public NewJob()
	{
		InitializeComponent();
        NavigatedTo += NewJob_NavigatedTo;

        cb_differentAddress.CheckedChanged += Cb_differentAddress_CheckedChanged;
	}

    private void Cb_differentAddress_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        
        if (cb_differentAddress.IsChecked)
        {
            t_d_area.IsVisible = true;
            t_d_area.IsEnabled = true;

            t_d_city.IsVisible = true;
            t_d_city.IsEnabled = true;

            t_d_houseNumberName.IsVisible = true;
            t_d_houseNumberName.IsEnabled = true;

            t_d_postcode.IsVisible = true;
            t_d_postcode.IsEnabled = true;

            t_d_street.IsVisible = true;
            t_d_street.IsEnabled = true;

            l_hide1.IsEnabled = true;
            l_hide1.IsVisible = true;

            l_hide2.IsEnabled = true;
            l_hide2.IsVisible = true;

            l_hide3.IsEnabled = true;
            l_hide3.IsVisible = true;

            l_hide4.IsEnabled = true;
            l_hide4.IsVisible = true;

            l_hide5.IsEnabled = true;
            l_hide5.IsVisible = true;

            return;
        }

        t_d_area.IsVisible = false;
        t_d_area.IsEnabled = false;

        t_d_city.IsVisible = false;
        t_d_city.IsEnabled = false;

        t_d_houseNumberName.IsVisible = false;
        t_d_houseNumberName.IsEnabled = false;

        t_d_postcode.IsVisible = false;
        t_d_postcode.IsEnabled = false;

        t_d_street.IsVisible = false;
        t_d_street.IsEnabled = false;

        l_hide1.IsEnabled = false;
        l_hide1.IsVisible = false;

        l_hide2.IsEnabled = false;
        l_hide2.IsVisible = false;

        l_hide3.IsEnabled = false;
        l_hide3.IsVisible = false;

        l_hide4.IsEnabled = false;
        l_hide4.IsVisible = false;

        l_hide5.IsEnabled = false;
        l_hide5.IsVisible = false;
    }

    private void NewJob_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        List<Customer> customers = Customer.Query();
        foreach (Customer c in customers)
            p_customer.Items.Add($"{c.FormattedOverview} - {c.FormattedAddress} #{c.Id}");

        if (AddNewJob)
        {
            t_frequency.SelectedIndex = 0;
            t_price.Text = "0.00";
            t_description.Text = string.Empty;
            t_name.Text = string.Empty;
            t_notes.Text = string.Empty;
            t_name.Text = string.Empty;
            dp_startDate.Date = DateTime.Now;
            _bnt_Delete.IsVisible = false;
            _bnt_Delete.IsEnabled = false;
            return;

        }

        cb_differentAddress.IsChecked = JobToAdd.CustomerAddressDifferentToJob;
        cb_tfc.IsChecked = JobToAdd.TAC;
        cb_tnb.IsChecked = JobToAdd.TNB;
        _bnt_Delete.IsVisible = true;
        _bnt_Delete.IsEnabled = true;
        dp_startDate.Date = JobToAdd.DueDate;
        t_frequency.SelectedIndex = JobToAdd.Frequence;
        t_name.Text = JobToAdd.Name;
        t_description.Text = JobToAdd.Description;
        t_notes.Text = JobToAdd.Notes;
        t_price.Text = JobToAdd.Price.ToString();
        _bnt_Add.Text = "Save";
        List<Customer> cust = new List<Customer>();
        cust = Customer.Query("id", JobToAdd.CustomerId.ToString());
        if (cust.Count > 0)
        {
            p_customer.Items.Add($"{cust[0].FormattedOverview} - {cust[0].FormattedAddress} #{cust[0].Id}");
            p_customer.SelectedIndex = p_customer.Items.Count - 1;
        }
    }

    private void bnt_Clicked(object sender, EventArgs e)
    {
        Navigation.PopToRootAsync();
        Navigation.PushAsync(new JobsList());
    }

    private void bnt_Add(object sender, EventArgs e)
    {
        if (t_houseNumberName.Text == null || t_houseNumberName.Text == String.Empty)
        {
            DisplayAlert("Cannot Save Job", "You can not add a job withouth an address", "Ok");
            return;
        }

        if (t_street.Text == null || t_street.Text == String.Empty)
        {
            DisplayAlert("Cannot Save Job", "You can not add a job withouth an address", "Ok");
            return;
        }

        if (cb_tnb.IsChecked)
            if (t_customerPhone.Text == null || t_customerPhone.Text == String.Empty)
            {
                DisplayAlert("Cannot Save Job", "No phone number added. You can not have 'Notify Customer Night Before' option enabled without a phone number.", "Ok");
                return;
            }

        if (cb_tfc.IsChecked)
            if (t_customerPhone.Text == null || t_customerPhone.Text == String.Empty)
            {
                DisplayAlert("Cannot Save Job", "No phone number added. You can not have 'Notify Customer When Job Compleate' option enabled without a phone number.", "Ok");
                return;
            }


        if (AddNewJob)
            JobToAdd = new Job();
        JobToAdd.Name = t_name.Text;

        JobToAdd.SetFrequence(t_frequency.SelectedIndex);
        JobToAdd.Description = t_description.Text;
        JobToAdd.Notes = t_notes.Text;
        JobToAdd.Price = (float)Convert.ToDouble(t_price.Text);

        if (JobToAdd.Address == null)
            JobToAdd.Address = new Location();
        JobToAdd.Address.PropertyNameNumber = t_houseNumberName.Text;
        JobToAdd.Address.Street = t_street.Text;
        JobToAdd.Address.Area = t_area.Text;
        JobToAdd.Address.City = t_city.Text;
        JobToAdd.Address.Postcode = t_postcode.Text;
        JobToAdd.DueDate = dp_startDate.Date;
        JobToAdd.TAC = cb_tfc.IsChecked;
        JobToAdd.TNB = cb_tnb.IsChecked;

        JobToAdd.CustomerAddressDifferentToJob = cb_differentAddress.IsChecked;

        if (customer != null)
        {
            customer.Email = t_customerEmail.Text;
            customer.FName = t_customerName.Text;
            customer.Phone = t_customerPhone.Text;

            if (cb_differentAddress.IsChecked)
            {
                customer.Address.PropertyNameNumber = t_d_houseNumberName.Text;
                customer.Address.Street = t_d_street.Text;
                customer.Address.City = t_d_city.Text;
                customer.Address.Area = t_d_area.Text;
                customer.Address.Postcode = t_d_postcode.Text;
            }
            Customer.Save();
        }
        else
        {
            //here we need to add a new customer
            customer = new Customer();

            if (cb_differentAddress.IsChecked)
            {
                customer.Address.PropertyNameNumber = t_d_houseNumberName.Text;
                customer.Address.Street = t_d_street.Text;
                customer.Address.City = t_d_city.Text;
                customer.Address.Area = t_d_area.Text;
                customer.Address.Postcode = t_d_postcode.Text;
            }
            else
                customer.Address = JobToAdd.Address.DeepCopy();

            customer.Email = t_customerEmail.Text;
            customer.FName = t_customerName.Text;
            customer.Phone = t_customerPhone.Text;



            Customer.Add(customer);
            JobToAdd.CustomerId = customer.Id;

            Customer.Save();
        }

        if (AddNewJob)
            Job.Add(JobToAdd);
        Job.Save();

        Navigation.PopToRootAsync();
        Navigation.PushAsync(new JobsList());

    }

    private void bnt_test(object sender, EventArgs e)
    {

    }

    private Customer customer;
    private void p_customerSelected(object sender, EventArgs e)
    {
        //lets get the id from the string
        string s = p_customer.SelectedItem as string;
        if (s == null)
            return;

        int i = s.IndexOf("#");
        if (i == -1)
            return;

        string custId = s.Substring(i + 1);

        List<Customer> cust = Customer.Query("id", custId);
        if (cust.Count <= 0)
            return;
        customer = cust[0];

        JobToAdd.CustomerId = customer.Id;

        bool autoFillInAddress = true;
        if (t_houseNumberName.Text != null && t_houseNumberName.Text != String.Empty)
            autoFillInAddress = false;

        if (t_street.Text != null && t_street.Text != String.Empty)
            autoFillInAddress = false;

        if (t_city.Text != null && t_city.Text != String.Empty)
            autoFillInAddress = false;

        if (t_area.Text != null && t_area.Text != String.Empty)
            autoFillInAddress = false;

        if (t_postcode.Text != null && t_postcode.Text != String.Empty)
            autoFillInAddress = false;

        if (autoFillInAddress)
        {
            t_houseNumberName.Text = customer.Address.PropertyNameNumber;
            t_street.Text = customer.Address.Street;
            t_city.Text = customer.Address.City;
            t_area.Text = customer.Address.Area;
            t_postcode.Text = customer.Address.Postcode;
        }

        t_d_houseNumberName.Text = customer.Address.PropertyNameNumber;
        t_d_street.Text = customer.Address.Street;
        t_d_city.Text = customer.Address.City;
        t_d_area.Text = customer.Address.Area;
        t_d_postcode.Text = customer.Address.Postcode;

        t_customerEmail.Text = customer.Email;
        t_customerName.Text = customer.FName;
        t_customerPhone.Text = customer.Phone;
        

    }

    private async void bnt_Delete(object sender, EventArgs e)
    {

        bool result = await DisplayAlert("Are you sure you want to Delete?", "This cannot be undone. The Job will be delete permintly! Are you sure?", "Yes Delete The Job", "No");
        
        if (result)
        {
            Job.Delete(JobToAdd.Id.ToString());
            Job.Save();
            Navigation.PopAsync();
            Navigation.PushAsync(new JobsList());

        }

      
    }
}