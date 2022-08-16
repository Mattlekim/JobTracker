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

        t_fName.Text = CurrentCustomer.FName;
        t_area.Text = CurrentCustomer.Address.Area;
        t_balance.Text = CurrentCustomer.Balance.ToString();
        t_city.Text = CurrentCustomer.Address.City;
        t_date.Date = CurrentCustomer.DateAdded;
        t_email.Text = CurrentCustomer.Email;
        t_phone.Text = CurrentCustomer.Phone;
        t_postcode.Text = CurrentCustomer.Address.Postcode;
        int i = 0;
        foreach (string s in t_preferedPayment.Items)
        {
            if (s == CurrentCustomer.NormalPaymentMethord.ToString())
            {
                t_preferedPayment.SelectedIndex = i;
                break;
            }
            i++;
        }
        //t_preferedPayment.SelectedIndex
        t_street.Text = CurrentCustomer.Address.Street;
        t_houseNumberName.Text = CurrentCustomer.Address.PropertyNameNumber;
        //now we need to populate the current customer
    }

    private void bnt_Cancel(object sender, EventArgs e)
    {
		Navigation.PopAsync();
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
            CurrentCustomer.Address.Area = t_area.Text;
            CurrentCustomer.Address.Postcode = t_postcode.Text;
            CurrentCustomer.Address.Street = t_street.Text;
            CurrentCustomer.Address.City = t_city.Text;
            CurrentCustomer.Address.PropertyNameNumber = t_houseNumberName.Text;

            CurrentCustomer.DateAdded = t_date.Date;
            CurrentCustomer.Balance = (float)Convert.ToDouble(t_balance.Text);
            CurrentCustomer.Email = t_email.Text;
            CurrentCustomer.DateBalanceLastUpdate = DateTime.Now;
            CurrentCustomer.FName = t_fName.Text;
            CurrentCustomer.NormalPaymentMethord = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)t_preferedPayment.SelectedItem);
            CurrentCustomer.Phone = t_phone.Text;
        }

        Customer.Save();
        Navigation.PopAsync();

    }

    private void bnt_Delete(object sender, EventArgs e)
    {
        Customer.Delete(CurrentCustomer.Id);
        Navigation.PopAsync();
    }
}