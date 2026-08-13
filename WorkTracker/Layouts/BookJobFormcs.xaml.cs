namespace UiInterface.Layouts;
using Kernel;
public partial class BookJobFormcs : ContentPage
{
    public static List<Job> jobs;

    /// <summary>
    /// the day the form opens on. work booked in from a day that has already
    /// been picked - a day tapped on the calendar - should not open on today
    /// and have the date typed in again. it is used once and goes back to
    /// today, so a caller with no particular day in mind cannot pick up the
    /// day somebody else set
    /// </summary>
    public static DateTime BookForDate = DateTime.MinValue;

    public BookJobFormcs()
    {
        InitializeComponent();

        dp_bookinDate.Date = BookForDate > DateTime.MinValue ? BookForDate.Date : DateTime.Now.Date;
        BookForDate = DateTime.MinValue;

        List<string> strings = new List<string>();
        float value = 0;
        int estimatedTime = 0;
        bool timeIsEstimate = false;
        foreach (Job j in jobs)
        {
            strings.Add(j.GetCustomer()?.FormattedAddress);
            if (j.TNB)
                strings[strings.Count - 1] = $"TNB*  {strings[strings.Count - 1]}";
            value += j.Price;
            estimatedTime += j.EstimatedTime;
            if (j.EstimatedTime == 0)
                timeIsEstimate = true;
        }

        
        lv_jobs.ItemsSource = strings;

        if (estimatedTime == 0)
            l_estimatedTime.Text = $"Unknown amount of time to complete";
        else
            if (timeIsEstimate)
            l_estimatedTime.Text = $"More than {estimatedTime}";
        else
            l_estimatedTime.Text = $"{estimatedTime}";

        l_value.Text = $"{Gloable.CurrenceSymbol}{value}";

    }

    /// <summary>
    /// the tags to put on the work being booked in. a tag says what this time
    /// of doing the job was like, so it goes on the jobs themselves - there is
    /// nothing else for a booking to keep it on
    /// </summary>
    private readonly List<string> _tags = new List<string>();

    private async void bnt_addTag(object sender, EventArgs e)
    {
        string tag = await TagPicker.AskAsync(this, "Tag This Work");
        if (tag == null)
            return;

        if (!_tags.Exists(x => string.Equals(x, tag, StringComparison.CurrentCultureIgnoreCase)))
            _tags.Add(tag);

        ShowTags();
    }

    private void bnt_clearTags_Clicked(object sender, EventArgs e)
    {
        _tags.Clear();
        ShowTags();
    }

    private void ShowTags()
    {
        l_tags.Text = string.Join(" • ", _tags);
        l_tags.IsVisible = _tags.Count > 0;
        bnt_clearTags.IsVisible = _tags.Count > 0;
    }

    private async void MsgCustomers()
    {
        string msgBody = string.Empty;
        
        foreach (Job j in jobs)
        {
            if (j.TNB)
            {
                if (msgBody == String.Empty)
                    msgBody = "The following customers will be texted";

                msgBody = $"{msgBody}\n{j.JobFormattedStreet}";
            }
          
        }

        bool sendEmail = false;
        foreach (Job j in jobs)
        {
            if (j.ENB)
            {
                if (!sendEmail)
                {
                    sendEmail = true;
                    msgBody += "\n\nThe following customers will be emailed";
                }

                msgBody = $"{msgBody}\n{j.JobFormattedStreet}";
            }

        }
        
        if (msgBody.Length > 0)
        {
            if (await DisplayAlert("Send messages to customers?", msgBody, "Yes", "No"))
            {
                
               await WorkPlanner.TextCustomers(jobs, dp_bookinDate.Date, WorkPlanner.DefaultTNBMessage, this);
                

               await WorkPlanner.EmailCustomers(jobs, dp_bookinDate.Date, WorkPlanner.DefaultTNBMessage, this);
            }

        }
    }
    private void bnt_Confirmed(object sender, EventArgs e)
    {
        Booking.AddBooking(jobs, dp_bookinDate.Date);

        //the tags go on the work being booked in rather than on the booking:
        //a booking is worked out from the jobs and never saved, so a tag kept
        //on it would be gone by the time the history was looked at
        int known = Job.TagNames.Count;

        foreach (string tag in _tags)
            Booking.TagJobs(jobs, tag);

        //a tag typed in here is new to the round, and the list of tags to
        //pick from lives with the settings
        if (Job.TagNames.Count != known)
            Settings.Save();

        MsgCustomers();
        Job.Save();
        Navigation.PopAsync();
    }
}