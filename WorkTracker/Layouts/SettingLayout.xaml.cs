namespace UiInterface.Layouts;

using Kernel;
using System.Xml.Serialization;
using System.IO;

using System.IO.Compression;


public struct SettingsData
{
    public string DefaultTNB, DefaultTAC, DefaultNotComming, DefaultResecdual;

    public int Date, Ref, Amount;
    public bool DebitAndCreditTogether;

    public List<string> JobNames;

    public int DefaultFrequence;
    public FrequenceType DefalutFrequenceType;

    public int DefaultJobDuration;

    public bool HaveShowenJobIntro;

    public string SymbolDone, SymbolPaid, SymbolDonePaid;
}

public class JobNamesSettingData
{
    public string Name { get; set; }
    public int Index { get; set; }
}

public class Settings
{

    public const string SaveDataFolder = "save";
    public const string BackupDataFolder = "backup";
    public static int DefaultFrequence = 4;
    public static FrequenceType DefaultFrequenceType = FrequenceType.Week;

    private static string _FilePath = "settings.txt";

    public static int DefaultJobDuration = 0;
    public static bool HaveShowenJobIntro = false;

    public static void Save(string dir = null)
    {
        string fileLocation = string.Empty;
        if (dir != null && dir != string.Empty)
        {
            fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
            fileLocation = Path.Combine(fileLocation, _FilePath);
        }
        else
            fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

        SettingsData sd = new SettingsData()
        {

        };
        sd = new SettingsData();
        sd.DefaultTAC = WorkPlanner.DefaultJobCompleateMessage;
        sd.DefaultTNB = WorkPlanner.DefaultTNBMessage;
        sd.DefaultResecdual = WorkPlanner.DefaultRearangeMessage;
        sd.DefaultNotComming = WorkPlanner.DefaultNotCommingMessage;

        sd.Date = StatmentViewer.Date;
        sd.Ref = StatmentViewer.Ref;
        sd.Amount = StatmentViewer.Amount;
        sd.DebitAndCreditTogether = StatmentViewer.DebitAndCreditTogether;

        sd.DefaultFrequence = DefaultFrequence;
        sd.DefalutFrequenceType = DefaultFrequenceType;

        sd.JobNames = new List<string>();
        Job.JobNames.Remove(string.Empty);
        sd.JobNames.AddRange(Job.JobNames);

        sd.DefaultJobDuration = DefaultJobDuration;
        sd.HaveShowenJobIntro = HaveShowenJobIntro;

        sd.SymbolDone = PaperView.PaperItem.StringDone;
        sd.SymbolPaid = PaperView.PaperItem.StringPaid;
        sd.SymbolDonePaid = PaperView.PaperItem.StringDonePaid;

        using (FileStream fs = File.Create(fileLocation))
        {
            XmlSerializer xs = new XmlSerializer(typeof(SettingsData));
            xs.Serialize(fs, sd);

        }

    }

