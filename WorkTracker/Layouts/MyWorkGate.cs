namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The way back out of extra work.
///
/// It is a gate rather than a page of anything: landing on it asks whether
/// you mean to leave, because leaving locks the extra work again and the
/// PIN will be wanted to get back in. Saying yes puts the normal tabs back
/// up; saying no puts you back on the extra work.
///
/// It has to be a page - a tab needs something behind it - but nobody is
/// meant to spend any time here, so all it holds is the same choice as the
/// question, for anybody who dismisses the alert some other way.
/// </summary>
public class MyWorkGate : ContentPage
{
    private bool _asking;

    public MyWorkGate()
    {
        Title = "My Work";

        Button leave = new Button() { Text = "Go To My Work" };
        leave.Clicked += (s, e) => AskAboutLeaving();

        Button stay = new Button() { Text = "Back To The Extra Work" };
        stay.Clicked += (s, e) => WorkTracker.AppShell.EnterExtraWork();

        Content = new VerticalStackLayout()
        {
            Padding = 24,
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label()
                {
                    Text = "Leaving the extra work locks it again - the PIN it was sent with is needed to get back in.",
                    HorizontalTextAlignment = TextAlignment.Center,
                },
                leave,
                stay,
            },
        };

        NavigatedTo += (s, e) => AskAboutLeaving();
    }

    private async void AskAboutLeaving()
    {
        //only while the extra work is actually open: coming back to this
        //page after leaving has nothing to ask about
        if (_asking || !WorkTracker.AppShell.InExtraWork)
            return;

        _asking = true;

        try
        {
            if (await DisplayAlert("My Work",
                    "Leave the extra work? It locks behind you, and getting back in needs the PIN it was sent with.",
                    "Leave", "Stay"))
                WorkTracker.AppShell.LeaveExtraWork();
            else
                WorkTracker.AppShell.EnterExtraWork();
        }
        finally
        {
            _asking = false;
        }
    }
}
