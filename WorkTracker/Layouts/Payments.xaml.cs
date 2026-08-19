namespace UiInterface.Layouts;
using Kernel;
using UiInterface.ImportExport;

public partial class Payments : ContentPage
{
	public Payments()
	{
	
		InitializeComponent();

        BuildMethodChips();
        BuildQuickDates();
        ResetFilters();

        //the panel is the list's header in the xaml and it starts closed, so
        //it comes straight back off again - otherwise the list opens with an
        //empty panel's worth of space above the first payment
        ShowFilterPanel(false);

        NavigatedTo += RefreshPage;
	}

    /// <summary>
    /// how far back the page opens on. A round takes money every week of the
    /// year, so the whole list is thousands of lines nobody scrolls: what is
    /// actually being asked when this page is opened is "has so and so paid
    /// me", and that is this fortnight's money
    /// </summary>
    private const int OpeningDays = 14;

    /// <summary>whether the date range is being applied at all</summary>
    private bool _filterDates = true;

    private DateTime _from, _to;

    /// <summary>
    /// the methods being shown. every one of them to start with, and the list
    /// is only narrowed when some are off - see <see cref="EveryMethod"/>
    /// </summary>
    private readonly HashSet<PaymentMethod> _methods = new HashSet<PaymentMethod>();

    /// <summary>the chips, so a tap can put its own one right again</summary>
    private readonly Dictionary<PaymentMethod, Border> _methodChips = new Dictionary<PaymentMethod, Border>();

    /// <summary>what a chip that is switched off is filled with. grey rather
    /// than empty, because a chip with no fill loses the white icon on it</summary>
    private static readonly Color ChipOff = Color.FromArgb("#969696");

    private void RefreshPage(object sender, NavigatedToEventArgs e)
    {
        RefreshPage();
    }

    private async void RefreshPage()
    {
        BuildList();

        //direct debits that cleared since last time show up as payments
        if (GoCardless.IsConnected && GoCardlessRequest.QueryPending().Count > 0)
        {
            await GoCardless.RefreshPendingAsync();
            BuildList();
        }
    }

    /// <summary>
    /// the list as the filters leave it, newest first.
    ///
    /// Newest first is what a date range is for: the page is opened to see
    /// what has just come in, and the payments file is in the order the money
    /// was recorded, which put this fortnight at the bottom of the year.
    /// </summary>
    private void BuildList()
    {
        List<Payment> everything = Payment.Query();

        //a copy, because Query hands back the master list itself and this
        //sorts what it is given - the order payments are held in is the order
        //they were recorded, and it is not ours to rearrange
        List<Payment> shown = new List<Payment>(everything);

        if (_filterDates)
            shown = shown.FindAll(x => x.Date.Date >= _from.Date && x.Date.Date <= _to.Date);

        //asking for none of the methods is asking for nothing, and it says so
        //on the bar rather than looking like an empty round
        if (!EveryMethod)
            shown = shown.FindAll(x => _methods.Contains(x.PaymentMethod));

        shown.Sort((a, b) => b.Date.CompareTo(a.Date));

        lv_Payments.ItemsSource = null;
        lv_Payments.ItemsSource = shown;

        ShowWhatIsOn(shown, everything.Count);
    }

    /// <summary>nothing is being left out by method</summary>
    private bool EveryMethod
    {
        get { return _methods.Count == Enum.GetValues(typeof(PaymentMethod)).Length; }
    }

    /// <summary>the page as it opens: the last fortnight, every method</summary>
    private void ResetFilters()
    {
        _filterDates = true;
        _to = UsfulFuctions.DateNow;
        _from = _to.AddDays(-OpeningDays);

        _methods.Clear();
        foreach (PaymentMethod m in Enum.GetValues(typeof(PaymentMethod)))
            _methods.Add(m);

        FillInThePanel();
    }

    /// <summary>
    /// the panel filled in from what is actually being filtered by, so it can
    /// never say one thing while the list is doing another.
    ///
    /// The box and the two pickers raise their changed events when they are
    /// set here as well as when somebody sets them, so the handlers are told
    /// to sit still while this runs - otherwise putting the panel right would
    /// read as an answer and build the list again under itself
    /// </summary>
    private void FillInThePanel()
    {
        _fillingIn = true;

        try
        {
            cb_filterDates.IsChecked = _filterDates;
            g_dateRange.IsVisible = _filterDates;
            dp_StartSearchDate.Date = _from;
            dp_EndSearchDate.Date = _to;

            foreach (KeyValuePair<PaymentMethod, Border> chip in _methodChips)
                PaintChip(chip.Key);
        }
        finally
        {
            _fillingIn = false;
        }
    }