    public static void Load(string dir = null)
    {
        string fileLocation = string.Empty;
        if (dir != null && dir != string.Empty)
        {
            fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
            fileLocation = Path.Combine(fileLocation, _FilePath);
        }
        else
            fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

        SettingsData sd = new SettingsData()
        {

        };
       try
        {
            using (FileStream fs = File.OpenRead(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(SettingsData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                sd = (SettingsData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.

                WorkPlanner.DefaultJobCompleateMessage = sd.DefaultTAC;
                WorkPlanner.DefaultTNBMessage = sd.DefaultTNB;
                WorkPlanner.DefaultRearangeMessage = sd.DefaultResecdual;
                WorkPlanner.DefaultNotCommingMessage = sd.DefaultNotComming;

                StatmentViewer.Date = sd.Date;
                StatmentViewer.Ref = sd.Ref;
                StatmentViewer.Amount = sd.Amount;
                StatmentViewer.DebitAndCreditTogether = sd.DebitAndCreditTogether;

                DefaultFrequence = sd.DefaultFrequence;
                DefaultFrequenceType = sd.DefalutFrequenceType;

                DefaultJobDuration = sd.DefaultJobDuration;
                HaveShowenJobIntro = sd.HaveShowenJobIntro;

                PaperView.PaperItem.StringDone = sd.SymbolDone;
                PaperView.PaperItem.StringPaid = sd.SymbolPaid;
                PaperView.PaperItem.StringDonePaid = sd.SymbolDonePaid;

                if (PaperView.PaperItem.StringPaid == null)
                    PaperView.PaperItem.StringPaid = "/";

                if (PaperView.PaperItem.StringDone == null)
                    PaperView.PaperItem.StringDone = "\\";

                if (PaperView.PaperItem.StringDonePaid == null)
                    PaperView.PaperItem.StringDonePaid = "X";
                if (sd.JobNames != null && sd.JobNames.Count > 0)
                {
                    Job.JobNames.Clear();
                    Job.JobNames.AddRange(sd.JobNames);
                }
            }
        }
        catch
        {

        }
    }
}
public partial class SettingLayout : ContentPage
{
    public SettingLayout()
    {
        InitializeComponent();


        NavigatedTo += SettingLayout_NavigatedTo;
        NavigatingFrom += SettingLayout_NavigatingFrom;

        RefreshCloudSection();
        CloudSync.StatusChanged += (status) =>
            MainThread.BeginInvokeOnMainThread(() => l_cloudStatus.Text = status);
    }

    private void RefreshCloudSection()
    {
        vsl_cloudSetup.IsVisible = !CloudSync.IsSignedIn;
        vsl_cloudConnected.IsVisible = CloudSync.IsSignedIn;

        //when the app ships with its own google credentials all that is
        //left for the user is the connect button
        vsl_cloudClientFields.IsVisible = !CloudSync.HasBuiltInClient;
        if (!CloudSync.HasBuiltInClient)
        {
            e_cloudClientId.Text = Preferences.Get("CloudSync_ClientId", string.Empty);
            e_cloudClientSecret.Text = Preferences.Get("CloudSync_ClientSecret", string.Empty);
        }

        sw_cloudAuto.IsToggled = CloudSync.AutoSync;
        l_cloudStatus.Text = $"Last sync: {CloudSync.LastSyncText}";
    }

    private async void bnt_cloudConnect_Clicked(object sender, EventArgs e)
    {
        if (vsl_cloudClientFields.IsVisible)
        {
            CloudSync.ClientId = e_cloudClientId.Text?.Trim();
            CloudSync.ClientSecret = e_cloudClientSecret.Text?.Trim();
        }

        if (string.IsNullOrWhiteSpace(CloudSync.ClientId))
        {
            await DisplayAlert("Cloud Sync",
                "You need a Client ID first.\n\n" +
                "1. Go to console.cloud.google.com and create a project\n" +
                "2. Enable the 'Google Drive API'\n" +
                "3. Create OAuth credentials of type 'Desktop app'\n" +
                "4. Paste the Client ID (and Client Secret) here", "Ok");
            return;
        }

        try
        {
            //opens the google login page in the browser; the app catches
            //the redirect itself so there is nothing to type or copy
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            bool ok = await CloudSync.SignInWithBrowserAsync(cts.Token);
            if (ok)
            {
                RefreshCloudSection();
                await DisplayAlert("Cloud Sync", "Connected! Your data will now sync with Google Drive.", "Ok");
                string result = await CloudSync.SyncNowAsync();
                l_cloudStatus.Text = result;
            }
            else
                await DisplayAlert("Cloud Sync", "Sign in was not completed. Try again.", "Ok");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cloud Sync", $"Could not connect: {ex.Message}", "Ok");
        }
    }

    private async void bnt_cloudSyncNow_Clicked(object sender, EventArgs e)
    {
        l_cloudStatus.Text = "Syncing...";
        string result = await CloudSync.SyncNowAsync();
        l_cloudStatus.Text = result;
    }

    private async void bnt_cloudDisconnect_Clicked(object sender, EventArgs e)
    {
        if (!await DisplayAlert("Cloud Sync", "Disconnect Google Drive? Your local data stays on this device.", "Disconnect", "Cancel"))
            return;
        CloudSync.SignOut();
        RefreshCloudSection();
    }

    private void sw_cloudAuto_Toggled(object sender, ToggledEventArgs e)
    {
        CloudSync.AutoSync = e.Value;
    }

    private void SettingLayout_NavigatedTo(object sender, NavigatedToEventArgs e)
    {

        List<JobNamesSettingData> jnsd
            = new List<JobNamesSettingData>();
        int index = 0;
        foreach (string s in Job.JobNames)
        {
            jnsd.Add(new JobNamesSettingData()
            {
                Name = s,
                Index = index,
            });
            index++;
        }

        l_jobNames.ItemsSource = jnsd;

        e_DefaultTNB.Text = WorkPlanner.DefaultTNBMessage;
        e_DefaultTAC.Text = WorkPlanner.DefaultJobCompleateMessage;
        e_DefaultNotComming.Text = WorkPlanner.DefaultNotCommingMessage;
        e_DefaultRearange.Text = WorkPlanner.DefaultRearangeMessage;

        e_defaultFrequence.Text = $"{Settings.DefaultFrequence}";
        p_frequencyType.SelectedItem = Settings.DefaultFrequenceType.ToString();

        e_defaultDuration.Text = Settings.DefaultJobDuration.ToString();

        
        e_pv_done.Text = PaperView.PaperItem.StringDone;
        e_pv_paid.Text = PaperView.PaperItem.StringPaid;
        e_pv_donepaid.Text = PaperView.PaperItem.StringDonePaid;
        SetZindexLables();
    }

    private void SetZindexLables()
    {
        var vt = sv_mainScrole.GetVisualTreeDescendants();
        Label l;
        Grid g;
        foreach (object o in vt)
        {
            l = o as Label;
            if (l != null)
            {
                l.ZIndex = 1;
            }
            else
            {
                g = o as Grid;
                if (g != null)
                    g.ZIndex = 1;
            }


        }
    }

    private void SettingLayout_NavigatingFrom(object sender, NavigatingFromEventArgs e)
    {
        WorkPlanner.DefaultTNBMessage = e_DefaultTNB.Text;
        WorkPlanner.DefaultJobCompleateMessage = e_DefaultTAC.Text;
        WorkPlanner.DefaultNotCommingMessage = e_DefaultNotComming.Text;
        WorkPlanner.DefaultRearangeMessage = e_DefaultRearange.Text;

        Settings.DefaultFrequence = (int)Convert.ToDecimal(e_defaultFrequence.Text);
        Settings.DefaultFrequenceType = (FrequenceType)p_frequencyType.SelectedIndex;

        Settings.DefaultJobDuration = (int)Convert.ToDecimal(e_defaultDuration.Text);

        PaperView.PaperItem.StringDone = e_pv_done.Text;
        PaperView.PaperItem.StringPaid = e_pv_paid.Text;
        PaperView.PaperItem.StringDonePaid = e_pv_donepaid.Text;

        Settings.Save();
    }

    private void Preview(string msg)
    {
        Job j = new Job()
        {
            Address = new Location()
            {
                Street = "Queen Street",
                PropertyNameNumber = "22",
                Area = "Rotherham"

            },
            Price = 7.5f,

        };

        Customer c = new Customer()
        {
            Address = j.Address,
            Balance = 15,
        };
        j.TmpSetCustomer(c);

        DisplayAlert("Message Privew", WorkPlanner.ReplaceTags(msg, DateTime.Today.AddDays(1), j), "Ok");
    }

    private void bnt_previewTNB(object sender, EventArgs e)
    {
        Preview(e_DefaultTNB.Text);

    }

    private void bnt_previewTAC(object sender, EventArgs e)
    {
        Preview(e_DefaultTAC.Text);
    }

    private void bnt_previewRearange(object sender, EventArgs e)
    {
        Preview(e_DefaultRearange.Text);
    }

    private void bnt_previewNotComming(object sender, EventArgs e)
    {
        Preview(e_DefaultNotComming.Text);
    }

    private void bnt_resetImportBanking(object sender, EventArgs e)
    {
        StatmentViewer.Reset();
        DisplayAlert("Reset", "Import settings have been reset", "Ok");
    }

    private void bnt_addJobType(object sender, EventArgs e)
    {
        List<JobNamesSettingData> jnsd
            = new List<JobNamesSettingData>();

        int index = 0;
        if (Job.JobNames.Contains(string.Empty))
            return;

        Job.JobNames.Add(string.Empty);

        foreach (string s in Job.JobNames)
        {
            jnsd.Add(new JobNamesSettingData()
            {
                Name = s,
                Index = index,
            });
            index++;
        }

        l_jobNames.ItemsSource = null;
        l_jobNames.ItemsSource = jnsd;


    }

    private void e_textChanged(object sender, TextChangedEventArgs e)
    {
        Entry entry = sender as Entry;
        if (e.OldTextValue == null)
            return;

        if (e.NewTextValue == null)
            return;

        int i = Convert.ToInt32(entry.ClassId);
        Job.JobNames[i] = entry.Text;
    }

    private async void CreateBackup()
    {
        //step one copy files to backup save folder

        string saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Settings.SaveDataFolder);

        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }

        Customer.Save(Settings.SaveDataFolder);
        Job.Save(Settings.SaveDataFolder);
        Payment.Save(Settings.SaveDataFolder);
        Expense.Save(Settings.SaveDataFolder);
        Settings.Save(Settings.SaveDataFolder);

        string backupfile = $"Backup{DateTime.Now}.rbf";
        backupfile = backupfile.Replace("/", "-");
        backupfile = backupfile.Replace(":", "");
        backupfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), backupfile);
        backupfile = Path.Combine(Settings.BackupDataFolder, backupfile);
        ZipFile.CreateFromDirectory(saveDir, backupfile);

