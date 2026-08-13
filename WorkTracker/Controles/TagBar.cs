namespace UiInterface.Controles;

using Kernel;
using UiInterface.Layouts;

/// <summary>
/// The strip at the top of every page work can be marked done from, holding
/// the tags to put on that work as it is written up (<see cref="Job.AutoTags"/>).
///
/// A round is usually all the same on the day: everything front only because
/// of the weather, or a whole street with nobody in. Tagging each house one
/// at a time will not happen while you are stood at a gate with wet hands, so
/// the tag is set once here and goes on by itself.
///
/// Folded away to a single small button while nothing is set, because most
/// days nothing is. Once something *is* set the tags stay on show whether it
/// is folded or not - a bar that could be closed over a tag still going on
/// everything marked done would be the one thing this must not do.
///
/// One class used by every page rather than the same row four times, so all
/// four cannot drift apart. They are all showing the same setting, so a
/// change on one tells the others (<see cref="Changed"/>) and each brings
/// itself up to date as it is shown.
/// </summary>
public class TagBar : ContentView
{
    /// <summary>a bar somewhere has changed the tags. every other bar follows</summary>
    private static event Action Changed;

    private bool _open;

    private readonly Button _header;
    private readonly HorizontalStackLayout _chips;
    private readonly ScrollView _chipRow;
    private readonly Button _add;
    private readonly Button _clear;

    private const string TagColour = "#00838F";

    public TagBar()
    {
        _header = SmallButton("Tag ▸", TagColour);
        //a button with an arrow on it says nothing about what it opens. a
        //long press brings this up on a phone, hovering does on a desktop
        ToolTipProperties.SetText(_header, "Tags to put on every job as you mark it done - front only, nobody in, and so on");
        _header.Clicked += (s, e) =>
        {
            _open = !_open;
            Show();
        };

        _chips = new HorizontalStackLayout() { Spacing = 6, VerticalOptions = LayoutOptions.Center };

        //the tags scroll sideways rather than pushing the buttons off the
        //edge, the same as the day picker on the booked work page
        _chipRow = new ScrollView()
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _chips,
            VerticalOptions = LayoutOptions.Center,
        };

        _add = SmallButton("Add", TagColour);
        ToolTipProperties.SetText(_add, "Pick a tag to go on the work you mark done from here on");
        _add.Clicked += Add_Clicked;

        _clear = SmallButton("Clear", "#6B7280");
        ToolTipProperties.SetText(_clear, "Stop tagging work as it is marked done");
        _clear.Clicked += (s, e) =>
        {
            Job.AutoTags.Clear();
            Settings.Save();
            ShowEverywhere();
        };

        Grid row = new Grid() { Padding = new Thickness(8, 4), ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        AddToRow(row, _header, 0);
        AddToRow(row, _chipRow, 1);
        AddToRow(row, _add, 2);
        AddToRow(row, _clear, 3);

        Content = row;

        //the page this is on may have been sat in the background while the
        //tags were changed on another one
        Loaded += (s, e) =>
        {
            Changed += Show;
            Show();
        };
        Unloaded += (s, e) => Changed -= Show;

        Show();
    }

    private void Add_Clicked(object sender, EventArgs e)
    {
        AddTag();
    }

    private async void AddTag()
    {
        Page page = HostPage();
        if (page == null)
            return;

        string tag = await TagPicker.AskAsync(page, "Tag Work As It Is Marked Done");
        if (tag == null)
            return;

        if (Job.AddAutoTag(tag))
            Settings.Save();

        ShowEverywhere();
    }

    private void Show()
    {
        _chips.Clear();

        foreach (string tag in Job.AutoTags)
            _chips.Add(Chip(tag));

        //what it is for, said once, for the first time it is opened
        if (_open && Job.AutoTags.Count == 0)
            _chips.Add(new Label()
            {
                Text = "Put on every job as it is marked done.",
                FontSize = 12,
                TextColor = Color.FromArgb("#6B7280"),
                VerticalOptions = LayoutOptions.Center,
            });

        _header.Text = _open ? "Tag ▾" : "Tag ▸";

        //never folded away over a tag that is still going on everything
        _chipRow.IsVisible = _open || Job.AutoTags.Count > 0;
        _add.IsVisible = _open;
        _clear.IsVisible = _open && Job.AutoTags.Count > 0;
    }

    private void ShowEverywhere()
    {
        Show();

        if (Changed != null)
            Changed();
    }

    /// <summary>one tag, which comes off again when it is tapped</summary>
    private View Chip(string tag)
    {
        Button chip = new Button()
        {
            Text = $"{tag}  ✕",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(10, 2),
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb(TagColour),
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
        };

        ToolTipProperties.SetText(chip, $"Take {tag} off - work marked done will stop being tagged with it");

        chip.Clicked += (s, e) =>
        {
            Job.RemoveAutoTag(tag);
            Settings.Save();
            ShowEverywhere();
        };

        return chip;
    }

    private static void AddToRow(Grid row, View view, int column)
    {
        row.Children.Add(view);
        Grid.SetColumn(view, column);
        Grid.SetRow(view, 0);
    }

    private static Button SmallButton(string text, string colour)
    {
        return new Button()
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(12, 2),
            CornerRadius = 8,
            BorderWidth = 2,
            BorderColor = Color.FromArgb(colour),
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb(colour),
            VerticalOptions = LayoutOptions.Center,
        };
    }

    /// <summary>the page this bar is sat on, for putting the tag list up</summary>
    private Page HostPage()
    {
        Element e = this;
        while (e != null && !(e is Page))
            e = e.Parent;

        return e as Page;
    }
}
