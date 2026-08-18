namespace UiInterface.Controles;

using Kernel;
using Microsoft.Maui.Controls.Shapes;

/// <summary>the pieces of a card a page can be told about being tapped</summary>
public enum JobCardPart
{
    Street,
    City,
    Area,
    Price,
    Owed,
    Type,
    Round,
}

/// <summary>which of the card's pieces the event is about, and the job on it</summary>
public class JobCardEventArgs : EventArgs
{
    public Job Job { get; set; }

    public JobCardPart Part { get; set; }

    /// <summary>what the tick box now says, on SelectionToggled</summary>
    public bool Selected { get; set; }
}

/// <summary>
/// One house, drawn the one way every list draws it: a card with the address
/// in bold and the price beside it in green, the town and area quiet
/// underneath, when it is due and what is owed said in colour as text, and
/// the tags under that.
///
/// The work list, the booked work page, the calendar's day list and All Jobs
/// all put this control in their row template rather than each writing the
/// row themselves - it is the same round looked at four ways and it should
/// not read as four different apps. The paper view is deliberately not one of
/// them: that page is a printed sheet, not a list of cards.
///
/// What differs between the pages is said in options rather than in copies:
/// each Show* property turns a piece of the card on or off, and the three
/// styles say how the address, the due date and the money are worded. The
/// options are plain properties set once in the template - what varies per
/// job is handled by bindings underneath them, so a piece that is on still
/// only shows when the job has something to say.
///
/// Everything around the card stays the page's own: the swipe actions, the
/// hold, the tap on the row and the desktop context menu all belong to the
/// page and go on this element exactly as they went on the old Border. What
/// is inside the card that can be tapped - the info button, the tick box, the
/// filter taps on the work list - comes back out as events carrying the job,
/// because the page cannot reach inside a template.
/// </summary>
public class JobCard : ContentView
{
    //the one definition of what a job card looks like
    private static readonly Color CardLight = Color.FromArgb("#F2F4F7");
    private static readonly Color CardDark = Color.FromArgb("#1E1E1E");
    private static readonly Color CaptionLight = Color.FromArgb("#6B7280");
    private static readonly Color CaptionDark = Color.FromArgb("#9CA3AF");
    private static readonly Color PriceGreen = Color.FromArgb("#2E7D32");
    private static readonly Color OverdueRed = Color.FromArgb("#C62828");
    private static readonly Color QuietGrey = Color.FromArgb("#6B7280");

    public event EventHandler<JobCardEventArgs> InfoClicked;

    /// <summary>the tick box changed. the card only reports it - what being
    /// picked means belongs to the page</summary>
    public event EventHandler<JobCardEventArgs> SelectionToggled;

    /// <summary>a filter tap - the street, the price, a chip. only raised
    /// while EnableFilterTaps is on, because the recognisers would otherwise
    /// swallow taps meant for the row</summary>
    public event EventHandler<JobCardEventArgs> PartTapped;

    // ----------------------------------------------------------- the options

    public enum AddressStyles
    {
        /// <summary>house number and street, for a list that mixes streets</summary>
        Full,

        /// <summary>the number alone, for a list whose street is already the
        /// heading above the row. A house with no number falls back to whoever
        /// lives there</summary>
        NumberOnly,
    }

    public enum DueStyles
    {
        /// <summary>the worked wording - Due In 3 Days - off JobFormattedDueTime</summary>
        Relative,

        /// <summary>the date written out with how far past it is, the way All
        /// Jobs reads a round over</summary>
        LongDate,
    }

    public enum OwedStyles
    {
        /// <summary>only when there is something to settle - ShowOwed gates it</summary>
        WhenOwed,

        /// <summary>always answered: owes, in credit or nothing owed</summary>
        Always,
    }

    public enum PriceStyles
    {
        /// <summary>the worked wording - Price £12 - off JobFormattedStringPrice</summary>
        Prefixed,

        /// <summary>the figure alone, off what the job actually charges -
        /// EffectivePrice, so a house on an alternative price says so</summary>
        Effective,
    }

