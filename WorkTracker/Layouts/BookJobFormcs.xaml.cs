namespace UiInterface.Layouts;
using Kernel;
public partial class BookJobFormcs : ContentPage
{
    public static List<Job> jobs;
    public BookJobFormcs()
    {
        InitializeComponent();
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
        MsgCustomers();
        Job.Save();
        Navigation.PopAsync();
    }
}