        await DisplayAlert("Backup Created", "Your backup has been created", "Ok");

        ShareFile sf = new ShareFile(backupfile);
        await Share.RequestAsync(new ShareFileRequest("Work Tracker Backup", sf));
    }

    private async void bnt_createBackup_Clicked(object sender, EventArgs e)
    {



        CreateBackup();
    }

    private async void bnt_restorBackup_Clicked(object sender, EventArgs e)
    {
        

     /*   var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { ".rbf" } }, // or general UTType values
                    { DevicePlatform.Android, new[] { "rbf" } },
                    { DevicePlatform.WinUI, new[] { ".rbf" } },
                    { DevicePlatform.Tizen, new[] { ".rbf" } },
                    { DevicePlatform.macOS, new[] { ".rbf" } }, // or general UTType values
                });

        PickOptions options = new()
        {
            PickerTitle = "Please select a comic file",
            FileTypes = customFileType,
        };
     */
        //FileResult fr = await FilePicker.Default.PickAsync(options);
        FileResult fr = await FilePicker.Default.PickAsync();
        if (fr != null)
        {
            if (fr.FileName.EndsWith("rbf", StringComparison.OrdinalIgnoreCase))
            {

                try
                {
                    ZipFile.ExtractToDirectory(fr.FullPath, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), true);

                    Customer.Load();
                    Settings.Load();
                    Job.Reset();
                    Job.Load();
                    Payment.Load();
                    Expense.Load();
                    DataRefreshNotifier.NotifyDataChanged();
                    await DisplayAlert("Success", "Backup has be restored sucsessfuly. Application will now restart", "ok");
                    
                    
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", "There was an error restoring backup. Please try again.", "ok");
                }

            }
            else
                await DisplayAlert("Unsupported File", "This is not a valid backup file. You need a rbf file", "ok");
        }
    }

    private async void bnt_importXlsx_Clicked(object sender, EventArgs e)
    {
        FileResult fr = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a round spreadsheet (.xlsx)",
        });
        if (fr == null)
            return;

        if (!fr.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Unsupported File", "This is not an Excel file. You need a .xlsx file.", "ok");
            return;
        }

        // the sheet has streets but no city/area - ask once for the file
        string city = await DisplayPromptAsync("Import", "City for the customers in this sheet (optional):",
            "Next", "Cancel", initialValue: GuessCityFromFileName(fr.FileName));
        if (city == null)
            return;
        string area = await DisplayPromptAsync("Import", "Area for the customers in this sheet (optional):",
            "Import", "Cancel", initialValue: "");
        if (area == null)
            return;

        try
        {
            ImportExport.ImportResult result;
            using (Stream stream = await fr.OpenReadAsync())
                result = ImportExport.CustomerImporter.Import(stream, city.Trim(), area.Trim());

            DataRefreshNotifier.NotifyDataChanged();

            string summary = $"Customers created: {result.Created}\nCustomers updated: {result.Updated}";
            if (result.MissingPrice > 0)
                summary += $"\n\n{result.MissingPrice} customer(s) had no readable price - they were imported with price 0 and a note, please set their price manually.";
            if (result.Problems.Count > 0)
                summary += $"\n\n{result.Problems.Count} row(s) failed:\n" + string.Join("\n", result.Problems.Take(5));
            await DisplayAlert("Import Complete", summary, "Ok");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Failed", $"Could not import this file: {ex.Message}", "ok");
        }
    }

    private async void bnt_exportXlsx_Clicked(object sender, EventArgs e)
    {
        try
        {
            string path = Path.Combine(FileSystem.CacheDirectory, $"Round {DateTime.Now:yyyy-MM-dd}.xlsx");
            using (FileStream fs = File.Create(path))
                ImportExport.RoundSheetWriter.Write(fs, Job.Query());

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Round",
                File = new ShareFile(path),
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Failed", $"Could not export the round: {ex.Message}", "ok");
        }
    }

    private static string GuessCityFromFileName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        int cut = name.IndexOfAny(new[] { '_', '-', ' ' });
        return cut > 0 ? name.Substring(0, cut) : name;
    }

    private async void bnt_deleteData_Clicked(object sender, EventArgs e)
    {
        if (await DisplayAlert("WARING!!!", "This can not be undone. Are you sure you wish to delete all data?", "Yes", "No"))
            if (await DisplayAlert("WARING!!!", "Are you sure", "Yes Delete It All", "No Don't Delete Anything"))
            {
                Job.DeleteData();
                Customer.DeleteData();
                Payment.DeleteData();
                Expense.DeleteData();

                Job.Save();
                Customer.Save();
                Payment.Save();
                Expense.Save();
                DataRefreshNotifier.NotifyDataChanged();
                await DisplayAlert("Complete", "All data erased", "Ok");
            }
    }

    private void bnt_messagesHelp_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Help", "Here you can edit the default messages for different situations.\nTags can also be used <date> and <owing> are currently in development.", "Ok");
    }
}