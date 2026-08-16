namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Sending a handful of jobs to somebody else's copy of the app.
///
/// The jobs were already picked on the work list - this page asks what
/// travels with them (prices, notes, phone numbers, whether money can be
/// taken at the door), who is doing the work, and the PIN the file is
/// locked with. The PIN and the name tag are filed on this phone under the
/// key the file carries, which is what lets the returned work open itself
/// and tag everything with the worker's name - see Kernel/WorkShare.cs.
///
/// Nothing here changes the jobs themselves: what goes out is a copy, and
/// the round carries on as if it had never been sent. What was done to the
/// copy only lands back on the round when the return is opened and
/// Update My Work is pressed.
/// </summary>
public partial class SendWork : ContentPage
{
    private readonly List<Job> _jobs;

    private bool _sending;

    public SendWork(List<Job> jobs)
    {
        _jobs = jobs ?? new List<Job>();

        InitializeComponent();

        l_count.Text = _jobs.Count == 1 ? "Sending 1 job" : $"Sending {_jobs.Count} jobs";
    }

    /// <summary>collecting money means knowing what to collect</summary>
    private void sw_collect_Toggled(object sender, ToggledEventArgs e)
    {
        if (e.Value)
            sw_prices.IsToggled = true;
    }

    private async void bnt_send_Clicked(object sender, EventArgs e)
    {
        //the share sheet can take a while to come back, and two files for
        //one press is two records claiming the same work
        if (_sending)
            return;

        string workerTag = (e_workerTag.Text ?? string.Empty).Trim();
        if (workerTag.Length == 0)
        {
            await DisplayAlert("Send Work",
                "Give the work a name tag - whoever is doing it. It is what the returned work is tagged with.", "Ok");
            return;
        }

        string pin = (e_pin.Text ?? string.Empty).Trim();
        if (pin.Length < 4 || !pin.All(char.IsDigit))
        {
            await DisplayAlert("Send Work", "The PIN needs to be at least 4 digits.", "Ok");
            return;
        }

        if (sw_collect.IsToggled && !sw_prices.IsToggled)
        {
            //the switch handler forces this, but a form should not trust its
            //own wiring with somebody else's money
            await DisplayAlert("Send Work", "Collecting money needs the prices sent with the work.", "Ok");
            return;
        }

        _sending = true;

        try
        {
            SharedWorkData data = WorkShare.BuildShare(_jobs,
                sw_prices.IsToggled, sw_notes.IsToggled, sw_phones.IsToggled,
                sw_collect.IsToggled, workerTag);

            string fileName = $"Work for {workerTag} {DateTime.Now:yyyy-MM-dd}{WorkShare.Extension}";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);

            WorkShare.WriteFile(path, data, pin, WorkShareKind.SentWork);

            //remembered before it is shared: a file that went out without
            //its record could never be opened when it came back
            WorkShare.RememberSentWork(data.Key, pin, workerTag, _jobs.Count);

            //the round says which work is out: every job sent is tagged
            //"Sent To <name>" on this phone, and Update My Work takes the
            //tag off when the return is put on the round. quietly, so the
            //tag picker is not filled up with worker names
            string sentTag = WorkShare.SentTag(workerTag);
            foreach (Job j in _jobs)
                j.AddTagQuietly(sentTag);
            Job.Save();

            await Share.RequestAsync(new ShareFileRequest($"Work for {workerTag}", new ShareFile(path)));

            await DisplayAlert("Send Work",
                $"Tell them the PIN some other way than with the file. When they return the work, open the file on this phone and it updates your jobs, tagged {workerTag}.",
                "Ok");

            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Send Work", ex.Message, "Ok");
        }
        finally
        {
            _sending = false;
        }
    }
}
