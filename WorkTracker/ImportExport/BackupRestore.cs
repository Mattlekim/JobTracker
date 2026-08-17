namespace UiInterface.ImportExport;

using System.IO.Compression;
using Kernel;
using UiInterface.Layouts;

/// <summary>
/// Putting a backup back, from wherever it was come at.
///
/// A .rbf is the round: everything in it replaces everything on the device,
/// so there is one way of doing it rather than one per place it can be
/// started from. The settings page picks a file; the phone hands one over
/// when a .rbf is opened from a file manager, an email or the downloads
/// list, which is how a backup normally reaches a new phone.
///
/// A file opened from outside arrives before there is a page to ask on -
/// often before the app has finished starting - so it is held here until
/// something can put the question up (<see cref="TakePending"/>).
/// </summary>
public static class BackupRestore
{
    /// <summary>what a backup is called</summary>
    public const string Extension = ".rbf";

    /// <summary>is this a backup, by its name</summary>
    public static bool LooksLikeBackup(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
    }

    private static string _pending;

    /// <summary>
    /// the phone has handed us a backup to open. it is kept rather than acted
    /// on, because this can arrive while the app is still starting and there
    /// is nothing to ask on yet
    /// </summary>
    public static void FileWasOpened(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _pending = path;

        Action opened = Opened;
        if (opened != null)
            opened();
    }

    /// <summary>raised when a backup has been handed to the app to open</summary>
    public static event Action Opened;

    /// <summary>
    /// the backup waiting to be dealt with, if any. taking it clears it, so
    /// two pages cannot both offer to restore the same file
    /// </summary>
    public static string TakePending()
    {
        string path = _pending;
        _pending = null;
        return path;
    }

    /// <summary>
    /// a .rbf given to the app on the command line - how a desktop opens a
    /// file with an app. Windows cannot register the file type without an
    /// installer, but Open With still works once somebody has pointed it here
    /// </summary>
    public static void CheckCommandLine()
    {
        string[] args;

        try
        {
            args = Environment.GetCommandLineArgs();
        }
        catch
        {
            return;
        }

        //the first argument is the app itself
        for (int i = 1; i < args.Length; i++)
            if (LooksLikeBackup(args[i]) && File.Exists(args[i]))
            {
                FileWasOpened(args[i]);
                return;
            }
    }

    /// <summary>
    /// Asks, then puts the backup back over everything on the device.
    ///
    /// The asking is not a formality: restoring is not a merge, and a round
    /// worked all week would be gone. That is also why the file is checked
    /// for being a backup at all before anything is unpacked.
    /// </summary>
    /// <returns>true when the data was replaced</returns>
    public static async Task<bool> RestoreAsync(string path, string fileName, Page page)
    {
        if (page == null)
            return false;

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = Path.GetFileName(path ?? string.Empty);

        if (!LooksLikeBackup(fileName))
        {
            await page.DisplayAlert("Unsupported File",
                $"This is not a backup file. You need a {Extension} file.", "Ok");
            return false;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await page.DisplayAlert("Restore Backup", "That backup could not be read.", "Ok");
            return false;
        }

        if (!await page.DisplayAlert("Restore Backup",
                $"{fileName}\n\nEverything on this device is replaced by what is in this backup. Anything done since it was made will be lost.",
                "Restore", "Cancel"))
            return false;

        try
        {
            ZipFile.ExtractToDirectory(path,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), true);

            //settings first: the job types live in there and the jobs are
            //read against them, the same order as the app starts in
            Settings.Load();
            Customer.Load();
            Job.Reset();
            Job.Load();
            Payment.Load();
            Expense.Load();
            ExpenseRule.Load();

            //after Settings.Load, so a backup from before bank accounts
            //existed still turns its one layout into the first account
            BankAccount.Load();
            StatementRecord.Load();
            GoCardlessRequest.Load();
            BalanceAdjustment.Load();

            DataRefreshNotifier.NotifyDataChanged();
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Restore Backup",
                $"There was a problem restoring that backup: {ex.Message}", "Ok");
            return false;
        }

        await page.DisplayAlert("Restored",
            "The backup has been put back. Close Work Tracker and open it again so every page is built from it.", "Ok");

        return true;
    }
}
