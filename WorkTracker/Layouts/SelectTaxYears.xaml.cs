namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Asks which tax years to include. Used by the manual backup, which offers
/// the choice as soon as there is more than one year of records, and by the
/// tax page when saving a single year on its own.
/// </summary>
public partial class SelectTaxYears : ContentPage
{
    private readonly List<int> _years;
    private readonly List<CheckBox> _boxes = new List<CheckBox>();
    private TaskCompletionSource<List<int>> _answer = new TaskCompletionSource<List<int>>();
    private bool _answered = false;

    private SelectTaxYears(string prompt, string confirm, List<int> years, List<int> ticked)
    {
        InitializeComponent();

        _years = years;
        l_prompt.Text = prompt;
        bnt_ok.Text = confirm;

        //newest first - the year you are in is the one normally wanted
        for (int i = _years.Count - 1; i >= 0; i--)
        {
            int year = _years[i];

            CheckBox box = new CheckBox()
            {
                ClassId = year.ToString(),
                IsChecked = ticked == null || ticked.Contains(year),
                VerticalOptions = LayoutOptions.Center,
            };
            _boxes.Add(box);

            HorizontalStackLayout row = new HorizontalStackLayout() { Spacing = 6 };
            row.Add(box);
            row.Add(new Label()
            {
                Text = $"{TaxCalendar.YearName(year)}   {Describe(year)}",
                VerticalOptions = LayoutOptions.Center,
            });

            vsl_years.Add(row);
        }
    }

    /// <summary>what is actually recorded for a year, so it is clear what leaving it out would miss</summary>
    private static string Describe(int year)
    {
        TaxPeriod period = TaxCalendar.WholeYear(year);

        int expenses = 0, payments = 0;
        foreach (Expense e in Expense.Query())
            if (period.Contains(e.Date))
                expenses++;
        foreach (Payment p in Payment.Query())
            if (period.Contains(p.Date))
                payments++;

        int statements = StatementRecord.QueryByYear(year).Count;

        return $"({payments} payments, {expenses} expenses, {statements} statements)";
    }

    /// <summary>
    /// puts the page up and waits for an answer. returns null when the user
    /// backs out, and never returns an empty list
    /// </summary>
    public static async Task<List<int>> AskAsync(INavigation navigation, string prompt, string confirm,
        List<int> years, List<int> ticked = null)
    {
        SelectTaxYears page = new SelectTaxYears(prompt, confirm, years, ticked);
        await navigation.PushModalAsync(page);
        return await page._answer.Task;
    }

    private void bnt_all_Clicked(object sender, EventArgs e)
    {
        foreach (CheckBox box in _boxes)
            box.IsChecked = true;
    }

    private void bnt_thisYear_Clicked(object sender, EventArgs e)
    {
        string current = TaxCalendar.TaxYearOf(UsfulFuctions.DateNow).ToString();
        foreach (CheckBox box in _boxes)
            box.IsChecked = box.ClassId == current;
    }

    private async void bnt_ok_Clicked(object sender, EventArgs e)
    {
        List<int> chosen = new List<int>();
        foreach (CheckBox box in _boxes)
            if (box.IsChecked)
                chosen.Add(Convert.ToInt32(box.ClassId));

        if (chosen.Count == 0)
        {
            await DisplayAlert("Tax Years", "Pick at least one tax year.", "Ok");
            return;
        }

        chosen.Sort();
        await Finish(chosen);
    }

    private async void bnt_cancel_Clicked(object sender, EventArgs e)
    {
        await Finish(null);
    }

    private async Task Finish(List<int> chosen)
    {
        if (_answered)
            return;
        _answered = true;

        await Navigation.PopModalAsync();
        _answer.TrySetResult(chosen);
    }

    /// <summary>a hardware back press is the same as cancelling</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_answered)
        {
            _answered = true;
            Navigation.PopModalAsync();
            _answer.TrySetResult(null);
        }
        return true;
    }
}
