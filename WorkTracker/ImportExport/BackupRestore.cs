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

    /// <summary>
    /// the way in to the figures on the restore question. Said once because
    /// the answer is compared against it - a wording changed in one place and
    /// not the other would quietly restore instead of showing the figures
    /// </summary>
    private const string ShowChanges = "Show What Would Change";

    /// <summary>is this a backup, by its name</summary>
    public static bool LooksLikeBackup(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Is this a backup, by what is inside it.
    ///
    /// The name cannot be relied on. What the phone hands over is a
    /// content:// uri belonging to whichever app sent it, and plenty of them
    /// carry no name at all - one out of the downloads list is a number, and
    /// a mail app hands over whatever it happened to cache the attachment as.
    /// Judged on the name alone those all came out as "not something Work
    /// Tracker recognises", which is a backup refused for having travelled by
    /// the ordinary route onto a new phone - the one route that matters.
    ///
    /// A .rbf is a zip of the data folder, so it says what it is: any of the
    /// app's own files at the top of it is proof enough, and nothing else the
    /// phone might hand over looks like that. A .rwk is not a zip at all, so
    /// there is nothing here for a work list to be mistaken for.
    /// </summary>
    public static bool ContentsLookLikeBackup(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            using (ZipArchive zip = ZipFile.OpenRead(path))
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string name = (entry.FullName ?? string.Empty).Replace('\\', '/');

                    if (name.EndsWith(".rjt", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("settings.txt", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("receipts/", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("statements/", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
        }
        catch
        {
            //not a zip, or one we cannot read - either way it is not a backup
        }

        return false;
    }

    /// <summary>a backup by either its name or what is in it</summary>
    public static bool IsBackup(string path, string fileName)
    {
        return LooksLikeBackup(fileName) || ContentsLookLikeBackup(path);
    }

    //  set on whichever thread copied the file in and read on the main one
    private static volatile string _pending;

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
    /// what is waiting, without claiming it.
    ///
    /// A backup that opened the app arrives before there is a page to put the
    /// question on, and taking it first and finding nowhere to ask second
    /// threw the file away for good - the app opened, nothing was said and
    /// nothing was restored. So it is looked at first and only taken once
    /// there is somewhere to ask.
    /// </summary>
    public static string PeekPending()
    {
        return _pending;
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

        //the file first, because what is in it is the better answer to
        //whether it is a backup and there is nothing to look inside if it
        //cannot be read
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await page.DisplayAlert("Restore Backup", "That backup could not be read.", "Ok");
            return false;
        }

        //the name is only the first way of telling. a backup handed over by
        //the downloads list or a mail app arrives named something else - or
        //nothing at all - and refusing those turned away exactly the backups
        //that reach a new phone by the ordinary route
        if (!IsBackup(path, fileName))
        {
            await page.DisplayAlert("Unsupported File",
                $"This is not a backup file. You need a {Extension} file.", "Ok");
            return false;
        }

        //a nameless one still has to be called something in the question
        string shownAs = LooksLikeBackup(fileName) ? fileName : $"{fileName} (a Work Tracker backup)";

        //what is in the backup, and what is here, before anything is unpacked.
        //reading the backup walks every job in it, so it is done off the UI
        //thread - what is on the device is counted on it, because those are
        //the app's own lists
        DataSnapshot backup = await Task.Run(() => DataSnapshot.FromBackup(path));
        DataSnapshot here = DataSnapshot.Current(backup.TaxYears);

        //the one thing worth stopping somebody over: a backup holding older
        //work than the phone it is about to be put on. The date comes out of
        //the backup rather than off the file, so a backup taken this morning
        //of a round last touched in March says March
        if (DataSnapshot.BackupIsOlder(backup, here))
        {
            string older = DataSnapshot.HowLong(here.LastModified - backup.LastModified);

            if (!await page.DisplayAlert("This Backup Is Older Than Your Data",
                    $"The data on this device was last changed {here.WhenText}.\n\n"
                    + $"This backup was last changed {backup.WhenText} - {older} earlier"
                    + (backup.DateIsGuessed ? ", going by the files in it" : string.Empty) + ".\n\n"
                    + "Restoring it puts the round back to how it was then. Anything done since is lost.",
                    "Carry On", "Cancel"))
                return false;
        }

        string when = backup.KnowsWhenItChanged
            ? (backup.DateIsGuessed
                ? $"It does not say when it was last changed. Going by the files in it, {backup.WhenText}."
                : $"Last changed {backup.WhenText}.")
            : "It does not say when it was last changed.";

        string question = $"{shownAs}\n{when}\n\n"
            + "Everything on this device is replaced by what is in this backup. Anything done since it was made will be lost.";

        //asked in a loop so the figures can be looked at and the question
        //comes back afterwards, rather than the answer being lost to a look
        while (true)
        {
            string[] options = backup.Readable
                ? new string[] { ShowChanges } : new string[0];

            string choice = await page.DisplayActionSheet(question, "Cancel", "Restore", options);

            if (choice == ShowChanges)
            {
                await page.DisplayAlert("What Would Change", DataSnapshot.Difference(backup, here), "Ok");
                continue;
            }

            if (choice != "Restore")
                return false;

            break;
        }

        try
        {
            string dataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            //the data folder is made by whatever wrote to it first, and on a
            //phone the app was only just installed on nothing has yet
            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);

            ZipFile.ExtractToDirectory(path, dataFolder, true);

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

            //the device's data is the backup's now, and so is the date it was
            //last changed - the stamp came out of the zip with everything else
            DataStamp.Load();

            DataRefreshNotifier.NotifyDataChanged();
        }
        catch (Exception ex)
        {
            //a restore that fell over is the one failure worth being able to
            //chase afterwards - what is on the device is half of two rounds
            WorkTracker.CrashLogger.Log("BackupRestore.RestoreAsync", ex);

            await page.DisplayAlert("Restore Backup",
                $"There was a problem restoring that backup: {ex.Message}", "Ok");
            return false;
        }

        await page.DisplayAlert("Restored",
            "The backup has been put back. Close Work Tracker and open it again so every page is built from it.", "Ok");

        return true;
    }
}
