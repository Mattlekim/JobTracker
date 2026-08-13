namespace UiInterface.Layouts;

using System.Globalization;
using Kernel;

/// <summary>
/// How long a job takes, changed from wherever the job is being looked at.
///
/// The estimate is what the day is planned off - the calendar, the booking
/// form and the round's figures all add it up - so it is the sort of thing
/// that gets corrected the moment somebody notices it is wrong, stood at the
/// house. Sending them into the job form to do it means it never gets done,
/// which is the same reason the balance has a Change beside it.
///
/// A job with no estimate of its own counts as the round's usual
/// (<see cref="Settings.DefaultJobDuration"/>), so nothing has to be filled
/// in for a house that takes as long as everywhere else.
/// </summary>
public static class JobDuration
{
    /// <summary>
    /// how long this job is counted as taking, its own estimate or the
    /// round's usual. <see cref="Job.Minutes"/> is where that is decided,
    /// once, for the rows and the day totals alike
    /// </summary>
    public static int MinutesFor(Job j)
    {
        return j == null ? 0 : j.Minutes;
    }

    /// <summary>what the job's time says on screen</summary>
    public static string Describe(Job j)
    {
        if (j == null)
            return string.Empty;

        if (j.EstimatedTime > 0)
            return Spell(j.EstimatedTime);

        //nothing on the job itself: say so, and say what it is being counted
        //as, or the figures on the calendar look like they came from nowhere
        return Settings.DefaultJobDuration > 0
            ? $"{Spell(Settings.DefaultJobDuration)} (the usual)"
            : "Not set";
    }

    /// <summary>
    /// minutes as somebody would say them. the same words the tag on the job
    /// rows uses, so the page and the row cannot word it differently
    /// </summary>
    public static string Spell(int minutes)
    {
        return minutes <= 0 ? "Not set" : Job.SpellMinutes(minutes);
    }

    /// <summary>
    /// asks how long the job takes and puts it on. returns true when it was
    /// changed
    /// </summary>
    public static async Task<bool> ChangeAsync(Job job, Page page)
    {
        if (job == null || page == null)
            return false;

        string typed = await page.DisplayPromptAsync("Time For Job",
            $"How long does this one take, in minutes?\n\nIt is {Describe(job)} now. Leave it empty to use the round's usual.",
            "Save", "Cancel",
            initialValue: job.EstimatedTime > 0 ? job.EstimatedTime.ToString() : string.Empty,
            keyboard: Keyboard.Numeric);

        if (typed == null)
            return false;

        typed = typed.Trim();

        //empty is an answer: this house is no different from the rest
        if (typed.Length == 0)
        {
            Apply(job, 0);
            return true;
        }

        int minutes;
        if (!TryReadMinutes(typed, out minutes) || minutes < 0)
        {
            await page.DisplayAlert("Time For Job", $"'{typed}' is not a number of minutes.", "Ok");
            return false;
        }

        Apply(job, minutes);
        return true;
    }

    /// <summary>
    /// reads what was typed. a keypad set to another country still puts its
    /// own separators in, and somebody typing 30.0 means half an hour
    /// </summary>
    private static bool TryReadMinutes(string typed, out int minutes)
    {
        minutes = 0;

        float read;
        if (!float.TryParse(typed, NumberStyles.Float, CultureInfo.CurrentCulture, out read)
            && !float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out read))
            return false;

        minutes = (int)Math.Round(read);
        return true;
    }

    /// <summary>
    /// puts the time on the job and on the visits it has still to come.
    ///
    /// How long a house takes is about the house rather than about one visit,
    /// and the job being looked at is as likely as not one already done - so
    /// changing it there and having the next clean still say the old figure
    /// would be no use at all. The chain is followed forwards only: a visit
    /// already written up keeps what it was worked to, and another job at the
    /// same house is a different job.
    /// </summary>
    public static void Apply(Job job, int minutes)
    {
        if (job == null)
            return;

        //visited guard: a JobNextId pointing back at an earlier job would
        //otherwise go round for ever
        HashSet<int> seen = new HashSet<int>();

        Job j = job;
        while (j != null && seen.Add(j.Id))
        {
            j.EstimatedTime = minutes;
            j.Refresh();

            j = Job.Query(QueryType.JobId, j.JobNextId).FirstOrDefault();
        }

        Job.Save();
        DataRefreshNotifier.NotifyDataChanged();
    }
}
