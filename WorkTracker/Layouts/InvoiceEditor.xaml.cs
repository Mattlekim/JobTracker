namespace UiInterface.Layouts;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Kernel;

/// <summary>
/// One editable line while an invoice is being written. It carries the boxes
/// as text and works its own total out as they are typed, so the row shows the
/// line total and the page can add the lines up live. It is turned into a
/// plain <see cref="InvoiceLine"/> when the invoice is saved.
///
/// A blank quantity counts as one and a blank price as nothing, so a line
/// half filled in still reads sensibly rather than blowing up.
/// </summary>
public class InvoiceLineEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>the page hooks this to add the lines up again</summary>
    public Action Changed;

    private string _description = string.Empty;
    private string _quantity = "1";
    private string _unitPrice = string.Empty;

    public string Description
    {
        get { return _description; }
        set
        {
            _description = value ?? string.Empty;
            if (Changed != null) Changed();
        }
    }

    public string Quantity
    {
        get { return _quantity; }
        set
        {
            _quantity = value ?? string.Empty;
            Raise(nameof(LineTotalText));
            if (Changed != null) Changed();
        }
    }

    public string UnitPrice
    {
        get { return _unitPrice; }
        set
        {
            _unitPrice = value ?? string.Empty;
            Raise(nameof(LineTotalText));
            if (Changed != null) Changed();
        }
    }

    public float QuantityValue { get { return Parse(_quantity, 1); } }

    public float UnitPriceValue { get { return Parse(_unitPrice, 0); } }

    public float LineTotal { get { return QuantityValue * UnitPriceValue; } }

    public string LineTotalText
    {
        get { return $"{Gloable.CurrenceSymbol}{LineTotal:0.00}"; }
    }

    private static float Parse(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;
        float value;
        if (float.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
            return value;
        if (float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return value;
        return fallback;
    }

    public InvoiceLine ToLine()
    {
        return new InvoiceLine()
        {
            Description = Description.Trim(),
            Quantity = QuantityValue,
            UnitPrice = UnitPriceValue,
        };
    }

    private void Raise(string name)
    {
        if (PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs(name));
    }
}

public partial class InvoiceEditor : ContentPage
{
    private readonly Invoice _invoice;
    private readonly bool _isNew;
    private readonly ObservableCollection<InvoiceLineEntry> _lines = new ObservableCollection<InvoiceLineEntry>();

    /// <summary>true while the boxes are being set up, so that does not count as editing</summary>
    private bool _loading = true;

    /// <summary>something has been typed that is not on disk yet</summary>
    private bool _dirty = false;

    /// <summary>we are on the way out, so back does not ask twice</summary>
    private bool _leaving = false;

    /// <summary>edits an existing invoice, or writes a new blank one</summary>
    public InvoiceEditor() : this(new Invoice() { Date = DateTime.Now.Date }, true)
    {
    }

    public InvoiceEditor(Invoice invoice, bool isNew)
    {
        InitializeComponent();

        _invoice = invoice ?? new Invoice() { Date = DateTime.Now.Date };
        _isNew = isNew;

        Populate();

        //once the form is filled in, anything touched marks it unsaved. these
        //are subscribed after Populate so setting the boxes up is not counted
        //as a change
        e_billName.TextChanged += MarkDirty;
        e_billAddress.TextChanged += MarkDirty;
        e_notes.TextChanged += MarkDirty;
        dp_date.DateSelected += MarkDirty;
        dp_due.DateSelected += MarkDirty;
        _loading = false;

        //the nav bar arrow has to ask as well, not just the phone's own back
        //button - an invoice typed and left is lost the same either way
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(() => AskThenLeave())
        });
    }

    /// <summary>
    /// A draft invoice made from a job, ready for the editor - billed to the
    /// job's customer with one line for the work. It is not saved: the editor
    /// saves it, so backing out changes nothing.
    /// </summary>
    public static Invoice DraftForJob(Job job)
    {
        Invoice invoice = new Invoice() { Date = DateTime.Now.Date };
        if (job == null)
            return invoice;

        invoice.CustomerId = job.CustomerId;

        Customer c = Customer.ById(job.CustomerId);
        if (c != null)
        {
            invoice.BillToName = $"{c.FName} {c.SName}".Trim();
            invoice.BillToAddress = AddressBlock(c.Address);
        }

        //the price the house is charged as things stand, not off whatever
        //visit happened to open the page - the same figure the customer page
        //shows
        float price = job.CurrentPrice;

        string what = string.IsNullOrWhiteSpace(job.Name) ? "Window cleaning" : job.Name;
        string where = job.Address == null ? string.Empty : $" - {job.Address}".TrimEnd();

        invoice.Lines.Add(new InvoiceLine()
        {
            Description = (what + where).Trim(),
            Quantity = 1,
            UnitPrice = price,
        });

        return invoice;
    }

    /// <summary>an address as the lines it should print on, real not screenshot</summary>
    private static string AddressBlock(Location address)
    {
        if (address == null)
            return string.Empty;

        List<string> lines = new List<string>();
        void Add(string part)
        {
            if (!string.IsNullOrWhiteSpace(part))
                lines.Add(part.Trim());
        }

        Add($"{address.PropertyNameNumber} {address.Street}".Trim());
        Add(address.City);
        Add(address.Area);
        Add(address.Postcode);

        return string.Join("\n", lines);
    }

    private void Populate()
    {
        l_invoiceNumber.Text = _isNew
            ? $"INV-{Invoice.PeekNextNumber():0000}"
            : _invoice.FormattedNumber;

        l_noBusiness.IsVisible = !BusinessInfo.IsSetUp;

        dp_date.Date = _invoice.Date > DateTime.MinValue ? _invoice.Date : DateTime.Now.Date;

        bool hasDue = _invoice.DueDate > DateTime.MinValue;
        cb_hasDue.IsChecked = hasDue;
        dp_due.IsVisible = hasDue;
        dp_due.Date = hasDue ? _invoice.DueDate : DateTime.Now.Date.AddDays(14);

        e_billName.Text = _invoice.BillToName;
        e_billAddress.Text = _invoice.BillToAddress;
        e_notes.Text = _invoice.Notes;

        if (_invoice.Lines != null)
            foreach (InvoiceLine line in _invoice.Lines)
                _lines.Add(Wrap(line));

        //an invoice always opens with at least one line to type into
        if (_lines.Count == 0)
            _lines.Add(Wrap(new InvoiceLine()));

        BindableLayout.SetItemsSource(sl_lines, _lines);
        RecomputeTotal();
    }

    private InvoiceLineEntry Wrap(InvoiceLine line)
    {
        InvoiceLineEntry entry = new InvoiceLineEntry()
        {
            Description = line.Description,
            //a fresh line comes in with the boxes as they read: 1 and blank
            Quantity = line.Quantity.ToString("0.##", CultureInfo.CurrentCulture),
            UnitPrice = line.UnitPrice > 0 ? line.UnitPrice.ToString("0.00", CultureInfo.CurrentCulture) : string.Empty,
        };
        entry.Changed = OnLineChanged;
        return entry;
    }

    /// <summary>a line was edited: mark unsaved and add the lines up again</summary>
    private void OnLineChanged()
    {
        if (!_loading)
            _dirty = true;
        RecomputeTotal();
    }

    private void MarkDirty(object sender, EventArgs e)
    {
        if (!_loading)
            _dirty = true;
    }

    private void RecomputeTotal()
    {
        float total = 0;
        foreach (InvoiceLineEntry entry in _lines)
            total += entry.LineTotal;

        l_total.Text = $"{Gloable.CurrenceSymbol}{total:0.00}";
    }

    private void cb_hasDue_Changed(object sender, CheckedChangedEventArgs e)
    {
        dp_due.IsVisible = e.Value;
        if (!_loading)
            _dirty = true;
    }

    private void bnt_addLine_Clicked(object sender, EventArgs e)
    {
        _lines.Add(Wrap(new InvoiceLine()));
        _dirty = true;
        RecomputeTotal();
    }

    private void bnt_removeLine_Clicked(object sender, EventArgs e)
    {
        Button button = sender as Button;
        InvoiceLineEntry entry = button == null ? null : button.BindingContext as InvoiceLineEntry;
        if (entry == null)
            return;

        _lines.Remove(entry);
        _dirty = true;

        //never leave nothing to type into
        if (_lines.Count == 0)
            _lines.Add(Wrap(new InvoiceLine()));

        RecomputeTotal();
    }

    /// <summary>
    /// reads the boxes onto the invoice and puts it on disk - added as a new
    /// record, or the existing one updated in place. blank lines are dropped:
    /// an empty row left at the bottom is not a line anybody meant to bill for
    /// </summary>
    private void CommitToInvoice()
    {
        _invoice.Date = dp_date.Date;
        _invoice.DueDate = cb_hasDue.IsChecked ? dp_due.Date : DateTime.MinValue;
        _invoice.BillToName = (e_billName.Text ?? string.Empty).Trim();
        _invoice.BillToAddress = (e_billAddress.Text ?? string.Empty).Trim();
        _invoice.Notes = (e_notes.Text ?? string.Empty).Trim();

        _invoice.Lines = new List<InvoiceLine>();
        foreach (InvoiceLineEntry entry in _lines)
        {
            //a wholly blank line is not billed for; a line with words or a
            //price on it is kept
            if (string.IsNullOrWhiteSpace(entry.Description) && entry.UnitPriceValue == 0)
                continue;
            _invoice.Lines.Add(entry.ToLine());
        }
    }

    /// <summary>saves the record. returns false when nothing had been entered</summary>
    private bool Save()
    {
        CommitToInvoice();

        if (_invoice.Lines.Count == 0
            && string.IsNullOrWhiteSpace(_invoice.BillToName)
            && string.IsNullOrWhiteSpace(_invoice.Notes))
            return false;

        if (IsNewNow)
        {
            //the number is taken here so it lands on the record; PeekNextNumber
            //only showed it. adding once means a second Save on the same page
            //would make a second invoice, so once saved this page edits what it
            //just made rather than adding again
            Invoice.Add(_invoice);
            _becameExisting = true;
        }
        else
            Invoice.Update(_invoice);

        _dirty = false;
        return true;
    }

    /// <summary>a new invoice becomes an existing one the moment it is saved</summary>
    private bool _becameExisting = false;

    private bool IsNewNow { get { return _isNew && !_becameExisting; } }

    private async void tbi_Save_Clicked(object sender, EventArgs e)
    {
        if (!Save())
        {
            await DisplayAlert("Nothing To Save", "Add a line, or a name, before saving.", "Ok");
            return;
        }

        l_invoiceNumber.Text = _invoice.FormattedNumber;
        _leaving = true;
        await Navigation.PopAsync();
    }

    /// <summary>
    /// saves the record and builds the invoice as a self-contained web page,
    /// then offers to share or save it. HTML rather than a pdf: it opens in any
    /// browser, prints to pdf, emails and saves, and needs no pdf library
    /// </summary>
    private async void bnt_share_Clicked(object sender, EventArgs e)
    {
        if (!Save())
        {
            await DisplayAlert("Nothing To Share", "Add a line, or a name, before sharing.", "Ok");
            return;
        }

        l_invoiceNumber.Text = _invoice.FormattedNumber;

        try
        {
            string html = InvoiceHtml.Build(_invoice);

            string fileName = DeviceFileSaver.SafeName($"Invoice {_invoice.FormattedNumber}.html");
            string path = Path.Combine(FileSystem.CacheDirectory, fileName);
            File.WriteAllText(path, html);

            await DeviceFileSaver.OfferAsync(this, $"Invoice {_invoice.FormattedNumber}",
                path, fileName, "text/html");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could Not Build Invoice", ex.Message, "Ok");
        }
    }

    /// <summary>
    /// leaving with something typed and not saved asks first, the same as the
    /// job form - a half written invoice backed out of by mistake is a real
    /// loss. both the phone's back button and the nav arrow come through here.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_dirty || _leaving)
            return base.OnBackButtonPressed();

        AskThenLeave();

        //we are dealing with going back ourselves
        return true;
    }

    private async void AskThenLeave()
    {
        await LeaveAsync();
    }

    private async Task LeaveAsync()
    {
        if (_leaving)
            return;

        if (_dirty)
        {
            string answer = await DisplayActionSheet(
                "This invoice has not been saved.",
                "Stay Here", null,
                SaveItOption, LeaveOption);

            if (answer == null || answer == "Stay Here")
                return;

            if (answer == SaveItOption)
            {
                if (!Save())
                {
                    await DisplayAlert("Nothing To Save", "Add a line, or a name, before saving.", "Ok");
                    return;
                }
            }
        }

        _leaving = true;
        await Navigation.PopAsync();
    }

    private const string SaveItOption = "Save It";
    private const string LeaveOption = "Leave Without Saving";
}
