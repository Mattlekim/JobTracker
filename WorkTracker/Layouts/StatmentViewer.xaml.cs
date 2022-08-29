namespace UiInterface.Layouts;
using Kernel;
public partial class StatmentViewer : ContentPage
{
    public static CSVFile CsvFile;

    public static int Date = -1, Ref = -1, Amount = -1;
    public static bool DebitAndCreditTogether = false;

    private bool _selectingCollums = false;
    private int _currentThingToSelect = 0;

    private List<int> _paymentsToProcess = new List<int>();
    public static void Reset()
    {
        Date = -1;
        Ref = -1;
        Amount = -1;
      
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
        translator[0] = Date;
        translator[1] = Ref;
        translator[2] = Amount;

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
                if (Date == translator[i])
                    _grid.Add(new Label() { Text = "Date" }, i, 1);

                if (Ref == translator[i])
                    _grid.Add(new Label() { Text = "Reference" }, i, 1);

                if (Amount == translator[i])
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

                if (CsvFile.data[y][Amount] == null || CsvFile.data[y][Amount] == String.Empty)
                    continue;

                if (DebitAndCreditTogether)
                    if (Convert.ToDecimal(CsvFile.data[y][Amount]) <= 0)
                        continue;


                if (Payment.IgnorePaymentList != null)
                    foreach (string s in Payment.IgnorePaymentList)
                    {
                        if (s == CsvFile.data[y][Ref])
                        {
                            ingnore = true;
                            break;
                        }
                    }

                if (!ingnore)
                    foreach (Customer cust in Customer.Query())
                    {
                        foreach (string s in cust.PaymentRefrences)
                            if (s == CsvFile.data[y][Ref])
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
                            _grid.Add(new Label() { Text = CsvFile.data[y][translator[x]], Padding = 2, TextColor = Colors.Green }, x, row + 2);
                        else
                            if (ingnore)
                            _grid.Add(new Label() { Text = CsvFile.data[y][translator[x]], Padding = 2, TextColor = Colors.Grey }, x, row + 2);
                        else
                            if (x == 0)
                            _grid.Add(new Label() { Text = CsvFile.data[y][translator[x]], TextColor=Colors.LightGray, Padding = 2 }, x, row + 2); //the date coloum
                        else
                            _grid.Add(new Label() { Text = CsvFile.data[y][translator[x]], Padding = 2 }, x, row + 2);
                }
                //if (add)
                if (ingnore)
                {
                    _grid.Add(new Label() { Text = "Ingnored" }, 3, row + 2);
                }
                else
                if (linked)
                {
                    HorizontalStackLayout hsl = new HorizontalStackLayout();
                 //   hsl.Add(new Label() { Text = "Linked" });

                    _paymentsToProcess.Add(y);
                    Button b = new Button()
                    {
                        Text = "Remove Link",
                        BackgroundColor = Colors.Transparent,
                        BorderColor = Colors.Red,
                        BorderWidth = 2,
                        TextColor = Colors.Red,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Center,
                        Padding = 4,
                        ClassId = CsvFile.data[y][Ref],

                    };
                    b.Clicked += bnt_RemoveLink_Clicked;
                    // _grid.Add(b, 3, row + 2);
                    hsl.Add(b);

                    _grid.Add(hsl, 3, row + 2);
                    _paymentsToProcess.Add(y);
                }
                else
                {
                    HorizontalStackLayout hsl = new HorizontalStackLayout();
                    _paymentsToProcess.Add(y);
                    Button b = new Button()
                    {
                        Text = "Link",
                        BackgroundColor = Colors.Transparent,
                        BorderColor = Colors.Green,
                        BorderWidth = 2,
                        TextColor = Colors.Green,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Center,
                        Padding = 4,
                        ClassId = CsvFile.data[y][Ref],

                    };
                    b.Clicked += B_Clicked;
                    // _grid.Add(b, 3, row + 2);
                    hsl.Add(b);

                    b = new Button()
                    {
                        Text = "Ignore",
                        BackgroundColor = Colors.Transparent,
                        BorderColor = Colors.Blue,
                        BorderWidth = 2,
                        TextColor = Colors.Blue,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Padding = 4,
                        ClassId = CsvFile.data[y][Ref],

                    };
                    b.Clicked += bnt_ignore;
                    //_grid.Add(b, 3, row + 2);
                    hsl.Add(b);

                    _grid.Add(hsl, 3, row + 2);
                }
                row++;
            }
        else
            for (int y = 0; y < CsvFile.data.Length; y++)
            {
                _grid.RowDefinitions.Add(new RowDefinition());
                for (int x = 0; x < CsvFile.data[x].Length; x++)
                {
                    _grid.Add(new Label() { Text = CsvFile.data[y][x] }, x, y + 2);
                }
            }

