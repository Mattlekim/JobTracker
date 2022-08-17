namespace UiInterface.Layouts;

using Kernel;
using System.Collections.ObjectModel;
using System.ComponentModel;
public partial class QuickAddCustomer : ContentPage
{
	public static Location TheAddress;
	public QuickAddCustomer()
	{
		InitializeComponent();
		vsl_main.BindingContext = TheAddress;
	}
}