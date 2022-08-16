namespace UiInterface.Layouts;

using Kernel;
public partial class JobsList : ContentPage
{
    private static bool JobLoaded = false;
    public JobsList()
	{
		InitializeComponent();
        NavigatedTo += Jobs_NavigatedTo;

        if (!JobLoaded)
        {
            Job.Load();
            JobLoaded = true;
        }

        Job.SortJobsByDateDue();
        lv_Jobs.ItemsSource = Job.Query();
        lv_Jobs.SelectionMode = SelectionMode.Single;
        lv_Jobs.SelectionChanged += Lv_Jobs_SelectionChanged;
      
        
     //   lv_Jobs.Refreshing += Lv_Jobs_Refreshing;
    }

    private void Lv_Jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lv_Jobs.SelectedItem is int)
            return;

        NewJob.JobToAdd = lv_Jobs.SelectedItem as Job;
        NewJob.AddNewJob = false;
        
        Navigation.PushAsync(new NewJob());
    }

    private void Lv_Jobs_ItemTapped(object sender, ItemTappedEventArgs e)
    {
       
    }


   
    private void Jobs_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        lv_Jobs.ItemsSource = null;
        lv_Jobs.ItemsSource = Job.Query();
        lv_Jobs.SelectedItem = -1;
        //lv.ItemsSource = Job.Query();
        //Job.Add(4, 5.5f, 4);

        //here we update the list
    }

    private void bnt_Add_Job(object sender, EventArgs e)
    {
        NewJob.JobToAdd = new Job();
        NewJob.AddNewJob = true;
        Navigation.PushAsync(new NewJob());
    }
}