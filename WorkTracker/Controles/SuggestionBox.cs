namespace UiInterface.Controles;

/// <summary>
/// Makes an Entry suggest what has been typed into it before.
///
/// A round is a few streets done over and over, so the address going into a
/// new customer is nearly always one already on the books. Suggestions come
/// up under the box as it is typed into and fill it in with one tap - which
/// also stops the same street going in three different ways and splitting
/// itself up in every list that groups by street.
///
/// The suggestions are put in a row rather than a dropdown on purpose: a
/// dropdown floating over the form has to be dismissed, and this is being
/// used one handed. Choosing one, or clearing the box, takes the row away.
/// </summary>
public static class SuggestionBox
{
    /// <summary>how many to offer - past this it is quicker to keep typing</summary>
    private const int Most = 6;

    /// <summary>
    /// wires an entry up to a row that will hold its suggestions
    /// </summary>
    /// <param name="entry">the box being typed into</param>
    /// <param name="row">an empty layout directly under it</param>
    /// <param name="source">what has been typed before, worked out fresh each time</param>
    /// <param name="chosen">run when a suggestion is tapped, for filling in the rest</param>
    public static void Attach(Entry entry, Layout row, Func<List<string>> source, Action<string> chosen = null)
    {
        if (entry == null || row == null || source == null)
            return;

        row.IsVisible = false;

        entry.TextChanged += (s, e) => Show(entry, row, source, chosen, e.NewTextValue);
    }

    private static void Show(Entry entry, Layout row, Func<List<string>> source, Action<string> chosen, string typed)
    {
        row.Clear();
        row.IsVisible = false;

        if (string.IsNullOrWhiteSpace(typed))
            return;

        typed = typed.Trim();

        List<string> matches = new List<string>();
        foreach (string value in source())
        {
            //already typed in full - there is nothing left to suggest
            if (string.Equals(value, typed, StringComparison.CurrentCultureIgnoreCase))
                return;

            if (value.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase))
                matches.Add(value);
        }

        //nothing starts with it, so try anywhere in the name - "street" then
        //finds "High Street"
        if (matches.Count == 0)
            foreach (string value in source())
                if (value.IndexOf(typed, StringComparison.CurrentCultureIgnoreCase) > 0)
                    matches.Add(value);

        if (matches.Count == 0)
            return;

        foreach (string value in matches)
        {
            if (row.Count >= Most)
                break;

            row.Add(Suggestion(value, () =>
            {
                entry.Text = value;

                //filling the box in fires TextChanged, which takes the row
                //away by itself - the text now matches the suggestion
                if (chosen != null)
                    chosen(value);
            }));
        }

        row.IsVisible = row.Count > 0;
    }

    private static Button Suggestion(string text, Action tapped)
    {
        Button b = new Button()
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(12, 4),
            CornerRadius = 8,
            BorderWidth = 2,
            BorderColor = Color.FromArgb("#1E88E5"),
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#1E88E5"),
        };

        b.Clicked += (s, e) => tapped();
        return b;
    }
}
