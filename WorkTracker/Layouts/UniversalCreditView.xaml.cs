namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// What a Universal Credit claim has to be told each month: the money that
/// actually came in and the money that actually went out, added up over the
/// claim's own month rather than the calendar's.
///
/// It is deliberately not the tax page in another hat. That one is 6 April to
/// 5 April and offers a choice of how income is counted; a claim is neither -
/// its month starts on the day the claim did, and only money that has really
/// moved counts. A figure off one page is no answer to the other, and the
/// two must not be made to look interchangeable.
///
/// The page is off by default (Settings.ShowUniversalCredit) - most rounds
/// are not on a claim.
/// </summary>
public partial class UniversalCreditView : ContentPage
{
    public UniversalCreditView()
    {
        InitializeComponent();

        //a claim cannot have started before Universal Credit existed, and
        //one that started after today has no month to report on yet
        dp_start.MinimumDate = new DateTime(2013, 4, 29);
        dp_start.MaximumDate = UsfulFuctions.DateNow;

        dp_start.Date = InRange(UniversalCredit.HaveStartDate
            ? UniversalCredit.StartDate
            : UsfulFuctions.DateNow);

        NavigatedTo += (s, e) => Refresh();

        Refresh();
    }

    /// <summary>
    /// a date the picker will actually take. A date read off a settings file
    /// written on another phone can sit outside the two ends - a clock a day
    /// ahead is all it takes - and a DatePicker handed one of those throws
    /// rather than shrugging
    /// </summary>
    private DateTime InRange(DateTime date)
    {
        if (date.Date < dp_start.MinimumDate.Date)
            return dp_start.MinimumDate.Date;
        if (date.Date > dp_start.MaximumDate.Date)
            return dp_start.MaximumDate.Date;
        return date.Date;
    }

    /// <summary>
    /// the picker moving is the ordinary way the date is changed once there
    /// is one. It cannot be the only way - see the button below
    /// </summary>
    private void dp_start_DateSelected(object sender, DateChangedEventArgs e)
    {
        SetStartDate(e.NewDate);
    }

    /// <summary>
    /// how the date is set the first time.
    ///
    /// The picker opens on today, so a claim that started today is the one
    /// answer the picker itself can never report - it would be asked to
    /// change to what it already says. The button is only up while nothing
    /// has been set, so it is not a second way of doing the same thing
    /// afterwards.
    /// </summary>
    private void bnt_setStart_Clicked(object sender, EventArgs e)
    {
        SetStartDate(dp_start.Date);
    }

    private void SetStartDate(DateTime date)
    {
        if (UniversalCredit.HaveStartDate && UniversalCredit.StartDate.Date == date.Date)
            return;

        Settings.UniversalCreditStart = date.Date;
        Settings.Save();

        Refresh();
    }

    private void Refresh()
    {
        DateTime today = UsfulFuctions.DateNow;

        //the page is built once and kept, so the far end of the picker is
        //moved on rather than left at whatever day the app was started
        if (dp_start.MaximumDate.Date != today)
            dp_start.MaximumDate = today;

        bool haveStart = UniversalCredit.HaveStartDate;

        bnt_setStart.IsVisible = !haveStart;
        l_notSet.IsVisible = !haveStart;

        if (!haveStart)
        {
            l_startExplain.Text = string.Empty;

            b_thisMonth.IsVisible = false;
            l_monthsHeading.IsVisible = false;
            vsl_months.Clear();
            l_nothing.IsVisible = false;

            ShowExplanation();
            return;
        }

        DateTime start = UniversalCredit.StartDate;

        //said back in words, with the first two months spelled out: the rule
        //is easy to say and easy to get wrong, and a date on its own does
        //not show anybody that the end of the month is the day before
        UniversalCreditPeriod first = UniversalCredit.Period(start, 0);
        UniversalCreditPeriod second = UniversalCredit.Period(start, 1);
        l_startExplain.Text =
            $"Your months run {first.FormattedDates}, then {second.FormattedDates}, and so on. "
            + "Tap the date above to change it.";

        List<UniversalCreditSummary> months = UniversalCreditSummary.BuildAll(start, today);

        ShowThisMonth(months, today);
        BuildMonths(months, today);
        ShowExplanation();
    }

