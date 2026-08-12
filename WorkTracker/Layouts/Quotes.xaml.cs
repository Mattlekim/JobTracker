namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The quotes, on a page of their own under Work.
///
/// A quote is priced up work that has not been taken on: it is not due, it
/// cannot be done or paid for, and it is kept in its own list rather than
/// with the round. That is exactly why it wants a page - a quote given out
/// and then not looked at again is a quote forgotten, and it has nowhere to
/// show up among work that is due.
///
/// Accepting one is the only way a quote becomes work. It keeps its id, its
/// price and how often it is to be done, and is put on the round from the
/// day chosen.
/// </summary>
public partial class Quotes : ContentPage
{
    public Quotes()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Build();
    }

    private void Build()
    {
        vsl_list.Clear();

        List<Job> quotes = Job.QueryQuotes();

        quotes = quotes
            .OrderBy(x => x.SortStreet, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SortHouseNumber)
            .ThenBy(x => x.SortHouseSuffix, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        l_empty.IsVisible = quotes.Count == 0;

        float total = 0;
        foreach (Job q in quotes)
            total += q.EffectivePrice;

        l_total.Text = quotes.Count == 0
            ? "No quotes out"
            : $"{(quotes.Count == 1 ? "1 quote" : $"{quotes.Count} quotes")} out, {Gloable.CurrenceSymbol}{total:0.00} in all";

        foreach (Job q in quotes)
            vsl_list.Add(Card(q));
    }

    private Border Card(Job quote)
    {
        VerticalStackLayout inner = new VerticalStackLayout() { Spacing = 4 };

        inner.Add(new Label()
        {
            Text = $"{quote.JobFormattedHouseNumber} {quote.JobFormattedStreetOnly}".Trim(),
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
        });

        string where = $"{quote.JobFormattedCity} {quote.JobFormattedArea}".Trim();
        if (where.Length > 0)
            inner.Add(Caption(where));

        List<string> about = new List<string>();
        about.Add($"{Gloable.CurrenceSymbol}{quote.EffectivePrice:0.00}");

        if (!string.IsNullOrWhiteSpace(quote.Name))
            about.Add(quote.Name);

        about.Add(quote.IsOneOff
            ? "one off"
            : $"every {quote.Frequence} {quote.Frequence_Type.ToString().ToLower()}{(quote.Frequence == 1 ? string.Empty : "s")}");

        if (quote.EstimatedTime > 0)
            about.Add($"{quote.EstimatedTime} mins");

        inner.Add(Caption(string.Join("   ", about)));

        Customer c = quote.GetCustomer();
        if (c != null && c.HaveContact)
            inner.Add(Caption(c.FormattedContact));

        if (!string.IsNullOrWhiteSpace(quote.Notes))
            inner.Add(new Label()
            {
                Text = quote.Notes,
                FontSize = 12,
                TextColor = Colors.White,
                BackgroundColor = Color.FromArgb("#FB8C00"),
                Padding = new Thickness(6, 2),
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 4, 0, 0),
            });

        HorizontalStackLayout buttons = new HorizontalStackLayout()
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
        };

        Button accept = Action("Accept", "#2E7D32");
        accept.Clicked += async (s, e) => await AcceptQuote(quote);
        buttons.Add(accept);

        Button delete = Action("Delete", "#E53935");
        delete.Clicked += async (s, e) => await DeleteQuote(quote);
        buttons.Add(delete);

        inner.Add(buttons);

        return new Border()
        {
            Style = (Style)Resources["Card"],
            Content = inner,
        };
    }

    private Label Caption(string text)
    {
        return new Label()
        {
            Text = text,
            FontSize = 12,
            TextColor = Color.FromArgb("#6B7280"),
        };
    }

    private Button Action(string text, string colour)
    {
        return new Button()
        {
            Text = text,
            FontSize = 13,
            Padding = new Thickness(14, 6),
            CornerRadius = 8,
            BorderWidth = 2,
            BorderColor = Color.FromArgb(colour),
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb(colour),
        };
    }

    /// <summary>
    /// the quote was taken up. when it starts is asked in the stretches work
    /// actually starts in rather than as a date to type, because this gets
    /// used standing at the door
    /// </summary>
    private async Task AcceptQuote(Job quote)
    {
        string[] choices = { "Today", "Tomorrow", "Next week", "In 2 weeks", "In a month" };
        string picked = await DisplayActionSheet("Start it when?", "Cancel", null, choices);

        DateTime due = UsfulFuctions.DateNow;
        switch (picked)
        {
            case "Today":
                break;

            case "Tomorrow":
                due = due.AddDays(1);
                break;

            case "Next week":
                due = due.AddDays(7);
                break;

            case "In 2 weeks":
                due = due.AddDays(14);
                break;

            case "In a month":
                due = due.AddMonths(1);
                break;

            //cancelled, or the sheet was dismissed
            default:
                return;
        }

        if (!Job.AcceptQuote(quote, due))
            return;

        quote.Refresh();
        quote.RefreshColors();
        DataRefreshNotifier.NotifyDataChanged();
        Build();

        await DisplayAlert("On The Round",
            $"{quote.JobFormattedStreet} is on the work list, due {due.ToShortDateString()}.", "Ok");
    }

    private async Task DeleteQuote(Job quote)
    {
        if (!await DisplayAlert("Delete Quote",
                $"The quote for {quote.JobFormattedStreet} will be deleted.\n\nThe customer stays on file.",
                "Delete", "Cancel"))
            return;

        Job.DeleteQuote(quote.Id);
        DataRefreshNotifier.NotifyDataChanged();
        Build();
    }

    private void bnt_newQuote_Clicked(object sender, EventArgs e)
    {
        NewJob.AddNewJob = true;
        NewJob.AddAsQuote = true;
        NewJob.JobToAdd = null;

        NewJob nj = new NewJob();
        nj.OnJobAdded += (Job j) => Build();
        Navigation.PushAsync(nj);
    }
}
