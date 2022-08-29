using Android.App.AppSearch;
using Kernel;

namespace UiInterface.Layouts;
using System.Collections.ObjectModel;
using System.ComponentModel;

public partial class LinkCustomerLayout : ContentPage
{
	public static string Reference;

	private ObservableCollection<Customer> customers;
	private List<Customer> baseCustomers = new List<Customer>();

	public LinkCustomerLayout()
	{
		InitializeComponent();


		l_pRef.Text = $"Payment Reference: {Reference}";

		//	p_customerList.Items.Clear();
		customers = new ObservableCollection<Customer>();
		lv_Customers.ItemsSource = customers;


        baseCustomers = Customer.Query();
        tmpList.Clear();
        tmpList = baseCustomers.FindAll(x => x.FormattedAddress.Contains(Reference, StringComparison.OrdinalIgnoreCase));

		if (tmpList.Count > 0)
			sb_Customers.Text = Reference;
		
       // customers = new ObservableCollection<Customer>(Customer.Query());
	//	foreach (Customer c in customers)
		//	p_customerList.Items.Add($"{c.Address.PropertyNameNumber} {c.Address.Street} {c.Address.Area}");
	}

    private void bnt_Link(object sender, EventArgs e)
    {
		if (lv_Customers.SelectedItem == null)
		{
			DisplayAlert("Error", "No Customer Selected", "Ok");
			return;
		}

		LinkPayments();
        
    }

	private async void LinkPayments()
    {
		Customer c = lv_Customers.SelectedItem as Customer;
		if (c == null)
			return;

       if (await DisplayAlert("Link?", $"Are you sure you want to link {c.FormattedAddress} to the reference '{Reference}'", "Yes", "No"))
        {
			if (c.PaymentRefrences.Contains(Reference))
				await DisplayAlert("Already Linked", $"This payment reference has already been linked", "Ok");
			else
				c.PaymentRefrences.Add(Reference);

			Customer.Save();
			await Navigation.PopAsync();
        }
    }

	private List<Customer> tmpList = new List<Customer>();
	private void sb_TextChanged(object sender, TextChangedEventArgs e)
	{
        SearchBar searchBar = (SearchBar)sender;
        baseCustomers = Customer.Query();
		tmpList.Clear();
		tmpList = baseCustomers.FindAll(x => x.FormattedAddress.Contains(searchBar.Text, StringComparison.OrdinalIgnoreCase));
		customers.Clear();
		foreach (Customer c in tmpList)
			customers.Add(c);

		lv_Customers.SelectedItem = null;
		//customers = new ObservableCollection<Customer>(baseCustomers);
      //  lv_Customers.ItemsSource = customers;
        //lv_Customers.BindingContext = customers;
        //lv_Customers.ItemsSource = DataService.GetSearchResults(searchBar.Text);
    }
}