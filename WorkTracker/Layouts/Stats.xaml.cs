namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The round in figures: what is left to do, what it is worth, what is owed
/// and what has been earned month by month.
///
/// All of it is worked out in Kernel/RoundStats so the same definition of
/// "left to do" is used here as anywhere else, and so the sums can be
/// checked without the app. This page only draws them.
/// </summary>
public partial class Stats : ContentPage
{
    /// <summary>how far back the month by month list goes</summary>
    private const int Months = 12;

    public Stats()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Build();
    }

    private void Build()
    {
        RoundStats stats = RoundStats.Now(Months);

        l_housesLeft.Text = $"{stats.HousesLeft}";
        l_valueLeft.Text = Money(stats.ValueLeft);
        l_timeLeft.Text = stats.FormattedTimeLeft;
        l_owed.Text = Money(stats.MoneyOwed);

        l_leftNote.Text = stats.HousesOverdue > 0
            ? $"{stats.HousesOverdue} of them past their day. {stats.HousesOnTheRound} houses on the round in all."
            : $"Nothing overdue. {stats.HousesOnTheRound} houses on the round in all.";

        if (stats.CustomersOwing == 0)
            l_owedNote.Text = "Nobody owes anything.";
        else
            l_owedNote.Text = stats.CustomersOwing == 1
                ? "1 customer behind."
                : $"{stats.CustomersOwing} customers behind.";

        l_perMonth.Text = Money(stats.ValuePerMonth);
        l_roundNote.Text = "Worked out from how often each house is done, so one done every four weeks counts for "
            + "more than one done every eight. One offs are left out - they are not coming round again.";

        BuildRounds();
        BuildMonths(stats);
    }

    /// <summary>
    /// What each round is, rather than how it is going.
    ///
    /// The cards above are the work in hand - what is left today, what is
    /// overdue, what has been done. A round is not asked about like that: it
    /// is a patch of the work you either have or you do not, so what is worth
    /// knowing about one is how big it is. How many houses, how long they
    /// take, what they come to and what is owed on them.
    ///
    /// Nothing here counts what is due today or what has been done, so these
    /// figures do not move about as the week is worked.
    /// </summary>
    private void BuildRounds()
    {
        vsl_rounds.Clear();

        //nothing is on a round, so this would be one row saying what the
        //cards above already say
        if (Job.RoundsInUse().Count == 0)
        {
            brd_rounds.IsVisible = false;
            return;
        }

        List<RoundStats> rounds = RoundStats.ByRound(Months);

        brd_rounds.IsVisible = rounds.Count > 0;
        l_roundsNote.Text = "Each round in full: how many houses are on it, how long they all take, what they come to "
            + "and what is owed on them. Tap one to see its houses.";

        foreach (RoundStats round in rounds)
            vsl_rounds.Add(RoundRow(round));
    }

    /// <summary>
    /// A round row is a way in to the round, not just a figure.
    ///
    /// A figure that raises a question - twelve houses on a round you thought
    /// had twenty, or a No Round you meant to have cleared - is no use
    /// without the houses behind it, and hunting them out of a list that
    /// only reaches a fortnight ahead is not something anybody will do. So
    /// tapping the row opens All Jobs cut down to that round, and the work on
    /// no round is reached the same way.
    /// </summary>
    private View RoundRow(RoundStats round)
    {
        VerticalStackLayout rows = new VerticalStackLayout() { Spacing = 2 };

        //a layout with nothing behind it still takes the tap in MAUI, but
        //the gaps between the two labels are where a thumb actually lands
        rows.BackgroundColor = Colors.Transparent;

        TapGestureRecognizer tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => ShowTheRound(round.Round);
        rows.GestureRecognizers.Add(tap);

        Grid top = new Grid()
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition() { Width = GridLength.Star },
                new ColumnDefinition() { Width = GridLength.Auto },
                new ColumnDefinition() { Width = GridLength.Auto },
            },
        };

        top.Add(new Label()
        {
            Text = round.RoundName,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalOptions = LayoutOptions.Center,
        }, 0);

        //what the whole round comes to, which is the figure somebody means
        //when they ask what a round is worth
        top.Add(new Label()
        {
            Text = Money(round.ValueOfTheRound),
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            TextColor = Color.FromArgb("#2E7D32"),
            VerticalOptions = LayoutOptions.Center,
        }, 1);

        //says the row goes somewhere. without it the card reads as figures to
        //look at and nobody finds out it can be tapped
        top.Add(new Label()
        {
            Text = "›",
            FontSize = 20,
            TextColor = Color.FromArgb("#9CA3AF"),
            VerticalOptions = LayoutOptions.Center,
        }, 2);

        rows.Add(top);

        //how many houses, how long they all take, and what is owed on them
        string houses = round.HousesOnTheRound == 1 ? "1 house" : $"{round.HousesOnTheRound} houses";
        string line = $"{houses} · {round.FormattedTimeForTheRound} · {Money(round.MoneyOwed)} owed";

        rows.Add(new Label()
        {
            Text = line,
            FontSize = 12,
            TextColor = Application.Current.PlatformAppTheme == AppTheme.Dark
                ? Color.FromArgb("#9CA3AF")
                : Color.FromArgb("#6B7280"),
        });

        return rows;
    }

    /// <summary>
    /// hands the round over to All Jobs and moves to it. Blank is the work
    /// that is on no round, which is a real answer and not the absence of one
    /// </summary>
    private void ShowTheRound(string round)
    {
        AllJobs.ShowRound(round ?? string.Empty);
        WorkTracker.AppShell.ShowAllJobs();
    }

    private void BuildMonths(RoundStats stats)
    {
        vsl_months.Clear();

        if (stats.Months.Count == 0)
        {
            l_monthsNote.Text = "Nothing marked done yet, so there is nothing to add up.";
            return;
        }

        string average = stats.Months.Count == 1
            ? "The last month with work in it."
            : $"The last {stats.Months.Count} months with work in them, {Money(stats.AverageMonth)} a month on average.";

        l_monthsNote.Text = $"{average} The figure in brackets is how many houses.";

        //the bars are drawn against the best month rather than a round
        //number, so the shape of the year shows however big the figures are
        MonthOfWork best = stats.BestMonth;
        float most = best == null || best.Value <= 0 ? 1 : best.Value;

        foreach (MonthOfWork month in stats.Months)
            vsl_months.Add(Row(month, most));
    }

    private View Row(MonthOfWork month, float most)
    {
        Grid row = new Grid()
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition() { Width = new GridLength(90) },
                new ColumnDefinition() { Width = GridLength.Star },
                new ColumnDefinition() { Width = GridLength.Auto },
            },
        };

        Label name = new Label()
        {
            Text = month.ShortName,
            FontSize = 13,
            VerticalOptions = LayoutOptions.Center,
        };
        row.Add(name, 0);

        //a bar in a bar: the outer one is the full width, the inner one is
        //this month against the best of them
        Border track = new Border()
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle() { CornerRadius = 6 },
            BackgroundColor = Color.FromArgb("#33808080"),
            HeightRequest = 18,
            VerticalOptions = LayoutOptions.Center,
            Padding = 0,
        };

        Grid inside = new Grid();
        inside.Add(new BoxView()
        {
            Color = Color.FromArgb("#2E7D32"),
            HorizontalOptions = LayoutOptions.Start,
            WidthRequest = 0,
        });
        track.Content = inside;

        //the width is only known once the row has been measured, so it is set
        //from the size rather than guessed at
        BoxView bar = (BoxView)inside.Children[0];
        track.SizeChanged += (s, e) =>
        {
            if (track.Width > 0)
                bar.WidthRequest = Math.Max(2, track.Width * (month.Value / most));
        };

        row.Add(track, 1);

        Label value = new Label()
        {
            Text = $"{Money(month.Value)}  ({month.Houses})",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
        };
        row.Add(value, 2);

        return row;
    }

    private string Money(float amount)
    {
        return $"{Gloable.CurrenceSymbol}{amount:0.00}";
    }
}
