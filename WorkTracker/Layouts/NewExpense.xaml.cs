namespace UiInterface.Layouts;

using Kernel;
using Plugin.Maui.OCR;

public partial class NewExpense : ContentPage
{
    /// <summary>
    /// job to attach the new expense to. null when the expense is only
    /// attached to a day
    /// </summary>
    public static Job JobToLink = null;

    /// <summary>
    /// day to attach the new expense to when there is no job (added from
    /// the calendar)
    /// </summary>
    public static DateTime? DateToUse = null;

    /// <summary>
    /// set to edit an existing expense instead of creating a new one
    /// </summary>
    public static Expense ExpenseToEdit = null;

    /// <summary>
    /// an outgoing off a bank statement being turned into an expense
    /// </summary>
    public class StatementPrefill
    {
        public DateTime Date;
        public float Amount;

        /// <summary>the payee exactly as the bank printed it</summary>
        public string Reference = string.Empty;

        /// <summary>the id that stops the same statement line being recorded twice</summary>
        public string ExternalReference = string.Empty;
    }

    /// <summary>
    /// set when the expense is being made from a bank statement outgoing.
    /// fills in what the statement already knows and offers to remember the
    /// payee, so the same bill logs itself next month
    /// </summary>
    public static StatementPrefill FromStatement = null;

    public Action OnExpenseSaved;

    private Job _job;
    private Expense _editing;
    private StatementPrefill _statement;

    /// <summary>
    /// receipt photo file (name inside the receipts folder) attached in this
    /// session but not saved yet - deleted again if the page is left
    /// without saving
    /// </summary>
    private string _pendingReceiptFile = string.Empty;

    private bool _saved = false;

    public NewExpense()
    {
        InitializeComponent();

        _job = JobToLink;
        _editing = ExpenseToEdit;
        _statement = FromStatement;
        JobToLink = null;
        ExpenseToEdit = null;
        FromStatement = null;

        foreach (string s in Enum.GetNames(typeof(ExpenseCategory)))
            p_category.Items.Add(s);
        p_category.SelectedIndex = 0;

        l_amountCaption.Text = $"Amount ({Gloable.CurrenceSymbol}) *";

        if (_editing != null)
        {
            Title = "Edit Expense";
            bnt_delete.IsVisible = true;
            bnt_save.Text = "Save Changes";

            if (_job == null && _editing.JobId != -1)
                _job = _editing.LinkedJob;

            e_amount.Text = _editing.Amount.ToString("0.00");
            e_merchant.Text = _editing.Merchant;
            e_notes.Text = _editing.Notes;
            dp_date.Date = _editing.Date;
            p_category.SelectedIndex = (int)_editing.Category;

            if (_editing.HasReceipt)
                ShowReceiptImage(_editing.ReceiptPhotoPath);
        }
        else
        {
            Title = "New Expense";

            if (DateToUse.HasValue)
                dp_date.Date = DateToUse.Value;
            else if (_job != null)
                dp_date.Date = _job.IsCompleted ? _job.DateCompleated : UsfulFuctions.DateNow;
            else
                dp_date.Date = UsfulFuctions.DateNow;
        }
        DateToUse = null;

        SetUpStatementExpense();

        if (_job != null)
            l_linkedTo.Text = $"Attached to job: {_job.JobFormattedStreet} {_job.JobFormattedCity}";
        else
            l_linkedTo.Text = "Attached to day (no job)";
    }

