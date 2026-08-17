namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The bank accounts statements are imported against, reached through
/// Banking on the settings page. Accounts are added, renamed and archived
/// here - never deleted, because everything imported from one is tracked
/// against its id and a dropped id would orphan the lot. Archiving is the
/// answer for an account that is closed or no longer imported from: it
/// stops being offered when a statement is imported, and that is all.
/// </summary>
public partial class BankAccounts : ContentPage
{
    public BankAccounts()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Refresh();
    }

    private void Refresh()
    {
        vsl_accounts.Clear();

        List<BankAccount> accounts = BankAccount.Query();

        if (accounts.Count == 0)
        {
            vsl_accounts.Add(new Label()
            {
                Text = "No accounts yet - one is made by itself the first time a statement is imported. Add one here first if you want it called something better than 'My Bank'.",
                FontSize = 12,
                TextColor = Colors.Grey,
                Padding = new Thickness(0, 12),
            });
            return;
        }

        //the accounts in use first, the shelf below them
        foreach (BankAccount account in accounts.Where(x => !x.Archived))
            vsl_accounts.Add(BuildRow(account));

        List<BankAccount> archived = accounts.Where(x => x.Archived).ToList();
        if (archived.Count > 0)
        {
            vsl_accounts.Add(new Label()
            {
                Text = "Archived",
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 8, 0, 0),
            });

            foreach (BankAccount account in archived)
                vsl_accounts.Add(BuildRow(account));
        }
    }

    private View BuildRow(BankAccount account)
    {
        VerticalStackLayout content = new VerticalStackLayout() { Spacing = 2 };

        content.Add(new Label() { Text = account.Name, FontAttributes = FontAttributes.Bold });

        content.Add(new Label()
        {
            Text = DescribeLayouts(account),
            FontSize = 12,
            TextColor = Colors.Grey,
        });

        HorizontalStackLayout buttons = new HorizontalStackLayout()
        {
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
        };

        buttons.Add(RowButton("Rename", "#1E88E5", (s, e) => Rename(account)));

        if (account.Archived)
            buttons.Add(RowButton("Unarchive", "#2E7D32", (s, e) => Unarchive(account)));
        else
            buttons.Add(RowButton("Archive", "#6B7280", (s, e) => Archive(account)));

        content.Add(buttons);

        Border border = new Border() { Content = content };
        border.Style = (Style)Resources["Card"];

        //an archived account reads as on the shelf, not gone
        if (account.Archived)
            border.Opacity = 0.6;

        return border;
    }

    /// <summary>what this account already knows, so a fresh one is not a mystery</summary>
    private static string DescribeLayouts(BankAccount account)
    {
        bool csv = account.Date != -1;
        bool pdf = account.PdfDate != -1;

        if (csv && pdf)
            return "Knows its csv and pdf statements";
        if (csv)
            return "Knows its csv statements";
        if (pdf)
            return "Knows its pdf statements";
        return "No statement imported yet - the columns are asked for on the first one";
    }

    private Button RowButton(string text, string colour, EventHandler clicked)
    {
        Button button = new Button()
        {
            Text = text,
            TextColor = Color.FromArgb(colour),
            BorderColor = Color.FromArgb(colour),
        };
        button.Style = (Style)Resources["RowButton"];
        button.Clicked += clicked;
        return button;
    }

    private async void bnt_add_Clicked(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync("Add Account",
            "What is the account called? The name is how it is offered when a statement is imported.",
            "Add", "Cancel");

        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        //the import question offers accounts by name, so two accounts with
        //one name could never be told apart there
        if (BankAccount.NameTaken(name))
        {
            await DisplayAlert("Add Account", $"There is already an account called '{name}'.", "Ok");
            return;
        }

        BankAccount.Add(name);
        BankAccount.Save();
        Refresh();
    }

    private async void Rename(BankAccount account)
    {
        string name = await DisplayPromptAsync("Rename Account",
            "Statements and expenses already imported stay with the account - only the name changes.",
            "Rename", "Cancel", initialValue: account.Name);

        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name == account.Name)
            return;

        if (BankAccount.NameTaken(name, account.Id))
        {
            await DisplayAlert("Rename Account", $"There is already an account called '{name}'.", "Ok");
            return;
        }

        account.Name = name;
        BankAccount.Save();
        Refresh();
    }

    private async void Archive(BankAccount account)
    {
        string message = $"'{account.Name}' will not be offered when a statement is imported. " +
            "Everything already imported from it stays exactly as it is, and it can be unarchived any time.";

        //the last active account going means the next import has nothing to
        //land on, which is worth knowing before rather than after
        if (BankAccount.QueryActive().Count == 1)
            message += "\n\nThis is the only account left in use - importing a statement will need it unarchived, or another account added.";

        if (!await DisplayAlert("Archive Account?", message, "Archive", "Cancel"))
            return;

        account.Archived = true;
        BankAccount.Save();
        Refresh();
    }

    private async void Unarchive(BankAccount account)
    {
        if (!await DisplayAlert("Unarchive Account?",
                $"'{account.Name}' will be offered again when a statement is imported, with everything it already knew.",
                "Unarchive", "Cancel"))
            return;

        account.Archived = false;
        BankAccount.Save();
        Refresh();
    }
}