    private bool _showSelection = false;
    private bool _showInfo = false;
    private bool _showPlace = true;
    private bool _showDue = true;
    private bool _showOwed = true;
    private bool _showChips = true;
    private bool _showDoneChip = false;
    private bool _showBookedChip = false;
    private bool _showCancelledChip = false;
    private bool _showRoundChip = false;
    private bool _showExtraChips = true;
    private bool _showTags = true;
    private bool _showNotes = true;
    private bool _collapseCancelled = false;
    private bool _collapseCompleted = false;
    private bool _enableFilterTaps = false;
    private AddressStyles _addressStyle = AddressStyles.Full;
    private DueStyles _dueStyle = DueStyles.Relative;
    private OwedStyles _owedStyle = OwedStyles.WhenOwed;
    private PriceStyles _priceStyle = PriceStyles.Prefixed;

    /// <summary>the tick box for picking work out. it only shows while the
    /// whole list is picking - Job.SelectionModeEnabled - like everything
    /// else, an option that is on still waits for the job to agree</summary>
    public bool ShowSelection { get { return _showSelection; } set { _showSelection = value; Apply(); } }

    /// <summary>the round info button, raising InfoClicked</summary>
    public bool ShowInfo { get { return _showInfo; } set { _showInfo = value; Apply(); } }

    /// <summary>the town and the area, quiet under the street</summary>
    public bool ShowPlace { get { return _showPlace; } set { _showPlace = value; Apply(); } }

    /// <summary>when it is due. off on the booked work page, where the day
    /// heading already is the date</summary>
    public bool ShowDue { get { return _showDue; } set { _showDue = value; Apply(); } }

    public bool ShowOwed { get { return _showOwed; } set { _showOwed = value; Apply(); } }

    /// <summary>the whole chip row in one go</summary>
    public bool ShowChips { get { return _showChips; } set { _showChips = value; Apply(); } }

    /// <summary>a green Done on completed work, for a list that keeps done
    /// and not-done side by side</summary>
    public bool ShowDoneChip { get { return _showDoneChip; } set { _showDoneChip = value; Apply(); } }

    /// <summary>a Bookin chip on booked work, for the one list that mixes
    /// booked and unbooked - the calendar</summary>
    public bool ShowBookedChip { get { return _showBookedChip; } set { _showBookedChip = value; Apply(); } }

    /// <summary>a Canceled chip, for a list that shows cancelled work rather
    /// than collapsing it</summary>
    public bool ShowCancelledChip { get { return _showCancelledChip; } set { _showCancelledChip = value; Apply(); } }

    /// <summary>whose round the job is on</summary>
    public bool ShowRoundChip { get { return _showRoundChip; } set { _showRoundChip = value; Apply(); } }

    /// <summary>TNB, ENB and a waiting direct debit - the chips about telling
    /// and paying rather than about the work itself</summary>
    public bool ShowExtraChips { get { return _showExtraChips; } set { _showExtraChips = value; Apply(); } }

    /// <summary>what was different about this visit</summary>
    public bool ShowTags { get { return _showTags; } set { _showTags = value; Apply(); } }

    public bool ShowNotes { get { return _showNotes; } set { _showNotes = value; Apply(); } }

    /// <summary>cancelled work folds to a greyed struck-through line, the way
    /// the work list shows it. Use one collapse option per page, not both</summary>
    public bool CollapseCancelled { get { return _collapseCancelled; } set { _collapseCancelled = value; Apply(); } }

    /// <summary>completed work folds to a faded line that taps back open,
    /// the way the calendar shows a day already worked</summary>
    public bool CollapseCompleted { get { return _collapseCompleted; } set { _collapseCompleted = value; Apply(); } }

    /// <summary>puts tap recognisers on the street, the price, the town, the
    /// area, the owed figure and the type and round chips, raising PartTapped.
    /// Off by default because a recogniser swallows the tap - a page whose
    /// rows are tapped as a whole must leave this off</summary>
    public bool EnableFilterTaps { get { return _enableFilterTaps; } set { _enableFilterTaps = value; Apply(); } }

    public AddressStyles AddressStyle { get { return _addressStyle; } set { _addressStyle = value; Apply(); RefreshComputed(); } }

    public DueStyles DueStyle { get { return _dueStyle; } set { _dueStyle = value; Apply(); RefreshComputed(); } }

