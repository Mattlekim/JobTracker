namespace UiInterface.Layouts;

using Kernel;
using UiInterface.ImportExport;

public partial class TaxView : ContentPage
{
    private List<int> _taxYears = new List<int>();
    private bool _building;

    public TaxView()
    {
        InitializeComponent();

        //the years there is any data for, newest first, always including
        //the one we are in now
        int thisYear = TaxCalendar.TaxYearOf(UsfulFuctions.DateNow);
        HashSet<int> years = new HashSet<int> { thisYear };
        foreach (Payment p in Payment.Query())
            years.Add(TaxCalendar.TaxYearOf(p.Date));
        foreach (Expense e in Expense.Query())
            years.Add(TaxCalendar.TaxYearOf(e.Date));
        foreach (Job j in Job.Query())
            if (j.IsCompleted && j.DateCompleated.Year > 2001)
                years.Add(TaxCalendar.TaxYearOf(j.DateCompleated));

        _taxYears = years.Where(y => y >= 2015 && y <= thisYear + 1).OrderByDescending(y => y).ToList();

        _building = true;
        foreach (int y in _taxYears)
            p_taxYear.Items.Add(TaxCalendar.YearName(y));
        p_taxYear.SelectedIndex = 0;

        p_basis.SelectedIndex = Preferences.Get("Tax_Accruals", false) ? 1 : 0;
        sw_calendarQuarters.IsToggled = Preferences.Get("Tax_CalendarQuarters", false);
        _building = false;

        NavigatedTo += (s, e) => Refresh();
    }

    private int SelectedTaxYear
    {
        get
        {
            if (p_taxYear.SelectedIndex < 0 || p_taxYear.SelectedIndex >= _taxYears.Count)
                return TaxCalendar.TaxYearOf(UsfulFuctions.DateNow);
            return _taxYears[p_taxYear.SelectedIndex];
        }
    }

    private AccountingBasis SelectedBasis
    {
        get { return p_basis.SelectedIndex == 1 ? AccountingBasis.Accruals : AccountingBasis.Cash; }
    }

    private void Selection_Changed(object sender, EventArgs e)
    {
        if (_building)
            return;
        Preferences.Set("Tax_Accruals", p_basis.SelectedIndex == 1);
        Refresh();
    }

    private void Switch_Toggled(object sender, ToggledEventArgs e)
    {
        if (_building)
            return;
        Preferences.Set("Tax_CalendarQuarters", sw_calendarQuarters.IsToggled);
        Refresh();
    }

    private void Refresh()
    {
        int taxYear = SelectedTaxYear;
        AccountingBasis basis = SelectedBasis;
        bool calendar = sw_calendarQuarters.IsToggled;

        List<TaxSummary> summaries = TaxSummary.BuildYear(taxYear, basis, calendar);
        TaxSummary year = summaries[summaries.Count - 1];

        l_yearTitle.Text = $"Tax year {TaxCalendar.YearName(taxYear)} ({year.Period.FormattedDates})";
        l_yearIncome.Text = year.FormattedIncome;
        l_yearExpenses.Text = year.FormattedExpenses;
        l_yearProfit.Text = year.FormattedProfit;
        l_yearCounts.Text = basis == AccountingBasis.Cash
            ? $"{year.IncomeCount} payment(s) received, {year.ExpenseCount} expense(s)"
            : $"{year.IncomeCount} job(s) completed, {year.ExpenseCount} expense(s)";

        l_receiptWarning.IsVisible = year.ExpensesWithoutReceipt > 0;
        l_receiptWarning.Text = $"{year.ExpensesWithoutReceipt} expense(s) have no receipt photo attached.";

        BuildQuarters(summaries);
        BuildBoxes(year);

        l_mtdExplain.Text =
            "Making Tax Digital means keeping your records digitally and sending HMRC a summary every quarter. " +
            "This app keeps those records - every job, payment and expense with its receipt - and works out the " +
            "quarterly figures for you.\n\n" +
            "It cannot send them to HMRC itself: only software HMRC has recognised can do that. The way to file " +
            "without paying for software is bridging software - HMRC's 'find compatible software' page lists the " +
            "options, some free. A bridging tool is pointed at the figures in the exported spreadsheet once and " +
            "files them each quarter; the figures sit in the same cells in every export, so the links keep " +
            "working. Export also offers the bare quarterly figures as a csv, for tools that import a table " +
            "instead. Either way, check the figures before filing - they are estimates from what is recorded here.";
    }

