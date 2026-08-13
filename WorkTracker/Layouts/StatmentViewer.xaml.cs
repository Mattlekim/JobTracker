namespace UiInterface.Layouts;
using Kernel;
using System.Globalization;
public partial class StatmentViewer : ContentPage
{
	public static CSVFile CsvFile;

	public static int Date = -1, Ref = -1, Amount = -1;
    public static bool DebitAndCreditTogether = false;

    /// <summary>
    /// the column money going out is printed in, for banks that keep money in
    /// and money out in separate columns. -1 when the statement has no such
    /// column, either because the amount column carries both or because the
    /// user said there is not one
    /// </summary>
    public static int Debit = -1;

    /// <summary>a pdf statement has different columns to the same bank's csv, so the two are remembered apart</summary>
    public static int PdfDate = -1, PdfRef = -1, PdfAmount = -1, PdfDebit = -1;
    public static bool PdfDebitAndCreditTogether = false;

    /// <summary>set by whoever loaded the file, before this page is pushed</summary>
    public static bool SourceIsPdf = false;

    /// <summary>
    /// The file is a PayPal export rather than a bank's statement.
    ///
    /// PayPal names its columns the same way every time, so they are read off
    /// the headings instead of being asked for - and they are never saved,
    /// which is what stops a PayPal export overwriting the layout that was
    /// set up for the bank. See ImportExport/PayPalStatement.
    /// </summary>
    public static bool SourceIsPayPal = false;

    public static int PayPalDate = -1, PayPalRef = -1, PayPalAmount = -1;

    /// <summary>
    /// what money coming in on this statement was paid by. money off a PayPal
    /// export came in through PayPal, and saying so is what makes the
    /// payments list and the tax figures tell the truth about it
    /// </summary>
    public static PaymentMethod ImportedPaymentMethod
    {
        get { return SourceIsPayPal ? PaymentMethod.Paypal : PaymentMethod.Bank; }
    }

    /// <summary>
    /// the statement file itself, held to one side by StatementFile so a copy
    /// can be filed under its tax year once the columns are known
    /// </summary>
    public static string SourceFilePath = null;
    public static string SourceFileName = null;

    /// <summary>
    /// set before the page is pushed when the statement was opened to deal
    /// with money going out. the money out page opens by itself once the
    /// columns are known, so the user is not left on the payments list
    /// </summary>
    public static bool OpenMoneyOut = false;

    private readonly bool _isPdf = SourceIsPdf;
    private bool _openMoneyOut = OpenMoneyOut;

    //a PayPal export knows its own columns, so nothing here is ever asked
    //for on one, and nothing is written back
    private readonly bool _isPayPal = SourceIsPayPal;

    private int DateColumn
    {
        get => _isPayPal ? PayPalDate : _isPdf ? PdfDate : Date;
        set { if (_isPayPal) PayPalDate = value; else if (_isPdf) PdfDate = value; else Date = value; }
    }

    private int RefColumn
    {
        get => _isPayPal ? PayPalRef : _isPdf ? PdfRef : Ref;
        set { if (_isPayPal) PayPalRef = value; else if (_isPdf) PdfRef = value; else Ref = value; }
    }

    private int AmountColumn
    {
        get => _isPayPal ? PayPalAmount : _isPdf ? PdfAmount : Amount;
        set { if (_isPayPal) PayPalAmount = value; else if (_isPdf) PdfAmount = value; else Amount = value; }
    }

    private int DebitColumn
    {
        get => _isPayPal ? -1 : _isPdf ? PdfDebit : Debit;
        set { if (!_isPayPal) { if (_isPdf) PdfDebit = value; else Debit = value; } }
    }

    private bool AmountIncludesDebits
    {
        get => _isPayPal ? true : _isPdf ? PdfDebitAndCreditTogether : DebitAndCreditTogether;
        set { if (!_isPayPal) { if (_isPdf) PdfDebitAndCreditTogether = value; else DebitAndCreditTogether = value; } }
    }

    //the columns of whichever statement is open, for pages that only read them
    public static int ActiveDateColumn => SourceIsPayPal ? PayPalDate : SourceIsPdf ? PdfDate : Date;
    public static int ActiveRefColumn => SourceIsPayPal ? PayPalRef : SourceIsPdf ? PdfRef : Ref;
    public static int ActiveAmountColumn => SourceIsPayPal ? PayPalAmount : SourceIsPdf ? PdfAmount : Amount;

