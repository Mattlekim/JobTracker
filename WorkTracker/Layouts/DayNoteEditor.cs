namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Writing a note against a day, asked the same way wherever it is asked
/// from - the way the balance and a job's duration are asked.
///
/// The prompt carries what is already written, so editing a note is changing
/// what is there rather than typing it out again, and **clearing it out is
/// how a note is taken off** - said on the prompt itself, because a box with
/// no delete button beside it does not look like it has one.
/// </summary>
public static class DayNoteEditor
{
    /// <summary>the most a note can be, so a stray paste cannot fill the file</summary>
    private const int MostThatFits = 300;

    /// <summary>what the button that opens this should say for a day</summary>
    public static string ButtonText(DateTime day)
    {
        return DayNote.Has(day) ? "Edit The Note" : "Add A Note";
    }

    /// <summary>
    /// asks for the day's note and writes it.
    /// </summary>
    /// <returns>true when the note was changed, so the caller can redraw</returns>
    public static async Task<bool> ChangeAsync(DateTime day, Page page)
    {
        if (page == null)
            return false;

        string already = DayNote.TextFor(day);

        string typed = await page.DisplayPromptAsync(
            $"Note For {day:ddd d MMM yyyy}",
            already.Length > 0
                ? "Clear it out to take the note off this day."
                : "Anything about the day itself - the van in for its MOT, a bank holiday, somebody coming out with you.",
            "Save", "Cancel",
            placeholder: "Nothing written yet",
            maxLength: MostThatFits,
            initialValue: already);

        //Cancel comes back null. An empty box is an answer, not a refusal:
        //it is how the note is taken off
        if (typed == null)
            return false;

        //written down here rather than in DayNote.Set, which changes the note
        //the way the rest of the kernel's setters do and leaves the saving to
        //whoever asked. This is the one place a note is written, so there is
        //nowhere else for it to be forgotten
        if (!DayNote.Set(day, typed))
            return false;

        DayNote.Save();
        return true;
    }
}
