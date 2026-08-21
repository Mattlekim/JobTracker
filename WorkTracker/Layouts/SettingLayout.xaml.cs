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

    /// <summary>
    /// whether the send-work-to-someone buttons are shown. off by default -
    /// most rounds are one person - and settings written before this existed
    /// read back as false, which is the same thing
    /// </summary>
    public bool EnableWorkSharing;

    /// <summary>
    /// whether the Universal Credit page is on the Money tab. off by default -
    /// most rounds are not on a claim - and settings written before this
    /// existed read back as false, which is the same thing
    /// </summary>
    public bool ShowUniversalCredit;

    /// <summary>
    /// the day the claim started, which every assessment period is measured
    /// from. MinValue - which is what an older settings file reads back as -
    /// is nobody having said
    /// </summary>
    public DateTime UniversalCreditStart;

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

    /// <summary>
    /// the sending half of work sharing is opt-in: most rounds are one
    /// person, and the buttons would only be in the way. receiving is not
    /// gated - somebody handed a work list needs no setting to open it
    /// </summary>
    public static bool EnableWorkSharing = false;

    /// <summary>
    /// whether the Universal Credit page shows on the Money tab. Off by
    /// default: most rounds are not on a claim, and a page of figures that
    /// mean nothing to you is worse than no page at all.
    /// </summary>
    public static bool ShowUniversalCredit = false;

    /// <summary>
    /// the day the Universal Credit claim started. It is kept on
    /// UniversalCredit rather than here, and this is a way in to the same
    /// date rather than a second copy of it - the kernel does the month
    /// arithmetic and cannot see the settings, exactly like
    /// DefaultJobDuration above.
    /// </summary>
    public static DateTime UniversalCreditStart
    {
        get { return UniversalCredit.StartDate; }
        set { UniversalCredit.StartDate = value; }
    }

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

        //the statement column layouts are not written here any more - each
        //bank account keeps its own in bankaccounts.rjt. the fields stay on
        //SettingsData so an old file still reads, and BankAccount turns what
        //it finds there into the first account

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
        sd.EnableWorkSharing = EnableWorkSharing;
        sd.ShowUniversalCredit = ShowUniversalCredit;
        sd.UniversalCreditStart = UniversalCreditStart;

        sd.SymbolDone = PaperView.PaperItem.StringDone;
        sd.SymbolPaid = PaperView.PaperItem.StringPaid;
        sd.SymbolDonePaid = PaperView.PaperItem.StringDonePaid;

        //written only when something in it would actually differ, so opening
        //the settings page and coming back out does not read as the round
        //having been changed
        if (YearlyStore.WriteIfChanged(fileLocation, YearlyStore.Serialise(sd)))
            DataStamp.Touch(DataStamp.Settings, dir);

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

                //the columns out of a settings file from before each bank
                //account kept its own layout. stashed for BankAccount.Load,
                //which turns them into the first account - 0,0,0 means the
                //file predates statement imports and there is nothing to keep
                BankAccount.LegacyDate = sd.Date;
                BankAccount.LegacyRef = sd.Ref;
                BankAccount.LegacyAmount = sd.Amount;
                BankAccount.LegacyDebitAndCreditTogether = sd.DebitAndCreditTogether;

                BankAccount.LegacyPdfDate = sd.PdfDate;
                BankAccount.LegacyPdfRef = sd.PdfRef;
                BankAccount.LegacyPdfAmount = sd.PdfAmount;
                BankAccount.LegacyPdfDebitAndCreditTogether = sd.PdfDebitAndCreditTogether;

                //0 is what settings saved before the money out column existed
                //read back as, and means it was never chosen
                BankAccount.LegacyDebit = sd.DebitPlusOne - 1;
                BankAccount.LegacyPdfDebit = sd.PdfDebitPlusOne - 1;

                //0 from an older settings file, which the property turns back
                //into the default
                ReceiptPhoto.MaxSize = sd.ReceiptPhotoMaxSize;
                ReceiptPhoto.Quality = sd.ReceiptPhotoQuality;

                DefaultFrequence = sd.DefaultFrequence;
                DefaultFrequenceType = sd.DefalutFrequenceType;

                DefaultJobDuration = sd.DefaultJobDuration;
                HaveShowenJobIntro = sd.HaveShowenJobIntro;
                EnableWorkSharing = sd.EnableWorkSharing;

                //false and MinValue are what a settings file written before
                //the page existed reads back as, and both are the right
                //answer: the page is off and no claim has been dated
                ShowUniversalCredit = sd.ShowUniversalCredit;
                UniversalCreditStart = sd.UniversalCreditStart;

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
        RefreshGoCardlessSection();
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
        ShowWhenTheDataChanged();

        ShowJobNames();
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

        //set straight rather than through the handler, so showing the
        //setting that is already on does not count as a change
        _loadingWorkSharing = true;
        sw_workSharing.IsToggled = Settings.EnableWorkSharing;
        _loadingWorkSharing = false;

        _loadingUniversalCredit = true;
        sw_universalCredit.IsToggled = Settings.ShowUniversalCredit;
        _loadingUniversalCredit = false;

        ShowDayViewSection();

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

    /// <summary>
    /// How the calendar and the booked work page list a day - the cards, or
    /// the paper round book's rows.
    ///
    /// It is a preference rather than a setting in the data files, like the
    /// paper view's own view options, so it is kept the moment it is chosen
    /// and there is nothing here for Settings.Save to write. The pages bind
    /// to it, so a day already on screen changes with it.
    /// </summary>
    private void ShowDayViewSection()
    {
        if (p_dayView.Items.Count == 0)
            foreach (string name in DayListView.ChoiceNames)
                p_dayView.Items.Add(name);

        //set straight rather than through the handler, so showing the view
        //that is already in use does not count as a change
        _loadingDayView = true;
        p_dayView.SelectedIndex = DayListView.Choice;
        _loadingDayView = false;
    }

    private bool _loadingDayView = false;

    private void p_dayView_Changed(object sender, EventArgs e)
    {
        if (_loadingDayView)
            return;

        if (p_dayView.SelectedIndex < 0)
            return;

        DayListView.Choice = p_dayView.SelectedIndex;
    }

    private bool _loadingPhotoQuality = false;

    private bool _loadingWorkSharing = false;

    /// <summary>
    /// the sending buttons come and go with this - the toolbars are rebuilt
    /// every time they are shown, so nothing else needs telling
    /// </summary>
    private void sw_workSharing_Toggled(object sender, ToggledEventArgs e)
    {
        if (_loadingWorkSharing)
            return;

        Settings.EnableWorkSharing = e.Value;
        Settings.Save();
    }

    private bool _loadingUniversalCredit = false;

    /// <summary>
    /// the Universal Credit page comes and goes with this. The shell has to
    /// be told outright - a page already built into the tab is only hidden
    /// or shown, and nothing else would go looking
    /// </summary>
    private void sw_universalCredit_Toggled(object sender, ToggledEventArgs e)
    {
        if (_loadingUniversalCredit)
            return;

        Settings.ShowUniversalCredit = e.Value;
        Settings.Save();

        WorkTracker.AppShell.RefreshUniversalCreditTab();
    }

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

    /// <summary>
    /// when the round was last changed. It is worth having in front of
    /// somebody about to press Create Backup: the date that goes into the
    /// backup is this one, not the day the backup is taken
    /// </summary>
    private void ShowWhenTheDataChanged()
    {
        if (!DataStamp.Known)
        {
            l_lastChanged.Text = "Nothing has been recorded on this device yet.";
            return;
        }

        DateTime when = DataStamp.LastModified;

        l_lastChanged.Text = $"Your data was last changed {when:d MMM yyyy} at {when:HH:mm}"
            + $" ({DataStamp.LastChanged}). A backup carries that date, not the day it is made.";
    }

    /// <summary>
    /// Goes over the round looking for the work that is quietly not set up
    /// properly, and says what it found.
    ///
    /// The finding is all <see cref="DataCheck"/>'s, in the kernel with the
    /// work - this only asks and puts the answer up. Nothing is changed:
    /// what a missing price or a missing phone number ought to be is not
    /// something the app can know, and filling one in with a guess would be
    /// worse than the gap.
    ///
    /// The figures come with a way through to the houses behind them, for
    /// the same reason the stats page's rounds do: "eleven houses have no
    /// price" is no use on its own, and nobody is going to find eleven
    /// houses out of a round by scrolling.
    /// </summary>
    private async void bnt_verifyData_Clicked(object sender, EventArgs e)
    {
        List<DataProblem> problems = DataCheck.Run();

        if (problems.Count == 0)
        {
            await DisplayAlert("Verify Data",
                "Nothing to put right. Every house on the round has a price and a time, and everybody set to be texted or emailed has a number or an address to reach them on.",
                "OK");
            return;
        }

        string houses = problems.Count == 1 ? "1 house needs" : $"{problems.Count} houses need";

        //the houses are counted here and the lines under them a problem at a
        //time, so a house with three things wrong with it is on three of
        //them. The lines can add up to more than the houses, and that is not
        //a miscount
        bool see = await DisplayAlert("Verify Data",
            $"{houses} putting right:\n\n{DataCheck.Summarise(problems)}",
            "See The Jobs", "Close");

        if (!see)
            return;

        await Navigation.PushAsync(new DataIssues());
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

    //the Banking section is only doors - the pages behind them do the work
    private void bnt_bankAccounts_Clicked(object sender, EventArgs e)
    {
        Navigation.PushAsync(new BankAccounts());
    }

    private void bnt_keptStatements_Clicked(object sender, EventArgs e)
    {
        Navigation.PushAsync(new KeptStatements());
    }

    private void bnt_expenseRules_Clicked(object sender, EventArgs e)
    {
        Navigation.PushAsync(new ExpenseRules());
    }

    /// <summary>
    /// the job types to pick from. the type is on the job, so taking one off
    /// here only stops it being offered
    /// </summary>
    private void ShowJobNames()
    {
        List<JobNamesSettingData> jnsd = new List<JobNamesSettingData>();

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

        l_jobNames.ItemsSource = null;
        l_jobNames.ItemsSource = jnsd;
    }

    private void bnt_addJobType(object sender, EventArgs e)
    {
        //one blank row at a time: a second would have nothing to tell it
        //apart from the first
        if (Job.JobNames.Contains(string.Empty))
            return;

        Job.JobNames.Add(string.Empty);
        ShowJobNames();
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

    //  ---------------------------------  taking one off one of these lists
    //
    //  All three lists are only what is offered when something is picked -
    //  the type, the tag and the round itself all live on the job. So taking
    //  one off here cannot undo any work, but it can leave work labelled with
    //  something that is not on any list, and nothing can put that right
    //  afterwards because there is no way left to pick it. That is why one
    //  that is in use is not deleted, and why saying so has to say where it
    //  is being used rather than only refusing.

    /// <summary>the entry a delete button belongs to, or -1</summary>
    private static int IndexOf(object sender, List<string> list)
    {
        Button button = sender as Button;
        if (button == null || list == null)
            return -1;

        int index;
        if (!int.TryParse(button.ClassId, out index))
            return -1;

        return index >= 0 && index < list.Count ? index : -1;
    }

    /// <summary>
    /// asks, then takes it off the list and writes the settings.
    /// </summary>
    /// <param name="what">"job type", "tag" or "round", for the wording</param>
    /// <param name="used">how many jobs are carrying it</param>
    /// <param name="carrying">how that reads: "jobs are this type" and so on</param>
    private async Task<bool> DeleteFromList(List<string> list, int index, string what, int used, string carrying)
    {
        if (index < 0)
            return false;

        string name = list[index];

        //a blank row was never anything, so it goes without a word
        if (string.IsNullOrWhiteSpace(name))
        {
            list.RemoveAt(index);
            Settings.Save();
            return true;
        }

        if (used > 0)
        {
            await DisplayAlert($"{name} is in use",
                $"{used} {carrying}.\n\nTaking it off this list would leave them labelled with something that cannot be picked again. "
                + $"Change them to something else first, and then this {what} can go.",
                "Ok");
            return false;
        }

        if (!await DisplayAlert($"Delete {name}?",
                $"It is not on any work, so nothing changes except that it stops being offered as a {what}.",
                "Delete", "Keep"))
            return false;

        list.RemoveAt(index);
        Settings.Save();
        return true;
    }

    private async void bnt_deleteJobType(object sender, EventArgs e)
    {
        int index = IndexOf(sender, Job.JobNames);
        if (index < 0)
            return;

        //something has to be the job type: Job.DefaultJobName is the first of
        //them, and work read off the file with no type is given it
        if (Job.JobNames.Count == 1 && !string.IsNullOrWhiteSpace(Job.JobNames[0]))
        {
            await DisplayAlert("The last job type",
                "There has to be at least one job type - it is what work with nothing else set on it is called. Add another one first.",
                "Ok");
            return;
        }

        string name = Job.JobNames[index];
        int used = Job.UsingJobType(name);

        if (await DeleteFromList(Job.JobNames, index, "job type", used,
                used == 1 ? "job is this type" : "jobs are this type"))
            ShowJobNames();
    }

    private async void bnt_deleteTag(object sender, EventArgs e)
    {
        int index = IndexOf(sender, Job.TagNames);
        if (index < 0)
            return;

        string name = Job.TagNames[index];

        //the tag bar puts this tag on everything marked done, so it is in use
        //whether or not any visit has it yet
        if (!string.IsNullOrWhiteSpace(name) && Job.AutoTags.Exists(
                x => string.Equals(x, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            await DisplayAlert($"{name} is in use",
                "It is set on the tag bar, so it is going on to everything marked done. Take it off the bar first, and then it can go from this list.",
                "Ok");
            return;
        }

        int used = Job.UsingTag(name);

        if (await DeleteFromList(Job.TagNames, index, "tag", used,
                used == 1 ? "visit is tagged with it" : "visits are tagged with it"))
            ShowTagNames();
    }

    private async void bnt_deleteRound(object sender, EventArgs e)
    {
        int index = IndexOf(sender, Job.RoundNames);
        if (index < 0)
            return;

        string name = Job.RoundNames[index];
        int used = Job.UsingRound(name);

        if (await DeleteFromList(Job.RoundNames, index, "round", used,
                used == 1 ? "job is on it" : "jobs are on it"))
            ShowRoundNames();
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
            $"Backed up {result.FormattedYears}, with {result.Receipts} receipt photo(s) and {result.Statements} bank statement(s)."
            + $"\n\nIt holds the round as it stood when it was last changed - {result.WhenTheDataChanged}.",
            "Ok");

        //the backup is written to the cache, which nothing else can see, so it
        //has to be sent somewhere before it counts as backed up at all
        if (!DeviceFileSaver.CanSave)
        {
            await Share.RequestAsync(new ShareFileRequest(shareTitle, new ShareFile(result.Path)));
            return;
        }

        string choice = await DisplayActionSheet(shareTitle, "Cancel", null, "Save To This Device", "Share");

        if (choice == "Share")
        {
            await Share.RequestAsync(new ShareFileRequest(shareTitle, new ShareFile(result.Path)));
            return;
        }

        if (choice != "Save To This Device")
            return;

        try
        {
            //saved as its own kind of file rather than left for the phone to
            //guess at, so tapping it in the downloads list offers Work Tracker
            //back again - which is the whole point of it being openable
            string saved = await DeviceFileSaver.SaveAsync(result.Path,
                System.IO.Path.GetFileName(result.Path), BackupMimeType);

            await DisplayAlert("Saved", $"Saved to {saved}.\n\nOpening it from there puts it back.", "Ok");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Failed", ex.Message, "Ok");
        }
    }

    /// <summary>
    /// what a .rbf is saved as. there is no type of its own for one, and this
    /// is what the phone calls a file it has no type for - the same thing the
    /// intent filter listens for, so a backup saved here can be opened from
    /// the downloads list
    /// </summary>
    private const string BackupMimeType = "application/octet-stream";

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

    /// <summary>
    /// The round spreadsheet - one layout, read by column position.
    ///
    /// This and the Squeegee import are two buttons rather than one that
    /// works out which file it was handed. Somebody moving off Squeegee
    /// goes looking for the word Squeegee, and the two are not equally
    /// proven: reading another app's export is marked Experimental and the
    /// spreadsheet import, which has been in use, must not be dragged under
    /// that marking with it.
    /// </summary>
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

        await ImportRoundSheet(fr);
    }

    /// <summary>the csv Squeegee downloads under Reporting</summary>
    private async void bnt_importSqueegee_Clicked(object sender, EventArgs e)
    {
        FileResult fr = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a Squeegee export (.csv)",
        });
        if (fr == null)
            return;

        if (!fr.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Unsupported File",
                "This is not a csv file. In Squeegee, go to Reporting and download the report as a csv.", "ok");
            return;
        }

        await ImportFromSqueegee(fr);
    }

    private async Task ImportRoundSheet(FileResult fr)
    {
        //a sheet says where the houses are and nothing above the street, so
        //the town, the round, the money and the first due date are all asked
        //once for the whole file - on one page rather than as a run of
        //alerts, so an answer can still be changed before Import is pressed
        ImportExport.ImportOptions options = await ImportSheet.AskAsync(
            Navigation, fr.FileName, GuessCityFromFileName(fr.FileName));
        if (options == null)
            return;

        try
        {
            int knownRounds = Job.RoundNames.Count;

            ImportExport.ImportResult result;
            using (Stream stream = await fr.OpenReadAsync())
                result = ImportExport.CustomerImporter.Import(stream, options);

            await FinishImport(knownRounds, result, options, null);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Failed", $"Could not import this file: {ex.Message}", "ok");
        }
    }

    /// <summary>
    /// Takes a round on out of a Squeegee export.
    ///
    /// **What the file was understood to say is put up before anything is
    /// imported.** An export is not a round: it is a list of invoices or of
    /// jobs, several rows to the house, with voided invoices and rows that
    /// are not work at all in among them. So "2,143 rows, 306 houses" is the
    /// only thing that tells somebody whether the file was read or mangled,
    /// and which heading was taken for what is the only thing that says why.
    /// The same rule Layouts/PriceRise follows: say what is about to be done
    /// before doing it.
    /// </summary>
    private async Task ImportFromSqueegee(FileResult fr)
    {
        ImportExport.SqueegeeImport read;
        try
        {
            //a round's worth of csv is not something to read on the ui thread
            string path = fr.FullPath;
            read = await Task.Run(() => ImportExport.SqueegeeCsvParser.Parse(CSV.Import(path)));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Failed", $"Could not read this file: {ex.Message}", "ok");
            return;
        }

        if (!read.HasAddress)
        {
            await DisplayAlert("Nothing To Import",
                "There is no address column in this file, so there is no telling which house any row is about."
                + "\n\nIn Squeegee the exports with the work on them are under Reporting."
                + "\n\nThe headings in this file were:\n" + ColumnList(read.ColumnsIgnored), "ok");
            return;
        }

        if (read.Rows.Count == 0)
        {
            await DisplayAlert("Nothing To Import",
                $"{read.RowsRead} row(s) were read and not one of them left a house to import.", "ok");
            return;
        }

        string what = $"{read.Rows.Count} house(s), read off {read.RowsRead} row(s).";
        if (read.DuplicatesFolded > 0)
            what += $"\n{read.DuplicatesFolded} row(s) were another go at a house already read - the newest of each was kept.";
        if (read.VoidsSkipped > 0)
            what += $"\n{read.VoidsSkipped} voided row(s) left out.";
        if (read.NoAddress > 0)
            what += $"\n{read.NoAddress} row(s) had nothing that reads as an address.";
        if (read.NoPrice > 0)
            what += $"\n{read.NoPrice} house(s) had no readable price - they come in at 0 with a note.";
        if (read.OneOffs > 0)
            what += $"\n{read.OneOffs} house(s) said nothing about coming round again - those come in as one offs.";

        what += "\n\nColumns understood:\n" + ColumnList(read.ColumnsUsed);
        if (read.ColumnsIgnored.Count > 0)
            what += "\n\nColumns not used:\n" + ColumnList(read.ColumnsIgnored);

        bool goOn = await DisplayAlert("Does This Look Right?", what, "Continue", "Cancel");
        if (!goOn)
            return;

        //the file says the town and the round itself, so those answers are
        //only used to fill in what it left out
        ImportExport.ImportOptions options = await ImportSheet.AskAsync(
            Navigation, fr.FileName, string.Empty, addressInFile: true);
        if (options == null)
            return;

        try
        {
            int knownRounds = Job.RoundNames.Count;
            ImportExport.ImportResult result = ImportExport.CustomerImporter.Import(read.Rows, options);
            await FinishImport(knownRounds, result, options, $"Read {read.RowsRead} row(s) as {read.Rows.Count} house(s).");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Failed", $"Could not import this file: {ex.Message}", "ok");
        }
    }

    /// <summary>a column list, one to a line, for an alert</summary>
    private static string ColumnList(List<string> lines)
        => lines.Count == 0 ? "(none)" : string.Join("\n", lines.Select(l => "  " + l));

    /// <summary>
    /// What is said after an import, and the two things that have to happen
    /// whichever kind of file it came out of. It is one method because the
    /// wording of an import is the same question however the file was read,
    /// and two copies of it would end up answering differently.
    /// </summary>
    private async Task FinishImport(int knownRounds, ImportExport.ImportResult result,
        ImportExport.ImportOptions options, string preamble)
    {
        //a round typed in rather than picked is new, and the list of
        //rounds lives with the settings
        if (Job.RoundNames.Count != knownRounds)
            Settings.Save();

        DataRefreshNotifier.NotifyDataChanged();

        string summary = string.IsNullOrEmpty(preamble) ? string.Empty : preamble + "\n\n";
        summary += $"Customers created: {result.Created}\nCustomers updated: {result.Updated}";
        if (result.RoundSet > 0)
        {
            summary += result.RoundsFromFile > 0
                ? $"\n{result.RoundSet} job(s) put on a round, and every visit of them ({result.RoundsFromFile} on the round the file named)"
                : $"\n{result.RoundSet} job(s) put on {options.Round}, and every visit of them";
        }
        if (result.DueDatesSet > 0)
            summary += $"\n{result.DueDatesSet} job(s) due {options.DueDate.Value:d MMM yyyy}";
        if (result.DueDatesLeftBooked > 0)
            summary += $"\n{result.DueDatesLeftBooked} job(s) left on the day they are booked in for";
        if (result.OneOffs > 0)
            summary += $"\n{result.OneOffs} job(s) came in as one offs - the file gave no repeat for them";
        if (result.BalancesCleared > 0)
            summary += $"\n{result.BalancesCleared} balance(s) cleared - each one is in that customer's history";
        if (result.EmailsFound > 0)
            summary += $"\n{result.EmailsFound} email address(es) brought in";
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
                //the bank accounts deliberately stay: like the settings,
                //they are how the app is set up rather than what it has
                //recorded, and everything ever imported was tracked against
                //their ids
                Job.DeleteData();
                Customer.DeleteData();
                Payment.DeleteData();
                Expense.DeleteData();
                ExpenseRule.DeleteData();
                StatementRecord.DeleteData();
                GoCardlessRequest.DeleteData();
                BalanceAdjustment.DeleteData();
                DayNote.DeleteData();

                Job.Save();
                Customer.Save();
                Payment.Save();
                Expense.Save();
                ExpenseRule.Save();
                StatementRecord.Save();
                GoCardlessRequest.Save();
                BalanceAdjustment.Save();
                DayNote.Save();
                DataRefreshNotifier.NotifyDataChanged();
                await DisplayAlert("Complete", "All data erased", "Ok");
            }
    }

    private void bnt_messagesHelp_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Help", "Here you can edit the default messages for different situations.\nTags can also be used <date> and <owing> are currently in development.", "Ok");
    }
}