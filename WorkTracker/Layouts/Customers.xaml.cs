namespace UiInterface.Layouts;

using System.Collections.ObjectModel;
using Kernel;

public partial class Customers : ContentPage
{

	public List<Customer> customers;
	public Customers()
	{
        /*
            Customer.Add(new Customer("32", "South Street", "Kiveton Park")
            {
                FName = "Matthew",
                SName = "",
                Phone = "0748394832"
            });

            Customer.Add(new Customer("34", "South Street", "Kiveton Park")
            {
                FName = "Paul",
                SName = "",
                Phone = "0750394832",
                Email = "d-dkdkl@gmail.com"
            });

            Customer.Add(new Customer("38", "South Street", "Kiveton Park")
            {
                FName = "Peter",
                SName = "",
                Email = "m-didld@jd.com"
            });*/
            //Customer.Save();
        
        Customer.Load();

        InitializeComponent();
        _lv_Customers.ItemTapped += _lv_Customers_ItemTapped;
        NavigatedTo += Customers_NavigatedTo;
		//_lv_Customers.SetBinding(ItemsView.ItemsSourceProperty, "CustomerInfo");
    }

    private void _lv_Customers_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        NewCustomer.CurrentCustomer = _lv_Customers.SelectedItem as Customer;
        NewCustomer.AddNewCustomer = false;

        Navigation.PushAsync(new NewCustomer());
    }

    private void _lv_Customers_ItemSelected(object sender, SelectedItemChangedEventArgs e)
    {
       
    }

    private void Customers_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        _lv_Customers.ItemsSource = Customer.Query();
        // throw new NotImplementedException();
    }

    private void Add_Customer_Button_Clicked(object sender, EventArgs e)
    {
        NewCustomer.AddNewCustomer = true;
        Navigation.PushAsync(new NewCustomer());
        //Navigation.PopAsync();
    }

    
    private void bnt_Search(object sender, EventArgs e)
    {
        if (t_searchString.Text == String.Empty)
        {
            _lv_Customers.ItemsSource = Customer.Query();
            return;
        }

        Filter filter = new Filter("name", t_searchString.Text, cb_absolute.IsChecked);
        List<Customer> cust = Customer.Query(filter);

        filter = new Filter("street", t_searchString.Text, cb_absolute.IsChecked);
        cust.AddRange(Customer.Query(filter));

        List<Customer> outList = new List<Customer>();

        foreach(Customer c in cust)
        {
            if (!outList.Contains(c))
                outList.Add(c);
        }

        _lv_Customers.ItemsSource = outList;
    }
}