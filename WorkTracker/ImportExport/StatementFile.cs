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
            PickerTitle = "Select a statement (.csv or .pdf, bank or PayPal)",
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

            //a PayPal export names its own columns, so it is spotted here and
            //never has them asked for. a bank's csv is left to the layout the
            //user set up for it
            StatmentViewer.SourceIsPayPal = !isPdf && PayPalStatement.Apply(file);

            //which account the statement is from - its remembered layout is
            //what the columns are read off, and its id is what the kept file
            //and the expenses off it are tracked against. a PayPal export is
            //its own source and has no account. backing out of the question
            //backs out of the import
            if (StatmentViewer.SourceIsPayPal)
                StatmentViewer.ActiveAccount = null;
            else
            {
                StatmentViewer.ActiveAccount = await ChooseAccountAsync(page);
                if (StatmentViewer.ActiveAccount == null)
                    return null;
            }

            //the picked file is kept to one side, because the statement is
            //filed away once the columns are known and by then the picker's
            //own copy of it may be gone
            StatmentViewer.SourceFileName = fr.FileName;
            StatmentViewer.SourceFilePath = await HoldOntoFileAsync(fr);

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
    /// which account is this statement from. not a question until there is
    /// more than one account to choose between - a round with one bank sees
    /// nothing new, and more accounts are added on the settings page
    /// </summary>
    private static async Task<BankAccount> ChooseAccountAsync(Page page)
    {
        List<BankAccount> accounts = BankAccount.Query();

        if (accounts.Count <= 1)
            return BankAccount.FirstOrMake();

        string[] names = accounts.Select(x => x.Name).ToArray();
        string picked = await page.DisplayActionSheet("Which account is this statement from?", "Cancel", null, names);

        return accounts.FirstOrDefault(x => x.Name == picked);
    }

    /// <summary>
    /// copies the picked file into the app's cache so it is still there when
    /// the statement gets filed under its tax year
    /// </summary>
    private static async Task<string> HoldOntoFileAsync(FileResult picked)
    {
        try
        {
            string path = Path.Combine(FileSystem.CacheDirectory, $"statement_pick_{Guid.NewGuid().ToString("N").Substring(0, 8)}{Path.GetExtension(picked.FileName)}");

            using (Stream source = await picked.OpenReadAsync())
            using (FileStream dest = File.Create(path))
                await source.CopyToAsync(dest);

            return path;
        }
        catch
        {
            //not being able to keep a copy must not stop the import itself
            return null;
        }
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