    //PayPal signs its gross column, so money out sits in it alongside money
    //in and there is no separate column for it
    public static int ActiveDebitColumn => SourceIsPayPal ? -1 : SourceIsPdf ? PdfDebit : Debit;
    public static bool ActiveAmountIncludesDebits => SourceIsPayPal ? true : SourceIsPdf ? PdfDebitAndCreditTogether : DebitAndCreditTogether;

    private bool _selectingCollums = false;
    private int _currentThingToSelect = 0;

    public static void Reset()
    {
        Date = -1;
        Ref = -1;
        Amount = -1;
        Debit = -1;

        PdfDate = -1;
        PdfRef = -1;
        PdfAmount = -1;
        PdfDebit = -1;
    }

    /// <summary>
    /// whether the loaded statement gives any way of telling money going out
    /// from money coming in
    /// </summary>
    public static bool CanReadMoneyOut()
    {
        if (CsvFile == null || ActiveDateColumn == -1 || ActiveRefColumn == -1)
            return false;

        //one signed column holds both, so a negative is money going out
        if (ActiveAmountIncludesDebits)
            return ActiveAmountColumn != -1;

        return ActiveDebitColumn != -1;
    }

    /// <summary>
    /// Statement amounts turn up as "1,234.56", "£12.00" or "12.00 CR" - none of which Convert reads,
    /// and a csv exported on a machine with another culture will not match this one either.
    /// </summary>
    public static bool TryParseAmount(string text, out decimal amount)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        bool negative = text.StartsWith("-") || text.StartsWith("(")
            || text.EndsWith("DR", StringComparison.OrdinalIgnoreCase);

        //drop the currency symbol, any CR / DR marker and anything else that is not part of the number
        string number = string.Empty;
        foreach (char c in text)
            if (char.IsDigit(c) || c == '.' || c == ',')
                number += c;

        //whichever separator comes last is the decimal point - the rest group the thousands, and
        //which character does which depends on the country the statement came from
        int separator = Math.Max(number.LastIndexOf('.'), number.LastIndexOf(','));
        int decimals = separator == -1 ? 0 : number.Length - separator - 1;

        if (separator != -1 && decimals >= 1 && decimals <= 2)
            number = number.Substring(0, separator).Replace(".", "").Replace(",", "")
                + "." + number.Substring(separator + 1);
        else
            number = number.Replace(".", "").Replace(",", "");

        if (number.Length == 0)
            return false;

        if (!decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            return false;

        if (negative)
            amount = -amount;

        return true;
    }


    private Grid _grid;

    /// <summary>one payment coming in off the statement, and what is known about it</summary>
    private class IncomingLine
    {
        /// <summary>which row of the file it came off, for the import run</summary>
        public int Row;

        public DateTime Date;
        public string DateText = string.Empty;

        /// <summary>the reference exactly as the bank printed it</summary>
        public string Reference = string.Empty;

        public float Amount;

        /// <summary>the customer whose reference this is, when it is recognised</summary>
        public Customer Customer;

        /// <summary>this payment has been brought in already, on this statement or an earlier one</summary>
        public bool AlreadyImported;

        public bool Ignored;
    }

    private List<IncomingLine> _lines = new List<IncomingLine>();

    private void BuildGrid()
    {
        if (_selectingCollums)
        {
            //picking the columns needs the file as the bank wrote it
            vsl_payments.Clear();
            _lines.Clear();
            ShowListChrome(false);
            BuildColumnPicker();
            return;
        }

        ClearColumnPicker();
        ShowListChrome(true);
        ReadStatement();
        BuildList();
    }

    /// <summary>the summary and buttons belong to the list, not the column picker</summary>
    private void ShowListChrome(bool showing)
    {
        l_overview.IsVisible = showing;
        l_explain.IsVisible = showing;
        bnt_import.IsVisible = showing;
    }

    private void ClearColumnPicker()
    {
        if (_grid != null)
            hsl_header.Remove(_grid);
        _grid = null;
    }