    /// <summary>true while <see cref="FillInThePanel"/> is setting the
    /// controls, so what they raise on the way is ignored</summary>
    private bool _fillingIn;

    /// <summary>
    /// The bar above the list, which says what is being shown and what is
    /// being left out. It is never empty on this page: the fortnight the page
    /// opens on is itself a filter, and a list quietly showing a fortnight of
    /// a year's money with nothing saying so is the thing this is here for.
    /// </summary>
    private void ShowWhatIsOn(List<Payment> shown, int everything)
    {
        float total = 0;
        foreach (Payment p in shown)
            total += p.Amount;

        l_filterResultsText.Text = FilterDescription();

        string money = $"{Gloable.CurrenceSymbol}{total:0.00}";
        string count = shown.Count == 1 ? "1 payment" : $"{shown.Count} payments";

        //what is not on screen is worth saying, so the total on the bar is
        //never mistaken for everything that has come in
        l_filterResultsCount.Text = shown.Count == everything
            ? $"{count}, {money}"
            : $"{count} of {everything}, {money}";

        bnt_showEverything.IsVisible = _filterDates || !EveryMethod;
    }

    /// <summary>what the list is being kept to, in the words it would be said in</summary>
    private string FilterDescription()
    {
        string dates = "Everything, however far back";

        if (_filterDates)
        {
            //the fortnight it opens on is worth naming rather than printing as
            //two dates that have to be read and worked out
            if (_to.Date == UsfulFuctions.DateNow && _from.Date == UsfulFuctions.DateNow.AddDays(-OpeningDays))
                dates = "The last fortnight";
            else
                dates = $"{_from.ToShortDateString()} to {_to.ToShortDateString()}";
        }

        if (EveryMethod)
            return dates;

        if (_methods.Count == 0)
            return $"{dates}, but no payment method is on";

        List<string> on = new List<string>(), off = new List<string>();
        foreach (PaymentMethod m in Enum.GetValues(typeof(PaymentMethod)))
            if (_methods.Contains(m))
                on.Add(Payment.NameFor(m));
            else
                off.Add(Payment.NameFor(m));

        //said whichever way round is shorter to read: one method left out of
        //seven is "without Cheque", not the other six listed out
        if (off.Count < on.Count)
            return $"{dates}, without {SpellList(off)}";

        return $"{dates}, {SpellList(on)} only";
    }

    /// <summary>a, b and c - so the bar reads as a sentence rather than a csv</summary>
    private static string SpellList(List<string> words)
    {
        if (words.Count == 0)
            return string.Empty;

        if (words.Count == 1)
            return words[0];

        string all = string.Join(", ", words.GetRange(0, words.Count - 1));
        return $"{all} and {words[words.Count - 1]}";
    }