    public OwedStyles OwedStyle { get { return _owedStyle; } set { _owedStyle = value; Apply(); RefreshComputed(); } }

    public PriceStyles PriceStyle { get { return _priceStyle; } set { _priceStyle = value; Apply(); RefreshComputed(); } }

    // ------------------------------------------------------------- the job

    /// <summary>
    /// The job the card is drawing. Bound rather than inherited, so a page
    /// whose rows are wrappers - All Jobs - can hand the job over while its
    /// own row stays the template's context.
    /// </summary>
    public static readonly BindableProperty JobProperty = BindableProperty.Create(
        nameof(Job), typeof(Job), typeof(JobCard), null, propertyChanged: OnJobChanged);

    public Job Job
    {
        get { return (Job)GetValue(JobProperty); }
        set { SetValue(JobProperty, value); }
    }

    private static void OnJobChanged(BindableObject bindable, object oldValue, object newValue)
    {
        JobCard card = (JobCard)bindable;

        //the card's insides bind to the job, whatever the row itself is
        card._border.BindingContext = newValue;
        card.RefreshComputed();
    }

    /// <summary>
    /// A caption line of the page's own - All Jobs puts how often the house
    /// comes round and which round it is on here. The card cannot work that
    /// wording out itself, because a round is read off the whole job through
    /// the page's grouping.
    /// </summary>
    public static readonly BindableProperty ExtraCaptionProperty = BindableProperty.Create(
        nameof(ExtraCaption), typeof(string), typeof(JobCard), null, propertyChanged: OnExtraCaptionChanged);

    public string ExtraCaption
    {
        get { return (string)GetValue(ExtraCaptionProperty); }
        set { SetValue(ExtraCaptionProperty, value); }
    }