    /// <summary>
    /// the month that is running now, on its own above the rest. It is the
    /// one being asked about, and it is also the one that is not finished -
    /// which the card says outright rather than leaving its figures to be
    /// read as the final ones
    /// </summary>
    private void ShowThisMonth(List<UniversalCreditSummary> months, DateTime today)
    {
        UniversalCreditSummary current = months.Find(x => x.Period.IsCurrent(today));

        b_thisMonth.IsVisible = current != null;
        if (current == null)
            return;

        l_thisTitle.Text = $"This month  -  month {current.Period.Number} of the claim";
        l_thisDates.Text = current.Period.FormattedDates;

        l_thisIncome.Text = current.FormattedIncome;
        l_thisExpenses.Text = current.FormattedExpenses;
        l_thisProfit.Text = current.FormattedProfit;
        l_thisProfit.BackgroundColor = current.IsLoss ? Color.FromArgb("#C62828") : Color.FromArgb("#00796B");

        l_thisCounts.Text = $"{current.IncomeCount} payment(s) in, {current.ExpenseCount} expense(s) out";

        int daysLeft = (current.Period.End.Date - today.Date).Days;
        l_thisReport.Text = daysLeft == 0
            ? "This month ends today - report it once the day's money is in."
            : $"Still running. It ends on {current.Period.End:d MMMM yyyy}, {daysLeft} day(s) away, and these figures will keep changing until then.";
    }

    /// <summary>
    /// every month of the claim, newest first. The one still running is
    /// marked, because its figures are not the whole story yet
    /// </summary>
    private void BuildMonths(List<UniversalCreditSummary> months, DateTime today)
    {
        vsl_months.Clear();

        l_monthsHeading.IsVisible = months.Count > 0;
        l_nothing.IsVisible = months.Count == 0;

        if (months.Count == 0)
        {
            l_nothing.Text = "The first month of the claim has not started yet.";
            return;
        }

        bool dark = Application.Current != null && Application.Current.PlatformAppTheme == AppTheme.Dark;

        foreach (UniversalCreditSummary month in months)
        {
            bool isCurrent = month.Period.IsCurrent(today);

            Border card = new Border
            {
                StrokeThickness = isCurrent ? 2 : 0,
                Stroke = Colors.Orange,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = 12,
                BackgroundColor = dark ? Color.FromArgb("#1E1E1E") : Color.FromArgb("#F2F4F7"),
            };

            VerticalStackLayout content = new VerticalStackLayout { Spacing = 4 };

            HorizontalStackLayout title = new HorizontalStackLayout { Spacing = 8 };
            title.Add(new Label
            {
                Text = $"Month {month.Period.Number}",
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
            });
            if (isCurrent)
                title.Add(new Label
                {
                    Text = "NOW",
                    TextColor = Colors.White,
                    BackgroundColor = Color.FromArgb("#EF6C00"),
                    Padding = new Thickness(6, 2),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.Center,
                });
            content.Add(title);

            content.Add(new Label
            {
                Text = isCurrent
                    ? $"{month.Period.FormattedDates}   -   still running"
                    : $"{month.Period.FormattedDates}   -   report on {month.Period.End:d MMM yyyy}",
                FontSize = 12,
                TextColor = Color.FromArgb("#9CA3AF"),
            });

            Grid figures = new Grid { ColumnSpacing = 8 };
            figures.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            figures.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            figures.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            figures.Add(Figure("Paid in", month.FormattedIncome, "#2E7D32"), 0, 0);
            figures.Add(Figure("Spent", month.FormattedExpenses, "#7B1FA2"), 1, 0);
            figures.Add(Figure("Left", month.FormattedProfit, month.IsLoss ? "#C62828" : "#00796B"), 2, 0);
            content.Add(figures);

            content.Add(new Label
            {
                Text = $"{month.IncomeCount} payment(s) in, {month.ExpenseCount} expense(s) out",
                FontSize = 12,
                TextColor = Color.FromArgb("#9CA3AF"),
            });

            card.Content = content;
            vsl_months.Add(card);
        }
    }

    private static View Figure(string caption, string value, string colour)
    {
        VerticalStackLayout v = new VerticalStackLayout { Spacing = 2 };
        v.Add(new Label
        {
            Text = caption,
            FontSize = 11,
            TextColor = Color.FromArgb("#9CA3AF"),
            HorizontalTextAlignment = TextAlignment.Center,
        });
        v.Add(new Label
        {
            Text = value,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb(colour),
            Padding = new Thickness(4),
            HorizontalTextAlignment = TextAlignment.Center,
        });
        return v;
    }

    private void ShowExplanation()
    {
        l_explain.Text =
            "Universal Credit asks self employed people to report their earnings at the end of every assessment period - "
            + "the month that starts on the day the claim did. What it wants is what actually moved: the money that came "
            + "in over that month and the money that went out of the business over it.\n\n"
            + "Paid in is every payment recorded here with a date inside the month, whoever it came from and however it "
            + "was paid. Spent is every expense recorded here with a date inside the month. Left is the one taken off the "
            + "other, and it can be a loss.\n\n"
            + "These are not your tax figures. The tax page counts a year at a time, 6 April to 5 April, and can count "
            + "income when the work was done rather than when it was paid for. Do not report one where the other is asked "
            + "for.";
    }
}
