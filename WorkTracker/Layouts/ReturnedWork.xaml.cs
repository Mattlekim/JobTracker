namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Work sent out with Send Work, come back marked off.
///
/// The file opened itself: the key it carries in the clear found the record
/// kept when the work went out, and the PIN filed there decrypted it -
/// AppShell did that before pushing this page. What is left is looking at
/// what was done and choosing to write it on to the round.
///
/// Update My Work is deliberately a button rather than automatic. It marks
/// jobs done, skipped and paid for real - money and next visits - and that
/// should happen while somebody is looking at what it is about to do, not
/// because a file was tapped.
///
/// Everything the worker touched is tagged with the name tag the work was
/// sent under, so the customer's history says who was there.
/// </summary>
public partial class ReturnedWork : ContentPage
{
    private readonly SharedWorkData _data;
    private readonly SentWorkRecord _record;

    private bool _updated;

    public ReturnedWork(SharedWorkData data, SentWorkRecord record)
    {
        _data = data;
        _record = record;

        InitializeComponent();

        int done = _data.Jobs.Count(x => x.Done);
        int skipped = _data.Jobs.Count(x => x.Skipped);
        int paid = _data.Jobs.Count(x => x.Paid);

        l_summary.Text = $"Work back from {_record.WorkerTag}";

        List<string> parts = new List<string>();
        parts.Add($"{done} done");
        if (skipped > 0)
            parts.Add($"{skipped} skipped");
        if (paid > 0)
            parts.Add($"{paid} paid");
        parts.Add($"of {_data.Jobs.Count} sent {_record.SentOn.ToShortDateString()}");

        //a return opened twice must not quietly charge everything twice.
        //the marks themselves cannot double up - a done job stays done -
        //but the warning saves puzzling over why nothing seems to happen
        if (_record.HasReturned)
            parts.Add($"already updated {_record.ReturnedOn.ToShortDateString()}");

        l_detail.Text = string.Join(" • ", parts);

        BuildList();
    }

    private void BuildList()
    {
        vsl_list.Children.Clear();

        foreach (SharedJob job in _data.Jobs)
        {
            VerticalStackLayout stack = new VerticalStackLayout() { Spacing = 4 };

            stack.Children.Add(new Label()
            {
                Text = job.FormattedAddress,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
            });

            if (!string.IsNullOrWhiteSpace(job.CustomerName) || !string.IsNullOrWhiteSpace(job.JobType))
                stack.Children.Add(Caption(string.Join(" • ",
                    new[] { job.CustomerName, job.JobType }.Where(x => !string.IsNullOrWhiteSpace(x)))));

            string status = job.FormattedStatus;
            stack.Children.Add(new Label()
            {
                Text = status.Length > 0 ? status : "Nothing marked",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = job.Done ? Colors.Green : (job.Skipped ? Colors.Orange : Colors.Gray),
            });

            //said now rather than found out after pressing the button: a job
            //deleted since it was sent has nothing to write the marks on to
            if (FindJob(job) == null)
                stack.Children.Add(new Label()
                {
                    Text = "Not found on your round - this one cannot be updated",
                    FontSize = 12,
                    TextColor = Colors.Red,
                });

            Border card = new Border()
            {
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle() { CornerRadius = 12 },
                Padding = 14,
                Content = stack,
            };
            card.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F2F4F7"), Color.FromArgb("#1E1E1E"));
            vsl_list.Children.Add(card);
        }
    }

    private static Label Caption(string text)
    {
        Label caption = new Label() { Text = text, FontSize = 12 };
        caption.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
        return caption;
    }

    private static Job FindJob(SharedJob shared)
    {
        return Job.Query().Find(x => x.Id == shared.JobId);
    }

    /// <summary>
    /// writes what came back on to the round: done, skipped and paid land on
    /// the jobs they were copied from, each one tagged with the worker's
    /// name tag and whatever tags they put on
    /// </summary>
    private async void bnt_update_Clicked(object sender, EventArgs e)
    {
        if (_updated)
        {
            await DisplayAlert("Returned Work", "This return has already been put on your work.", "Ok");
            return;
        }

        if (_record.HasReturned
            && !await DisplayAlert("Returned Work",
                "This return was opened and updated before. Jobs already marked done or paid are left alone, but anything not applied then is applied now. Carry on?",
                "Carry On", "Cancel"))
            return;

        if (!await DisplayAlert("Update My Work",
                $"Everything {_record.WorkerTag} marked off is written on to your jobs - done, skipped and money taken - and tagged {_record.WorkerTag}. Carry on?",
                "Update", "Cancel"))
            return;

        int done = 0, skipped = 0, paid = 0, missed = 0;
        int knownTags = Job.TagNames.Count;

        foreach (SharedJob shared in _data.Jobs)
        {
            bool touched = shared.Done || shared.Skipped || shared.Paid || shared.Tags.Count > 0;
            if (!touched)
                continue;

            Job job = FindJob(shared);
            if (job == null)
            {
                missed++;
                continue;
            }

            if (shared.Done && !job.IsCompleted)
            {
                //done first: paid needs a completed job's balance behind it.
                //saving is left to the end so one return is one write
                job.MarkJobDone(shared.DoneOn > UsfulFuctions.DateBase ? shared.DoneOn : UsfulFuctions.DateNow,
                    forceNotSave: true);
                done++;
            }

            if (shared.Skipped && !shared.Done && !job.IsCompleted && !job.HaveSkipped)
            {
                //through the same call the swipes use, so the booked day the
                //job may have been on is put right as well
                WorkPlanner.MarkJobSkipped(job);
                skipped++;
            }

            if (_data.AllowCollect && shared.Paid && !job.IsPaidFor)
            {
                job.MarkJobPaid(shared.PaidAmount, PaymentMethod.Cash);
                paid++;
            }

            //what the worker said about the visit, and who the worker was
            foreach (string tag in shared.Tags)
                job.AddTag(tag);
            job.AddTag(_record.WorkerTag);

            job.Refresh();
            job.RefreshColors();
        }

        Job.Save();
        Customer.Save();

        //tags that were new to this phone went on the list to pick from,
        //and that list lives with the settings
        if (Job.TagNames.Count != knownTags)
            Settings.Save();

        WorkShare.MarkReturned(_record);
        _updated = true;
        bnt_update.IsEnabled = false;

        DataRefreshNotifier.NotifyDataChanged();

        string missedText = missed > 0
            ? $"\n\n{missed} job(s) could not be matched to your round and were left alone."
            : string.Empty;

        await DisplayAlert("Updated",
            $"{done} marked done, {skipped} skipped, {paid} paid - all tagged {_record.WorkerTag}.{missedText}",
            "Ok");

        BuildList();
    }
}
