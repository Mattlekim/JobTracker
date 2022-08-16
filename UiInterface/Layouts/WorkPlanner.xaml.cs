namespace UiInterface.Layouts;

using Kernel;
public partial class WorkPlanner : ContentPage
{
	public WorkPlanner()
	{
		InitializeComponent();
		Job.Load();
        Job.Add(new Job()
        {
            CustomerId = 0,
            Price = 10,
            DateCompleated = new DateTime(2022, 05, 05),
            IsCompleted = true,
        });
        NavigatedTo += WorkPlanner_NavigatedTo;
        //Job.SortJobsByDateDue();
      //  Job.RefreshJobs();
       
        
    }

    private void WorkPlanner_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        RefreshPage();
    }

    private List<Job> GetJobs()
    {
        List<Job> jobs = Job.Query();
        jobs.RemoveAll(x => x.HaveCanceled & (DateTime.Now - x.DateCanceled).Days != 0 || x.IsCompleted & (DateTime.Now - x.DateCompleated).Days > 7);
        foreach (Job j in jobs)
            j.Refresh();
        jobs = jobs.OrderBy(x => x.DateCompleated).ThenBy(x => x.DueDate).ToList();
        return jobs;
    }

    private void Lv_Jobs_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        return;
        ListView lv = sender as ListView;
        IReadOnlyList<IVisualTreeElement> v = lv.GetVisualTreeDescendants();
        int i = 0;
        foreach (IVisualTreeElement vchild in v)
            if (vchild.GetType() == typeof(SwipeView))
            {
                if (i == e.ItemIndex)
                {
                    SwipeView sv = vchild as SwipeView;
                    sv.Open(OpenSwipeItem.LeftItems);

                }
                i++;
            }
    }

   

    private Job GetJobForSwipe(object sender)
    {
        List<Job> j = Job.Query(QueryType.JobId, Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
        if (j.Count > 0)
            return j[0];
        return null;
    }
    private void On_Job_Compleated(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j.IsCompleted)
            j.UnMarkJobDone();
        else
            j.MarkJobDone();

        RefreshPage();
        Job.Save();
    }

    private void RefreshPage()
    {
        // refreshPage.IsRefreshing = true;
        // lv_Jobs.IsRefreshing = true;
       
       
        lv_Jobs.ItemsSource = null;

        lv_Jobs.ItemsSource = GetJobs();

        int jobsDue = 0;
        float moneyOwed = 0;

        foreach (Job j in Job.Query())
        {
            if (!j.HaveCanceled)
                if (!j.IsCompleted)
                    if ((DateTime.Now - j.DueDate).Days >= 0)
                        jobsDue++;
        }

        foreach (Customer c in Customer.Query())
            if (c.Balance > 0)
                moneyOwed += c.Balance;

        t_job_overview.Text = $"Jobs Due {jobsDue}.   Money Owed {Gloable.CurrenceSymbol}{moneyOwed}";
    
    }
    private void On_Job_Paid(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j.IsPaidFor)
            j.UnMarkJobPaid();
        else
            j.MarkJobPaid();
        RefreshPage();
        Job.Save();
    }

    private void On_Job_CompleatedPaid(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        j.MarkJobDone();
        j.MarkJobPaid();
        RefreshPage();
        Job.Save();
    }

    private bool altColour = false;
    Color altColor = new Color(235, 235, 255);
    private void list_child_added(object sender, ElementEventArgs e)
    {
        if (e.Element is HorizontalStackLayout)
        {
            VerticalStackLayout vsl = sender as VerticalStackLayout;

            if (altColour)
                vsl.BackgroundColor = altColor;
            altColour = !altColour;
                

        }

    }

    private void On_Job_Skipped(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        j.SkipJob();
        RefreshPage();
        Job.Save();
    }

    private void On_Job_Canceled(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);
        if (j.HaveCanceled)
            j.UnCancelJob();
        else
            j.CancelJob();
        RefreshPage();
        Job.Save();
    }

    private void On_Job_Detials(object sender, EventArgs e)
    {
        Job j = GetJobForSwipe(sender);

        RefreshPage();
    }

   
    private void swip_started(object sender, SwipeStartedEventArgs e)
    {
        SwipeView sv = sender as SwipeView;
        Job j = GetJobForSwipe(sv.LeftItems[0]);

        if (j.IsCompleted)
        {
            SwipeItem si = sv.LeftItems[0] as SwipeItem;
            si.Text = "Not Done";
        }

        if (j.IsPaidFor)
        {
            SwipeItem si = sv.LeftItems[1] as SwipeItem;
            si.Text = "Not Paid";
        }

        if (j.HaveCanceled)
        {
            SwipeItem si = sv.RightItems[1] as SwipeItem;
            si.Text = "Resume";
        }
    }
}