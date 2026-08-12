namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The bank statements that have been kept, newest first, under the tax year
/// each one is filed in. They can be opened - which is what you want when
/// the taxman asks where a figure came from - or thrown away when the year
/// is long settled and the space is wanted back.
/// </summary>
public partial class KeptStatements : ContentPage
{
    public KeptStatements()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Refresh();
    }

    private void Refresh()
    {
        vsl_statements.Clear();

        List<StatementRecord> records = StatementRecord.Query()
            .OrderByDescending(x => x.TaxYear)
            .ThenByDescending(x => x.LastTransaction)
            .ToList();

        l_nothing.IsVisible = records.Count == 0;

        long size = 0;
        int lastYear = int.MinValue;

        foreach (StatementRecord record in records)
        {
            if (record.TaxYear != lastYear)
            {
                lastYear = record.TaxYear;
                vsl_statements.Add(new Label
                {
                    Text = record.FormattedTaxYear,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 8, 0, 0),
                });
            }

            size += record.FileKept ? record.FileSize : 0;
            vsl_statements.Add(BuildRow(record));
        }

        l_overview.Text = records.Count == 0
            ? "No bank statements kept"
            : $"{records.Count} statement(s) kept, using {ReceiptPhoto.FormatSize(size)}.";
    }

    private View BuildRow(StatementRecord record)
    {
        VerticalStackLayout content = new VerticalStackLayout() { Spacing = 2 };

        content.Add(new Label { Text = record.OriginalFileName, FontAttributes = FontAttributes.Bold });
        content.Add(new Label { Text = record.FormattedPeriod, FontSize = 13 });
        content.Add(new Label { Text = record.FormattedImported, FontSize = 12, TextColor = Colors.Grey });

        if (record.Crossover)
            content.Add(new Label
            {
                Text = record.FormattedCrossover,
                FontSize = 12,
                TextColor = Color.FromArgb("#EF6C00"),
            });

        if (!record.FileKept)
            content.Add(new Label
            {
                Text = "The file itself is not on this device - it will come down with the next sync.",
                FontSize = 12,
                TextColor = Colors.Grey,
            });

        HorizontalStackLayout buttons = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
        };

        if (record.FileKept)
            buttons.Add(RowButton("Open", "#1E88E5", (s, e) => Open(record)));
        buttons.Add(RowButton("Delete", "#E53935", (s, e) => Delete(record)));
        content.Add(buttons);

        Border border = new Border { Content = content };
        border.Style = (Style)Resources["Card"];
        return border;
    }

    private Button RowButton(string text, string colour, EventHandler clicked)
    {
        Button button = new Button
        {
            Text = text,
            TextColor = Color.FromArgb(colour),
            BorderColor = Color.FromArgb(colour),
        };
        button.Style = (Style)Resources["RowButton"];
        button.Clicked += clicked;
        return button;
    }

    private async void Open(StatementRecord record)
    {
        try
        {
            await Share.RequestAsync(new ShareFileRequest(record.OriginalFileName, new ShareFile(record.StoredPath)));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could Not Open", ex.Message, "Ok");
        }
    }

    private async void Delete(StatementRecord record)
    {
        string message = $"'{record.OriginalFileName}' will be thrown away. The payments and expenses read off it are kept - only the statement itself goes.";
        if (record.Crossover)
            message += $"\n\nThis statement runs across 5 April. Only the {TaxCalendar.YearName(record.TaxYear)} copy goes; the other tax year keeps its own.";

        if (!await DisplayAlert("Delete Statement?", message, "Delete", "Cancel"))
            return;

        StatementRecord.Remove(record.Id);
        StatementRecord.Save();
        Refresh();
    }
}
