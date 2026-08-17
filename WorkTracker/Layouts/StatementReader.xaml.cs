namespace UiInterface.Layouts;

using Kernel;
using UiInterface.ImportExport;

/// <summary>
/// Reads a kept bank statement inside the app, opened from the Open button
/// on Layouts/KeptStatements - the statement is the evidence behind the
/// figures, and checking a figure should not mean finding another app that
/// can open the file.
///
/// A pdf is drawn page by page as the bank printed it (PdfPageImages, the
/// platform's own renderer). A platform with no renderer, or a pdf the
/// platform cannot open - a password-locked one - falls back to the rows
/// the import reads, which can still ask for the password; the page says
/// when it is showing the rows rather than the paper. A csv is its rows,
/// laid out as the table it is, scrolling both ways because a statement is
/// wider than a phone.
/// </summary>
public partial class StatementReader : ContentPage
{
    private readonly StatementRecord _record;

    private bool _built = false;

    public StatementReader(StatementRecord record)
    {
        InitializeComponent();

        _record = record;
        Title = record == null ? "Statement" : record.OriginalFileName;

        NavigatedTo += async (s, e) =>
        {
            if (_built)
                return;
            _built = true;
            await BuildAsync();
        };
    }

    private async Task BuildAsync()
    {
        if (_record == null || !_record.FileKept)
        {
            Say("The statement file is not on this device - it will come down with the next sync.");
            return;
        }

        //whose statement and when, before the file itself
        string heading = $"{_record.FormattedPeriod} - {_record.FormattedImported}";
        if (!string.IsNullOrEmpty(_record.AccountName))
            heading += $"\nAccount: {_record.AccountName}";
        Say(heading);

        ai_busy.IsRunning = true;
        try
        {
            if (_record.StoredPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                await ShowPdfAsync();
            else
                await ShowCsvAsync();
        }
        catch (Exception ex)
        {
            Say($"The statement could not be read: {ex.Message}. Share it off the statements page to open it somewhere else.");
        }
        finally
        {
            ai_busy.IsRunning = false;
        }
    }

    private async Task ShowPdfAsync()
    {
        try
        {
            (List<byte[]> pages, int total) = await PdfPageImages.RenderAsync(_record.StoredPath);

            foreach (byte[] png in pages)
            {
                byte[] data = png;
                vsl_content.Add(new Image()
                {
                    Source = ImageSource.FromStream(() => new MemoryStream(data)),
                    Aspect = Aspect.AspectFit,
                });
            }

            if (total > pages.Count)
                Say($"Only the first {pages.Count} of {total} pages are drawn.");

            return;
        }
        catch
        {
            //a locked pdf, or a platform with no renderer - fall through to
            //the rows the import reads, which can still ask for the password
        }

        CSVFile file = await StatementFile.ImportPdfAsync(this, _record.StoredPath);
        if (file == null)
        {
            Say("The pdf could not be drawn or read here. Share it off the statements page to open it somewhere else.");
            return;
        }

        Say("Showing the rows read out of the pdf rather than the pdf itself.");
        ShowTable(file);
    }

    private async Task ShowCsvAsync()
    {
        CSVFile file = await Task.Run(() => CSV.Import(_record.StoredPath));
        ShowTable(file);
    }

    /// <summary>the statement as the table it is - header bold, one row per line</summary>
    private void ShowTable(CSVFile file)
    {
        //a statement is wider than a phone, so the table scrolls sideways too
        sv_page.Orientation = ScrollOrientation.Both;

        List<string[]> rows = new List<string[]>();
        if (file.data != null)
            foreach (string[] row in file.data)
                if (row != null)
                    rows.Add(row);

        int columns = file.Header == null ? 0 : file.Header.Length;
        foreach (string[] row in rows)
            columns = Math.Max(columns, row.Length);

        if (columns == 0)
        {
            Say("The file has nothing in it to show.");
            return;
        }

        Grid grid = new Grid() { ColumnSpacing = 14, RowSpacing = 3, Padding = new Thickness(0, 6) };
        for (int i = 0; i < columns; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        int r = 0;

        if (file.Header != null && file.Header.Length > 0)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < file.Header.Length; c++)
                if (!string.IsNullOrWhiteSpace(file.Header[c]))
                    grid.Add(new Label() { Text = file.Header[c].Trim(), FontAttributes = FontAttributes.Bold, FontSize = 13 }, c, r);
            r++;
        }

        //enough for years of statements in one file; a bigger one is cut
        //and says so below rather than quietly ending
        const int MostRows = 1500;

        foreach (string[] row in rows)
        {
            if (r > MostRows)
                break;

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < row.Length; c++)
                if (!string.IsNullOrWhiteSpace(row[c]))
                    grid.Add(new Label() { Text = row[c].Trim(), FontSize = 13 }, c, r);
            r++;
        }

        vsl_content.Add(grid);

        if (rows.Count > MostRows)
            Say($"Only the first {MostRows} lines are shown - the file itself keeps the lot.");
    }

    private void Say(string text)
    {
        vsl_content.Add(new Label() { Text = text, FontSize = 12, TextColor = Colors.Grey });
    }
}
