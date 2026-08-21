namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Asking which part of the round something is about - all of it, one round,
/// or one area - in one place, so it is the same question however it is
/// reached.
///
/// It is deliberately not <see cref="RoundPicker"/>. That one asks which
/// round work should go *on*, so it offers No Round and typing a new one:
/// both are answers about where work belongs. This one asks which round or
/// area to *look at*, so it offers what work is actually on - a round nobody
/// has any houses on is nothing to export - and never offers to invent one.
/// </summary>
public static class RoundPartPicker
{
    private const string WholeRound = "The Whole Round";
    private const string ARound = "One Round...";
    private const string AnArea = "One Area...";
    private const string NoRound = "Work On No Round";
    private const string NoArea = "Houses With No Area";
    private const string Cancel = "Cancel";

    /// <summary>
    /// asks which part of the round.
    /// </summary>
    /// <returns>the part, or null when nothing was picked</returns>
    public static async Task<RoundPart> AskAsync(Page page, string title, List<Job> jobs)
    {
        if (page == null || jobs == null)
            return null;

        List<string> rounds = RoundPart.RoundsWithWork(jobs);
        List<string> areas = RoundPart.AreasWithWork(jobs);

        bool anyNoRound = RoundPart.AnyWithNoRound(jobs);
        bool anyNoArea = RoundPart.AnyWithNoArea(jobs);

        List<string> choices = new List<string> { WholeRound };

        //  Offered only where there is a real split.
        //
        //  One answer is not a choice: a round nobody has organised has
        //  every house under Work On No Round, and picking it would hand
        //  back the whole round under a name that says it did something.
        //  Same for an area on a round where nobody fills the area in. So
        //  the test is two or more answers, counting the unnamed one.
        if (rounds.Count + (anyNoRound ? 1 : 0) > 1)
            choices.Add(ARound);

        if (areas.Count + (anyNoArea ? 1 : 0) > 1)
            choices.Add(AnArea);

        //nothing is split up at all - the whole round is the only answer
        //there is, so it is given rather than asked for
        if (choices.Count == 1)
            return RoundPart.Everything();

        string picked = await page.DisplayActionSheet(title, Cancel, null, choices.ToArray());

        if (picked == null || picked == Cancel)
            return null;

        if (picked == WholeRound)
            return RoundPart.Everything();

        if (picked == ARound)
            return await AskWhichAsync(page, "Which round?", rounds, anyNoRound, NoRound, true);

        if (picked == AnArea)
            return await AskWhichAsync(page, "Which area?", areas, anyNoArea, NoArea, false);

        return null;
    }

    /// <summary>
    /// the second question - which of them. The work with no round on it and
    /// the houses with no area are on the end rather than left out: work
    /// nobody has organised yet is exactly what somebody organising it wants
    /// on a sheet
    /// </summary>
    private static async Task<RoundPart> AskWhichAsync(
        Page page, string title, List<string> named, bool anyUnnamed, string unnamedOption, bool isRound)
    {
        List<string> choices = new List<string>(named);

        if (anyUnnamed)
            choices.Add(unnamedOption);

        string picked = await page.DisplayActionSheet(title, Cancel, null, choices.ToArray());

        if (picked == null || picked == Cancel)
            return null;

        //blank is what says the work on no round, which is a real answer
        //rather than a missing one
        string name = picked == unnamedOption ? string.Empty : picked;

        return isRound ? RoundPart.OnRound(name) : RoundPart.InArea(name);
    }
}