    private void BuildQuarters(List<TaxSummary> summaries)
    {
        vsl_quarters.Clear();

        DateTime today = UsfulFuctions.DateNow;

        //everything but the last entry, which is the whole year
        for (int i = 0; i < summaries.Count - 1; i++)
        {
            TaxSummary q = summaries[i];

            bool isCurrent = q.Period.Contains(today);
            bool isPast = today > q.Period.End;

            Border card = new Border
            {
                StrokeThickness = isCurrent ? 2 : 0,
                Stroke = Colors.Orange,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = 12,
                BackgroundColor = Application.Current.PlatformAppTheme == AppTheme.Dark
                    ? Color.FromArgb("#1E1E1E")
                    : Color.FromArgb("#F2F4F7"),
            };

            VerticalStackLayout content = new VerticalStackLayout { Spacing = 4 };

            HorizontalStackLayout title = new HorizontalStackLayout { Spacing = 8 };
            title.Add(new Label
            {
                Text = q.Period.Name,
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
                Text = $"{q.Period.FormattedDates}   -   due with HMRC {q.Period.Due.ToShortDateString()}",
                FontSize = 12,
                TextColor = isPast && q.Period.Due < today ? Color.FromArgb("#C62828") : Color.FromArgb("#9CA3AF"),
            });

            Grid figures = new Grid { ColumnSpacing = 8 };
            figures.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            figures.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            figures.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            figures.Add(Figure("Income", q.FormattedIncome, "#2E7D32"), 0, 0);
            figures.Add(Figure("Expenses", q.FormattedExpenses, "#7B1FA2"), 1, 0);
            figures.Add(Figure("Profit", q.FormattedProfit, "#00796B"), 2, 0);
            content.Add(figures);

            card.Content = content;
            vsl_quarters.Add(card);
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

    private void BuildBoxes(TaxSummary year)
    {
        vsl_boxes.Clear();

        if (year.ExpensesByCategory.Count == 0)
        {
            vsl_boxes.Add(new Label { Text = "No expenses recorded for this tax year.", FontSize = 12 });
            return;
        }

        foreach (var pair in year.ExpensesByCategory.OrderByDescending(x => x.Value))
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Add(new Label
            {
                Text = TaxCalendar.HmrcCategoryName(pair.Key),
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
            }, 0, 0);

            row.Add(new Label
            {
                Text = $"{Gloable.CurrenceSymbol}{pair.Value:0.00}",
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
            }, 1, 0);

            vsl_boxes.Add(row);
        }
    }

    /// <summary>
    /// keeps the whole of one tax year - the figures, the receipt photos and
    /// the bank statements they were read off - in a single file that can be
    /// put somewhere safe or handed to an accountant
    /// </summary>
    private async void bnt_saveYear_Clicked(object sender, EventArgs e)
    {
        await SaveYears(new List<int> { SelectedTaxYear });
    }

    private void bnt_statements_Clicked(object sender, EventArgs e)
    {
        Navigation.PushAsync(new KeptStatements());
    }

    private async void bnt_saveYears_Clicked(object sender, EventArgs e)
    {
        List<int> years = TaxCalendar.YearsWithData();

        List<int> chosen = await SelectTaxYears.AskAsync(Navigation,
            "Which tax years do you want to save? Each one takes its receipts and bank statements with it.",
            "Save", years, new List<int> { SelectedTaxYear });

        if (chosen == null)
            return;

        await SaveYears(chosen);
    }

    private async Task SaveYears(List<int> years)
    {
        TaxYearBackup.BackupResult result;
        try
        {
            result = TaxYearBackup.Create(years, TaxYearBackup.FileNameFor(years, false));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Failed", ex.Message, "Ok");
            return;
        }

        await DisplayAlert("Tax Year Saved",
            $"Saved {result.FormattedYears}, with {result.Receipts} receipt photo(s) and {result.Statements} bank statement(s). Your customers and round are in there too.",
            "Ok");

        //saving onto the device is a real backup on its own - the same
        //choice the settings page backup offers, for the same reason
        string title = "Work Tracker Tax Year";

        if (!DeviceFileSaver.CanSave)
        {
            await Share.RequestAsync(new ShareFileRequest(title, new ShareFile(result.Path)));
            return;
        }

        string choice = await DisplayActionSheet(title, "Cancel", null, "Save To This Device", "Share");

        if (choice == "Share")
        {
            await Share.RequestAsync(new ShareFileRequest(title, new ShareFile(result.Path)));
            return;
        }

        if (choice != "Save To This Device")
            return;

        try
        {
            //as its own kind of file rather than left for the phone to guess
            //at, so tapping it in the downloads list offers Work Tracker back
            string saved = await DeviceFileSaver.SaveAsync(result.Path,
                Path.GetFileName(result.Path), BackupType);

            await DisplayAlert("Saved", $"Saved to {saved}.\n\nOpening it from there puts it back.", "Ok");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Failed", ex.Message, "Ok");
        }
    }

    /// <summary>what a .rbf is saved as - see the same constant on the settings page</summary>
    private const string BackupType = "application/octet-stream";

    /// <summary>what a spreadsheet is called, so the device opens it properly</summary>
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string SpreadsheetOption = "Spreadsheet (.xlsx)";
    private const string MtdCsvOption = "MTD figures only (.csv)";

    private async void bnt_export_Clicked(object sender, EventArgs e)
    {
        int taxYear = SelectedTaxYear;

        //two shapes for two readers: the spreadsheet for a person - or for
        //bridging software linked to its cells, which never move between
        //exports - and the bare csv of the quarterly figures for MTD
        //software that imports a table instead
        string format = await DisplayActionSheet("Export what?", "Cancel", null,
            SpreadsheetOption, MtdCsvOption);

        if (format == null || format == "Cancel")
            return;

        bool csv = format == MtdCsvOption;

        string fileName = csv
            ? $"MTD {TaxCalendar.YearName(taxYear).Replace('/', '-')}.csv"
            : $"Tax {TaxCalendar.YearName(taxYear).Replace('/', '-')}.xlsx";
        string path = Path.Combine(FileSystem.CacheDirectory, fileName);

        try
        {
            using (FileStream fs = File.Create(path))
            {
                if (csv)
                    TaxReportWriter.WriteMtdCsv(fs, taxYear, SelectedBasis, sw_calendarQuarters.IsToggled);
                else
                    TaxReportWriter.Write(fs, taxYear, SelectedBasis, sw_calendarQuarters.IsToggled);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Failed", ex.Message, "Ok");
            return;
        }

        //the file is written either way. what is left is whether it goes
        //off to another app or onto this device, where it can be found
        //again without sending it anywhere
        string title = csv
            ? $"MTD figures {TaxCalendar.YearName(taxYear)}"
            : $"Tax {TaxCalendar.YearName(taxYear)}";
        string mime = csv ? "text/csv" : XlsxType;

        if (!DeviceFileSaver.CanSave)
        {
            await Share.RequestAsync(new ShareFileRequest(title, new ShareFile(path)));
            return;
        }

        string choice = await DisplayActionSheet(title, "Cancel", null, "Save To This Device", "Share");
        if (choice == "Share")
        {
            await Share.RequestAsync(new ShareFileRequest(title, new ShareFile(path)));
            return;
        }

        if (choice != "Save To This Device")
            return;

        try
        {
            string saved = await DeviceFileSaver.SaveAsync(path, fileName, mime);
            await DisplayAlert("Saved", $"Saved to {saved}", "Ok");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Failed", ex.Message, "Ok");
        }
    }
}
