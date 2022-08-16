using Kernel;

namespace UiInterface.Layouts;

public partial class LinkCustomerLayout : ContentPage
{
	public static string Reference;

	private List<Customer> customers;
	public LinkCustomerLayout()
	{
		InitializeComponent();


		l_pRef.Text = $"Payment Reference: {Reference}";

		p_customerList.Items.Clear();
        customers = Customer.Query();
		foreach (Customer c in customers)
			p_customerList.Items.Add($"{c.Address.PropertyNameNumber} {c.Address.Street} {c.Address.Area}");
	}

    private void bnt_Link(object sender, EventArgs e)
    {
		if (p_customerList.SelectedIndex == -1)
		{
			DisplayAlert("Error", "No Customer Selected", "Ok");
			return;
		}

		LinkPayments();
        
    }

	private async void LinkPayments()
    {
        if (await DisplayAlert("Link?", $"Are you sure you want to link {p_customerList.SelectedItem} to the reference '{Reference}'", "Yes", "No"))
        {
			if (customers[p_customerList.SelectedIndex].PaymentRefrences.Contains(Reference))
				await DisplayAlert("Already Linked", $"This payment reference has already been linked", "Ok");
			else
				customers[p_customerList.SelectedIndex].PaymentRefrences.Add(Reference);

			Customer.Save();
			await Navigation.PopAsync();
        }
    }
}