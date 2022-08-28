namespace UiInterface.Layouts;
using Kernel;
public partial class NewJob : ContentPage
{

    public static Job JobToAdd = new Job();

    public static bool AddNewJob = false;

    public Action<Job> OnJobAdded;

    public Action<Job> OnJobUpdated;
	public NewJob()
	{
        
        InitializeComponent();

       

        p_JobType.ItemsSource = Job.JobNames;
        NavigatedTo += NewJob_NavigatedTo;

        cb_differentAddress.CheckedChanged += Cb_differentAddress_CheckedChanged;


        SetZindexLables();

    }

    private void SetZindexLables()
    {
        var vt = sv_mainScrole.GetVisualTreeDescendants();
        Label l;
        Grid g;
        foreach(object o in vt)
        {
            l = o as Label;
            if (l != null)
            {
                l.ZIndex = 1;
            }
            else
            {
                g = o as Grid;
                if (g != null)
                    g.ZIndex = 1;
            }
                
            
        }
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
        try
        {
            List<Customer> customers = Customer.Query();
            p_customer.Items.Clear();
            foreach (Customer c in customers)
                p_customer.Items.Add($"{c.FormattedOverview} - {c.FormattedAddress} #{c.Id}");

            if (AddNewJob)
            {
                e_frequence.Text = $"{Settings.DefaultFrequence}";
                p_frequencyType.SelectedItem = Settings.DefaultFrequenceType.ToString();

                t_price.Text = "0.00";
                e_startingBallence.Text = "0.00";
                p_ballenceType.SelectedItem = "Credit";
                t_description.Text = string.Empty;
                p_JobType.SelectedItem = Job.JobNames[0];
                t_notes.Text = string.Empty;

                e_estimatedDruation.Text = $"{Settings.DefaultJobDuration}";
                dp_startDate.Date = UsfulFuctions.DateNow;
                _bnt_Delete.IsVisible = false;
                _bnt_Delete.IsEnabled = false;
                //g_alterativePrice.IsVisible = false;
                g_alterativePrice.IsVisible = false;
                cb_alternativePrice.IsChecked = false;
                l_ballence.Text = "Starting Balance";

                _bnt_Add.Text = "Create Customer";

                cp_title.Title = "Add New Job";

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


                return;

            }

            cp_title.Title = $"{JobToAdd.Address.PropertyNameNumber} {JobToAdd.Address.Street}";
            _bnt_Add.Text = "Save Changes";
            l_ballence.Text = "Customer Balance";

            cb_differentAddress.IsChecked = JobToAdd.CustomerAddressDifferentToJob;
            cb_tfc.IsChecked = JobToAdd.TAC;
            cb_tnb.IsChecked = JobToAdd.TNB;
            cb_enb.IsChecked = JobToAdd.ENB;
            cb_eac.IsChecked = JobToAdd.EAC;
            _bnt_Delete.IsVisible = true;
            _bnt_Delete.IsEnabled = true;
            dp_startDate.Date = JobToAdd.DueDate;

            e_estimatedDruation.Text = JobToAdd.EstimatedTime.ToString();
            e_frequence.Text = $"{JobToAdd.Frequence}";
            p_frequencyType.SelectedIndex = (int)JobToAdd.Frequence_Type;

            p_JobType.SelectedItem = JobToAdd.Name;
            t_description.Text = JobToAdd.Description;
            t_notes.Text = JobToAdd.Notes;
            t_price.Text = JobToAdd.Price.ToString();
            if (JobToAdd.GetCustomer() != null)
            {
                float input = JobToAdd.GetCustomer().Balance;
                if (input > 0)
                    p_ballenceType.SelectedItem = "Debt";
                else
                    p_ballenceType.SelectedItem = "Credit";

                input = Math.Abs(input);
                e_startingBallence.Text = input.ToString();
            }
            else
                e_startingBallence.Text = "0.00";
            _bnt_Add.Text = "Save";

            if (JobToAdd.AlternativePrices != null && JobToAdd.AlternativePrices.Count > 0)
            {
                cb_alternativePrice.IsChecked = true;
                e_alterativeName.Text = JobToAdd.AlternativePrices[0].Description;
                e_alterativePrice.Text = $"{JobToAdd.AlternativePrices[0].Price}";
            }
            else
                cb_alternativePrice.IsChecked = false;
            List<Customer> cust = new List<Customer>();
            cust = Customer.Query("id", JobToAdd.CustomerId.ToString());
            if (cust.Count > 0)
            {
                p_customer.Items.Add($"{cust[0].FormattedOverview} - {cust[0].FormattedAddress} #{cust[0].Id}");
                p_customer.SelectedIndex = p_customer.Items.Count - 1;
            }

            if (JobToAdd.CustomerAddressDifferentToJob)
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

            }
            else
            {
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

            t_houseNumberName.Text = JobToAdd.Address.PropertyNameNumber;
            t_street.Text = JobToAdd.Address.Street;
            t_city.Text = JobToAdd.Address.City;
            t_area.Text = JobToAdd.Address.Area;
            t_postcode.Text = JobToAdd.Address.Postcode;
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", ex.Message, "Ok");
        }
    }