    /// <summary>
    /// fills in what the bank statement already knows and offers to remember
    /// the payee, so a bill that comes round every month logs itself the next
    /// time a statement is imported
    /// </summary>
    private void SetUpStatementExpense()
    {
        if (_statement == null)
            return;

        vsl_remember.IsVisible = true;
        l_statementLine.Text = $"From your bank statement: {_statement.Date.ToShortDateString()}  " +
            $"{Gloable.CurrenceSymbol}{_statement.Amount:0.00}\n{_statement.Reference}";

        //an expense already logged from this line keeps whatever was typed
        //on it before - only a new one takes its details from the statement
        if (_editing == null)
        {
            Title = "Statement Expense";
            e_amount.Text = _statement.Amount.ToString("0.00");
            dp_date.Date = _statement.Date;
            e_merchant.Text = ExpenseRule.FriendlyMerchant(_statement.Reference);
        }

        ExpenseRule existing = ExpenseRule.FindMatch(_statement.Reference);
        if (existing != null && !existing.Ignore)
        {
            //already a remembered payee - keep it that way unless told otherwise
            sw_remember.IsToggled = true;
            if (_editing == null)
            {
                p_category.SelectedIndex = (int)existing.Category;
                if (string.IsNullOrWhiteSpace(e_notes.Text))
                    e_notes.Text = existing.Notes;
            }
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        //throw away a photo that was taken but never saved
        if (!_saved && _pendingReceiptFile != string.Empty)
        {
            TryDeleteReceiptFile(_pendingReceiptFile);
            _pendingReceiptFile = string.Empty;
        }
        base.OnNavigatedFrom(args);
    }

    private static void TryDeleteReceiptFile(string fileName)
    {
        try
        {
            string path = Path.Combine(Expense.GetReceiptFolderPath(), fileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private void ShowReceiptImage(string path)
    {
        img_receipt.Source = ImageSource.FromFile(path);
        img_receipt.IsVisible = true;
        bnt_removePhoto.IsVisible = true;
        bnt_rescan.IsVisible = true;
    }

    private string CurrentReceiptFile()
    {
        if (_pendingReceiptFile != string.Empty)
            return _pendingReceiptFile;
        if (_editing != null)
            return _editing.ReceiptFileName;
        return string.Empty;
    }

    private async void bnt_takePhoto_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert("Not Supported", "Taking photos is not supported on this device. Use 'Choose Photo' instead.", "Ok");
                return;
            }

            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo == null)
                return;

            await AttachPhoto(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Camera Error", ex.Message, "Ok");
        }
    }

    private async void bnt_pickPhoto_Clicked(object sender, EventArgs e)
    {
        try
        {
            FileResult photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo == null)
                return;

            await AttachPhoto(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Photo Error", ex.Message, "Ok");
        }
    }

    private async Task AttachPhoto(FileResult photo)
    {
        //copy the photo into the app's receipts folder, scaled down on the
        //way in - a camera photo is several megabytes and every one of them
        //is kept, backed up and synced for as long as the records are
        string fileName = $"receipt_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}.jpg";
        string path = Path.Combine(Expense.GetReceiptFolderPath(), fileName);

        using (Stream source = await photo.OpenReadAsync())
        {
            await ReceiptPhoto.SaveCompressedAsync(source, path);
        }

        //a photo attached earlier this session is replaced, not kept
        if (_pendingReceiptFile != string.Empty)
            TryDeleteReceiptFile(_pendingReceiptFile);
        _pendingReceiptFile = fileName;

        ShowReceiptImage(path);

        await ScanReceipt();
    }

    private void bnt_removePhoto_Clicked(object sender, EventArgs e)
    {
        if (_pendingReceiptFile != string.Empty)
        {
            TryDeleteReceiptFile(_pendingReceiptFile);
            _pendingReceiptFile = string.Empty;
        }
        else if (_editing != null && _editing.ReceiptFileName != string.Empty)
        {
            //saved photo on an existing expense - removed for good on save
            _removeExistingReceipt = true;
        }

        img_receipt.Source = null;
        img_receipt.IsVisible = false;
        bnt_removePhoto.IsVisible = false;
        bnt_rescan.IsVisible = false;
        l_scanStatus.IsVisible = false;
    }

    private bool _removeExistingReceipt = false;

    private async void bnt_rescan_Clicked(object sender, EventArgs e)
    {
        await ScanReceipt();
    }

    /// <summary>
    /// run OCR over the attached receipt photo and fill in the details it
    /// finds. anything that cannot be read is left for manual entry
    /// </summary>
    private async Task ScanReceipt()
    {
        string file = CurrentReceiptFile();
        if (file == string.Empty || _removeExistingReceipt)
            return;

        string path = Path.Combine(Expense.GetReceiptFolderPath(), file);
        if (!File.Exists(path))
            return;

        l_scanStatus.IsVisible = true;
        l_scanStatus.Text = "Reading receipt...";

        try
        {
            await OcrPlugin.Default.InitAsync();

            byte[] imageData = await File.ReadAllBytesAsync(path);
            var result = await OcrPlugin.Default.RecognizeTextAsync(imageData);

            if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.AllText))
            {
                l_scanStatus.Text = "Could not read any text from the photo. Enter the details manually.";
                return;
            }

            ReceiptReader.ReceiptData data = ReceiptReader.Read(result.AllText);

            if (!data.FoundAnything)
            {
                l_scanStatus.Text = "Text was read but no amount or date was recognised. Enter the details manually.";
                return;
            }

            string found = string.Empty;

            if (data.FoundAmount)
            {
                e_amount.Text = data.Amount.ToString("0.00");
                found += $"amount {Gloable.CurrenceSymbol}{data.Amount:0.00}";
            }

            if (data.FoundDate)
            {
                dp_date.Date = data.Date;
                if (found != string.Empty)
                    found += ", ";
                found += $"date {data.Date.ToShortDateString()}";
            }

            if (data.FoundMerchant)
            {
                if (string.IsNullOrWhiteSpace(e_merchant.Text))
                    e_merchant.Text = data.Merchant;
                if (found != string.Empty)
                    found += ", ";
                found += $"shop '{data.Merchant}'";
            }

            l_scanStatus.Text = $"Read from receipt: {found}. Check the details before saving.";
        }
        catch (Exception ex)
        {
            l_scanStatus.Text = $"Could not read the receipt ({ex.Message}). Enter the details manually.";
        }
    }

    private async void bnt_save_Clicked(object sender, EventArgs e)
    {
        float amount;
        try
        {
            amount = (float)Convert.ToDouble(e_amount.Text);
        }
        catch
        {
            await DisplayAlert("Error", "Please enter a valid amount", "Ok");
            return;
        }

        if (amount <= 0)
        {
            await DisplayAlert("Error", "The amount must be more than 0", "Ok");
            return;
        }

        Expense expense;
        if (_editing != null)
            expense = _editing;
        else
            expense = new Expense();

        expense.Amount = amount;
        expense.Merchant = e_merchant.Text ?? string.Empty;
        expense.Notes = e_notes.Text ?? string.Empty;
        expense.Date = dp_date.Date;
        if (p_category.SelectedIndex >= 0)
            expense.Category = (ExpenseCategory)p_category.SelectedIndex;

        if (_job != null)
            expense.JobId = _job.Id;

        if (_removeExistingReceipt)
            expense.DeleteReceiptPhoto();

        if (_pendingReceiptFile != string.Empty)
        {
            //swap out the old photo when a new one was taken
            if (expense.ReceiptFileName != string.Empty && expense.ReceiptFileName != _pendingReceiptFile)
                expense.DeleteReceiptPhoto();
            expense.ReceiptFileName = _pendingReceiptFile;
        }

        //the id off the statement line, which is what stops the same
        //transaction being recorded again when statements overlap
        if (_statement != null && !string.IsNullOrWhiteSpace(_statement.ExternalReference))
            expense.ExternalReference = _statement.ExternalReference;

        if (_editing == null)
            Expense.Add(expense);

        //the receipt lives in the folder for the expense's tax year, so
        //changing the date can move the paperwork with it
        expense.FileReceiptWithItsYear();

        Expense.Save();

        RememberPayee(expense);

        _saved = true;

        if (OnExpenseSaved != null)
            OnExpenseSaved();

        await Navigation.PopAsync();
    }

    /// <summary>
    /// keeps - or drops - the rule that logs this payee automatically the
    /// next time it turns up on a statement
    /// </summary>
    private void RememberPayee(Expense expense)
    {
        if (_statement == null)
            return;

        if (sw_remember.IsToggled)
        {
            ExpenseRule.Remember(_statement.Reference, false, expense.Category, expense.Notes);
            ExpenseRule.Save();
            return;
        }

        //switched off: an expense rule left behind would keep logging it
        ExpenseRule rule = ExpenseRule.FindMatch(_statement.Reference);
        if (rule != null && !rule.Ignore)
        {
            ExpenseRule.Remove(rule.Id);
            ExpenseRule.Save();
        }
    }

    private async void bnt_delete_Clicked(object sender, EventArgs e)
    {
        if (_editing == null)
            return;

        if (!await DisplayAlert("Delete Expense?", "Are you sure you want to delete this expense? This cannot be undone.", "Yes", "No"))
            return;

        Expense.Remove(_editing.Id);
        Expense.Save();

        _saved = true;

        if (OnExpenseSaved != null)
            OnExpenseSaved();

        await Navigation.PopAsync();
    }
}
