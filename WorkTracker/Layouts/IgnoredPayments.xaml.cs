namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The payment references being skipped on statement import.
///
/// Pressing Ignore while importing is one tap and it sticks for every
/// statement from then on, so it is easy to do to the wrong row. This is
/// where that gets put right - without having to find the statement and
/// import it again.
/// </summary>
public partial class IgnoredPayments : ContentPage
{
    public IgnoredPayments()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Refresh();
    }

    private void Refresh()
    {
        vsl_references.Clear();

        List<string> references = new List<string>(Payment.IgnorePaymentList ?? new List<string>());
        references.Sort(StringComparer.CurrentCultureIgnoreCase);

        foreach (string reference in references)
            vsl_references.Add(BuildRow(reference));

        l_nothing.IsVisible = references.Count == 0;
        bnt_clearAll.IsVisible = references.Count > 1;

        l_overview.Text = references.Count == 0
            ? "Nothing is being ignored"
            : references.Count == 1
                ? "1 reference is being skipped on import"
                : $"{references.Count} references are being skipped on import";
    }

    private View BuildRow(string reference)
    {
        Grid row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(reference) ? "(blank reference)" : reference,
            VerticalOptions = LayoutOptions.Center,
        }, 0, 0);

        Button undo = new Button
        {
            Text = "Stop Ignoring",
            BackgroundColor = Colors.Transparent,
            BorderWidth = 2,
            BorderColor = Color.FromArgb("#EF6C00"),
            TextColor = Color.FromArgb("#EF6C00"),
            CornerRadius = 8,
            Padding = new Thickness(12, 4),
            FontSize = 13,
        };
        undo.Clicked += (s, e) => StopIgnoring(reference);
        row.Add(undo, 1, 0);

        Border border = new Border { Content = row };
        border.Style = (Style)Resources["Card"];
        return border;
    }

    private void StopIgnoring(string reference)
    {
        Payment.StopIgnoring(reference);
        Payment.Save();
        Refresh();
    }

    private async void bnt_clearAll_Clicked(object sender, EventArgs e)
    {
        if (!await DisplayAlert("Stop Ignoring Everything?",
                "Every reference here will be brought back, so they all turn up on the next statement import.",
                "Yes", "Cancel"))
            return;

        Payment.StopIgnoringEverything();
        Payment.Save();
        Refresh();
    }
}
