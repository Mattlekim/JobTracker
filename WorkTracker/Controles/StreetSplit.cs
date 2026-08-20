namespace UiInterface.Controles;

using Kernel;

/// <summary>
/// A street's name standing above its houses on a list - the way
/// Layouts/AllJobs reads a round over, now shared by the booked work page and
/// the calendar's day list. Only a heading: the houses under it are the same
/// Job rows they always were.
/// </summary>
public class StreetHeading
{
    /// <summary>the display name, so screenshot mode masks it like any
    /// other street on screen</summary>
    public string Street { get; set; }
}

/// <summary>
/// A title above a run of streets saying what the work under it is - the
/// calendar's day list keeps the work booked in for a day apart from the work
/// that merely falls due on it, and this is the line that says which is which.
///
/// It stands above the street headings rather than beside them, so it is drawn
/// bigger, in its section's colour and with a rule under it: two headings that
/// read the same would only be one heading twice.
/// </summary>
public class SectionHeading
{
    public string Title { get; set; }

    /// <summary>what is under it, in a line - "5 houses - £45.00 to do"</summary>
    public string Detail { get; set; } = string.Empty;

    public bool HaveDetail
    {
        get { return !string.IsNullOrEmpty(Detail); }
    }

    /// <summary>the section's own colour, so the two sections of a day are
    /// told apart without the titles being read</summary>
    public Color Colour { get; set; } = Colors.Gray;
}

/// <summary>
/// Turns a handful of jobs into the rows a street-grouped list draws: sorted
/// street by street and up each street by house number, with a
/// <see cref="StreetHeading"/> wherever the street changes - the same order
/// and the same rule Layouts/AllJobs uses, so a day on the booked work page
/// and the whole round cannot read differently.
///
/// Grouping keys off the real street (<see cref="Job.SortStreet"/>) while the
/// heading is drawn from the display name, so screenshot mode changes what is
/// on screen and not what groups with what - see Kernel/ScreenshotMode.
/// </summary>
public static class StreetSplit
{
    public static List<object> WithHeadings(IEnumerable<Job> jobs)
    {
        List<object> rows = new List<object>();

        //null rather than empty, so the first job always starts a street even
        //when it has no street at all
        string street = null;

        IEnumerable<Job> ordered = jobs
            .OrderBy(x => x.SortStreet, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SortHouseNumber)
            .ThenBy(x => x.SortHouseSuffix, StringComparer.CurrentCultureIgnoreCase);

        foreach (Job j in ordered)
        {
            if (street == null || !string.Equals(street, j.SortStreet, StringComparison.CurrentCultureIgnoreCase))
            {
                street = j.SortStreet;
                rows.Add(new StreetHeading()
                {
                    Street = street.Length == 0 ? "No Street" : j.JobFormattedStreetOnly.Trim(),
                });
            }

            rows.Add(j);
        }

        return rows;
    }
}

/// <summary>
/// hands a list its kinds of row: a section title, a street heading, or a
/// house. Both heading templates are labels and nothing else, so a heading
/// cannot be swiped, held or tapped like work.
///
/// A list with nothing to section - the booked work page, where every job on
/// a day is booked in by definition - simply leaves
/// <see cref="SectionTemplate"/> unset and never draws one.
/// </summary>
public class StreetSplitTemplateSelector : DataTemplateSelector
{
    public DataTemplate HeadingTemplate { get; set; }

    public DataTemplate JobTemplate { get; set; }

    /// <summary>optional: only a list that splits its work into sections
    /// (the calendar's day list) sets this</summary>
    public DataTemplate SectionTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is SectionHeading)
            return SectionTemplate ?? HeadingTemplate;

        return item is StreetHeading ? HeadingTemplate : JobTemplate;
    }
}
