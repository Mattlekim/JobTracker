namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Somebody else's work list, taken on as a .rwk and worked from here.
///
/// The list stays encrypted on disk and is opened with its PIN every time
/// extra work is entered - leaving (the My Work gate) forgets the PIN and
/// the list both, which is what makes handing a list over safe: the phone
/// it lands on cannot show it to anybody who does not know the PIN.
///
/// Work is marked off much like on the owner's own list - done, skipped,
/// tagged front only - but it cannot be cancelled: the round is not this
/// phone's to change. Paid is only offered when the sender ticked
/// "allow them to collect". Every mark is written straight back into the
/// encrypted file, so nothing is lost to the app being closed mid-round.
///
/// Return Work sends the list back as it stands. The sender's copy of the
/// app matches it up by the key in the file's header and opens it with the
/// PIN kept on their side - see Kernel/WorkShare.cs.
/// </summary>
public partial class ExtraWork : ContentPage
{
    /// <summary>one question at a time, however many times the page appears</summary>
    private bool _askingForPin;

    public ExtraWork()
    {
        InitializeComponent();

        ToolbarItem remove = new ToolbarItem();
        remove.Text = "Remove From Phone";
        remove.Order = ToolbarItemOrder.Secondary;
        remove.Clicked += bnt_remove_Clicked;
        ToolbarItems.Add(remove);

        NavigatedTo += ExtraWork_NavigatedTo;
    }

    private void ExtraWork_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        Refresh();

