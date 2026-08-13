namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Asking which round work goes on, in one place, so it is the same question
/// however it is reached.
///
/// The rounds already in use come first because that is nearly always the
/// answer, with No Round and typing a new one under them. A round typed in is
/// remembered, so a round only ever has to be named once.
/// </summary>
public static class RoundPicker
{
    private const string NewRound = "Type A New Round...";
    private const string NoRound = "No Round";

    /// <summary>
    /// asks which round.
    /// </summary>
    /// <returns>
    /// the round, an empty string for No Round, or null when nothing was
    /// picked - blank and null are different answers here, because taking
    /// work back off a round is a thing somebody means to do
    /// </returns>
    public static async Task<string> AskAsync(Page page, string title)
    {
        if (page == null)
            return null;

        List<string> choices = new List<string>();

        //what the round is actually split into, whether or not somebody has
        //kept the settings list tidy
        foreach (string round in Job.RoundNames)
            if (!string.IsNullOrWhiteSpace(round) && !choices.Contains(round))
                choices.Add(round);

        foreach (string round in Job.RoundsInUse())
            if (!choices.Exists(x => string.Equals(x, round, StringComparison.CurrentCultureIgnoreCase)))
                choices.Add(round);

        choices.Add(NoRound);
        choices.Add(NewRound);

        string picked = await page.DisplayActionSheet(title, "Cancel", null, choices.ToArray());

        if (picked == null || picked == "Cancel")
            return null;

        if (picked == NoRound)
            return string.Empty;

        if (picked != NewRound)
            return picked;

        string typed = await page.DisplayPromptAsync(title, "What is the round called?", "Add", "Cancel");
        return string.IsNullOrWhiteSpace(typed) ? null : typed.Trim();
    }
}
