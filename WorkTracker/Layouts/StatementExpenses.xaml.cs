namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// The other half of a bank statement: what went out. Every outgoing is
/// listed so it can be flagged as a business expense or ignored, and both
/// answers are remembered against the payee - so a recurring bill like
/// insurance or car tax is logged by itself the next time a statement is
/// imported, and the shopping stays out of the way.
///
/// Nothing is ever recorded twice. Each line carries an id built from the
/// date, the payee and the amount (see <see cref="Expense.StatementReference"/>),
/// so re-importing the same statement - or the next one, which overlaps it -
/// finds the expense already there and leaves it alone.
/// </summary>
public partial class StatementExpenses : ContentPage
{
    /// <summary>one outgoing off the statement, and what has become of it</summary>
    private class StatementLine
    {
        public DateTime Date;

        /// <summary>the payee exactly as the bank printed it</summary>
        public string Reference = string.Empty;

        public float Amount;

        /// <summary>the id that stops this line being recorded twice</summary>
        public string Id = string.Empty;

        /// <summary>the remembered decision for this payee, if there is one</summary>
        public ExpenseRule Rule;

        /// <summary>the expense recorded for this line, if there is one</summary>
        public Expense Logged;

        public bool Ignored { get { return Rule != null && Rule.Ignore; } }
    }

    private List<StatementLine> _lines = new List<StatementLine>();