    /// <summary>
    /// one chip per payment method, built from the enum rather than written
    /// out, so a method added to it turns up here on its own. Each carries
    /// that method's colour and icon - the same two the rows are drawn with,
    /// asked of Payment so the chip and the row cannot disagree
    /// </summary>
    private void BuildMethodChips()
    {
        foreach (PaymentMethod m in Enum.GetValues(typeof(PaymentMethod)))
        {
            PaymentMethod method = m;

            Image icon = new Image()
            {
                Source = Payment.IconFor(method),
                WidthRequest = 14,
                HeightRequest = 14,
                VerticalOptions = LayoutOptions.Center
            };

            Label name = new Label()
            {
                Text = Payment.NameFor(method),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center
            };

            HorizontalStackLayout inside = new HorizontalStackLayout() { Spacing = 5 };
            inside.Add(icon);
            inside.Add(name);

            Border chip = new Border()
            {
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 8, 8),
                Content = inside
            };

            TapGestureRecognizer tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => ToggleMethod(method);
            chip.GestureRecognizers.Add(tap);

            ToolTipProperties.SetText(chip, $"Show or hide {Payment.NameFor(method)} payments");

            _methodChips[method] = chip;
            fl_methods.Add(chip);
        }
    }

    /// <summary>lit in the method's own colour while it is on, grey while it
    /// is not - a chip with no fill at all would lose the white icon on it</summary>
    private void PaintChip(PaymentMethod method)
    {
        if (!_methodChips.TryGetValue(method, out Border chip))
            return;

        chip.BackgroundColor = _methods.Contains(method) ? Payment.ColourFor(method) : ChipOff;
        chip.Opacity = _methods.Contains(method) ? 1 : 0.7;
    }

    private void ToggleMethod(PaymentMethod method)
    {
        if (!_methods.Add(method))
            _methods.Remove(method);

        PaintChip(method);
        BuildList();
    }

    /// <summary>
    /// the ranges that actually get asked for, so the usual answer is one tap
    /// rather than two dates dragged about
    /// </summary>
    private void BuildQuickDates()
    {
        AddQuickDate("2 Weeks", () => UsfulFuctions.DateNow.AddDays(-OpeningDays));
        AddQuickDate("3 Months", () => UsfulFuctions.DateNow.AddMonths(-3));
        AddQuickDate("Tax Year", () => TaxCalendar.YearStart(TaxCalendar.TaxYearOf(UsfulFuctions.DateNow)));
    }

    private void AddQuickDate(string text, Func<DateTime> from)
    {
        Button b = new Button()
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(14, 2),
            CornerRadius = 8,
            BackgroundColor = Colors.Transparent,
            BorderWidth = 2,
            BorderColor = Color.FromArgb("#2E7D32"),
            TextColor = Color.FromArgb("#2E7D32")
        };

        b.Clicked += (s, e) =>
        {
            _filterDates = true;
            _from = from();
            _to = UsfulFuctions.DateNow;
            FillInThePanel();
            BuildList();
        };

        hsl_quickDates.Add(b);
    }

    private void ShowFilterPanel(bool show)
    {
        g_filter.IsVisible = show;
        lv_Payments.Header = show ? g_filter : null;
    }

    /// <summary>
    /// the Filters toolbar item. it opens and closes the panel rather than
    /// only opening it, so the same button puts it away again
    /// </summary>
    private void bnt_filters_Clicked(object sender, EventArgs e)
    {
        ShowFilterPanel(!g_filter.IsVisible);

        if (!g_filter.IsVisible)
            return;

        FillInThePanel();
        ScrollToTheTop();
    }

    /// <summary>
    /// Back to the top of the list, the panel included: it sits at the top of
    /// the list's own content, so opening it while the list is scrolled down
    /// would put it out of sight and the button would look like it had done
    /// nothing
    /// </summary>
    private void ScrollToTheTop()
    {
        if (lv_Payments.ItemsSource is IList<Payment> shown && shown.Count > 0)
            lv_Payments.ScrollTo(shown[0], position: ScrollToPosition.End, animate: false);
    }

    private void bnt_hideFilter_Clicked(object sender, EventArgs e)
    {
        ShowFilterPanel(false);
    }

    private void cb_filterDates_Changed(object sender, CheckedChangedEventArgs e)
    {
        if (_fillingIn)
            return;

        _filterDates = cb_filterDates.IsChecked;
        g_dateRange.IsVisible = _filterDates;
        BuildList();
    }

    private void dp_start_Changed(object sender, DateChangedEventArgs e)
    {
        if (_fillingIn)
            return;

        _from = dp_StartSearchDate.Date;
        BuildList();
    }

    private void dp_end_Changed(object sender, DateChangedEventArgs e)
    {
        if (_fillingIn)
            return;

        _to = dp_EndSearchDate.Date;
        BuildList();
    }

    private void bnt_allMethods_Clicked(object sender, EventArgs e)
    {
        foreach (PaymentMethod m in Enum.GetValues(typeof(PaymentMethod)))
            _methods.Add(m);

        FillInThePanel();
        BuildList();
    }

    private void bnt_noMethods_Clicked(object sender, EventArgs e)
    {
        _methods.Clear();
        FillInThePanel();
        BuildList();
    }

    private void bnt_resetFilter_Clicked(object sender, EventArgs e)
    {
        ResetFilters();
        BuildList();
    }

    /// <summary>
    /// the Show All on the bar: every payment there has ever been, dates off
    /// and every method on. It is the way out of a filter that has left the
    /// page looking empty
    /// </summary>
    private void bnt_showEverything_Clicked(object sender, EventArgs e)
    {
        _filterDates = false;

        foreach (PaymentMethod m in Enum.GetValues(typeof(PaymentMethod)))
            _methods.Add(m);

        FillInThePanel();
        BuildList();
    }

    private async void selectFile()
    {
        CSVFile file = await StatementFile.PickAsync(this);
        if (file == null)
            return;

        await Navigation.PushAsync(new StatmentViewer());
    }

    private void bnt_ImportBank(object sender, EventArgs e)
    {
        selectFile();

    }

    /// <summary>
    /// where a reference ignored by mistake gets put back, without having to
    /// find the statement and import it again
    /// </summary>
    private void bnt_ignoredPayments(object sender, EventArgs e)
    {
        Navigation.PushAsync(new IgnoredPayments());
    }
}
