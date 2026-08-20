namespace UiInterface;

using System.ComponentModel;
using UiInterface.Controles;

/// <summary>
/// How a day's work is read: as the cards every list has always drawn, or as
/// the paper round book's tight rows.
///
/// It is one answer for the two pages a day is looked at on - the calendar's
/// day panel and <c>Layouts/BookedWork</c> - because they are the same day
/// looked at twice, and a round read one way on one of them and the other way
/// on the next would only be two habits to keep up. The work list, All Jobs
/// and the round's own paper view are not in it: the first two are the round
/// rather than a day, and the paper view is already the paper view.
///
/// It is a preference and not a setting in the data files, like the paper
/// view's own view options and the calendar's <c>HideDueWork</c>: how a page
/// is being read is not something about the round, so it has no business
/// travelling in a backup or being handed to another phone.
///
/// <para>
/// The one thing to be careful of. A row on a virtualised list is built when
/// it is scrolled into view and handed a different house every time it comes
/// back round, so a card that read this off a static once and remembered the
/// answer would draw whatever the setting was when it happened to be built -
/// half a day in cards and half of it on paper, which is the shape of bug the
/// tick boxes on the work list once had. So the cards <b>bind</b> to
/// <see cref="Current"/> instead, and changing the setting tells every row
/// that exists, whenever it was built.
/// </para>
/// </summary>
public class DayListView : INotifyPropertyChanged
{
    /// <summary>
    /// the one instance the day lists bind to. A field rather than a
    /// property so the pages can reach it with x:Static
    /// </summary>
    public static readonly DayListView Current = new DayListView();

    private const string PreferenceName = "DayListView_Paper";

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// whether a day is drawn as paper rows. Off to begin with: the cards
    /// are what the app has always shown, and nobody is given a new looking
    /// app for having updated it
    /// </summary>
    public static bool Paper
    {
        get { return Preferences.Get(PreferenceName, false); }
        set
        {
            if (Paper == value)
                return;

            Preferences.Set(PreferenceName, value);
            Current.Changed();
        }
    }

    /// <summary>what a card on a day list draws itself as - the bound half
    /// of <see cref="Paper"/></summary>
    public JobCard.RowStyles RowStyle
    {
        get { return Paper ? JobCard.RowStyles.Paper : JobCard.RowStyles.Card; }
    }

    /// <summary>the two answers, in the order the settings picker offers
    /// them - the index is only ever read back through <see cref="Choice"/>
    /// so the two cannot drift apart</summary>
    public static readonly string[] ChoiceNames = new string[] { "List View", "Paper View" };

    /// <summary>which of <see cref="ChoiceNames"/> is in use</summary>
    public static int Choice
    {
        get { return Paper ? 1 : 0; }
        set { Paper = value == 1; }
    }

    private void Changed()
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null)
            handler(this, new PropertyChangedEventArgs(nameof(RowStyle)));
    }
}
