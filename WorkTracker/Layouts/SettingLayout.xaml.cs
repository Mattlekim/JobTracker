namespace UiInterface.Layouts;

using Kernel;
using UiInterface.ImportExport;
using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;
using System.IO;

using System.IO.Compression;


public struct SettingsData
{
    public string DefaultTNB, DefaultTAC, DefaultNotComming, DefaultResecdual;

    public int Date, Ref, Amount;
    public bool DebitAndCreditTogether;

    /// <summary>the same three columns again, for pdf statements - they rarely match the bank's csv</summary>
    public int PdfDate, PdfRef, PdfAmount;
    public bool PdfDebitAndCreditTogether;

    /// <summary>
    /// the money out column, for banks that keep money in and money out
    /// apart. saved as "the column plus one" so settings written before this
    /// existed read back as 0, which means "not chosen yet" rather than
    /// "column 0"
    /// </summary>
    public int DebitPlusOne, PdfDebitPlusOne;

    /// <summary>how hard receipt photos are squashed before they are stored</summary>
    public int ReceiptPhotoMaxSize, ReceiptPhotoQuality;

    public List<string> JobNames;

    /// <summary>
    /// the tags offered when a job or a day's work is tagged. what a visit
    /// was actually tagged with is kept on the visit, not here
    /// </summary>
    public List<string> TagNames;

    /// <summary>
    /// the tags the tag bar is set to put on work as it is marked done. kept
    /// so a round is not left half tagged by the app being closed part way
    /// through the day
    /// </summary>
    public List<string> AutoTags;

    /// <summary>the rounds work can be put on</summary>
    public List<string> RoundNames;

    /// <summary>the paypal.me name a payment link is built from</summary>
    public string PayPalHandle;

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

    /// <summary>
    /// how long a job with no estimate of its own takes.
    ///
    /// It is kept on Job rather than here, and this is a way in to the same
    /// figure rather than a second copy of it: every page that shows work
    /// asks the job how long it is, and a job cannot see the settings.
    /// </summary>
    public static int DefaultJobDuration
    {
        get { return Job.DefaultDuration; }
        set { Job.DefaultDuration = value; }
    }

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

        sd.PdfDate = StatmentViewer.PdfDate;
        sd.PdfRef = StatmentViewer.PdfRef;
        sd.PdfAmount = StatmentViewer.PdfAmount;
        sd.PdfDebitAndCreditTogether = StatmentViewer.PdfDebitAndCreditTogether;

        sd.DebitPlusOne = StatmentViewer.Debit + 1;
        sd.PdfDebitPlusOne = StatmentViewer.PdfDebit + 1;

        sd.ReceiptPhotoMaxSize = ReceiptPhoto.MaxSize;
        sd.ReceiptPhotoQuality = ReceiptPhoto.Quality;

        sd.DefaultFrequence = DefaultFrequence;
        sd.DefalutFrequenceType = DefaultFrequenceType;

        sd.JobNames = new List<string>();
        Job.JobNames.Remove(string.Empty);
        sd.JobNames.AddRange(Job.JobNames);

        sd.TagNames = new List<string>();
        Job.TagNames.Remove(string.Empty);
        sd.TagNames.AddRange(Job.TagNames);

        sd.AutoTags = new List<string>();
        sd.AutoTags.AddRange(Job.AutoTags);

        sd.RoundNames = new List<string>();
        Job.RoundNames.Remove(string.Empty);
        sd.RoundNames.AddRange(Job.RoundNames);

        sd.PayPalHandle = PayPal.Handle;

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

                //settings saved before pdf import existed have no pdf columns, which reads back as
                //0,0,0 - the viewer treats that as not chosen yet and asks for them
                StatmentViewer.PdfDate = sd.PdfDate;
                StatmentViewer.PdfRef = sd.PdfRef;
                StatmentViewer.PdfAmount = sd.PdfAmount;
                StatmentViewer.PdfDebitAndCreditTogether = sd.PdfDebitAndCreditTogether;

                //0 is what settings saved before the money out column existed
                //read back as, and means it has not been chosen
                StatmentViewer.Debit = sd.DebitPlusOne - 1;
                StatmentViewer.PdfDebit = sd.PdfDebitPlusOne - 1;

                //0 from an older settings file, which the property turns back
                //into the default
                ReceiptPhoto.MaxSize = sd.ReceiptPhotoMaxSize;
                ReceiptPhoto.Quality = sd.ReceiptPhotoQuality;

