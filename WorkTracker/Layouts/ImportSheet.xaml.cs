namespace UiInterface.Layouts;

using Kernel;
using UiInterface.ImportExport;

/// <summary>
/// What is being asked of one import of a round spreadsheet.
///
/// A sheet says where the houses are, what they cost and how often they come
/// round. Four things it cannot say have to come from somewhere, and asking
/// them one alert at a time - which is how the town and the area were asked -
/// gives no way of changing an answer already given and no room to say what
/// any of them mean. So they are one page: the town and area, which round the
/// work is on, whether anybody starts out owing anything, and when the whole
/// lot is first due.
///
/// The page decides nothing. It hands back an <see cref="ImportOptions"/> and
/// <see cref="CustomerImporter"/> does the work, so an import started from
/// anywhere else would behave the same.
/// </summary>
public partial class ImportSheet : ContentPage
{
    private readonly TaskCompletionSource<ImportOptions> _answer = new TaskCompletionSource<ImportOptions>();
    private bool _answered = false;

    /// <summary>blank is a real answer here - it is the work on no round</summary>
    private string _round = string.Empty;

    private ImportSheet(string fileName, string suggestedCity)
    {
        InitializeComponent();

        l_file.Text = string.IsNullOrWhiteSpace(fileName) ? "Round spreadsheet" : fileName;
        e_city.Text = suggestedCity ?? string.Empty;
        dp_dueDate.Date = UsfulFuctions.DateNow.Date;
        ShowRound();
    }

    /// <summary>
    /// puts the page up and waits for an answer. returns null when the user
    /// backs out
    /// </summary>
    public static async Task<ImportOptions> AskAsync(INavigation navigation, string fileName, string suggestedCity)
    {
        ImportSheet page = new ImportSheet(fileName, suggestedCity);
        await navigation.PushModalAsync(page);
        return await page._answer.Task;
    }

    /// <summary>
    /// the one place that asks which round, so the question is worded the
    /// same here as it is on the work list
    /// </summary>
    private async void bnt_round_Clicked(object sender, EventArgs e)
    {
        string picked = await RoundPicker.AskAsync(this, "Put this sheet's work on which round?");
        if (picked == null)
            return;

        _round = picked;
        ShowRound();
    }

    private void ShowRound()
    {
        bnt_round.Text = _round.Length == 0 ? "No round" : _round;
    }

    /// <summary>the date is no answer at all while the switch is off</summary>
    private void sw_setDueDate_Toggled(object sender, ToggledEventArgs e)
    {
        dp_dueDate.IsEnabled = sw_setDueDate.IsToggled;
    }

    private async void bnt_import_Clicked(object sender, EventArgs e)
    {
        ImportOptions options = new ImportOptions()
        {
            City = (e_city.Text ?? string.Empty).Trim(),
            Area = (e_area.Text ?? string.Empty).Trim(),
            Round = _round,
            ZeroBalances = sw_zeroBalances.IsToggled,
            DueDate = sw_setDueDate.IsToggled ? dp_dueDate.Date.Date : (DateTime?)null,
        };

        await Finish(options);
    }

    private async void bnt_cancel_Clicked(object sender, EventArgs e)
    {
        await Finish(null);
    }

    private async Task Finish(ImportOptions options)
    {
        if (_answered)
            return;
        _answered = true;

        await Navigation.PopModalAsync();
        _answer.TrySetResult(options);
    }

    /// <summary>a hardware back press is the same as cancelling</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_answered)
        {
            _answered = true;
            Navigation.PopModalAsync();
            _answer.TrySetResult(null);
        }
        return true;
    }
}
