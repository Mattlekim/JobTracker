
using Kernel;

namespace UiInterface.Layouts;
using System.Collections.ObjectModel;
using System.ComponentModel;

public partial class LinkCustomerLayout : ContentPage
{
	public static string TextOutput;

	public string DefaultSearch = null;
	public bool AutoClose = true;

	private ObservableCollection<Customer> customers;
	private List<Customer> baseCustomers = new List<Customer>();

	public Action<Customer> OnFound;
	public LinkCustomerLayout()
	{
		InitializeComponent();


		l_pRef.Text = $"{TextOutput}";

		//	p_customerList.Items.Clear();
		customers = new ObservableCollection<Customer>();
		lv_Customers.ItemsSource = customers;


        baseCustomers = Customer.Query();
        tmpList.Clear();

		if (DefaultSearch != null)
		{
			tmpList = baseCustomers.FindAll(x => x.FormattedAddress.Contains(DefaultSearch, StringComparison.OrdinalIgnoreCase));

			if (tmpList.Count > 0)
				sb_Customers.Text = DefaultSearch;
		}
		
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

		Done();
        
    }

	private async void Done()
    {
		Customer c = lv_Customers.SelectedItem as Customer;
		if (c == null)
		{
			await DisplayAlert("Error #102", "There was an unexpected error. Please try again.", "Ok");
            if (AutoClose)
                await Navigation.PopAsync();
            return;
		}

		if (OnFound != null)
			OnFound(c);

		if (AutoClose)
			await Navigation.PopAsync();

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