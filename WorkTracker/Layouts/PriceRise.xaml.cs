namespace UiInterface.Layouts;

using System.Globalization;
using Kernel;

/// <summary>
/// Putting the prices up.
///
/// A round is repriced a patch at a time - a percentage across the lot, or a
/// pound on everything under a tenner - and it is agreed with the customer
/// before it happens, so the thing that matters is the *day it starts*, not
/// the day somebody got round to typing it in. That is what this page asks
/// for alongside the figure, and it is what the customer's page shows them
/// afterwards.
///
/// What the rise actually does to the work is <see cref="Job.SetPriceRise"/>'s
/// - in the kernel with the rest of the money, so a visit generated months
/// from now still comes out at the agreed price with nobody having to
/// remember.
/// </summary>
public partial class PriceRise : ContentPage
{
    private readonly List<Job> _jobs;
    private readonly TaskCompletionSource<int> _answer = new TaskCompletionSource<int>();
    private bool _answered = false;

    private const string ByAmount = "Put prices up by an amount";
    private const string ByPercent = "Put prices up by a percentage";
    private const string ToPrice = "Set one new price";

    /// <summary>how many houses the preview lists before it stops naming them</summary>
    private const int PreviewLimit = 12;

    private PriceRise(List<Job> jobs)
    {
        InitializeComponent();

        _jobs = jobs;

        l_count.Text = _jobs.Count == 1
            ? "1 job"
            : $"{_jobs.Count} jobs";

        p_how.ItemsSource = _jobs.Count == 1
            ? new List<string>() { ByAmount, ByPercent, ToPrice }
            //setting one price across a whole street would say every house
            //costs the same, which is not what a round looks like
            : new List<string>() { ByAmount, ByPercent };
        p_how.SelectedIndex = 0;

        dp_from.Date = UsfulFuctions.DateNow.Date;

        ShowWhatItComesTo();
    }

    /// <summary>
    /// puts the page up, and puts the prices up if that is what comes back.
    /// returns how many jobs were changed, or 0 when nothing was
    /// </summary>
    public static async Task<int> AskAsync(INavigation navigation, List<Job> jobs)
    {
        List<Job> oneEach = OneVisitEach(jobs);
        if (oneEach.Count == 0)
            return 0;

        PriceRise page = new PriceRise(oneEach);
        await navigation.PushModalAsync(page);
        return await page._answer.Task;
    }

    /// <summary>
    /// One visit per house.
    ///
    /// _Jobs holds every visit of a house, so a list picked off the work list
    /// can easily hold two of the same job - and a rise is the job's, not the
    /// visit's. Putting it on twice would raise the same house's price twice
    /// over, or say it was going up from a figure it had already gone up from.
    /// </summary>
    private static List<Job> OneVisitEach(List<Job> jobs)
    {
        List<Job> oneEach = new List<Job>();
        HashSet<string> seen = new HashSet<string>();

        foreach (Job j in jobs ?? new List<Job>())
        {
            if (j == null || j.CustomerId == -1)
                continue;

            if (seen.Add(j.SameJobKey))
                oneEach.Add(j);
        }

        return oneEach;
    }

    private string How
    {
        get { return p_how.SelectedItem as string ?? ByAmount; }
    }

    /// <summary>
    /// what the figure typed in means. an amount and a set price are money;
    /// a percentage is not, and rounding only has anything to say about one
    /// that has been worked out
    /// </summary>
    private void p_how_SelectedIndexChanged(object sender, EventArgs e)
    {
        e_amount.Placeholder = How == ByPercent
            ? "Percentage, e.g. 10"
            : $"Amount in {Gloable.CurrenceSymbol}, e.g. 1.00";

        g_rounding.IsVisible = How == ByPercent;
        ShowWhatItComesTo();
    }

    private void e_amount_TextChanged(object sender, TextChangedEventArgs e)
    {
        ShowWhatItComesTo();
    }

    private void sw_round50_Toggled(object sender, ToggledEventArgs e)
    {
        ShowWhatItComesTo();
    }

    private void dp_from_DateSelected(object sender, DateChangedEventArgs e)
    {
        ShowWhatItComesTo();
    }

    /// <summary>
    /// the new price for one job, or null when the figure typed in says
    /// nothing useful
    /// </summary>
    private float? NewPriceFor(Job job)
    {
        if (!TryReadFigure(out float figure) || figure <= 0)
            return null;

        float now = job.CurrentPrice;

        if (How == ToPrice)
            return Money(figure);

        if (How == ByPercent)
        {
            float raised = now * (1 + figure / 100f);
            return sw_round50.IsToggled ? ToNearest50p(raised) : Money(raised);
        }

        return Money(now + figure);
    }