    public StatementExpenses()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Refresh();
    }

    private void Refresh()
    {
        l_autoLogged.IsVisible = false;

        ReadStatement();
        int autoLogged = ApplyRules();
        BuildList();

        if (autoLogged > 0)
        {
            l_autoLogged.IsVisible = true;
            l_autoLogged.Text = autoLogged == 1
                ? "1 recurring expense was logged automatically from a payee you have flagged before."
                : $"{autoLogged} recurring expenses were logged automatically from payees you have flagged before.";
        }
    }

    /// <summary>
    /// pulls the outgoings out of the loaded statement, in the order the bank
    /// printed them
    /// </summary>
    private void ReadStatement()
    {
        _lines.Clear();

        CSVFile file = StatmentViewer.CsvFile;
        if (file == null || file.data == null || !StatmentViewer.CanReadMoneyOut())
            return;

        int dateColumn = StatmentViewer.ActiveDateColumn;
        int refColumn = StatmentViewer.ActiveRefColumn;

        //counts identical transactions on the same day so two £5 fuel stops
        //on the same forecourt stay two expenses rather than one
        Dictionary<string, int> seen = new Dictionary<string, int>();

        foreach (string[] row in file.data)
        {
            if (row == null)
                continue;

            if (row.Length <= dateColumn || row.Length <= refColumn)
                continue;

            decimal amount;
            if (!TryGetOutgoing(row, out amount))
                continue;

            DateTime date;
            if (!StatementText.TryParseDate(row[dateColumn], out date))
                continue;

            StatementLine line = new StatementLine()
            {
                Date = date,
                Reference = row[refColumn] == null ? string.Empty : row[refColumn].Trim(),
                Amount = (float)amount,
            };

            string key = Expense.StatementReference(line.Date, line.Reference, line.Amount, 0);
            int occurrence = seen.TryGetValue(key, out int count) ? count : 0;
            seen[key] = occurrence + 1;

            line.Id = Expense.StatementReference(line.Date, line.Reference, line.Amount, occurrence);
            line.Logged = Expense.FindByReference(line.Id);
            line.Rule = ExpenseRule.FindMatch(line.Reference);

            _lines.Add(line);
        }
    }

    /// <summary>
    /// how much left the account on this line, whichever way the bank writes
    /// it - its own money out column, or a negative in a single signed column
    /// </summary>
    private static bool TryGetOutgoing(string[] row, out decimal amount)
    {
        amount = 0;

        if (StatmentViewer.ActiveAmountIncludesDebits)
        {
            int column = StatmentViewer.ActiveAmountColumn;
            if (column < 0 || row.Length <= column)
                return false;

            decimal signed;
            if (!StatmentViewer.TryParseAmount(row[column], out signed))
                return false;

            if (signed >= 0) //money coming in, which the payments page deals with
                return false;

            amount = -signed;
            return true;
        }

        int debit = StatmentViewer.ActiveDebitColumn;
        if (debit < 0 || row.Length <= debit)
            return false;

        decimal paidOut;
        if (!StatmentViewer.TryParseAmount(row[debit], out paidOut))
            return false;

        //some banks print the money out column as a positive, some as a
        //negative - either way it went out
        paidOut = Math.Abs(paidOut);
        if (paidOut <= 0)
            return false;

        amount = paidOut;
        return true;
    }

    /// <summary>
    /// logs the outgoings whose payee has been flagged as an expense before.
    /// this is what makes a recurring bill look after itself
    /// </summary>
    /// <returns>how many were logged this time round</returns>
    private int ApplyRules()
    {
        int logged = 0;

        foreach (StatementLine line in _lines)
        {
            if (line.Logged != null || line.Rule == null || line.Rule.Ignore)
                continue;

            Expense expense = new Expense()
            {
                Date = line.Date,
                Amount = line.Amount,
                Merchant = string.IsNullOrWhiteSpace(line.Rule.Merchant)
                    ? ExpenseRule.FriendlyMerchant(line.Reference)
                    : line.Rule.Merchant,
                Category = line.Rule.Category,
                Notes = line.Rule.Notes,
                ExternalReference = line.Id,
            };

            Expense.Add(expense);
            line.Logged = expense;
            line.Rule.MarkUsed(line.Date);
            logged++;
        }

        if (logged > 0)
        {
            Expense.Save();
            ExpenseRule.Save();
            DataRefreshNotifier.NotifyDataChanged();
        }

        return logged;
    }

    private void BuildList()
    {
        vsl_lines.Clear();

        int waiting = 0, expenses = 0, ignored = 0;
        float total = 0;

        foreach (StatementLine line in _lines)
        {
            if (line.Logged != null)
            {
                expenses++;
                total += line.Amount;
            }
            else if (line.Ignored)
                ignored++;
            else
                waiting++;

            vsl_lines.Add(BuildRow(line));
        }

        l_nothing.IsVisible = _lines.Count == 0;

        l_overview.Text = _lines.Count == 0
            ? "Nothing going out on this statement"
            : $"{_lines.Count} payments out. {expenses} logged as expenses ({Gloable.CurrenceSymbol}{total:0.00}), {ignored} ignored, {waiting} still to decide.";
    }

    private View BuildRow(StatementLine line)
    {
        VerticalStackLayout content = new VerticalStackLayout() { Spacing = 2 };

        content.Add(new Label()
        {
            Text = $"{line.Date.ToShortDateString()}   {Gloable.CurrenceSymbol}{line.Amount:0.00}",
            FontAttributes = FontAttributes.Bold,
        });

        content.Add(new Label() { Text = line.Reference, FontSize = 13 });

        if (line.Logged != null)
        {
            string state = $"Logged as an expense - {line.Logged.Category}";
            if (line.Logged.HaveNotes)
                state += $"\nNote: {line.Logged.Notes}";
            content.Add(new Label() { Text = state, FontSize = 12, TextColor = Colors.Green });
        }
        else if (line.Ignored)
            content.Add(new Label() { Text = "Ignored - not a business expense", FontSize = 12, TextColor = Colors.Grey });

        content.Add(BuildButtons(line));

        Border border = new Border() { Content = content };
        border.Style = (Style)Resources["Card"];
        return border;
    }

    private View BuildButtons(StatementLine line)
    {
        HorizontalStackLayout buttons = new HorizontalStackLayout() { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };

        if (line.Logged != null)
        {
            buttons.Add(RowButton("Edit", "#1E88E5", (s, e) => EditExpense(line)));
            buttons.Add(RowButton("Not An Expense", "#E53935", (s, e) => UndoExpense(line)));
        }
        else if (line.Ignored)
        {
            buttons.Add(RowButton("Stop Ignoring", "#EF6C00", (s, e) => StopIgnoring(line)));
        }
        else
        {
            buttons.Add(RowButton("Expense", "#2E7D32", (s, e) => FlagAsExpense(line)));
            buttons.Add(RowButton("Ignore", "#6B7280", (s, e) => Ignore(line)));
        }

        return buttons;
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

    /// <summary>
    /// hands the outgoing over to the normal expense page, so the category,
    /// the note and even a photo of the paperwork can be filled in the same
    /// way as any other expense
    /// </summary>
    private void FlagAsExpense(StatementLine line)
    {
        NewExpense.JobToLink = null;
        NewExpense.DateToUse = null;
        NewExpense.ExpenseToEdit = null;
        NewExpense.FromStatement = new NewExpense.StatementPrefill()
        {
            Date = line.Date,
            Amount = line.Amount,
            Reference = line.Reference,
            ExternalReference = line.Id,
        };
        Navigation.PushAsync(new NewExpense());
    }

    private void EditExpense(StatementLine line)
    {
        NewExpense.JobToLink = null;
        NewExpense.DateToUse = null;
        NewExpense.FromStatement = new NewExpense.StatementPrefill()
        {
            Date = line.Date,
            Amount = line.Amount,
            Reference = line.Reference,
            ExternalReference = line.Id,
        };
        NewExpense.ExpenseToEdit = line.Logged;
        Navigation.PushAsync(new NewExpense());
    }

    private async void Ignore(StatementLine line)
    {
        string payee = ExpenseRule.FriendlyMerchant(line.Reference);

        if (!await DisplayAlert("Ignore This?",
                $"'{payee}' will be left alone here and on every statement from now on. You can change your mind from the expense rules page.",
                "Ignore It", "Cancel"))
            return;

        ExpenseRule.Remember(line.Reference, true, ExpenseCategory.General, string.Empty);
        ExpenseRule.Save();
        Refresh();
    }

    private void StopIgnoring(StatementLine line)
    {
        if (line.Rule != null)
            ExpenseRule.Remove(line.Rule.Id);
        ExpenseRule.Save();
        Refresh();
    }

    /// <summary>
    /// takes back an expense that was logged from this line - normally one
    /// the rules logged by themselves against a payee that has turned out not
    /// to be a business cost after all
    /// </summary>
    private async void UndoExpense(StatementLine line)
    {
        if (line.Logged == null)
            return;

        string payee = ExpenseRule.FriendlyMerchant(line.Reference);

        string answer = await DisplayActionSheet(
            $"Remove the {Gloable.CurrenceSymbol}{line.Logged.Amount:0.00} expense for '{payee}'?",
            "Cancel", null,
            "Remove this one only",
            "Remove it and never log this payee again");

        if (answer == null || answer == "Cancel")
            return;

        Expense.Remove(line.Logged.Id);
        Expense.Save();
        line.Logged = null;

        if (answer == "Remove it and never log this payee again")
        {
            ExpenseRule.Remember(line.Reference, true, ExpenseCategory.General, string.Empty);
            ExpenseRule.Save();
        }

        DataRefreshNotifier.NotifyDataChanged();
        Refresh();
    }
}