        //arriving with the list locked is arriving to open it
        if (WorkShare.HaveExtraWork() && WorkShare.OpenedWork == null)
            PromptForPin();
    }

    private void Refresh()
    {
        if (!WorkShare.HaveExtraWork())
        {
            brd_header.IsVisible = false;
            sv_jobs.IsVisible = false;
            vsl_locked.IsVisible = true;
            l_locked.Text = "There is no extra work on this phone. A work list somebody sends you opens with Work Tracker and lands here.";
            bnt_unlock.IsVisible = false;
            return;
        }

        if (WorkShare.OpenedWork == null)
        {
            brd_header.IsVisible = false;
            sv_jobs.IsVisible = false;
            vsl_locked.IsVisible = true;
            l_locked.Text = "This work list is locked. It needs the PIN it was sent with.";
            bnt_unlock.IsVisible = true;
            return;
        }

        vsl_locked.IsVisible = false;
        brd_header.IsVisible = true;
        sv_jobs.IsVisible = true;
        BuildList();
    }

    private async void PromptForPin()
    {
        if (_askingForPin)
            return;

        _askingForPin = true;

        try
        {
            while (true)
            {
                string pin = await DisplayPromptAsync("Extra Work",
                    "Enter the PIN this work was sent with.", "Unlock", "Not Now",
                    keyboard: Keyboard.Numeric, maxLength: 12);

                //walked away from: back to the phone's own tabs
                if (pin == null)
                {
                    WorkTracker.AppShell.BackOutOfExtraWork();
                    return;
                }

                if (WorkShare.Unlock(pin))
                {
                    //the tabs cut down to the extra work now it is open
                    WorkTracker.AppShell.EnterExtraWork();
                    Refresh();
                    return;
                }

                if (!await DisplayAlert("Extra Work", "That is not the PIN this work was sent with.",
                        "Try Again", "Not Now"))
                {
                    WorkTracker.AppShell.BackOutOfExtraWork();
                    return;
                }
            }
        }
        finally
        {
            _askingForPin = false;
        }
    }

    private void bnt_unlock_Clicked(object sender, EventArgs e)
    {
        PromptForPin();
    }

    //  ------------------------------------------------------------  the list

    private void BuildList()
    {
        SharedWorkData work = WorkShare.OpenedWork;
        if (work == null)
            return;

        l_summary.Text = work.Jobs.Count == 1 ? "1 job of extra work" : $"{work.Jobs.Count} jobs of extra work";
        RefreshProgress();

        vsl_list.Children.Clear();
        foreach (SharedJob job in work.Jobs)
            vsl_list.Children.Add(BuildCard(job));
    }

    private void RefreshProgress()
    {
        SharedWorkData work = WorkShare.OpenedWork;
        if (work == null)
            return;

        int done = work.Jobs.Count(x => x.Done);
        int skipped = work.Jobs.Count(x => x.Skipped);
        int left = work.Jobs.Count(x => !x.Done && !x.Skipped);

        List<string> parts = new List<string>();
        parts.Add($"{done} done");
        if (skipped > 0)
            parts.Add($"{skipped} skipped");
        if (work.AllowCollect)
            parts.Add($"{work.Jobs.Count(x => x.Paid)} paid");
        parts.Add($"{left} left");
        parts.Add($"sent {work.SentOn.ToShortDateString()}");

        l_progress.Text = string.Join(" • ", parts);
    }

    /// <summary>
    /// one job as a card. rebuilt in place after every mark rather than
    /// rebuilding the whole list, so the page does not jump back to the top
    /// under somebody working down it
    /// </summary>
    private View BuildCard(SharedJob job)
    {
        SharedWorkData work = WorkShare.OpenedWork;

        VerticalStackLayout stack = new VerticalStackLayout() { Spacing = 4 };

        Label address = new Label()
        {
            Text = job.FormattedAddress,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
        };
        stack.Children.Add(address);

        List<string> about = new List<string>();
        if (!string.IsNullOrWhiteSpace(job.CustomerName))
            about.Add(job.CustomerName);
        if (!string.IsNullOrWhiteSpace(job.JobType))
            about.Add(job.JobType);
        if (!string.IsNullOrWhiteSpace(job.Frequency))
            about.Add(job.Frequency);
        if (job.DueDate > UsfulFuctions.DateBase)
            about.Add($"Due {job.DueDate.ToShortDateString()}");
        if (about.Count > 0)
            stack.Children.Add(Caption(string.Join(" • ", about)));

        if (job.HasPrice)
            stack.Children.Add(new Label() { Text = $"Price {Gloable.CurrenceSymbol}{job.Price}" });

        if (!string.IsNullOrWhiteSpace(job.Notes))
            stack.Children.Add(new Label() { Text = job.Notes, FontAttributes = FontAttributes.Italic, FontSize = 13 });

        if (!string.IsNullOrWhiteSpace(job.Phone))
            stack.Children.Add(Caption(job.Phone));

        if (job.Done || job.Skipped || job.Paid || job.Tags.Count > 0)
        {
            Label status = new Label()
            {
                Text = job.FormattedStatus,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = job.Done ? Colors.Green : (job.Skipped ? Colors.Orange : Colors.SteelBlue),
            };
            stack.Children.Add(status);
        }

        //what can be done to it. no cancel anywhere: the work is not this
        //phone's to take off the round
        HorizontalStackLayout buttons = new HorizontalStackLayout() { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };

        Button done = SmallButton(job.Done ? "Clear" : "Done");
        done.Clicked += (s, e) => ToggleDone(job);
        buttons.Children.Add(done);

        if (!job.Done)
        {
            Button skip = SmallButton(job.Skipped ? "Unskip" : "Skip");
            skip.Clicked += (s, e) => ToggleSkip(job);
            buttons.Children.Add(skip);
        }

        Button tag = SmallButton("Tag");
        tag.Clicked += (s, e) => PickTag(job);
        buttons.Children.Add(tag);

        if (work != null && work.AllowCollect)
        {
            Button paid = SmallButton(job.Paid ? "Unpaid" : "Paid");
            paid.Clicked += (s, e) => TogglePaid(job);
            buttons.Children.Add(paid);
        }

        stack.Children.Add(buttons);

        Border card = new Border()
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle() { CornerRadius = 12 },
            Padding = 14,
            Content = stack,
        };
        card.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F2F4F7"), Color.FromArgb("#1E1E1E"));
        return card;
    }

    private static Label Caption(string text)
    {
        Label caption = new Label() { Text = text, FontSize = 12 };
        caption.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
        return caption;
    }

    private static Button SmallButton(string text)
    {
        return new Button()
        {
            Text = text,
            FontSize = 13,
            Padding = new Thickness(12, 6),
            MinimumHeightRequest = 34,
        };
    }

    /// <summary>the mark has changed: redraw that card, save, update the header</summary>
    private void CardChanged(SharedJob job)
    {
        SharedWorkData work = WorkShare.OpenedWork;
        if (work == null)
            return;

        int index = work.Jobs.IndexOf(job);
        if (index >= 0 && index < vsl_list.Children.Count)
            vsl_list.Children[index] = BuildCard(job);

        RefreshProgress();

        //straight back into the encrypted file, so nothing marked off today
        //is sitting only in memory
        WorkShare.SaveOpenedWork();
    }

    //  -----------------------------------------------------------  the marks

    private async void ToggleDone(SharedJob job)
    {
        if (job.Done)
        {
            if (!await DisplayAlert("Clear", $"{job.FormattedAddress}\n\nTake the done mark back off?", "Clear It", "Leave It"))
                return;

            job.Done = false;
            job.DoneOn = UsfulFuctions.DateBase;
        }
        else
        {
            job.Done = true;
            job.DoneOn = UsfulFuctions.DateNow;
            job.Skipped = false;
        }

        CardChanged(job);
    }

    private void ToggleSkip(SharedJob job)
    {
        job.Skipped = !job.Skipped;
        CardChanged(job);
    }

    /// <summary>
    /// front only and the rest. the tags offered are this phone's own list -
    /// they travel back with the work as plain words, so the two phones do
    /// not need matching lists
    /// </summary>
    private async void PickTag(SharedJob job)
    {
        const string typeOne = "Type one in…";

        List<string> options = new List<string>();
        foreach (string name in Job.TagNames)
            options.Add(job.Tags.Contains(name) ? $"✓ {name}" : name);
        options.Add(typeOne);

        string picked = await DisplayActionSheet("Tag this job", "Cancel", null, options.ToArray());
        if (picked == null || picked == "Cancel")
            return;

        if (picked == typeOne)
        {
            picked = await DisplayPromptAsync("Tag", "What was different about this job?");
            if (string.IsNullOrWhiteSpace(picked))
                return;
            picked = picked.Trim();
        }
        else if (picked.StartsWith("✓ "))
            picked = picked.Substring(2);

        //picking a tag that is on takes it back off
        if (job.Tags.Contains(picked))
            job.Tags.Remove(picked);
        else
            job.Tags.Add(picked);

        CardChanged(job);
    }

    private async void TogglePaid(SharedJob job)
    {
        if (job.Paid)
        {
            if (!await DisplayAlert("Unpaid", $"{job.FormattedAddress}\n\nTake the paid mark back off?", "Take It Off", "Leave It"))
                return;

            job.Paid = false;
            job.PaidAmount = 0;
            CardChanged(job);
            return;
        }

        string suggested = job.HasPrice ? job.Price.ToString() : string.Empty;
        string amount = await DisplayPromptAsync("Paid", "How much was taken?", "Paid", "Cancel",
            initialValue: suggested, keyboard: Keyboard.Numeric);

        if (amount == null)
            return;

        if (!float.TryParse(amount, out float paid) || paid < 0)
        {
            await DisplayAlert("Paid", "That is not an amount.", "Ok");
            return;
        }

        job.Paid = true;
        job.PaidAmount = paid;
        CardChanged(job);
    }

    //  --------------------------------------------------  returning the work

    /// <summary>
    /// the list goes back as it stands - done, skipped, tagged and paid
    /// travel with it. it can be returned part way through and again later:
    /// each return is the state at that moment
    /// </summary>
    private async void bnt_return_Clicked(object sender, EventArgs e)
    {
        SharedWorkData work = WorkShare.OpenedWork;
        if (work == null)
            return;

        int left = work.Jobs.Count(x => !x.Done && !x.Skipped);
        string warning = left > 0 ? $"\n\n{left} job(s) are not marked done or skipped yet." : string.Empty;

        if (!await DisplayAlert("Return Work",
                $"Send the work back as it stands?{warning}", "Return It", "Not Yet"))
            return;

        try
        {
            string fileName = $"Returned Work {DateTime.Now:yyyy-MM-dd}{WorkShare.Extension}";
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);
            if (!WorkShare.WriteReturn(path))
                return;

            await Share.RequestAsync(new ShareFileRequest("Return Work", new ShareFile(path)));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Return Work", ex.Message, "Ok");
            return;
        }

        if (await DisplayAlert("Return Work",
                "Once it has been sent, do you want to take the extra work off this phone? "
                + "Keep it if you are not finished with it.", "Take It Off", "Keep It"))
        {
            WorkShare.RemoveExtraWork();
            WorkTracker.AppShell.RefreshShareTabs();
            WorkTracker.AppShell.BackOutOfExtraWork();
        }
    }

    private async void bnt_remove_Clicked(object sender, EventArgs e)
    {
        if (!await DisplayAlert("Remove Extra Work",
                "Take this work list off the phone? Anything marked off and not returned goes with it.",
                "Remove It", "Keep It"))
            return;

        WorkShare.RemoveExtraWork();
        WorkTracker.AppShell.RefreshShareTabs();
        WorkTracker.AppShell.BackOutOfExtraWork();
    }
}