    /// <summary>
    /// what is being asked for, house by house, before it is done.
    ///
    /// A price rise across a round is not something to find out about
    /// afterwards, so the figures are on screen next to the button that does
    /// it. Only the first few houses are named - a round is hundreds of them,
    /// and a wall of prices says less than a total does.
    /// </summary>
    private void ShowWhatItComesTo()
    {
        vsl_preview.Clear();

        if (!TryReadFigure(out float figure) || figure <= 0)
        {
            l_total.Text = "Type how much the prices go up by.";
            bnt_apply.IsEnabled = false;
            return;
        }

        int changing = 0;
        float before = 0, after = 0;
        int shown = 0;

        foreach (Job job in _jobs)
        {
            float now = job.CurrentPrice;
            float? next = NewPriceFor(job);
            if (next == null || next.Value == now)
                continue;

            changing++;
            before += now;
            after += next.Value;

            if (shown < PreviewLimit)
            {
                shown++;
                vsl_preview.Add(new Label()
                {
                    Text = $"{Where(job)}  {Gloable.CurrenceSymbol}{now:0.00} to {Gloable.CurrenceSymbol}{next.Value:0.00}",
                    FontSize = 12,
                });
            }
        }

        if (changing > shown)
            vsl_preview.Add(new Label()
            {
                Text = $"and {changing - shown} more",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic,
            });

        bnt_apply.IsEnabled = changing > 0;

        if (changing == 0)
        {
            l_total.Text = "Nothing changes - every one of these is already at that price.";
            return;
        }

        l_total.Text = $"{changing} job(s) go from {Gloable.CurrenceSymbol}{before:0.00} "
            + $"to {Gloable.CurrenceSymbol}{after:0.00} a time round, "
            + $"from {dp_from.Date:d MMM yyyy}.";
    }

    /// <summary>
    /// which house a preview line is about. the display street, not the real
    /// one, so screenshot mode masks this page like every other
    /// </summary>
    private static string Where(Job job)
    {
        if (job?.Address == null)
            return "This job";

        return $"{job.Address.PropertyNameNumber} {job.Address.DisplayStreet}".Trim();
    }

    private bool TryReadFigure(out float figure)
    {
        figure = 0;
        string typed = (e_amount.Text ?? string.Empty).Trim();
        if (typed.Length == 0)
            return false;

        //read the way it was typed on this device, or pasted from something
        //set to another country
        return float.TryParse(typed, NumberStyles.Float, CultureInfo.CurrentCulture, out figure)
            || float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out figure);
    }

    private static float Money(float value)
    {
        return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static float ToNearest50p(float value)
    {
        return (float)(Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2);
    }

    private async void bnt_apply_Clicked(object sender, EventArgs e)
    {
        DateTime from = dp_from.Date.Date;

        int jobsChanged = 0, visitsRepriced = 0;

        foreach (Job job in _jobs)
        {
            float? next = NewPriceFor(job);
            if (next == null || next.Value == job.CurrentPrice)
                continue;

            visitsRepriced += job.SetPriceRise(next.Value, from);
            jobsChanged++;
        }

        if (jobsChanged == 0)
        {
            await DisplayAlert("Price Increase", "Nothing changed.", "Ok");
            return;
        }

        Job.Save();
        DataRefreshNotifier.NotifyDataChanged();

        //a rise that starts today has already reached the work on the round;
        //one dated ahead has not, and saying so is the difference between
        //"it is done" and "it is agreed"
        string reached = visitsRepriced == 0
            ? "None of the work on the round has reached that day yet - it goes up as they come round."
            : $"{visitsRepriced} visit(s) already on the round are at the new price.";

        await DisplayAlert("Price Increase",
            $"{jobsChanged} job(s) going up on {from:d MMM yyyy}.\n\n{reached}", "Ok");

        await Finish(jobsChanged);
    }

    private async void bnt_cancel_Clicked(object sender, EventArgs e)
    {
        await Finish(0);
    }

    private async Task Finish(int jobsChanged)
    {
        if (_answered)
            return;
        _answered = true;

        await Navigation.PopModalAsync();
        _answer.TrySetResult(jobsChanged);
    }

    /// <summary>a hardware back press is the same as cancelling</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_answered)
        {
            _answered = true;
            Navigation.PopModalAsync();
            _answer.TrySetResult(0);
        }
        return true;
    }
}
