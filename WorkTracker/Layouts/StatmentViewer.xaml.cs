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

    private int DateColumn
    {
        get => _isPdf ? PdfDate : Date;
        set { if (_isPdf) PdfDate = value; else Date = value; }
    }

    private int RefColumn
    {
        get => _isPdf ? PdfRef : Ref;
        set { if (_isPdf) PdfRef = value; else Ref = value; }
    }

    private int AmountColumn
    {
        get => _isPdf ? PdfAmount : Amount;
        set { if (_isPdf) PdfAmount = value; else Amount = value; }
    }

    private int DebitColumn
    {
        get => _isPdf ? PdfDebit : Debit;
        set { if (_isPdf) PdfDebit = value; else Debit = value; }
    }

    private bool AmountIncludesDebits
    {
        get => _isPdf ? PdfDebitAndCreditTogether : DebitAndCreditTogether;
        set { if (_isPdf) PdfDebitAndCreditTogether = value; else DebitAndCreditTogether = value; }
    }

    //the columns of whichever statement is open, for pages that only read them
    public static int ActiveDateColumn => SourceIsPdf ? PdfDate : Date;
    public static int ActiveRefColumn => SourceIsPdf ? PdfRef : Ref;
    public static int ActiveAmountColumn => SourceIsPdf ? PdfAmount : Amount;
    public static int ActiveDebitColumn => SourceIsPdf ? PdfDebit : Debit;
    public static bool ActiveAmountIncludesDebits => SourceIsPdf ? PdfDebitAndCreditTogether : DebitAndCreditTogether;

    private bool _selectingCollums = false;
    private int _currentThingToSelect = 0;

    private List<int> _paymentsToProcess = new List<int>();
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

    

    private void BuildGrid()
    {
        _paymentsToProcess.Clear();

        if (_grid != null)
            hsl_header.Remove(_grid);

        if (_selectingCollums)
            _grid = new Grid() { WidthRequest = 1200, HeightRequest = CsvFile.data.Length * 40};
        else
            _grid = new Grid() { };

        int rows = CsvFile.data.Length;

       
        _grid.RowDefinitions.Add(new RowDefinition());
        _grid.RowDefinitions.Add(new RowDefinition());


        if (_selectingCollums)
        {
            for (int i = 0; i < CsvFile.Header.Length; i++)
            {
                CheckBox cb = new CheckBox() { HorizontalOptions = LayoutOptions.Center, ClassId = i.ToString() };
                cb.CheckedChanged += Cb_CheckedChanged;
                _grid.Add(cb, i, 0);
            }
        }
        else
        {
     //    _grid.Add(new Label() { Text = "Date", BackgroundColor = Colors.Orange }, Date, 0);
      //    _grid.Add(new Label() { Text = "Reference", BackgroundColor = Colors.Orange }, Ref, 0);
        //  _grid.Add(new Label() { Text = "Credit", BackgroundColor = Colors.Orange }, Amount, 0);


        //  _grid.Add(new Label() { Text = "Debit", BackgroundColor = Colors.Orange }, Debit, 0);
       
        }

        if (_selectingCollums)
            foreach (string s in CsvFile.Header)
            {
                _grid.ColumnDefinitions.Add(new ColumnDefinition());
            }
        else
        {

            _grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0.5,GridUnitType.Star)});
            _grid.ColumnDefinitions.Add(new ColumnDefinition());
            _grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0.5, GridUnitType.Star)});
            _grid.ColumnDefinitions.Add(new ColumnDefinition());
       
        }


        int[] translator = new int[3];
        translator[0] = DateColumn;
        translator[1] = RefColumn;
        translator[2] = AmountColumn;

        int c = 0;
        if (_selectingCollums)
            foreach (string s in CsvFile.Header)
            {

                _grid.Add(new Label() { Text = s }, c, 1);
                c++;
            }
        else
        {
            for (int i = 0; i <3; i++)
            {
                if (DateColumn == translator[i])
                    _grid.Add(new Label() { Text = "Date" }, i, 1);

                if (RefColumn == translator[i])
                    _grid.Add(new Label() { Text = "Reference" }, i, 1);

                if (AmountColumn == translator[i])
                    _grid.Add(new Label() { Text = "Amount" }, i, 1);
            }
        }

        bool add = false;
        int width = 0;

        int row = 0;
        bool linked = false;
        bool ingnore = false;
        if (!_selectingCollums)

            for (int y = 0; y < CsvFile.data.Length; y++)
            {
                add = false;
                linked = false;
                ingnore = false;

                //a short row is a blank line or a page footer, not a payment
                if (CsvFile.data[y].Length <= Math.Max(DateColumn, Math.Max(RefColumn, AmountColumn)))
                    continue;

                decimal paid;
                if (!TryParseAmount(CsvFile.data[y][AmountColumn], out paid))
                    continue;

                if (AmountIncludesDebits)
                    if (paid <= 0)
                        continue;


                if (Payment.IgnorePaymentList != null)
                    foreach (string s in Payment.IgnorePaymentList)
                    {
                        if (s == CsvFile.data[y][RefColumn])
                        {
                            ingnore = true;
                            break;
                        }
                    }

                if (!ingnore)
                    foreach (Customer cust in Customer.Query())
                    {
                        foreach (string s in cust.PaymentRefrences)
                            if (s == CsvFile.data[y][RefColumn])
                            {
                                linked = true;
                                break;
                            }
                        if (linked)
                            break;
                    }

              

                _grid.RowDefinitions.Add(new RowDefinition());
                for (int x = 0; x < 4; x++)
                {
                    if (x < 3)
                        if (linked)
                            _grid.Add(new Label() { Text = CsvFile.data[y][translator[x]], TextColor = Colors.Green }, x, row + 2);
                        else
                            if (ingnore)
                            _grid.Add(new Label() { Text = CsvFile.data[y][translator[x]], TextColor = Colors.Grey }, x, row + 2);
                        else
                            _grid.Add(new Label() { Text = CsvFile.data[y][translator[x]] }, x, row + 2);
                }
                //if (add)
                if (ingnore)
                {
                    _grid.Add(new Label() { Text = "Ingnored" }, 3, row + 2);
                }
                else
                if (linked)
                {
                    _grid.Add(new Label() { Text = "Already Linked"}, 3, row + 2);
                    _paymentsToProcess.Add(y);
                }
                else
                {
                    _paymentsToProcess.Add(y);
                    Button b = new Button()
                    {
                        Text = "Link",
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Center,
                        Padding = 4,
                        ClassId = CsvFile.data[y][RefColumn],

                    };
                    b.Clicked += B_Clicked;
                    _grid.Add(b, 3, row + 2);


                    b = new Button()
                    {
                        Text = "Ignore",
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Padding = 4,
                        ClassId = CsvFile.data[y][RefColumn],

                    };
                    b.Clicked += bnt_ignore;
                    _grid.Add(b, 3, row + 2);
                }
                row++;
            }
        else
            for (int y = 0; y < CsvFile.data.Length; y++)
            {
                _grid.RowDefinitions.Add(new RowDefinition());
                for (int x = 0; x < CsvFile.data[y].Length && x < CsvFile.Header.Length; x++)
                {
                    _grid.Add(new Label() { Text = CsvFile.data[y][x] }, x, y + 2);
                }
            }

        hsl_header.Add(_grid);
    }

    private void bnt_ignore(object sender, EventArgs e)
    {
        Button b = sender as Button;

        Payment.IgnorePaymentList.Add(b.ClassId);
        BuildGrid();
        Payment.Save();
    }

    private void B_Clicked(object sender, EventArgs e)
    {
        Button b = sender as Button;

        LinkCustomerLayout.Reference = b.ClassId;
        Navigation.PushAsync(new LinkCustomerLayout());
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

            //the same file picked twice keeps the copy already filed
            long size = new FileInfo(SourceFilePath).Length;
            if (StatementRecord.FindSameFile(SourceFileName, size) != null)
                return;

            if (StatementRecord.Keep(SourceFilePath, SourceFileName, dates) != null)
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

    private void AskForColumnsIfNeeded()
    {
        if (ColumnsAreValid())
        {
            _selectingCollums = false;
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
        DateTime dt;
        payments.Clear();
        Payment pay = null;
        bool customerFound = false;
        List<string> failed = new List<string>();
        int unmatch = 0;

        try
        {
            decimal amount;
            foreach (int i in _paymentsToProcess)
            {
                if (!TryParseAmount(CsvFile.data[i][AmountColumn], out amount))
                    continue;

                dt = UsfulFuctions.StringToDateTime(CsvFile.data[i][DateColumn]);
                pay = Payment.AddToCustomer(CsvFile.data[i][RefColumn], (float)amount, dt, PaymentMethod.Bank, out customerFound);
                if (pay != null)
                    payments.Add(pay);
                else
                    if (customerFound)
                    failed.Add($"{CsvFile.data[i][DateColumn]} {CsvFile.data[i][RefColumn]} {Gloable.CurrenceSymbol}{amount}");
                else
                    unmatch++;


            }

            string msg = string.Empty;
            string text = string.Empty;
            Customer c;
            foreach (Payment p in payments)
            {
                c = p.GetCustomer();
                msg += $"{c.Address.PropertyNameNumber} {c.Address.Street} {c.Address.Area} has paid\n";
            }

            foreach (string s in failed)
                msg += $"{s} has already been added\n";

            if (unmatch > 0)
                msg += $"{unmatch} payments not matched to customer";

            DisplayAlert($"Imported {payments.Count} payments. {failed.Count} not imported", msg, "Ok");

            
        }
        catch
        {
            DisplayAlert("Error", "There was an error with import. Error Code 1001", "Ok");
        }
        //Payment.AddToCustomer()
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