namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Asking which tag to put on, in one place, so tagging one job and tagging
/// a whole day booked in ask the same question in the same words.
///
/// Tags already used are offered first because that is nearly always what is
/// wanted, with typing a new one at the bottom of the list. A round that has
/// had every tag deleted off the settings page goes straight to typing one -
/// an action sheet with nothing on it but Cancel is a dead end.
/// </summary>
public static class TagPicker
{
    private const string NewTag = "Type A New Tag...";

    /// <summary>
    /// asks which tag to put on.
    /// </summary>
    /// <returns>the tag, or null when nothing was picked</returns>
    public static async Task<string> AskAsync(Page page, string title)
    {
        if (page == null)
            return null;

        List<string> choices = new List<string>();
        foreach (string tag in Job.TagNames)
            if (!string.IsNullOrWhiteSpace(tag))
                choices.Add(tag);

        string picked = NewTag;

        if (choices.Count > 0)
        {
            choices.Add(NewTag);
            picked = await page.DisplayActionSheet(title, "Cancel", null, choices.ToArray());

            if (picked == null || picked == "Cancel")
                return null;
        }

        if (picked != NewTag)
            return picked;

        string typed = await page.DisplayPromptAsync(title, "What do you want to call it?", "Add", "Cancel");
        return string.IsNullOrWhiteSpace(typed) ? null : typed.Trim();
    }

    /// <summary>
    /// Tagging a piece of work, whether that is one house or a whole day
    /// booked in - a day's work is a list of jobs like any other, so both go
    /// through here and behave the same.
    ///
    /// Saves whatever it changes, so a caller only has to rebuild its list.
    /// </summary>
    /// <param name="jobs">the work to tag</param>
    /// <param name="what">what is being tagged, as it reads in the questions</param>
    /// <returns>true when something was changed</returns>
    public static async Task<bool> EditAsync(Page page, List<Job> jobs, string what)
    {
        if (page == null || jobs == null || jobs.Count == 0)
            return false;

        List<string> alreadyOn = Booking.TagsOn(jobs);

        //nothing on it yet, so there is only one thing that can be done and
        //no point asking which
        string doing = "Add A Tag";
        if (alreadyOn.Count > 0)
        {
            doing = await page.DisplayActionSheet($"Tags On {what}", "Cancel", null, "Add A Tag", "Take A Tag Off");
            if (doing == null || doing == "Cancel")
                return false;
        }

        if (doing == "Take A Tag Off")
        {
            string off = await AskWhichToRemoveAsync(page, alreadyOn, $"Which Tag Comes Off {what}?");
            if (off == null || Booking.UntagJobs(jobs, off) == 0)
                return false;

            Job.Save();
            return true;
        }

        string tag = await AskAsync(page, $"Tag {what}");
        if (tag == null)
            return false;

        int known = Job.TagNames.Count;
        bool tagged = Booking.TagJobs(jobs, tag) > 0;

        //a tag typed in is new to the round, and the list of tags to pick
        //from lives with the settings rather than with the jobs
        if (Job.TagNames.Count != known)
            Settings.Save();

        if (tagged)
            Job.Save();

        return tagged;
    }

    /// <summary>
    /// asks which of the tags already on to take off.
    /// </summary>
    /// <returns>the tag, or null when nothing was picked</returns>
    public static async Task<string> AskWhichToRemoveAsync(Page page, List<string> tags, string title)
    {
        if (page == null || tags == null || tags.Count == 0)
            return null;

        string picked = await page.DisplayActionSheet(title, "Cancel", null, tags.ToArray());

        if (picked == null || picked == "Cancel")
            return null;

        return picked;
    }
}
