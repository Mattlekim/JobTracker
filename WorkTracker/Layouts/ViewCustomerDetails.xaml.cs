namespace UiInterface.Layouts;

using Kernel;
public partial class ViewCustomerDetails : ContentPage
{
	List<Job> _customerJobs = new List<Job>();

	public static Job CurrentJob;

    public Action<Job> OnJobDetialsUpdated;

    public ViewCustomerDetails()
    {
        InitializeComponent();
        if (CurrentJob == null)
            return;

        if (CurrentJob.HaveCanceled)
            tbi_cancelJob.Text = "Resume Job";
        else
            tbi_cancelJob.Text = "Cancel Job";

        Job job = Job.Query(QueryType.CustomerId, CurrentJob.CustomerId).FirstOrDefault();

        while (job != null)
        {
            _customerJobs.Add(job);
            job = Job.Query(QueryType.JobId, job.JobNextId).FirstOrDefault();
        }

     

       

        List<History> history = new List<History>();

        foreach (Job j in _customerJobs)
            history.Add(new History(j));

        List<Payment> payments = Payment.Query(QueryType.CustomerId, CurrentJob.CustomerId);
        foreach (Payment p in payments)
            history.Add(new History(p));

        history = history.OrderByDescending(x => x.SortDate).ToList();


        bool altColour = false;
        for (int i = 0; i < history.Count; i++)
        {
            if (Application.Current.PlatformAppTheme == AppTheme.Dark)
            {
                if (altColour)
                    history[i].AltColour = WorkPlanner.MainColorDark;
                else
                    history[i].AltColour = WorkPlanner.altColorDark;
            }
            else
            {
                if (altColour)
                    history[i].AltColour = WorkPlanner.MainColor;
                else
                    history[i].AltColour = WorkPlanner.altColor;
            }

            altColour = !altColour;
            //_jobsToAddFrom = i;
        }

        cv_jobList.ItemsSource = history;

        Customer c = CurrentJob.GetCustomer();

        History h = new History(CurrentJob);
        l_owing.BindingContext = h;
        l_creditDebit.BindingContext = h;
       
        l_customerName.Text = $"{c.FName} {c.SName}";
        l_customerAddressl1.Text = $"{c.Address.PropertyNameNumber} {c.Address.Street}";
        l_customerAddressl2.Text = $"{c.Address.City}";
        if (c.Address.Area == null || c.Address.Area == string.Empty)
            l_customerAddressl3.IsVisible = false;
        else
            l_customerAddressl3.IsVisible = true;
        l_customerAddressl3.Text = $"{c.Address.Area}";

        if (c.Address.Postcode == null || c.Address.Postcode == string.Empty)
            l_customerAddressl4.IsVisible = false;
        else
            l_customerAddressl4.IsVisible = true;
        l_customerAddressl4.Text = $"{c.Address.Postcode}";

        l_phone.Text = c.Phone;
        l_email.Text = c.Email;



    }

    private async void l_emailClicked(object sender, EventArgs e)
    {
        List<Job> jobs = new List<Job>();
        jobs.Add(CurrentJob);
        await WorkPlanner.EmailCustomers(jobs, DateTime.Now, string.Empty, this);
    }

    private async void l_phoneClicked(object sender, EventArgs e)
    {
        List<Job> jobs = new List<Job>();
        jobs.Add(CurrentJob);
        await WorkPlanner.TextCustomers(jobs, DateTime.Now, string.Empty, this);
    }

    private void tbi_EditDetails_Clicked(object sender, EventArgs e)
    {
        NewJob.AddNewJob = false;
        NewJob.JobToAdd = CurrentJob;

        NewJob nj = new NewJob();
        nj.OnJobUpdated += (j) =>
        {
            if (OnJobDetialsUpdated != null)
                OnJobDetialsUpdated(CurrentJob);
        };

        Navigation.PushAsync(nj);
    }

    private void tbi_Cancel_Job_Clicked(object sender, EventArgs e)
    {
        if (!CurrentJob.HaveCanceled)
            CurrentJob.CancelJob();
        else
            CurrentJob.UnCancelJob();

        if (CurrentJob.HaveCanceled)
            tbi_cancelJob.Text = "Resume Job";
        else
            tbi_cancelJob.Text = "Cancel Job";
    }
}

