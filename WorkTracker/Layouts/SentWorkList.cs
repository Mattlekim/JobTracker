namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The work this phone has sent out, and the way to clear it.
///
/// A send normally clears itself: the return comes back, Update My Work
/// writes it on the round and takes the "Sent To" tags off. But not every
/// send comes back as a file - the worker tells you at the gate what got
/// done, or never finishes the week - and the round was left saying the
/// work was out with no way to say it is not.
///
/// Clearing changes nothing on the other phone. The file they have carries
/// everything it needs, so their copy keeps working; what is cleared here
/// is only this phone's memory of the send. The one thing to be careful of
/// is the PIN: it lives on the record, so forgetting a send means a return
/// of it can never open itself here again - which is why taking the tags
/// off and forgetting the send are two different buttons.
/// </summary>
public class SentWorkList : ContentPage
{
    private readonly VerticalStackLayout _list = new VerticalStackLayout() { Spacing = 12 };

    public SentWorkList()
    {
        Title = "Work Sent Out";

        Label explain = new Label()
        {
            Text = "Clearing a send changes nothing on their phone - the file they have still works. "
                + "It only clears what this phone remembers.",
            FontSize = 12,
        };
        explain.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));

        Content = new ScrollView()
        {
            Content = new VerticalStackLayout()
            {
                Padding = 12,
                Spacing = 12,
                Children = { explain, _list },
            },
        };

        NavigatedTo += (s, e) => BuildList();
    }

    private void BuildList()
    {
        _list.Children.Clear();

        List<SentWorkRecord> records = WorkShare.AllRecords();

        if (records.Count == 0)
        {
            _list.Children.Add(new Label()
            {
                Text = "Nothing is out at the moment. Sending work from the work list, a booked day or the calendar puts it here.",
                Padding = new Thickness(4, 10),
            });
            return;
        }

        foreach (SentWorkRecord record in records)
            _list.Children.Add(BuildCard(record));
    }

    private View BuildCard(SentWorkRecord record)
    {
        VerticalStackLayout stack = new VerticalStackLayout() { Spacing = 4 };

        stack.Children.Add(new Label()
        {
            Text = $"Sent to {record.WorkerTag}",
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
        });

        List<string> parts = new List<string>();
        parts.Add(record.JobCount == 1 ? "1 job" : $"{record.JobCount} jobs");
        parts.Add($"sent {record.SentOn.ToShortDateString()}");
        parts.Add(record.HasReturned ? $"came back {record.ReturnedOn.ToShortDateString()}" : "not back yet");

        Label caption = new Label() { Text = string.Join(" • ", parts), FontSize = 12 };
        caption.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
        stack.Children.Add(caption);

        HorizontalStackLayout buttons = new HorizontalStackLayout() { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };

        Button clear = new Button()
        {
            Text = "Clear It",
            FontSize = 13,
            Padding = new Thickness(12, 6),
            MinimumHeightRequest = 34,
        };
        clear.Clicked += (s, e) => ClearRecord(record);
        buttons.Children.Add(clear);

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

    private async void ClearRecord(SentWorkRecord record)
    {
        const string tagsOnly = "Take The Sent Tags Off";
        const string forget = "Forget This Send";

        string choice = await DisplayActionSheet($"Sent to {record.WorkerTag}", "Cancel", null, tagsOnly, forget);

        if (choice == tagsOnly)
        {
            //the work stops reading as out, but the record - and the PIN on
            //it - stays, so a return can still open itself later
            int cleared = WorkShare.ClearSentTags(record.WorkerTag);
            DataRefreshNotifier.NotifyDataChanged();

            await DisplayAlert("Cleared",
                cleared == 0
                    ? $"No jobs were still tagged Sent To {record.WorkerTag}."
                    : $"The tag came off {cleared} job(s). If their return turns up later it can still be opened - the send itself is still remembered here.",
                "Ok");
            return;
        }

        if (choice != forget)
            return;

        //the PIN goes with the record, and that cannot be undone - said
        //plainly, because "clear" must never quietly cost somebody a return
        if (!await DisplayAlert("Forget This Send",
                $"The Sent To {record.WorkerTag} tags come off and this phone forgets the send completely.\n\n"
                + "Their copy still works - but if they send the work back, the returned file can never be opened here, "
                + "because the PIN is forgotten with it. Only do this when the return is not coming, or has already been dealt with.",
                "Forget It", "Keep It"))
            return;

        WorkShare.ClearSentTags(record.WorkerTag);
        WorkShare.ForgetRecord(record);
        DataRefreshNotifier.NotifyDataChanged();
        BuildList();
    }
}