    private void bnt_Clicked(object sender, EventArgs e)
    {
        Navigation.PopToRootAsync();
        Navigation.PushAsync(new JobsList());
    }

    private async void bnt_Add(object sender, EventArgs e)
    {
        if (t_houseNumberName.Text == null || t_houseNumberName.Text == String.Empty)
        {
            await DisplayAlert("Cannot Save Job", "You can not add a job withouth an address", "Ok");
            return;
        }

        if (t_street.Text == null || t_street.Text == String.Empty)
        {
            await DisplayAlert("Cannot Save Job", "You can not add a job withouth an address", "Ok");
            return;
        }

        if (cb_tnb.IsChecked || cb_tfc.IsChecked)
            if (t_customerPhone.Text == null || t_customerPhone.Text == String.Empty)
            {
                await DisplayAlert("Cannot Save Job", "No phone number added. You can not have texting option enabled without a phone number.", "Ok");
                return;
            }

        if (cb_enb.IsChecked || cb_eac.IsChecked)
            if (t_customerEmail.Text == null || t_customerEmail.Text == String.Empty)
            {
                await DisplayAlert("Cannot Save Job", "No email added. You can not have email option enabled without a email address.", "Ok");
                return;
            }

        if (cb_alternativePrice.IsChecked)
        {
            if (e_alterativeName.Text == null || e_alterativeName.Text == string.Empty)

            {
                await DisplayAlert("Cannot Save Job", "You must set a name for the alterative price", "Ok");
                return;
            }

            if (e_alterativePrice.Text == null || e_alterativePrice.Text == string.Empty)

            {
                await DisplayAlert("Cannot Save Job", "You must set a price for the alterative price", "Ok");
                return;
            }


        }

        if (AddNewJob)
            JobToAdd = new Job();

        if (cb_alternativePrice.IsChecked)
        {
            try
            {
                if (JobToAdd.AlternativePrices == null)
                    JobToAdd.AlternativePrices = new List<AlternativePrice>();

                if (JobToAdd.AlternativePrices.Count == 0)
                    JobToAdd.AlternativePrices.Add(new AlternativePrice()
                    {
                        Description = e_alterativeName.Text,
                        Price = (float)Convert.ToDouble(e_alterativePrice.Text)
                    });
                else
                    JobToAdd.AlternativePrices[0] = new AlternativePrice()
                    {
                        Description = e_alterativeName.Text,
                        Price = (float)Convert.ToDouble(e_alterativePrice.Text)
                    };
            }
            catch
            {
                await DisplayAlert("Cannot Save Job", "Invalid price on alternative price", "Ok");
                return;
            }
        }
        else
            JobToAdd.AlternativePrices = null;


        JobToAdd.Name = p_JobType.SelectedItem as string;

        JobToAdd.SetFrequence(Convert.ToInt32(e_frequence.Text), (FrequenceType)p_frequencyType.SelectedIndex);
        JobToAdd.EstimatedTime = Convert.ToInt32(e_estimatedDruation.Text);
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

        JobToAdd.ENB = cb_enb.IsChecked;
        JobToAdd.EAC = cb_eac.IsChecked;

        JobToAdd.CustomerAddressDifferentToJob = cb_differentAddress.IsChecked;



        if (customer != null)
        {
            customer.Email = t_customerEmail.Text;
            customer.FName = t_customerName.Text;
            customer.Phone = t_customerPhone.Text;


            if ((string)p_ballenceType.SelectedItem == "Debt")
                customer.Balance = Math.Abs((float)Convert.ToDouble(e_startingBallence.Text));
            else
                customer.Balance = -Math.Abs((float)Convert.ToDouble(e_startingBallence.Text));

            if (cb_differentAddress.IsChecked)
            {
                customer.Address.PropertyNameNumber = t_d_houseNumberName.Text;
                customer.Address.Street = t_d_street.Text;
                customer.Address.City = t_d_city.Text;
                customer.Address.Area = t_d_area.Text;
                customer.Address.Postcode = t_d_postcode.Text;
            }
            else
            {
                customer.Address.PropertyNameNumber = t_houseNumberName.Text;
                customer.Address.Street = t_street.Text;
                customer.Address.City = t_city.Text;
                customer.Address.Area = t_area.Text;
                customer.Address.Postcode = t_postcode.Text;
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


            if ((string)p_ballenceType.SelectedItem == "Debt")
                customer.Balance = Math.Abs((float)Convert.ToDouble(e_startingBallence.Text));
            else
                customer.Balance = -Math.Abs((float)Convert.ToDouble(e_startingBallence.Text));


            Customer.Add(customer);
            JobToAdd.CustomerId = customer.Id; //link the job to the customer

            Customer.Save();
        }

        if (AddNewJob)
        {
            Job.Add(JobToAdd);

            if (OnJobAdded != null)
                OnJobAdded(JobToAdd);
        }
        else
            if (OnJobUpdated != null)
                OnJobUpdated(JobToAdd);
        Job.Save();


        if (AddNewJob)
        {
            if (await DisplayAlert("Job Created", "Would you like to add another job", "Yes", "No"))
            {
                t_customerEmail.Text = String.Empty;
                t_customerName.Text = String.Empty;
                t_customerPhone.Text = String.Empty;

                cb_tnb.IsChecked = false;
                cb_tfc.IsChecked = false;
                cb_enb.IsChecked = false;
                cb_alternativePrice.IsChecked = false;
                cb_differentAddress.IsChecked = false;
                cb_eac.IsChecked = false;
                t_notes.Text = String.Empty;
                t_d_houseNumberName.Text = String.Empty;
                t_description.Text = String.Empty;
                e_startingBallence.Text = "0.00";
                p_customer.SelectedIndex = -1;
                JobToAdd = new Job();
                customer = null;
                sv_mainScrole.ScrollToAsync(0, 0, true);
            }
            else
                Navigation.PopToRootAsync();
        }
        else
            Navigation.PopToRootAsync();

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
            await Navigation.PopAsync();
            //Navigation.PushAsync(new JobsList());

        }

      
    }

    private void cb_alterativePrice_Checked(object sender, CheckedChangedEventArgs e)
    {
        if (cb_alternativePrice.IsChecked)
            g_alterativePrice.IsVisible = true;
        else
            g_alterativePrice.IsVisible = false;
    }

    private void bnt_existingCustomerHelp_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Existing Customer", "You would use this option if an existing customer requests that you do an extra job. Maybe a different property or different work on the same propertiy.", "Ok");
    }
}