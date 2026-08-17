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
                BankAccount account = await ChooseAccountAsync(page, file, isPdf);
                if (account == null)
                    return null;

                //the file's headings are remembered as what this account's
                //statements look like, so the next one can be recognised
                //without asking
                string signature = BankAccount.SignatureOf(file.Header);
                if (!string.IsNullOrEmpty(signature) && account.Signature(isPdf) != signature)
                {
                    account.RememberSignature(isPdf, signature);
                    BankAccount.Save();
                }

                StatmentViewer.ActiveAccount = account;
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
    /// more than one active account to choose between - a round with one
    /// bank sees nothing new, and more accounts are added under Banking on
    /// the settings page.
    ///
    /// with more than one, picking the wrong account files everything on
    /// the statement against it - so the file's headings are matched to the
    /// account whose statements they look like, the top of the file is
    /// shown so the answer can be checked against something real, and a
    /// pick that goes against what the file looks like is asked about twice
    /// </summary>
    private static async Task<BankAccount> ChooseAccountAsync(Page page, CSVFile file, bool isPdf)
    {
        List<BankAccount> active = BankAccount.QueryActive();

        //archived accounts are not offered - that is what archiving is
        if (active.Count == 0 && BankAccount.Count > 0)
        {
            await page.DisplayAlert("All Accounts Archived",
                "Every bank account is archived, so there is nothing to import this against. Unarchive one under Settings, Banking, Bank Accounts and import again.", "Ok");
            return null;
        }

        if (active.Count == 0)
            return BankAccount.FirstOrMake();

        if (active.Count == 1)
            return active[0];

        string signature = BankAccount.SignatureOf(file.Header);
        BankAccount guess = BankAccount.FindBySignature(signature, isPdf);
        string preview = PreviewOf(file);

        if (guess != null && await page.DisplayAlert($"Looks Like {guess.Name}",
                $"This file looks like {guess.Name}'s statements.\n\nThe top of the file:\n{preview}\n\nImport it against {guess.Name}?",
                $"Yes, {guess.Name}", "Pick An Account"))
            return guess;

        string picked = await page.DisplayActionSheet("Which account is this statement from?", "Cancel", null,
            active.Select(x => x.Name).ToArray());

        BankAccount chosen = active.FirstOrDefault(x => x.Name == picked);
        if (chosen == null)
            return null;

        if (guess != null && chosen != guess)
        {
            //picking against what the file looks like is exactly the moment
            //to look twice
            if (!await page.DisplayAlert("Are You Sure?",
                    $"This file looks like {guess.Name}'s statements, not {chosen.Name}'s.\n\nThe top of the file:\n{preview}\n\nImport it against {chosen.Name} anyway?",
                    $"Yes, {chosen.Name}", "Cancel"))
                return null;
        }
        else if (guess == null)
        {
            //nothing recognised, so the pick is all there is to go on -
            //show what is about to be filed against it before anything is
            if (!await page.DisplayAlert($"Import Against {chosen.Name}?",
                    $"The top of the file:\n{preview}\n\nIf this is not {chosen.Name}'s statement, cancel and pick again.",
                    $"Import As {chosen.Name}", "Cancel"))
                return null;
        }

        return chosen;
    }

    /// <summary>
    /// the top of the file, short enough for an alert - the headings and
    /// the first few lines, each cut down to fit. enough to recognise whose
    /// statement it is without opening anything
    /// </summary>
    private static string PreviewOf(CSVFile file)
    {
        List<string> lines = new List<string>();

        if (file.Header != null && file.Header.Length > 0)
            lines.Add(OneLine(file.Header));

        if (file.data != null)
            foreach (string[] row in file.data)
            {
                if (row == null)
                    continue;

                lines.Add(OneLine(row));
                if (lines.Count >= 4)
                    break;
            }

        return lines.Count == 0 ? "(the file has nothing to show)" : string.Join("\n", lines);
    }

    private static string OneLine(string[] row)
    {
        string line = string.Join(", ", row.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        return line.Length <= 80 ? line : line.Substring(0, 77) + "...";
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
    /// if the bank locked the file. Public because the statement reader falls back to it for a pdf
    /// the platform cannot draw.
    /// </summary>
    public static async Task<CSVFile> ImportPdfAsync(Page page, string path)
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
