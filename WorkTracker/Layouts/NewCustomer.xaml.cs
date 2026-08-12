namespace UiInterface.Layouts;
using Kernel;
public partial class NewCustomer : ContentPage
{
    public static bool AddNewCustomer = true;

    public static Customer CurrentCustomer;

	public NewCustomer()
	{
		InitializeComponent();
        NavigatedTo += NewCustomer_NavigatedTo;
	}

    /// <summary>
    /// The customer as the rest of the app holds it, looked up by id rather
    /// than trusted from the reference this page was handed.
    ///
    /// Loading the customers - a cloud sync pulling a newer copy down, or a
    /// backup being restored - builds a whole new set of Customer objects.
    /// Anything still holding one of the old ones is then writing to a record
    /// nothing else can see: this page would show the balance that was typed
    /// into it while every other page showed the one that came down, which is
    /// exactly the shape of "it says 6 here and 0 everywhere else".
    /// </summary>
    private static Customer Live()
    {
        if (CurrentCustomer == null)
            return null;

        List<Customer> found = Customer.Query("id", CurrentCustomer.Id.ToString());
        if (found.Count > 0)
            CurrentCustomer = found[0];

        return CurrentCustomer;
    }

    private void NewCustomer_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        if (AddNewCustomer)
        {
            _bnt_Delete.IsVisible = false;
            _bnt_Delete.IsEnabled = false;
            _bnt_Add.Text = "Add";
            t_preferedPayment.SelectedItem = 0;
            t_balance.Text = "0.00";
            return;
        }



        _bnt_Delete.IsVisible = true;
        _bnt_Delete.IsEnabled = true;

        _bnt_Add.Text = "Save";

        Customer customer = Live();
        if (customer == null)
            return;

        t_fName.Text = customer.FName;
        t_area.Text = customer.Address.Area;
        t_balance.Text = customer.Balance.ToString();
        t_city.Text = customer.Address.City;
        t_date.Date = customer.DateAdded;
        t_email.Text = customer.Email;
        t_phone.Text = customer.Phone;
        t_postcode.Text = customer.Address.Postcode;
        int i = 0;
        foreach (string s in t_preferedPayment.Items)
        {
            if (s == customer.NormalPaymentMethord.ToString())
            {
                t_preferedPayment.SelectedIndex = i;
                break;
            }
            i++;
        }
        //t_preferedPayment.SelectedIndex
        t_street.Text = customer.Address.Street;
        t_houseNumberName.Text = customer.Address.PropertyNameNumber;
        //now we need to populate the current customer
    }

    private void bnt_Cancel(object sender, EventArgs e)
    {
		Navigation.PopAsync();
    }

    /// <summary>
    /// fills the name, phone and email in from the phone's own contacts. a
    /// field the contact has nothing for is left as it stands rather than
    /// wiped, so this can be used to top up a half filled in customer
    /// </summary>
    private async void bnt_FromContacts(object sender, EventArgs e)
    {
        ContactFill.Details picked = await ContactFill.PickAsync(this);
        if (picked == null)
            return;

        if (!string.IsNullOrWhiteSpace(picked.Name))
            t_fName.Text = picked.Name;

        if (!string.IsNullOrWhiteSpace(picked.Phone))
            t_phone.Text = picked.Phone;

        if (!string.IsNullOrWhiteSpace(picked.Email))
            t_email.Text = picked.Email;
    }

    private void bnt_Add(object sender, EventArgs e)
    {
        if (AddNewCustomer)
        {
            Customer customer = new Customer(t_houseNumberName.Text, t_street.Text, t_city.Text);
            customer.Address.Area = t_area.Text;
            customer.Address.Postcode = t_postcode.Text;
            customer.Address.Street = t_street.Text;
            customer.Address.City = t_city.Text;
            customer.Address.PropertyNameNumber = t_houseNumberName.Text;
            
            customer.DateAdded = t_date.Date;
            customer.Balance = (float)Convert.ToDouble(t_balance.Text);
            customer.Email = t_email.Text;
            customer.DateBalanceLastUpdate = DateTime.Now;
            customer.FName = t_fName.Text;
            customer.NormalPaymentMethord = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)t_preferedPayment.SelectedItem);
            customer.Phone = t_phone.Text;
            Customer.Add(customer);
            
        }
        else
        {
            //the live record, not whatever reference this page was handed
            Customer customer = Live();
            if (customer == null)
                return;

            customer.Address.Area = t_area.Text;
            customer.Address.Postcode = t_postcode.Text;
            customer.Address.Street = t_street.Text;
            customer.Address.City = t_city.Text;
            customer.Address.PropertyNameNumber = t_houseNumberName.Text;

            customer.DateAdded = t_date.Date;
            customer.Balance = (float)Convert.ToDouble(t_balance.Text);
            customer.Email = t_email.Text;
            customer.DateBalanceLastUpdate = DateTime.Now;
            customer.FName = t_fName.Text;
            customer.NormalPaymentMethord = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)t_preferedPayment.SelectedItem);
            customer.Phone = t_phone.Text;

            //the balance shows against every job this customer has, and those
            //rows are only redrawn when the job says something changed
            foreach (Job j in Job.Query(QueryType.CustomerId, customer.Id))
            {
                j.Refresh();
                j.RefreshColors();
            }
        }

        Customer.Save();
        DataRefreshNotifier.NotifyDataChanged();
        Navigation.PopAsync();

    }

    private void bnt_Delete(object sender, EventArgs e)
    {
        Customer.Delete(CurrentCustomer.Id);
        Navigation.PopAsync();
    }
}