    private static void OnExtraCaptionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        JobCard card = (JobCard)bindable;
        card._extra.Text = newValue as string ?? string.Empty;
        card._extra.IsVisible = card._extra.Text.Length > 0;
    }

    // ------------------------------------------------------------ the pieces

    private readonly Border _border;
    private readonly HorizontalStackLayout _cancelledLine;
    private readonly Label _cancelledStreet;
    private readonly HorizontalStackLayout _collapsedLine;
    private readonly Label _collapsedStreet;
    private readonly Grid _grid;
    private readonly CheckBox _check;
    private readonly HorizontalStackLayout _addressStack;
    private readonly Label _number;
    private readonly Label _street;
    private readonly Label _where;
    private readonly Label _price;
    private readonly ImageButton _info;
    private readonly HorizontalStackLayout _placeStack;
    private readonly Label _city;
    private readonly Label _area;
    private readonly Label _due;
    private readonly Label _owed;
    private readonly Label _extra;
    private readonly HorizontalStackLayout _chips;
    private readonly Label _chipDone;
    private readonly Label _chipBooked;
    private readonly Label _chipCancelled;
    private readonly Label _chipType;
    private readonly Label _chipRound;
    private readonly Label _chipOneOff;
    private readonly Label _chipLength;
    private readonly Label _chipTNB;
    private readonly Label _chipENB;
    private readonly Label _chipPending;
    private readonly Label _tags;
    private readonly Label _notes;

    /// <summary>one recogniser per tappable piece, made once and put on or
    /// taken off as EnableFilterTaps changes</summary>
    private readonly Dictionary<Label, TapGestureRecognizer> _filterTaps = new Dictionary<Label, TapGestureRecognizer>();

    public JobCard()
    {
        // ---- the collapsed lines

        _cancelledLine = new HorizontalStackLayout() { Opacity = 0.4, Spacing = 6 };
        _cancelledLine.Children.Add(Struck("JobFormattedHouseNumber", true));
        _cancelledLine.Children.Add(_cancelledStreet = Struck("JobFormattedStreetOnly", true));
        _cancelledLine.Children.Add(Struck("JobFormattedCity", false));
        Label cancelledWord = new Label() { Text = "Cancelled", TextColor = OverdueRed, FontAttributes = FontAttributes.Bold };
        _cancelledLine.Children.Add(cancelledWord);

        _collapsedLine = new HorizontalStackLayout() { Opacity = 0.4, Spacing = 6 };
        _collapsedLine.Children.Add(Plain("JobFormattedHouseNumber", true));
        _collapsedLine.Children.Add(_collapsedStreet = Plain("JobFormattedStreetOnly", true));
        _collapsedLine.Children.Add(Plain("JobFormattedCity", false));

        //tapping the folded line opens it back up. the fold is the job's own
        //state, so the card can answer this itself
        TapGestureRecognizer unfold = new TapGestureRecognizer();
        unfold.Tapped += (s, e) => { if (Job != null) Job.CollapsedInList = false; };
        _collapsedLine.GestureRecognizers.Add(unfold);

        // ---- the card proper: fixed rows rather than a wrapping layout, so
        //      nothing depends on a measure pass that could collapse inside a
        //      virtualised list

        _grid = new Grid() { ColumnSpacing = 8, RowSpacing = 2 };
        for (int i = 0; i < 7; i++)
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); //the tick box
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); //the address side
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); //the money side
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); //the info button

        //the tick box belongs to the whole card rather than to any one line
        _check = new CheckBox() { BackgroundColor = Colors.Transparent, VerticalOptions = LayoutOptions.Center };
        _check.SetBinding(CheckBox.IsCheckedProperty, "IsSelected");
        _check.CheckedChanged += (s, e) => SelectionToggled?.Invoke(this, new JobCardEventArgs() { Job = Job, Selected = e.Value });
        Put(_check, 0, 0, rowSpan: 7);

        //the address, which is what somebody stood at a gate is actually
        //reading. the street takes whatever width is left and truncates
        //rather than pushing the price off the row
        _number = new Label() { FontAttributes = FontAttributes.Bold, FontSize = 15, VerticalOptions = LayoutOptions.Center };
        _number.SetBinding(Label.TextProperty, "JobFormattedHouseNumber");
        _street = new Label() { FontAttributes = FontAttributes.Bold, FontSize = 15, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        _street.SetBinding(Label.TextProperty, "JobFormattedStreetOnly");
        _addressStack = new HorizontalStackLayout() { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        _addressStack.Children.Add(_number);
        _addressStack.Children.Add(_street);
        Put(_addressStack, 0, 1);

        //the number-only wording, for a list whose street is the heading
        _where = new Label() { FontAttributes = FontAttributes.Bold, FontSize = 15, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        Put(_where, 0, 1);

        //what it says is Apply's to decide - the wording is a PriceStyle
        _price = new Label() { FontAttributes = FontAttributes.Bold, FontSize = 15, TextColor = PriceGreen, VerticalOptions = LayoutOptions.Center };
        Put(_price, 0, 2);

        //an icon with no words to it, so it carries a tooltip. one size
        //everywhere - it is the same button and it should not change between
        //pages
        _info = new ImageButton()
        {
            Source = "info.png",
            HeightRequest = 34,
            WidthRequest = 34,
            Padding = 5,
            CornerRadius = 17,
            BackgroundColor = Color.FromArgb("#1E88E5"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        ToolTipProperties.SetText(_info, "Everything about this job - price, notes, when it is due and who it is for");
        _info.Clicked += (s, e) => InfoClicked?.Invoke(this, new JobCardEventArgs() { Job = Job });
        Put(_info, 0, 3, rowSpan: 3);

        //the town and the area, quiet under the street
        _city = Caption();
        _city.SetBinding(Label.TextProperty, "JobFormattedCity");
        _area = Caption();
        _area.SetBinding(Label.TextProperty, "JobFormattedArea");
        _placeStack = new HorizontalStackLayout() { Spacing = 6 };
        _placeStack.Children.Add(_city);
        _placeStack.Children.Add(_area);
        Put(_placeStack, 1, 1, columnSpan: 2);

        //when it is due and what is owed, said in colour rather than sat on
        //it. the chip colours are made to be read white on colour and cannot
        //be read as text on a card - see JobDisplay.DueTextColour
        _due = new Label() { FontSize = 12, VerticalOptions = LayoutOptions.Center };
        Put(_due, 2, 1);

        _owed = new Label() { FontSize = 12, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        Put(_owed, 2, 2);

        //the page's own caption line - how often, on All Jobs
        _extra = Caption();
        _extra.IsVisible = false;
        Put(_extra, 3, 1);

        //the chips: what the work is and what is owing about it
        _chips = new HorizontalStackLayout() { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
        _chips.Children.Add(_chipDone = Chip("Done", "#4CAF50"));
        _chips.Children.Add(_chipBooked = Chip("Bookin", "OrangeRed"));
        _chips.Children.Add(_chipCancelled = Chip("Canceled", "Red"));
        _chips.Children.Add(_chipType = Chip(null, "#EF6C00"));
        _chipType.SetBinding(Label.TextProperty, "Name");
        _chipType.LineBreakMode = LineBreakMode.TailTruncation;
        _chips.Children.Add(_chipRound = Chip(null, "#5E35B1"));
        _chipRound.SetBinding(Label.TextProperty, "Round");
        _chipRound.LineBreakMode = LineBreakMode.TailTruncation;
        _chips.Children.Add(_chipOneOff = Chip("One off", "#546E7A"));
        _chips.Children.Add(_chipLength = Chip(null, "#5D4037"));
        _chipLength.SetBinding(Label.TextProperty, "LengthText");
        _chips.Children.Add(_chipTNB = Chip("TNB", "#C62828"));
        _chips.Children.Add(_chipENB = Chip("ENB", "#C62828"));
        _chips.Children.Add(_chipPending = Chip(null, "#6A1B9A"));
        _chipPending.SetBinding(Label.TextProperty, "PaymentPendingText");
        Put(_chips, 4, 1, columnSpan: 3);

        //what was different about this visit. its own line, because a day's
        //worth of tags is longer than a row
        _tags = Chip(null, "#00838F");
        _tags.SetBinding(Label.TextProperty, "TagsText");
        _tags.LineBreakMode = LineBreakMode.TailTruncation;
        _tags.MaxLines = 1;
        _tags.HorizontalOptions = LayoutOptions.Start;
        _tags.Margin = new Thickness(0, 2, 0, 0);
        Put(_tags, 5, 1, columnSpan: 3);

        //notes get a line of their own so a long one cannot push the tags off
        _notes = Chip(null, "#FB8C00");
        _notes.FontAttributes = FontAttributes.None;
        _notes.SetBinding(Label.TextProperty, "JobFormattedStringNotes");
        _notes.LineBreakMode = LineBreakMode.TailTruncation;
        _notes.MaxLines = 1;
        _notes.HorizontalOptions = LayoutOptions.Start;
        _notes.Margin = new Thickness(0, 2, 0, 0);
        Put(_notes, 6, 1, columnSpan: 3);

        // ---- the card around it all

        VerticalStackLayout stack = new VerticalStackLayout();
        stack.Children.Add(_cancelledLine);
        stack.Children.Add(_collapsedLine);
        stack.Children.Add(_grid);

        _border = new Border()
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(12) },
            Margin = new Thickness(12, 0, 12, 6),
            Padding = new Thickness(12, 10),
            Content = stack,
        };
        _border.SetAppThemeColor(Border.BackgroundColorProperty, CardLight, CardDark);

        Content = _border;
        Apply();
    }

    // --------------------------------------------------- putting options on

    /// <summary>
    /// Everything the options decide, decided again from scratch - the same
    /// rule as the work list's toolbar, so no order of setting things can
    /// leave a piece half switched. An option that is off takes the binding
    /// off too, because a binding would put the piece straight back.
    /// </summary>
    private void Apply()
    {
        Gate(_cancelledLine, _collapseCancelled, "HaveCanceled");
        Gate(_collapsedLine, _collapseCompleted, "CollapsedInList");

        //the folded lines stand in for the card, so the card steps aside for
        //whichever fold this page uses. one fold per page - see the options
        if (_collapseCancelled)
            Gate(_grid, true, "NotCanceled");
        else if (_collapseCompleted)
            Gate(_grid, true, "ExpandedInList");
        else
            Gate(_grid, true);

        Gate(_check, _showSelection, "SelectionModeEnabled");
        Gate(_info, _showInfo);

        Gate(_addressStack, _addressStyle == AddressStyles.Full);
        Gate(_where, _addressStyle == AddressStyles.NumberOnly);

        //number-only means the street is the heading above the row, so the
        //fold lines stop repeating it too
        Gate(_cancelledStreet, _addressStyle == AddressStyles.Full);
        Gate(_collapsedStreet, _addressStyle == AddressStyles.Full);

        Gate(_placeStack, _showPlace);

        if (_priceStyle == PriceStyles.Prefixed)
            _price.SetBinding(Label.TextProperty, "JobFormattedStringPrice");
        else
            _price.RemoveBinding(Label.TextProperty);

        Gate(_due, _showDue);
        if (_dueStyle == DueStyles.Relative)
        {
            _due.SetBinding(Label.TextProperty, "JobFormattedDueTime");
            _due.SetBinding(Label.TextColorProperty, "DueTextColour");
            Grid.SetColumnSpan(_due, 1);
        }
        else
        {
            //worded by the card itself - RefreshComputed - so the date gets
            //the whole width and the owed figure moves down beside the caption
            _due.RemoveBinding(Label.TextProperty);
            _due.RemoveBinding(Label.TextColorProperty);
            Grid.SetColumnSpan(_due, 2);
        }

        if (_owedStyle == OwedStyles.WhenOwed)
        {
            _owed.SetBinding(Label.TextProperty, "JobFormattedOwed");
            _owed.SetBinding(Label.TextColorProperty, "OwedTextColour");
            Grid.SetRow(_owed, 2);
            Gate(_owed, _showOwed, "ShowOwed");
        }
        else
        {
            _owed.RemoveBinding(Label.TextProperty);
            _owed.RemoveBinding(Label.TextColorProperty);
            Grid.SetRow(_owed, 3);
            Gate(_owed, _showOwed);
        }

        Gate(_chips, _showChips);
        Gate(_chipDone, _showChips && _showDoneChip, "IsCompleted");
        Gate(_chipBooked, _showChips && _showBookedChip, "IsBookedIn");
        Gate(_chipCancelled, _showChips && _showCancelledChip, "HaveCanceled");
        Gate(_chipType, _showChips, "HaveJobName");
        Gate(_chipRound, _showChips && _showRoundChip, "HaveRound");
        Gate(_chipOneOff, _showChips, "ShowOneOff");
        Gate(_chipLength, _showChips, "HaveLength");
        Gate(_chipTNB, _showChips && _showExtraChips, "TNB");
        Gate(_chipENB, _showChips && _showExtraChips, "ENB");
        Gate(_chipPending, _showChips && _showExtraChips, "PaymentPending");

        Gate(_tags, _showTags, "HaveTags");
        Gate(_notes, _showNotes, "HaveJobNotes");

        FilterTap(_street, JobCardPart.Street);
        FilterTap(_city, JobCardPart.City);
        FilterTap(_area, JobCardPart.Area);
        FilterTap(_price, JobCardPart.Price);
        FilterTap(_owed, JobCardPart.Owed);
        FilterTap(_chipType, JobCardPart.Type);
        FilterTap(_chipRound, JobCardPart.Round);
    }

    /// <summary>
    /// whether a piece is on the card at all. An option that is on hands the
    /// question down to the job through the binding; an option that is off
    /// takes the binding away as well, so nothing can turn the piece back on
    /// </summary>
    private static void Gate(VisualElement piece, bool option, string bindingPath = null)
    {
        piece.RemoveBinding(IsVisibleProperty);

        if (!option)
        {
            piece.IsVisible = false;
            return;
        }

        if (bindingPath == null)
            piece.IsVisible = true;
        else
            piece.SetBinding(IsVisibleProperty, bindingPath);
    }

    /// <summary>
    /// a filter tap on one piece. The recogniser is genuinely put on and
    /// taken off rather than ignored while off, because a recogniser on a
    /// label swallows the tap the row itself was waiting for
    /// </summary>
    private void FilterTap(Label piece, JobCardPart part)
    {
        TapGestureRecognizer tap;
        if (!_filterTaps.TryGetValue(piece, out tap))
        {
            tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => PartTapped?.Invoke(this, new JobCardEventArgs() { Job = Job, Part = part });
            _filterTaps[piece] = tap;
        }

        if (_enableFilterTaps && !piece.GestureRecognizers.Contains(tap))
            piece.GestureRecognizers.Add(tap);
        else if (!_enableFilterTaps)
            piece.GestureRecognizers.Remove(tap);
    }

    // ------------------------------------- the wordings the card works out

    /// <summary>
    /// The three wordings bindings cannot do: the number-only address, the
    /// written-out due date and the always-answered balance. Worked out when
    /// the job changes - the pages that use these rebuild their lists on
    /// change, the same as they always did.
    /// </summary>
    private void RefreshComputed()
    {
        Job j = Job;
        if (j == null)
            return;

        Customer c = j.GetCustomer();

        if (_addressStyle == AddressStyles.NumberOnly)
        {
            //the street is the heading above the row, so the house only has
            //to say which one it is. A house with no number falls back to
            //whoever lives there, the next best way to tell it apart
            string number = j.JobFormattedHouseNumber.Trim();
            string who = c == null ? string.Empty : $"{c.FName} {c.SName}".Trim();

            if (number.Length > 0)
                _where.Text = number;
            else if (who.Length > 0)
                _where.Text = who;
            else
                _where.Text = "(no address)";
        }

        if (_priceStyle == PriceStyles.Effective)
            _price.Text = Money(j.EffectivePrice);

        if (_dueStyle == DueStyles.LongDate)
        {
            //days past the day it fell due. positive is overdue, which is
            //the way round somebody stood in front of the house reads it
            int days = (int)(UsfulFuctions.DateNow.Date - j.DueDate.Date).TotalDays;
            string due = $"Due {j.DueDate.ToString("ddd d MMM yyyy")}";

            if (days > 0)
            {
                _due.Text = $"{due} · {days} day{(days == 1 ? string.Empty : "s")} overdue";
                _due.TextColor = OverdueRed;
            }
            else if (days == 0)
            {
                _due.Text = $"{due} · today";
                _due.TextColor = PriceGreen;
            }
            else
            {
                _due.Text = due;
                _due.TextColor = QuietGrey;
            }
        }

        if (_owedStyle == OwedStyles.Always)
        {
            float balance = c == null ? 0 : c.Balance;

            if (c == null)
            {
                _owed.Text = string.Empty;
                _owed.TextColor = Colors.Transparent;
            }
            else if (balance > 0)
            {
                _owed.Text = $"Owes {Money(balance)}";
                _owed.TextColor = OverdueRed;
            }
            else if (balance < 0)
            {
                _owed.Text = $"{Money(Math.Abs(balance))} in credit";
                _owed.TextColor = PriceGreen;
            }
            else
            {
                _owed.Text = "Nothing owed";
                _owed.TextColor = QuietGrey;
            }
        }
    }

    private static string Money(float amount)
    {
        return $"{Gloable.CurrenceSymbol}{amount:0.00}";
    }

    // -------------------------------------------------------- small makers

    private void Put(View piece, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        Grid.SetRow(piece, row);
        Grid.SetColumn(piece, column);
        if (rowSpan > 1)
            Grid.SetRowSpan(piece, rowSpan);
        if (columnSpan > 1)
            Grid.SetColumnSpan(piece, columnSpan);
        _grid.Children.Add(piece);
    }

    private static Label Caption()
    {
        Label l = new Label() { FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation };
        l.SetAppThemeColor(Label.TextColorProperty, CaptionLight, CaptionDark);
        return l;
    }

    private static Label Chip(string text, string colour)
    {
        return new Label()
        {
            Text = text,
            TextColor = Colors.White,
            BackgroundColor = Color.Parse(colour),
            Padding = new Thickness(6, 2),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
        };
    }

    private static Label Struck(string path, bool bold)
    {
        Label l = Plain(path, bold);
        l.TextDecorations = TextDecorations.Strikethrough;
        return l;
    }

    private static Label Plain(string path, bool bold)
    {
        Label l = new Label()
        {
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.TailTruncation,
        };
        l.SetBinding(Label.TextProperty, path);
        return l;
    }
}