        hsl_header.Add(_grid);
    }

    private async void bnt_RemoveLink_Clicked(object sender, EventArgs e)
    {
        Button b = sender as Button;

        LinkCustomerLayout.Reference = b.ClassId;

        if (await DisplayAlert("Confirm", $"Are you sure you wish to remove the link for {b.ClassId}?", "Remove Link", "Cancel"))
        {
            Customer c = Customer.Query().FirstOrDefault(x=>x.PaymentRefrences.Contains(b.ClassId));
            if (c!= null)
            {
                c.PaymentRefrences.Remove(b.ClassId);

                BuildGrid();
            }
        }
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
            DebitAndCreditTogether = true;
            l_nextField.IsVisible = false;
        }
        else
            DebitAndCreditTogether = false;

        _selectingCollums = false;
        Settings.Save();
        BuildGrid();
    }
    private void Cb_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
            return;

        CheckBox cb = sender as CheckBox;
        int i = Convert.ToInt32(cb.ClassId);
        string s = string.Empty;
        switch(_currentThingToSelect)
        {
            case 0: //date
                Date = i;
                DisplayAlert("Select Reference", "Select the reference field", "Ok");
                l_nextField.Text = "Select the reference field";
                s = "Date";
                break;
            case 1: //ref
                Ref = i;
                DisplayAlert("Select Amount", "Select the amount paid / credit", "Ok");
                l_nextField.Text = "Select the amount paid / credit";
                s = "Reference";
                break;
            case 2: //amount
                Amount = i;
                s = "Amount";
                UpdateFields();
                break;
         
        }
        cb.IsChecked = false;
        cb.IsVisible = false;
        _grid.Add(new Label() { Text = s, BackgroundColor = Colors.OrangeRed }, i, 0);
        _currentThingToSelect++;
    }

    public StatmentViewer()
    {
        //date ref amount
        InitializeComponent();

        NavigatedTo += StatmentViewer_NavigatedTo;


        //now lets detect whats what

        /*	for (int i = 0; i < CsvFile.Header.Length; i++)
            {
                if (CsvFile.Header[i].ToLower().Contains("date"))
                    Date = i;

                if (CsvFile.Header[i].ToLower().Contains("desc") || CsvFile.Header[i].ToLower().Contains("ref"))
                    Ref = i;

                if (CsvFile.Header[i].ToLower().Contains("credit"))
                    Amount = i;

                if (CsvFile.Header[i].ToLower().Contains("debit"))
                    Debit = i;
            }
        */
        //		cv_items.ItemsLayout	


        bool invalidData = false;
        if (Date == -1 || Ref == -1 || Amount == -1)
            invalidData = true;

        if (Date == Ref || Date == Amount)
            invalidData = true;

        if (Ref == Amount || Date == Amount)
            invalidData = true;

        if (invalidData)
        {
            _selectingCollums = true;
            _currentThingToSelect = 0;
            DisplayAlert("Select Date", "Select the date field", "Ok");
            l_nextField.IsVisible = true;
            l_nextField.Text = "Select the date field";
        }
        else
            _selectingCollums = false;

        BuildGrid();
        Skip = true;
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
            foreach (int i in _paymentsToProcess)
            {
                dt = UsfulFuctions.StringToDateTime(CsvFile.data[i][Date]);
                pay = Payment.AddToCustomer(CsvFile.data[i][Ref], (float)Convert.ToDouble(CsvFile.data[i][Amount]), dt, PaymentMethod.Bank, out customerFound);
                if (pay != null)
                    payments.Add(pay);
                else
                    if (customerFound)
                    failed.Add($"{CsvFile.data[i][Date]} {CsvFile.data[i][Ref]} {Gloable.CurrenceSymbol}{(float)Convert.ToDouble(CsvFile.data[i][Amount])}");
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
            return;
        }
        bool invalidData = false;
        if (Date == -1 || Ref == -1 || Amount == -1)
            invalidData = true;

        if (Date == Ref || Date == Amount)
            invalidData = true;

        if (Ref == Amount || Date == Amount)
            invalidData = true;

        if (invalidData)
        {
            _selectingCollums = true;
            _currentThingToSelect = 0;
            DisplayAlert("Select Date", "Select the date field", "Ok");
            l_nextField.IsVisible = true;
            l_nextField.Text = "Select the date field";
        }
        else
            _selectingCollums = false;

        BuildGrid();
    }
}