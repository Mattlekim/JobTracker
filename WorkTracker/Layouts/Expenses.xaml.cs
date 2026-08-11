namespace UiInterface.Layouts;

using Kernel;

public partial class Expenses : ContentPage
{
    public Expenses()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => RefreshPage();
    }

    private void RefreshPage()
    {
        List<Expense> expenses = Expense.Query();
        expenses = expenses.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToList();

        lv_expenses.ItemsSource = null;
        lv_expenses.ItemsSource = expenses;

        l_noExpenses.IsVisible = expenses.Count == 0;

        DateTime now = UsfulFuctions.DateNow;
        float monthTotal = 0;
        float allTotal = 0;
        foreach (Expense ex in expenses)
        {
            allTotal += ex.Amount;
            if (ex.Date.Year == now.Year && ex.Date.Month == now.Month)
                monthTotal += ex.Amount;
        }

        l_overview.Text = $"Spent this month {Gloable.CurrenceSymbol}{monthTotal:0.00}. Total recorded {Gloable.CurrenceSymbol}{allTotal:0.00}";
    }

    private void bnt_addExpense_Clicked(object sender, EventArgs e)
    {
        NewExpense.JobToLink = null;
        NewExpense.DateToUse = null;
        NewExpense.ExpenseToEdit = null;
        Navigation.PushAsync(new NewExpense());
    }

    private Expense GetExpenseForMenu(object sender)
    {
        return Expense.Get(Convert.ToInt32(((MenuItem)sender).CommandParameter?.ToString()));
    }

    private void EditExpense(Expense ex)
    {
        if (ex == null)
            return;
        NewExpense.JobToLink = null;
        NewExpense.DateToUse = null;
        NewExpense.ExpenseToEdit = ex;
        Navigation.PushAsync(new NewExpense());
    }

    private void On_Expense_Edit(object sender, EventArgs e)
    {
        EditExpense(GetExpenseForMenu(sender));
    }

    private async void On_Expense_Delete(object sender, EventArgs e)
    {
        Expense ex = GetExpenseForMenu(sender);
        if (ex == null)
            return;

        if (!await DisplayAlert("Delete Expense?", $"Delete the {ex.FormattedAmount} expense from {ex.FormattedDate}? This cannot be undone.", "Yes", "No"))
            return;

        Expense.Remove(ex.Id);
        Expense.Save();
        RefreshPage();
    }

    private void lv_expenses_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lv_expenses.SelectedItem == null)
            return;

        Expense ex = lv_expenses.SelectedItem as Expense;
        lv_expenses.SelectedItem = null;
        EditExpense(ex);
    }
}
