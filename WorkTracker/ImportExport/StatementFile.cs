namespace UiInterface.ImportExport;

using Kernel;
using UiInterface.Layouts;

/// <summary>
/// Picking and reading a bank statement, shared by the payments page (money
/// coming in) and the expenses page (money going out) so both understand the
/// same files in the same way.
/// </summary>
public static class StatementFile
{
    /// <summary>
    /// asks for a statement file and reads it. returns null when the user
    /// backed out, the password was not given, or the file could not be read
    /// - all of which have already been explained to them
    /// </summary>
    public static async Task<CSVFile> PickAsync(Page page)
    {
        FileResult fr = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a bank statement (.csv or .pdf)",
        });

        if (fr == null)
            return null;

        bool isPdf = fr.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        try
        {
            CSVFile file = isPdf ? await ImportPdfAsync(page, fr.FullPath) : CSV.Import(fr.FullPath);
            if (file == null) //the password was needed and not given
                return null;

            StatmentViewer.SourceIsPdf = isPdf;
            StatmentViewer.CsvFile = file;
            return file;
        }
        catch (InvalidDataException ex)
        {
            await page.DisplayAlert("Nothing to import", ex.Message, "Ok");
        }
        catch
        {
            await page.DisplayAlert("Error", "There was a problem importing the file. Make sure the file type is supported", "Ok");
        }

        return null;
    }

    /// <summary>
    /// Reads the statement off the UI thread - a long pdf takes a moment - asking for the password
    /// if the bank locked the file.
    /// </summary>
    private static async Task<CSVFile> ImportPdfAsync(Page page, string path)
    {
        string password = null;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                string pw = password;
                return await Task.Run(() => PdfStatementImporter.Import(path, pw));
            }
            catch (PdfStatementPasswordException ex)
            {
                password = await page.DisplayPromptAsync("Password needed", $"{ex.Message} Enter the password to open it.",
                    "Open", "Cancel");

                if (string.IsNullOrEmpty(password))
                    return null;
            }
        }

        await page.DisplayAlert("Error", "The statement could not be opened with that password", "Ok");
        return null;
    }
}
