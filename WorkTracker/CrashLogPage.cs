namespace WorkTracker
{
    // Built entirely in C# (no XAML) so it can still be shown when
    // XAML or app data loading is the thing that is crashing.
    public class CrashLogPage : ContentPage
    {
        public CrashLogPage()
        {
            Title = "Crash Log";
            var log = CrashLogger.ReadLog();

            var header = new Label
            {
                Text = "The app crashed. The details below can be shared to help fix it.",
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 10, 0, 0),
            };

            var editor = new Editor
            {
                Text = log,
                IsReadOnly = true,
                FontSize = 11,
            };

            var shareButton = new Button { Text = "Share log" };
            shareButton.Clicked += async (s, e) =>
            {
                try
                {
                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = "WorkTracker crash log",
                        File = new ShareFile(CrashLogger.LogPath),
                    });
                }
                catch
                {
                    try
                    {
                        await Clipboard.SetTextAsync(log);
                        await DisplayAlert("Copied", "Sharing failed, so the log was copied to the clipboard instead.", "OK");
                    }
                    catch { }
                }
            };

            var copyButton = new Button { Text = "Copy to clipboard" };
            copyButton.Clicked += async (s, e) =>
            {
                try
                {
                    await Clipboard.SetTextAsync(log);
                    await DisplayAlert("Copied", "Crash log copied to the clipboard.", "OK");
                }
                catch { }
            };

            var continueButton = new Button { Text = "Delete log and open app" };
            continueButton.Clicked += (s, e) =>
            {
                CrashLogger.Clear();
                try
                {
                    Application.Current.MainPage = new AppShell();
                }
                catch (Exception ex)
                {
                    CrashLogger.Log("Startup (AppShell)", ex);
                    Application.Current.MainPage = new CrashLogPage();
                }
            };

            var grid = new Grid
            {
                Padding = 10,
                RowSpacing = 8,
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                },
            };
            grid.Add(header, 0, 0);
            grid.Add(editor, 0, 1);
            grid.Add(shareButton, 0, 2);
            grid.Add(copyButton, 0, 3);
            grid.Add(continueButton, 0, 4);

            Content = grid;
        }
    }
}
