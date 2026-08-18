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
/// hands a list its two kinds of row: a street heading, or a house. The
/// heading template is a label and nothing else, so a heading cannot be
/// swiped, held or tapped like work
/// </summary>
public class StreetSplitTemplateSelector : DataTemplateSelector
{
    public DataTemplate HeadingTemplate { get; set; }

    public DataTemplate JobTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item is StreetHeading ? HeadingTemplate : JobTemplate;
    }
}
