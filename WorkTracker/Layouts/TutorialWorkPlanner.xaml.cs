namespace UiInterface.Layouts;

public partial class TutorialWorkPlanner : ContentPage
{
	public TutorialWorkPlanner()
	{
		InitializeComponent();
	}

    private void bntOk_Clicked(object sender, EventArgs e)
    {
		Navigation.PopAsync();
    }
}