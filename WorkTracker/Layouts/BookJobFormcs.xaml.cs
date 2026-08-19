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
            //the round's usual counts towards the day, but a house that has
            //never been timed is still a guess, which is what the wording
            //below is about
            estimatedTime += j.Minutes;
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

    /// <summary>
    /// Offers the night-before messages for the work being booked in.
    ///
    /// This hands back a Task rather than being an async void, and the
    /// difference is the whole of the bug it was written for: an async void
    /// cannot be waited for, so the caller carried straight on to
    /// Navigation.PopAsync while this was still sat on its first alert.
    /// The form was then gone, and every alert after that one went to a page
    /// that no longer had a handler to show it - which does not fail, it
    /// simply never comes back. Twelve customers due a text got nothing, and
    /// nothing was said about it either. Anything that puts alerts up must be
    /// finished with before the page it puts them on is taken away.
    /// </summary>
    private async Task MsgCustomers()
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
    /// <summary>
    /// whether the booking is already being confirmed. The form now stays up
    /// through the whole queue of messages, which is long enough to press
    /// the button again - and that would book the same work in twice
    /// </summary>
    private bool _confirming;

    private async void bnt_Confirmed(object sender, EventArgs e)
    {
        if (_confirming)
            return;

        _confirming = true;

        //greyed out as well as guarded, so the button says it has been
        //pressed rather than looking like it did nothing
        if (sender is Button pressed)
            pressed.IsEnabled = false;

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

        //the booking itself is written down first, so it is safe whatever
        //happens to the messages
        Job.Save();

        //  Waited for, and the form is not taken away until it is done.
        //
        //  This used to be fired off and left, with the pop below running
        //  while the messages were still being offered. The first alert had
        //  already been asked for so it appeared and could be answered - and
        //  then the form went, and every alert after it was put on a page
        //  with no handler left to show it. Those do not throw; they simply
        //  never come back. So the run of texts stopped dead before the first
        //  one was composed, with nothing on screen to say so: a day booked
        //  in with twelve to tell went out to nobody.
        await MsgCustomers();

        await Navigation.PopAsync();
    }
}