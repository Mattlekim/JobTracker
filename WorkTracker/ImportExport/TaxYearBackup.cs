namespace UiInterface.ImportExport;

using System.IO.Compression;
using Kernel;
using UiInterface.Layouts;

/// <summary>
/// Builds a backup file (.rbf) out of the chosen tax years.
///
/// Two kinds of thing go in. The round itself - customers, jobs, quotes,
/// remembered payees, direct debits, settings - belongs to no tax year and
/// goes into every backup, because you need last year's customers to make
/// sense of last year's work. The tax records - expenses, income, the
/// statements they were read off and the receipt photos that back them up -
/// are filed by tax year and only the years asked for go in.
///
/// Restoring is unchanged: the zip is unpacked over the data folder, so a
/// backup of one year puts that year back and leaves the others alone.
/// </summary>
public static class TaxYearBackup
{
    /// <summary>
    /// what the backup is made from on disk. everything is written into this
    /// folder and then zipped
    /// </summary>
    private static string SaveFolder()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Settings.SaveDataFolder);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    public class BackupResult
    {
        public string Path = string.Empty;
        public int Receipts;
        public int Statements;
        public List<int> Years = new List<int>();

        public string FormattedYears
        {
            get
            {
                if (Years.Count == 0)
                    return "no tax years";
                if (Years.Count == 1)
                    return $"tax year {TaxCalendar.YearName(Years[0])}";
                return $"{Years.Count} tax years";
            }
        }
    }

    /// <summary>
    /// writes the backup and returns where it ended up. the file goes in the
    /// cache folder, because it is made to be shared straight out and should
    /// not sit in with the live data taking up room
    /// </summary>
    public static BackupResult Create(List<int> years, string fileName)
    {
        BackupResult result = new BackupResult();
        result.Years = new List<int>(years);

        HashSet<int> wanted = new HashSet<int>(years);

        string saveDir = SaveFolder();
        ClearFolder(saveDir);

        //the round, which belongs to no one tax year
        Customer.Save(Settings.SaveDataFolder);
        Job.Save(Settings.SaveDataFolder);
        ExpenseRule.Save(Settings.SaveDataFolder);
        GoCardlessRequest.Save(Settings.SaveDataFolder);
        Settings.Save(Settings.SaveDataFolder);

        //the tax records, for the years asked for
        Expense.Save(Settings.SaveDataFolder, wanted);
        Payment.Save(Settings.SaveDataFolder, wanted);
        StatementRecord.Save(Settings.SaveDataFolder, wanted);

        //and the paperwork behind them
        foreach (int year in wanted)
        {
            result.Receipts += CopyYearFolder(Expense.GetReceiptFolderPath(year),
                Path.Combine(saveDir, Expense.ReceiptFolder, TaxCalendar.YearFolderName(year)));

            result.Statements += CopyYearFolder(StatementRecord.GetStatementFolderPath(year),
                Path.Combine(saveDir, StatementRecord.StatementFolder, TaxCalendar.YearFolderName(year)));
        }

        //receipts from before they were filed by year, which no year folder
        //claims - they go in so nothing is left behind
        result.Receipts += CopyLooseReceipts(Path.Combine(saveDir, Expense.ReceiptFolder));

        string path = Path.Combine(FileSystem.CacheDirectory, fileName);
        if (File.Exists(path))
            File.Delete(path);

        ZipFile.CreateFromDirectory(saveDir, path);

        result.Path = path;
        return result;
    }

    /// <summary>a name for the backup that says what is in it</summary>
    public static string FileNameFor(List<int> years, bool everything)
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd HHmm");

        if (!everything && years.Count == 1)
            return $"Tax Year {TaxCalendar.YearFolderName(years[0])} {stamp}.rbf";

        return $"Backup {stamp}.rbf";
    }

    private static void ClearFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
        catch
        {
        }
        Directory.CreateDirectory(folder);
    }

    private static int CopyYearFolder(string source, string destination)
    {
        int count = 0;
        try
        {
            if (!Directory.Exists(source))
                return 0;

            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
                count++;
            }
        }
        catch
        {
        }
        return count;
    }

    /// <summary>
    /// photos still sitting loose in the receipts folder, from before they
    /// were filed by tax year
    /// </summary>
    private static int CopyLooseReceipts(string destination)
    {
        int count = 0;
        try
        {
            string source = Expense.GetReceiptFolderPath();
            string[] files = Directory.GetFiles(source);
            if (files.Length == 0)
                return 0;

            Directory.CreateDirectory(destination);
            foreach (string file in files)
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
                count++;
            }
        }
        catch
        {
        }
        return count;
    }
}