                DefaultFrequence = sd.DefaultFrequence;
                DefaultFrequenceType = sd.DefalutFrequenceType;

                DefaultJobDuration = sd.DefaultJobDuration;
                HaveShowenJobIntro = sd.HaveShowenJobIntro;

                //settings written before this existed read back as null,
                //which is the same as never having set one
                PayPal.Handle = sd.PayPalHandle;

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

                //null is a settings file written before tags existed, which
                //keeps the ones this starts with. an empty list is a round
                //that has deleted the lot on purpose, and that is left empty
                if (sd.TagNames != null)
                {
                    Job.TagNames.Clear();
                    Job.TagNames.AddRange(sd.TagNames);
                }

                Job.AutoTags.Clear();
                if (sd.AutoTags != null)
                    Job.AutoTags.AddRange(sd.AutoTags);

                //there are no rounds to start with, so whatever is in the
                //file is the whole truth - including none at all
                Job.RoundNames.Clear();
                if (sd.RoundNames != null)
                    Job.RoundNames.AddRange(sd.RoundNames);
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

        ShowAppVersion();
        RefreshCloudSection();
        RefreshGoCardlessSection();
        CloudSync.StatusChanged += (status) =>
            MainThread.BeginInvokeOnMainThread(() => l_cloudStatus.Text = status);
    }

    /// <summary>
    /// opens or closes a settings section. the section to work on is named
    /// in the header button's ClassId
    /// </summary>
    private void Section_Clicked(object sender, EventArgs e)
    {
        Button header = sender as Button;
        if (header == null || string.IsNullOrEmpty(header.ClassId))
            return;

        View section = this.FindByName<View>(header.ClassId);
        if (section == null)
            return;

        section.IsVisible = !section.IsVisible;

        //swap the arrow on the front of the heading for the other one
        string title = header.Text.Length > 2 ? header.Text.Substring(2) : header.Text;
        header.Text = (section.IsVisible ? "▾ " : "▸ ") + title;
    }

    /// <summary>
    /// reads the version straight out of the built app, and the build date
    /// out of the assembly, so what is shown here is always what is
    /// installed rather than a number somebody forgot to change
    /// </summary>
    private void ShowAppVersion()
    {
        string version = AppInfo.Current.VersionString;
        string build = AppInfo.Current.BuildString;

        l_appVersion.Text = string.IsNullOrWhiteSpace(build) || build == version
            ? $"Version {version}"
            : $"Version {version} (build {build})";

        string released = BuildDate();
        l_appReleased.Text = string.IsNullOrWhiteSpace(released)
            ? string.Empty
            : $"Released {released}";
        l_appReleased.IsVisible = !string.IsNullOrWhiteSpace(released);
    }

    /// <summary>the date this build was compiled, stamped in by the build</summary>
    private static string BuildDate()
    {
        try
        {
            foreach (AssemblyMetadataAttribute a in typeof(SettingLayout).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>())
                if (a.Key == "BuildDate" && !string.IsNullOrWhiteSpace(a.Value))
                    if (DateTime.TryParse(a.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
                        return d.ToShortDateString();
                    else
                        return a.Value;
        }
        catch
        {
        }
        return string.Empty;
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

        if (!CloudSync.HasUsableClientId)
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

    private void RefreshGoCardlessSection()
    {
        vsl_gcSetup.IsVisible = !GoCardless.IsConnected;
        vsl_gcConnected.IsVisible = GoCardless.IsConnected;
        sw_gcSandbox.IsToggled = GoCardless.UseSandbox;
        if (GoCardless.IsConnected)
            l_gcStatus.Text = GoCardless.UseSandbox ? "Connected (sandbox)" : "Connected";

        int pending = GoCardlessRequest.QueryPending().Count;
        float total = 0;
        foreach (GoCardlessRequest r in GoCardlessRequest.QueryPending())
            total += r.Amount;
        l_gcPending.Text = pending == 0
            ? "No direct debits waiting"
            : $"{pending} direct debit(s) waiting, {Gloable.CurrenceSymbol}{total:0.00} in total";

        sw_gcCustomPricing.IsToggled = GoCardless.CustomPricing;
        l_gcPricingHint.Text = GoCardless.CustomPricing
            ? "GoCardless invoices you for its fees separately, so they cannot be read off your payouts. Add each fee invoice as an expense under Bank charges so it is claimed against tax."
            : "GoCardless takes its fee out of each payout, so the fees are recorded as expenses for you automatically under Bank charges. Turn this on if you are on a custom pricing plan and are invoiced for fees instead.";
    }

    private async void sw_gcCustomPricing_Toggled(object sender, ToggledEventArgs e)
    {
        if (GoCardless.CustomPricing == e.Value)
            return;

        GoCardless.CustomPricing = e.Value;
        RefreshGoCardlessSection();

        if (e.Value)
            return;

        //back on deducted fees, so anything missed can be picked up now
        int added = await GoCardless.RecordPayoutFeesAsync();
        if (added > 0)
        {
            RefreshGoCardlessSection();
            await DisplayAlert("GoCardless", $"{added} payout fee(s) recorded as expenses.", "Ok");
        }
    }

    private async void bnt_gcCheck_Clicked(object sender, EventArgs e)
    {
        l_gcPending.Text = "Checking...";
        string result = await GoCardless.RefreshPendingAsync();
        RefreshGoCardlessSection();
        await DisplayAlert("GoCardless", result, "Ok");
    }

    private async void bnt_gcConnect_Clicked(object sender, EventArgs e)
    {
        string token = e_gcToken.Text?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlert("GoCardless",
                "You need an access token first.\n\n" +
                "1. Log in to your GoCardless dashboard\n" +
                "2. Go to Developers -> Create -> Access token\n" +
                "3. Give it read-write access and paste it here", "Ok");
            return;
        }

        GoCardless.AccessToken = token;
        GoCardless.UseSandbox = sw_gcSandbox.IsToggled;

        try
        {
            string name = await GoCardless.VerifyAsync();
            RefreshGoCardlessSection();
            l_gcStatus.Text = GoCardless.UseSandbox ? $"Connected to {name} (sandbox)" : $"Connected to {name}";
            await DisplayAlert("GoCardless", $"Connected to {name}. Now link customers to their direct debits from the customer details page.", "Ok");
        }
        catch (Exception ex)
        {
            GoCardless.Disconnect();
            await DisplayAlert("GoCardless", $"Could not connect: {ex.Message}", "Ok");
        }
    }

    private async void bnt_gcDisconnect_Clicked(object sender, EventArgs e)
    {
        if (!await DisplayAlert("GoCardless", "Disconnect GoCardless? Customers stay linked and no direct debits are cancelled - you just cannot take payments from this app until you connect again.", "Disconnect", "Cancel"))
            return;
        GoCardless.Disconnect();
        e_gcToken.Text = string.Empty;
        RefreshGoCardlessSection();
    }

    private void SettingLayout_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        ShowTidyCustomers();

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

        ShowTagNames();
        ShowRoundNames();

        //not read from a file - it is only ever whatever it was set to since
        //the app started
        cb_screenshotMode.IsChecked = ScreenshotMode.On;

        e_paypalHandle.Text = PayPal.Handle;
        ShowPayPalExample();

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
        ShowReceiptPhotoSection();
        SetZindexLables();
    }

    /// <summary>
    /// how hard receipt photos are squashed, and how much room the ones
    /// already taken are using
    /// </summary>
    private void ShowReceiptPhotoSection()
    {
        if (p_photoQuality.Items.Count == 0)
            foreach (string name in ReceiptPhoto.QualityNames)
                p_photoQuality.Items.Add(name);

        //set straight, so choosing the setting that is already in use does
        //not count as a change and save over itself
        _loadingPhotoQuality = true;
        p_photoQuality.SelectedIndex = ReceiptPhoto.QualityChoice;
        _loadingPhotoQuality = false;

        ShowPhotoQualityDetail();
        ShowReceiptStorage();
    }

    private bool _loadingPhotoQuality = false;

    private void ShowPhotoQualityDetail()
    {
        l_photoQualityDetail.Text = $"Photos are scaled to {ReceiptPhoto.MaxSize} pixels on the longest side at {ReceiptPhoto.Quality}% quality.";
    }

    private void ShowReceiptStorage()
    {
        long size = ReceiptPhoto.StoredSize();
        l_receiptStorage.Text = size == 0
            ? "No receipt photos stored yet."
            : $"Receipt photos are using {ReceiptPhoto.FormatSize(size)}.";
    }

    private void p_photoQuality_Changed(object sender, EventArgs e)
    {
        if (_loadingPhotoQuality || p_photoQuality.SelectedIndex < 0)
            return;

        ReceiptPhoto.QualityChoice = p_photoQuality.SelectedIndex;
        Settings.Save();
        ShowPhotoQualityDetail();
    }

    /// <summary>
    /// goes back over the photos taken before any of this existed and
    /// squashes them too, which is where most of the space is
    /// </summary>
    private async void bnt_recompressPhotos_Clicked(object sender, EventArgs e)
    {
        long before = ReceiptPhoto.StoredSize();
        if (before == 0)
        {
            await DisplayAlert("Receipt Photos", "There are no receipt photos to shrink.", "Ok");
            return;
        }

        if (!await DisplayAlert("Shrink Photos?",
                $"Every receipt photo will be scaled down to the size set above. They stay readable, but the full size originals cannot be got back afterwards. {ReceiptPhoto.FormatSize(before)} is being used now.",
                "Shrink Them", "Cancel"))
            return;

        bnt_recompressPhotos.IsEnabled = false;
        bnt_recompressPhotos.Text = "Shrinking...";

        (int shrunk, long saved) = await ReceiptPhoto.RecompressStoredAsync();

        bnt_recompressPhotos.Text = "Shrink Photos Already Saved";
        bnt_recompressPhotos.IsEnabled = true;

        ShowReceiptStorage();

        await DisplayAlert("Receipt Photos",
            shrunk == 0
                ? "Nothing to do - the photos are already as small as they are going to get."
                : $"{shrunk} photo(s) shrunk, saving {ReceiptPhoto.FormatSize(saved)}.",
            "Ok");
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

    /// <summary>
    /// says how many customer records have no work against them, so the
    /// button is worth pressing rather than a page you have to go and look at
    /// </summary>
    private void ShowTidyCustomers()
    {
        int spare = Customer.WithoutWork().Count;

        l_tidyCustomers.IsVisible = spare > 0;
        l_tidyCustomers.Text = spare == 1
            ? "1 customer has no work against them."
            : $"{spare} customers have no work against them.";
    }

    private async void bnt_tidyCustomers_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new TidyCustomers());
        ShowTidyCustomers();
    }

    /// <summary>
    /// Swaps the road and town names on screen for made up ones, for
    /// photographing the round.
    ///
    /// **Nothing is saved here on purpose.** ScreenshotMode.On is a plain
    /// static that no part of Settings.Save writes down, so it is off again
    /// the next time the app starts - which is the only way to be sure a
    /// round is never quietly showing made up addresses weeks later.
    /// </summary>
    private void cb_screenshotMode_Changed(object sender, CheckedChangedEventArgs e)
    {
        CheckBox cb = sender as CheckBox;
        if (cb == null)
            return;

        ScreenshotMode.On = cb.IsChecked;

        //every address on screen is worked out from the job, so the pages
        //only need telling to build themselves again
        Job.RefreshJobs();
        DataRefreshNotifier.NotifyDataChanged();
    }

    /// <summary>
    /// the paypal.me name a payment link is built from. saved with the rest
    /// of the settings when the page is left, like everything else here
    /// </summary>
    private void e_paypalHandle_Changed(object sender, TextChangedEventArgs e)
    {
        PayPal.Handle = e.NewTextValue == null ? string.Empty : e.NewTextValue.Trim();
        ShowPayPalExample();
    }

    /// <summary>
    /// the link as it will actually be sent, so a name typed wrong is
    /// obvious before a customer gets it
    /// </summary>
    private void ShowPayPalExample()
    {
        l_paypalExample.Text = PayPal.IsSetUp
            ? $"A {Gloable.CurrenceSymbol}10 job would be sent as {PayPal.LinkFor(10)}"
            : "No name set, so there is nothing to send yet.";
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

    /// <summary>
    /// the tags to pick from when something is tagged. only the list is
    /// edited here - a tag already put on a visit stays on it, because it
    /// says what happened that day
    /// </summary>
    private void ShowTagNames()
    {
        List<JobNamesSettingData> tags = new List<JobNamesSettingData>();

        int index = 0;
        foreach (string s in Job.TagNames)
        {
            tags.Add(new JobNamesSettingData()
            {
                Name = s,
                Index = index,
            });
            index++;
        }

        l_tagNames.ItemsSource = null;
        l_tagNames.ItemsSource = tags;
    }

    /// <summary>
    /// the rounds work can be put on. taking one off here does not take the
    /// work off it - the round is on the job, and Job.RoundsInUse is what the
    /// filters go by
    /// </summary>
    private void ShowRoundNames()
    {
        List<JobNamesSettingData> rounds = new List<JobNamesSettingData>();

        int index = 0;
        foreach (string s in Job.RoundNames)
        {
            rounds.Add(new JobNamesSettingData()
            {
                Name = s,
                Index = index,
            });
            index++;
        }

        l_roundNames.ItemsSource = null;
        l_roundNames.ItemsSource = rounds;
    }

    private void bnt_addRound(object sender, EventArgs e)
    {
        if (Job.RoundNames.Contains(string.Empty))
            return;

        Job.RoundNames.Add(string.Empty);
        ShowRoundNames();
    }

    private void e_roundTextChanged(object sender, TextChangedEventArgs e)
    {
        Entry entry = sender as Entry;
        if (e.OldTextValue == null || e.NewTextValue == null)
            return;

        int i = Convert.ToInt32(entry.ClassId);
        Job.RoundNames[i] = entry.Text;
    }

    private void bnt_addTag(object sender, EventArgs e)
    {
        //one blank row at a time, the same as the job types: a second one
        //would have nothing to tell it apart from the first
        if (Job.TagNames.Contains(string.Empty))
            return;

        Job.TagNames.Add(string.Empty);
        ShowTagNames();
    }

    private void e_tagTextChanged(object sender, TextChangedEventArgs e)
    {
        Entry entry = sender as Entry;
        if (e.OldTextValue == null || e.NewTextValue == null)
            return;

        int i = Convert.ToInt32(entry.ClassId);
        Job.TagNames[i] = entry.Text;
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

    /// <summary>
    /// The round - customers, jobs, quotes, remembered payees, settings -
    /// goes into every backup, because last year's figures make no sense
    /// without the customers they came from. The tax records are filed by
    /// year, so once there is more than one year of them the backup asks
    /// which years to take.
    /// </summary>
    private async void CreateBackup()
    {
        List<int> years = TaxCalendar.YearsWithData();
        bool everything = true;

        if (years.Count > 1)
        {
            List<int> chosen = await SelectTaxYears.AskAsync(Navigation,
                "Which tax years do you want in this backup? Customers, jobs and settings always go in.",
                "Back Up", years);

            if (chosen == null)
                return;

            everything = chosen.Count == years.Count;
            years = chosen;
        }

        await RunBackup(years, everything, "Work Tracker Backup");
    }

    private async Task RunBackup(List<int> years, bool everything, string shareTitle)
    {
        TaxYearBackup.BackupResult result;
        try
        {
            result = TaxYearBackup.Create(years, TaxYearBackup.FileNameFor(years, everything));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Backup Failed", $"The backup could not be created: {ex.Message}", "Ok");
            return;
        }

        await DisplayAlert("Backup Created",
            $"Backed up {result.FormattedYears}, with {result.Receipts} receipt photo(s) and {result.Statements} bank statement(s).",
            "Ok");

        ShareFile sf = new ShareFile(result.Path);
        await Share.RequestAsync(new ShareFileRequest(shareTitle, sf));
    }

    private void bnt_createBackup_Clicked(object sender, EventArgs e)
    {
        CreateBackup();
    }

    /// <summary>
    /// Picks a backup and puts it back.
    ///
    /// The restoring itself is BackupRestore's, not this page's: a backup
    /// opened from a file manager or an email has to do exactly the same
    /// thing, and two of them would drift apart.
    /// </summary>
    private async void bnt_restorBackup_Clicked(object sender, EventArgs e)
    {
        //android has no type of its own for .rbf, so the picker is left
        //showing everything there and the name is checked afterwards. giving
        //it a made up mime type shows a picker with nothing in it at all
        FileResult fr = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a Work Tracker backup (.rbf)",
        });

        if (fr == null)
            return;

        await UiInterface.ImportExport.BackupRestore.RestoreAsync(fr.FullPath, fr.FileName, this);
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
            if (result.TnbFromNotes > 0)
                summary += $"\n{result.TnbFromNotes} set to text the night before (from notes)";
            if (result.PhonesFound > 0)
                summary += $"\n{result.PhonesFound} phone number(s) taken out of notes";
            if (result.FrontPrices > 0)
                summary += $"\n{result.FrontPrices} front only price(s) added as an alternative price";
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
                ExpenseRule.DeleteData();
                StatementRecord.DeleteData();
                GoCardlessRequest.DeleteData();

                Job.Save();
                Customer.Save();
                Payment.Save();
                Expense.Save();
                ExpenseRule.Save();
                StatementRecord.Save();
                GoCardlessRequest.Save();
                DataRefreshNotifier.NotifyDataChanged();
                await DisplayAlert("Complete", "All data erased", "Ok");
            }
    }

    private void bnt_messagesHelp_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Help", "Here you can edit the default messages for different situations.\nTags can also be used <date> and <owing> are currently in development.", "Ok");
    }
}