    /// <summary>the whole file with a tick box over every column, to point at the ones that matter</summary>
    private void BuildColumnPicker()
    {
        ClearColumnPicker();

        _grid = new Grid() { WidthRequest = 1200, HeightRequest = CsvFile.data.Length * 40 };
        _grid.RowDefinitions.Add(new RowDefinition());
        _grid.RowDefinitions.Add(new RowDefinition());

        for (int i = 0; i < CsvFile.Header.Length; i++)
        {
            CheckBox cb = new CheckBox() { HorizontalOptions = LayoutOptions.Center, ClassId = i.ToString() };
            cb.CheckedChanged += Cb_CheckedChanged;
            _grid.Add(cb, i, 0);
        }

        int c = 0;
        foreach (string s in CsvFile.Header)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition());
            _grid.Add(new Label() { Text = s }, c, 1);
            c++;
        }

        for (int y = 0; y < CsvFile.data.Length; y++)
        {
            _grid.RowDefinitions.Add(new RowDefinition());
            for (int x = 0; x < CsvFile.data[y].Length && x < CsvFile.Header.Length; x++)
                _grid.Add(new Label() { Text = CsvFile.data[y][x] }, x, y + 2);
        }

        hsl_header.Add(_grid);
    }

    /// <summary>pulls the money coming in out of the statement, in the order the bank printed it</summary>
    private void ReadStatement()
    {
        _lines.Clear();

        if (CsvFile == null || CsvFile.data == null || !ColumnsAreValid())
            return;

        for (int y = 0; y < CsvFile.data.Length; y++)
        {
            string[] row = CsvFile.data[y];
            if (row == null)
                continue;

            //a short row is a blank line or a page footer, not a payment
            if (row.Length <= Math.Max(DateColumn, Math.Max(RefColumn, AmountColumn)))
                continue;

            decimal paid;
            if (!TryParseAmount(row[AmountColumn], out paid))
                continue;

            //one signed column carries both, so money out is a negative here
            if (AmountIncludesDebits && paid <= 0)
                continue;

            IncomingLine line = new IncomingLine()
            {
                Row = y,
                DateText = row[DateColumn] ?? string.Empty,
                Reference = row[RefColumn] == null ? string.Empty : row[RefColumn].Trim(),
                Amount = (float)paid,
            };

            if (!StatementText.TryParseDate(line.DateText, out line.Date))
                line.Date = UsfulFuctions.StringToDateTime(line.DateText);

            line.Ignored = Payment.IsIgnored(row[RefColumn]);
            if (!line.Ignored)
            {
                line.Customer = Payment.CustomerForReference(row[RefColumn]);
                line.AlreadyImported = Payment.AlreadyRecorded(row[RefColumn], line.Amount, line.Date);
            }

            _lines.Add(line);
        }
    }

    private void BuildList()
    {
        vsl_payments.Clear();

        int matched = 0, unknown = 0, ignored = 0, already = 0;
        float total = 0;

        foreach (IncomingLine line in _lines)
        {
            if (line.Ignored)
                ignored++;
            else if (line.AlreadyImported)
                already++;
            else if (line.Customer != null)
            {
                matched++;
                total += line.Amount;
            }
            else
                unknown++;

            vsl_payments.Add(BuildRow(line));
        }

        l_nothing.IsVisible = _lines.Count == 0;

        l_overview.Text = _lines.Count == 0
            ? "Nothing coming in on this statement"
            : $"{_lines.Count} payments in. {matched} ready to import ({Gloable.CurrenceSymbol}{total:0.00}), " +
              $"{already} already in, {unknown} not recognised, {ignored} ignored.";
    }

    private View BuildRow(IncomingLine line)
    {
        VerticalStackLayout content = new VerticalStackLayout() { Spacing = 2 };

        content.Add(new Label()
        {
            Text = $"{line.DateText}   {Gloable.CurrenceSymbol}{line.Amount:0.00}",
            FontAttributes = FontAttributes.Bold,
        });

        content.Add(new Label() { Text = line.Reference, FontSize = 13 });

        if (line.Ignored)
            content.Add(new Label() { Text = "Ignored - left out of every statement", FontSize = 12, TextColor = Colors.Grey });
        else if (line.AlreadyImported)
            content.Add(new Label()
            {
                Text = $"Already in - {CustomerName(line)}",
                FontSize = 12,
                TextColor = Colors.Grey,
            });
        else if (line.Customer != null)
            content.Add(new Label()
            {
                Text = $"Goes to {CustomerName(line)}",
                FontSize = 12,
                TextColor = Colors.Green,
            });
        else
            content.Add(new Label()
            {
                Text = "Not recognised - link it to whoever sent it",
                FontSize = 12,
                TextColor = Color.FromArgb("#EF6C00"),
            });

        content.Add(BuildButtons(line));

        Border border = new Border() { Content = content };
        border.Style = (Style)Resources["Card"];
        return border;
    }

    private static string CustomerName(IncomingLine line)
    {
        if (line.Customer == null)
            return "a customer";
        return $"{line.Customer.FName} {line.Customer.FormattedAddress}".Trim();
    }

    private View BuildButtons(IncomingLine line)
    {
        HorizontalStackLayout buttons = new HorizontalStackLayout() { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };

        if (line.Ignored)
        {
            //ignoring is easy to do by accident, and it sticks for every
            //statement from now on - so there has to be a way straight back
            buttons.Add(RowButton("Stop Ignoring", "#EF6C00", (s, e) => StopIgnoring(line)));
            return buttons;
        }

        if (line.Customer == null)
            buttons.Add(RowButton("Link", "#1E88E5", (s, e) => LinkToCustomer(line)));

        if (!line.AlreadyImported)
            buttons.Add(RowButton("Ignore", "#6B7280", (s, e) => Ignore(line)));

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

    private void LinkToCustomer(IncomingLine line)
    {
        LinkCustomerLayout.Reference = line.Reference;
        Navigation.PushAsync(new LinkCustomerLayout());
    }

    private void Ignore(IncomingLine line)
    {
        Payment.IgnorePaymentList.Add(line.Reference);
        Payment.Save();
        BuildGrid();
    }

    private void StopIgnoring(IncomingLine line)
    {
        Payment.StopIgnoring(line.Reference);
        Payment.Save();
        BuildGrid();
    }

    private async void UpdateFields()
    {
        if (await DisplayAlert("Debits / Credits same field", "Are the credits and debits of this statment part of the same field?", "Yes", "No"))
        {
            //one signed column: money out is simply a negative amount
            AmountIncludesDebits = true;
            DebitColumn = -1;
            l_nextField.IsVisible = false;
            FinishColumnSelection();
            return;
        }

        AmountIncludesDebits = false;

        //money in and money out are kept apart, so the money out column has
        //to be pointed at as well before the expenses can be read
        l_nextField.IsVisible = true;
        l_nextField.Text = "Select the money out / paid out field";
        bnt_noMoneyOut.IsVisible = true;
        await DisplayAlert("Select Money Out",
            "Select the money out / paid out field, so outgoings can be flagged as expenses. Press 'No Money Out Column' if this statement has not got one.", "Ok");
    }

    /// <summary>the statement has money in only - there is nothing to flag as an expense</summary>
    private void bnt_noMoneyOut_Clicked(object sender, EventArgs e)
    {
        DebitColumn = -1;
        FinishColumnSelection();
    }

    private void FinishColumnSelection()
    {
        _selectingCollums = false;
        _currentThingToSelect = 0;
        l_nextField.IsVisible = false;
        bnt_noMoneyOut.IsVisible = false;
        Settings.Save();
        BuildGrid();
        ShowMoneyOutState();
        ArchiveStatement();
        TryOpenMoneyOut();
    }

    private void Cb_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
            return;

        CheckBox cb = sender as CheckBox;
        int i = Convert.ToInt32(cb.ClassId);
        string s = string.Empty;

        //the last column finishes the selection, which throws this grid away
        //and builds the statement one - so it has to wait until the marker
        //has been put on the column that was just picked
        bool lastColumn = false;

        switch(_currentThingToSelect)
        {
            case 0: //date
                DateColumn = i;
                DisplayAlert("Select Reference", "Select the reference field", "Ok");
                l_nextField.Text = "Select the reference field";
                s = "Date";
                break;
            case 1: //ref
                RefColumn = i;
                DisplayAlert("Select Amount", "Select the amount paid / credit", "Ok");
                l_nextField.Text = "Select the amount paid / credit";
                s = "Reference";
                break;
            case 2: //amount
                AmountColumn = i;
                s = "Amount";
                UpdateFields();
                break;
            case 3: //money out, when the bank keeps it in its own column
                DebitColumn = i;
                s = "Money Out";
                lastColumn = true;
                break;
        }
        cb.IsChecked = false;
        cb.IsVisible = false;
        _grid.Add(new Label() { Text = s, BackgroundColor = Colors.OrangeRed }, i, 0);
        _currentThingToSelect++;

        if (lastColumn)
            FinishColumnSelection();
    }

    public StatmentViewer()
	{
		//date ref amount
		InitializeComponent();

        NavigatedTo += StatmentViewer_NavigatedTo;

        //a pdf rebuilds its own headings, so the columns can usually be picked out without asking
        if (_isPdf && !ColumnsAreValid())
            GuessColumnsFromHeader();

        OpenMoneyOut = false;

        AskForColumnsIfNeeded();

        BuildGrid();
        ShowMoneyOutState();
        ArchiveStatement();
        Skip = true;
    }

    private void ShowMoneyOutState()
    {
        bnt_moneyOut.IsVisible = !_selectingCollums && CsvFile != null;
        l_noMoneyOut.IsVisible = !_selectingCollums && !CanReadMoneyOut();
    }

    private bool _archived = false;

    /// <summary>
    /// keeps a copy of the statement, filed under the tax year most of it
    /// falls in. the statement is the evidence the figures came off, so it is
    /// kept with that year's receipts and goes into its backup.
    /// only possible once the date column is known, which is why it happens
    /// here rather than when the file was picked
    /// </summary>
    private void ArchiveStatement()
    {
        if (_archived || _selectingCollums || CsvFile == null)
            return;

        if (DateColumn < 0 || string.IsNullOrEmpty(SourceFilePath))
            return;

        _archived = true;

        try
        {
            List<DateTime> dates = new List<DateTime>();
            foreach (string[] row in CsvFile.data)
            {
                if (row == null || row.Length <= DateColumn)
                    continue;

                if (StatementText.TryParseDate(row[DateColumn], out DateTime date))
                    dates.Add(date);
            }

            //a statement that runs across 5 April is kept in both tax years.
            //Keep leaves alone any year it is already filed in, so picking
            //the same file twice does not make a second copy
            if (StatementRecord.Keep(SourceFilePath, SourceFileName, dates).Count > 0)
                StatementRecord.Save();
        }
        catch
        {
            //failing to keep the file must not stop the import - the figures
            //read off it matter more
        }
    }

    /// <summary>true once the money out column has been asked about, so a statement without one is not asked about again and again</summary>
    private bool _askedForMoneyOut = false;

    /// <summary>
    /// opens the money out page by itself when the statement was imported to
    /// deal with expenses. a statement set up before money out could be read
    /// has no money out column yet, so that gets asked for first
    /// </summary>
    private void TryOpenMoneyOut()
    {
        if (!_openMoneyOut || _selectingCollums)
            return;

        if (CanReadMoneyOut())
        {
            _openMoneyOut = false;
            Navigation.PushAsync(new StatementExpenses());
            return;
        }

        if (_askedForMoneyOut || !ColumnsAreValid())
        {
            _openMoneyOut = false;
            return;
        }

        _askedForMoneyOut = true;
        StartMoneyOutSelection();
    }

    private void bnt_moneyOut_Clicked(object sender, EventArgs e)
    {
        if (CanReadMoneyOut())
        {
            Navigation.PushAsync(new StatementExpenses());
            return;
        }

        _askedForMoneyOut = true;
        StartMoneyOutSelection();
    }

    /// <summary>
    /// puts the column pickers back up for the money out column on its own,
    /// for a statement whose other columns were set up before outgoings could
    /// be read
    /// </summary>
    private void StartMoneyOutSelection()
    {
        _selectingCollums = true;
        _currentThingToSelect = 3;

        l_nextField.IsVisible = true;
        l_nextField.Text = "Select the money out / paid out field";
        bnt_noMoneyOut.IsVisible = true;

        BuildGrid();
        ShowMoneyOutState();
    }

    private bool ColumnsAreValid()
    {
        if (DateColumn == -1 || RefColumn == -1 || AmountColumn == -1)
            return false;

        if (CsvFile != null && CsvFile.Header != null)
            if (DateColumn >= CsvFile.Header.Length || RefColumn >= CsvFile.Header.Length
                || AmountColumn >= CsvFile.Header.Length)
                return false;

        if (DateColumn == RefColumn || DateColumn == AmountColumn || RefColumn == AmountColumn)
            return false;

        return true;
    }

    /// <summary>the headings a pdf statement prints are plain english, so read them rather than ask</summary>
    private void GuessColumnsFromHeader()
    {
        if (CsvFile == null || CsvFile.Header == null)
            return;

        for (int i = 0; i < CsvFile.Header.Length; i++)
        {
            string h = CsvFile.Header[i].ToLowerInvariant();

            if (DateColumn == -1 && h.Contains("date"))
                DateColumn = i;

            if (RefColumn == -1 && (h.Contains("desc") || h.Contains("ref") || h.Contains("detail")
                || h.Contains("narrative") || h.Contains("payee") || h.Contains("particulars")))
                RefColumn = i;

            //what money coming in is called varies - paid in, credit, receipts, money in
            if (AmountColumn == -1 && (h.Contains("paid in") || h.Contains("credit") || h.Contains("receipt")
                || h.Contains("money in") || h.Trim() == "in"))
            {
                AmountColumn = i;
                AmountIncludesDebits = false; //a money in column only ever holds money coming in
            }
        }

        //a single signed amount column, which the viewer then has to filter the debits out of
        if (AmountColumn == -1)
            for (int i = 0; i < CsvFile.Header.Length; i++)
                if (CsvFile.Header[i].ToLowerInvariant().Contains("amount") && i != DateColumn && i != RefColumn)
                {
                    AmountColumn = i;
                    AmountIncludesDebits = true;
                    break;
                }

        //money going out, so outgoings can be flagged as expenses. only
        //looked for when money in has its own column - a signed amount column
        //already carries both
        if (!AmountIncludesDebits && DebitColumn == -1)
            for (int i = 0; i < CsvFile.Header.Length; i++)
            {
                string h = CsvFile.Header[i].ToLowerInvariant();
                if (i == DateColumn || i == RefColumn || i == AmountColumn)
                    continue;

                if (h.Contains("paid out") || h.Contains("debit") || h.Contains("money out")
                    || h.Contains("withdraw") || h.Contains("payment") || h.Trim() == "out")
                {
                    DebitColumn = i;
                    break;
                }
            }

        if (!ColumnsAreValid()) //a partial guess is worse than none - fall back to asking
        {
            DateColumn = -1;
            RefColumn = -1;
            AmountColumn = -1;
            DebitColumn = -1;
        }
    }

    /// <summary>said once per import, so a PayPal export is not a mystery</summary>
    private bool _saidItIsPayPal = false;

    private void AskForColumnsIfNeeded()
    {
        if (ColumnsAreValid())
        {
            _selectingCollums = false;

            if (_isPayPal && !_saidItIsPayPal)
            {
                _saidItIsPayPal = true;
                DisplayAlert("PayPal Statement",
                    "This is a PayPal export, so the columns have been read straight off it - the bank's own columns are left as they were.\n\n" +
                    "Money in is matched to customers and goes down as paid by PayPal. PayPal's fees are in a column of their own and are not brought in, so put those in as an expense off the statement itself.",
                    "Ok");
            }

            return;
        }

        _selectingCollums = true;
        _currentThingToSelect = 0;
        bnt_noMoneyOut.IsVisible = false;
        DisplayAlert("Select Date", "Select the date field", "Ok");
        l_nextField.IsVisible = true;
        l_nextField.Text = "Select the date field";
    }

    private List<Payment> payments = new List<Payment>();
    private void bnt_importPayments(object sender, EventArgs e)
    {
        payments.Clear();
        List<string> already = new List<string>();
        int unmatched = 0;

        try
        {
            //the same lines the list was built from, so what comes in is
            //exactly what it said would come in
            foreach (IncomingLine line in _lines)
            {
                if (line.Ignored)
                    continue;

                bool customerFound;
                Payment pay = Payment.AddToCustomer(line.Reference, line.Amount, line.Date, ImportedPaymentMethod, out customerFound);

                if (pay != null)
                    payments.Add(pay);
                else if (customerFound)
                    already.Add($"{line.DateText} {line.Reference} {Gloable.CurrenceSymbol}{line.Amount:0.00}");
                else
                    unmatched++;
            }

            string msg = string.Empty;
            foreach (Payment p in payments)
            {
                Customer c = p.GetCustomer();
                if (c != null)
                    msg += $"{c.Address.PropertyNameNumber} {c.Address.Street} {c.Address.Area} has paid\n";
            }

            foreach (string s in already)
                msg += $"{s} was already in\n";

            if (unmatched > 0)
                msg += $"{unmatched} payments not matched to a customer";

            DisplayAlert($"Imported {payments.Count} payments. {already.Count} already in", msg, "Ok");

            //the badges change once the payments are in
            BuildGrid();
        }
        catch
        {
            DisplayAlert("Error", "There was an error with import. Error Code 1001", "Ok");
        }
    }

    private bool Skip = false;
    
    private void StatmentViewer_NavigatedTo(object sender, NavigatedToEventArgs e)
    {
        if (Skip)
        {
            Skip = false;
            TryOpenMoneyOut();
            return;
        }
        AskForColumnsIfNeeded();
        BuildGrid();
        ShowMoneyOutState();
    }
}