namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The payees the app has been told about while going through bank
/// statements: which ones are business expenses (and how to file them) and
/// which ones to leave alone. Kept editable here so a payee that was flagged
/// by mistake, or one whose category has changed, can be put right without
/// having to find a statement to re-import.
/// </summary>
public partial class ExpenseRules : ContentPage
{
    public ExpenseRules()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => RefreshPage();
    }

    private void RefreshPage()
    {
        List<ExpenseRule> rules = ExpenseRule.Query()
            .OrderBy(x => x.Ignore)
            .ThenBy(x => x.FormattedMerchant)
            .ToList();

        lv_rules.ItemsSource = null;
        lv_rules.ItemsSource = rules;

        l_noRules.IsVisible = rules.Count == 0;
    }

    private ExpenseRule GetRuleForMenu(object sender)
    {
        return ExpenseRule.Get(Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
    }

    private void On_Rule_Change(object sender, EventArgs e)
    {
        ChangeRule(GetRuleForMenu(sender));
    }

    private async void On_Rule_Forget(object sender, EventArgs e)
    {
        ExpenseRule rule = GetRuleForMenu(sender);
        if (rule == null)
            return;

        if (!await DisplayAlert("Forget This Payee?",
                $"'{rule.FormattedMerchant}' will be asked about again the next time it turns up on a statement. Expenses already logged for it are kept.",
                "Forget", "Cancel"))
            return;

        ExpenseRule.Remove(rule.Id);
        ExpenseRule.Save();
        RefreshPage();
    }

    private void lv_rules_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lv_rules.SelectedItem == null)
            return;

        ExpenseRule rule = lv_rules.SelectedItem as ExpenseRule;
        lv_rules.SelectedItem = null;
        ChangeRule(rule);
    }

    /// <summary>
    /// swaps a payee between "always an expense" and "never an expense", and
    /// lets the category and the note that go on the expense be changed
    /// </summary>
    private async void ChangeRule(ExpenseRule rule)
    {
        if (rule == null)
            return;

        string answer = await DisplayActionSheet(rule.FormattedMerchant, "Cancel", null,
            "Log as an expense", "Never an expense", "Change the note");

        if (answer == null || answer == "Cancel")
            return;

        if (answer == "Never an expense")
        {
            rule.Ignore = true;
            rule.Notes = string.Empty;
        }
        else if (answer == "Log as an expense")
        {
            string category = await DisplayActionSheet("Which category?", "Cancel", null,
                Enum.GetNames(typeof(ExpenseCategory)));

            if (category == null || category == "Cancel")
                return;

            rule.Ignore = false;
            rule.Category = (ExpenseCategory)Enum.Parse(typeof(ExpenseCategory), category);
        }
        else
        {
            string note = await DisplayPromptAsync("Note", "This note goes on every expense logged for this payee.",
                "Save", "Cancel", initialValue: rule.Notes ?? string.Empty);

            if (note == null)
                return;

            rule.Notes = note;
        }

        ExpenseRule.Save();
        RefreshPage();
    }
}
