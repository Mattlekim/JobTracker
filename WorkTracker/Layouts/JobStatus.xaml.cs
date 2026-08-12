namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Everything that can be marked on one job, in one window: whether it is
/// done, whether it is paid, and how much of what is owed the money covers.
///
/// The swipe actions only do the two common cases - done, and done and paid
/// in full in cash. This is where the rest live: a customer settling the
/// whole of their account at once, one paying part of it, a job done last
/// Tuesday rather than today, or a price other than the usual one.
/// </summary>
public partial class JobStatus : ContentPage
{
    /// <summary>the job to show, set before the page is pushed</summary>
    public static Job JobToShow = null;

    /// <summary>run after Save, so the list behind can rebuild itself</summary>
    public static Action OnSaved = null;

    private readonly Job _job;
    private readonly Action _onSaved;

    /// <summary>how the amount paid is worked out</summary>
    private enum AmountMode
    {
        ThisJob,
        Everything,
        Custom,
    }

    private bool _building = true;

    public JobStatus()
    {
        InitializeComponent();

        _job = JobToShow;
        _onSaved = OnSaved;
        JobToShow = null;
        OnSaved = null;

        if (_job == null)
            return;

        foreach (string s in Enum.GetNames(typeof(PaymentMethod)))
            p_paymentType.Items.Add(s);

        l_currency.Text = Gloable.CurrenceSymbol;

        Build();
    }

    private void Build()
    {
        _building = true;

        l_address.Text = $"{_job.JobFormattedStreet} {_job.JobFormattedCity}";
        l_jobType.Text = string.IsNullOrWhiteSpace(_job.Name) ? "Job" : _job.Name;

        BuildPricePicker();

        cb_done.IsChecked = _job.IsCompleted;
        dp_done.Date = _job.DateCompleated <= UsfulFuctions.DateBase
            ? UsfulFuctions.DateNow
            : _job.DateCompleated;

        cb_paid.IsChecked = _job.IsPaidFor;

        //a direct debit already on its way collects the money itself, so
        //taking it here as well would charge the customer twice
        l_pending.IsVisible = _job.PaymentPending;
        if (_job.PaymentPending)
            l_pending.Text = $"A direct debit is on its way ({_job.PaymentPendingText}). The job is marked paid by itself when it lands.";

        Payment existing = _job.JobPayment;
        if (existing != null)
        {
            p_paymentType.SelectedItem = existing.PaymentMethod.ToString();
            e_amount.Text = existing.Amount.ToString("0.00");
            rb_custom.IsChecked = true;
        }
        else
        {
            p_paymentType.SelectedItem = PaymentMethod.Cash.ToString();
            rb_thisJob.IsChecked = true;
        }

        _building = false;

        ShowAmounts();
        ShowPaidSection();
        ShowDoneSection();
        ShowOneOffSection();
    }

    //  -------------------------------------------------------------  one off

    /// <summary>
    /// what a one off has left to say for itself. a job that repeats brings
    /// itself back the moment it is marked done, so this section is only
    /// there for the ones that do not
    /// </summary>
    private void ShowOneOffSection()
    {
        brd_oneOff.IsVisible = _job.IsOneOff;
        if (!_job.IsOneOff)
            return;

        bnt_doAgain.IsVisible = _job.CanDoAgain;

        if (_job.CanDoAgain)
        {
            l_oneOff.Text = "Done, and that is the end of it. Put it back on the round for another go.";
            return;
        }

        if (!_job.IsCompleted)
        {
            l_oneOff.Text = "This job does not repeat. Once it is done it comes off the round, and you can put it back on from here afterwards.";
            return;
        }

        List<Job> next = Job.Query(QueryType.JobId, _job.JobNextId);
        l_oneOff.Text = next.Count > 0
            ? $"Back on the round, due {next[0].DueDate.ToShortDateString()}."
            : "Done, and that is the end of it.";
    }

    private async void bnt_doAgain_Clicked(object sender, EventArgs e)
    {
        if (!await WorkPlanner.DoJobAgain(_job, this))
            return;

        ShowOneOffSection();

        //the list behind is now a visit short of what is actually on the
        //round, so it gets rebuilt without waiting for Save
        if (_onSaved != null)
            _onSaved();
    }

    //  ---------------------------------------------------------------  price

    private void BuildPricePicker()
    {
        p_priceToUse.Items.Clear();
        p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{_job.Price}");

        if (_job.AlternativePrices != null)
            foreach (AlternativePrice a in _job.AlternativePrices)
                p_priceToUse.Items.Add($"{a.Description} {Gloable.CurrenceSymbol}{a.Price}");

        int chosen = _job.UseAlterativePrice + 1;
        if (chosen < 0 || chosen >= p_priceToUse.Items.Count)
            chosen = 0;
        p_priceToUse.SelectedIndex = chosen;

        bnt_removePrice.IsVisible = chosen > 0;
    }

    /// <summary>the price this visit is being charged at, as picked above</summary>
    private float ChosenPrice()
    {
        int index = p_priceToUse.SelectedIndex - 1;
        if (index < 0 || _job.AlternativePrices == null || index >= _job.AlternativePrices.Count)
            return _job.Price;
        return _job.AlternativePrices[index].Price;
    }

    private void p_priceToUse_Changed(object sender, EventArgs e)
    {
        bnt_removePrice.IsVisible = p_priceToUse.SelectedIndex > 0;
        if (!_building)
            ShowAmounts();
    }

    private void bnt_addPrice_Clicked(object sender, EventArgs e)
    {
        vsl_newPrice.IsVisible = true;
        e_priceName.Text = string.Empty;
        e_priceAmount.Text = string.Empty;
    }

    private void bnt_cancelPrice_Clicked(object sender, EventArgs e)
    {
        vsl_newPrice.IsVisible = false;
    }

    private async void bnt_savePrice_Clicked(object sender, EventArgs e)
    {
        float price;
        try
        {
            price = (float)Convert.ToDouble(e_priceAmount.Text);
        }
        catch
        {
            await DisplayAlert("Error", "Enter a valid price", "Ok");
            return;
        }

        if (_job.AlternativePrices == null)
            _job.AlternativePrices = new List<AlternativePrice>();

        _job.AlternativePrices.Add(new AlternativePrice()
        {
            Description = string.IsNullOrWhiteSpace(e_priceName.Text) ? "Other" : e_priceName.Text,
            Price = price,
        });

        Job.Save();

        vsl_newPrice.IsVisible = false;

        _building = true;
        BuildPricePicker();
        p_priceToUse.SelectedIndex = _job.AlternativePrices.Count;
        _building = false;

        ShowAmounts();
    }

    private async void bnt_removePrice_Clicked(object sender, EventArgs e)
    {
        int index = p_priceToUse.SelectedIndex - 1;
        if (index < 0 || _job.AlternativePrices == null || index >= _job.AlternativePrices.Count)
            return;

        if (!await DisplayAlert("Remove Price?",
                $"'{_job.AlternativePrices[index].Description}' will be taken off this job.", "Remove", "Cancel"))
            return;

        _job.AlternativePrices.RemoveAt(index);
        if (_job.UseAlterativePrice == index)
            _job.UseAlterativePrice = -1;
        Job.Save();

        _building = true;
        BuildPricePicker();
        _building = false;

        ShowAmounts();
    }

    //  ----------------------------------------------------------------  done

    private void cb_done_Changed(object sender, CheckedChangedEventArgs e)
    {
        if (_building)
            return;

        ShowDoneSection();
        ShowAmounts();
    }

    private void ShowDoneSection()
    {
        hsl_doneDate.IsVisible = cb_done.IsChecked;
    }

    //  ----------------------------------------------------------------  paid

    private void cb_paid_Changed(object sender, CheckedChangedEventArgs e)
    {
        if (_building)
            return;

        ShowPaidSection();
        ShowAmounts();
    }

    private void ShowPaidSection()
    {
        vsl_paid.IsVisible = cb_paid.IsChecked;

        //a payment that came in through the bank is not this page's to change
        bool locked = _job.IsPaidFor && !_job.CanClearPayment;
        l_paidLocked.IsVisible = locked;
        if (locked)
            l_paidLocked.Text = $"This was paid by {_job.JobPayment.PaymentMethod}, which came in through the bank. " +
                "Change it on the payments page rather than here.";
    }

    private void AmountMode_Changed(object sender, CheckedChangedEventArgs e)
    {
        if (_building)
            return;

        ShowAmounts();
    }

    /// <summary>true while the box is being filled in from one of the other two choices</summary>
    private bool _settingAmount = false;

    private void e_amount_Changed(object sender, TextChangedEventArgs e)
    {
        //typing an amount is what "some of it" means - but only when it is
        //the user typing, not this page filling the box in
        if (_building || _settingAmount)
            return;

        if (!rb_custom.IsChecked)
            rb_custom.IsChecked = true;
    }

    private AmountMode SelectedMode()
    {
        if (rb_everything.IsChecked)
            return AmountMode.Everything;
        if (rb_custom.IsChecked)
            return AmountMode.Custom;
        return AmountMode.ThisJob;
    }

    /// <summary>
    /// everything the customer owes once this visit is counted. marking the
    /// job done is what puts its price on the account, so a job about to be
    /// ticked done is added in - that is what makes "everything owed" cover
    /// today's work as well
    /// </summary>
    private float EverythingOwed()
    {
        float owed = _job.CustomerOwes;

        if (_job.IsCompleted)
            owed -= _job.EffectivePrice;

        if (cb_done.IsChecked)
            owed += ChosenPrice();

        return owed;
    }

    private void ShowAmounts()
    {
        float thisJob = ChosenPrice();
        float everything = EverythingOwed();

        l_thisJob.Text = $"This job - {Gloable.CurrenceSymbol}{thisJob:0.00}";
        l_everything.Text = $"Everything owed - {Gloable.CurrenceSymbol}{everything:0.00}";

        //worth saying out loud, because it is the difference between the two
        l_everythingNote.IsVisible = cb_done.IsChecked && Math.Abs(everything - thisJob) > 0.005f;
        l_everythingNote.Text = "Clears the whole account, this visit included.";

        l_owed.Text = $"Owed {Gloable.CurrenceSymbol}{_job.CustomerOwes:0.00}";

        if (!rb_custom.IsChecked)
        {
            _settingAmount = true;
            e_amount.Text = (SelectedMode() == AmountMode.Everything ? everything : thisJob).ToString("0.00");
            _settingAmount = false;
        }
    }

    /// <summary>what is actually going to be paid</summary>
    private bool TryGetAmount(out float amount)
    {
        amount = 0;

        switch (SelectedMode())
        {
            case AmountMode.ThisJob:
                amount = ChosenPrice();
                return true;

            case AmountMode.Everything:
                amount = EverythingOwed();
                return true;

            default:
                try
                {
                    amount = (float)Convert.ToDouble(e_amount.Text);
                    return true;
                }
                catch
                {
                    return false;
                }
        }
    }

    //  ----------------------------------------------------------------  save

    private void bnt_cancel_Clicked(object sender, EventArgs e)
    {
        Navigation.PopAsync();
    }

    private async void bnt_save_Clicked(object sender, EventArgs e)
    {
        if (_job == null)
            return;

        float amount = 0;
        if (cb_paid.IsChecked && !TryGetAmount(out amount))
        {
            await DisplayAlert("Error", "Enter a valid amount", "Ok");
            return;
        }

        PaymentMethod method = PaymentMethod.Cash;
        if (p_paymentType.SelectedItem != null)
            method = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)p_paymentType.SelectedItem);

        if (!await ApplyPaid(amount, method))
            return;

        ApplyDone();

        _job.Refresh();
        _job.RefreshColors();

        Job.Save();
        Payment.Save();
        Customer.Save();

        if (_onSaved != null)
            _onSaved();

        await Navigation.PopAsync();
    }

    /// <summary>
    /// the done tick, and the price this visit was charged at. taking a job
    /// back off done and putting it on again is how a changed price gets on
    /// to the customer's account
    /// </summary>
    private void ApplyDone()
    {
        int price = p_priceToUse.SelectedIndex - 1;

        if (_job.IsCompleted && !cb_done.IsChecked)
        {
            _job.UnMarkJobDone(true);
            _job.UseAlterativePrice = price;
            return;
        }

        if (_job.IsCompleted && price != _job.UseAlterativePrice)
        {
            _job.UnMarkJobDone(true);
            _job.UseAlterativePrice = price;
            _job.MarkJobDone(dp_done.Date, true);
        }

        _job.UseAlterativePrice = price;

        if (!_job.IsCompleted && cb_done.IsChecked)
            _job.MarkJobDone(dp_done.Date);

        if (_job.IsCompleted && cb_done.IsChecked)
            _job.DateCompleated = dp_done.Date;
    }

    /// <summary>
    /// the paid tick. returns false when nothing should be saved because the
    /// user has been asked something and it did not go ahead
    /// </summary>
    private async Task<bool> ApplyPaid(float amount, PaymentMethod method)
    {
        //direct debits are requested, not taken: the job stays unpaid until
        //the money actually arrives
        if (!_job.IsPaidFor && cb_paid.IsChecked && method == PaymentMethod.GoCardless)
        {
            await WorkPlanner.RequestGoCardlessPayment(_job, amount, this);
            cb_paid.IsChecked = false;
            return true;
        }

        if (_job.IsPaidFor && !cb_paid.IsChecked)
        {
            if (!_job.CanClearPayment)
            {
                await DisplayAlert("Paid By Bank",
                    $"This job was paid by {_job.JobPayment.PaymentMethod}, which came in through the bank. " +
                    "Remove that payment from the payments page rather than here.", "Ok");
                return false;
            }

            _job.UnMarkJobPaid();
            return true;
        }

        //still paid - the amount or the method may have been changed
        if (_job.IsPaidFor && cb_paid.IsChecked)
        {
            Payment p = _job.JobPayment;
            if (p != null)
            {
                p.PaymentMethod = method;
                _job.AddToBalenceCredit(amount - p.Amount);
                p.Amount = amount;
            }
            return true;
        }

        if (!_job.IsPaidFor && cb_paid.IsChecked)
        {
            GoCardlessRequest pending = GoCardlessRequest.PendingForJob(_job.Id);
            if (pending != null)
            {
                await DisplayAlert("Payment Pending",
                    $"A direct debit is already on its way for this job ({pending.FormattedSummary}). " +
                    "It will be marked paid automatically once the money comes through.", "Ok");
                return false;
            }

            //done first, so paying everything owed covers today's work too
            if (cb_done.IsChecked && !_job.IsCompleted)
            {
                _job.UseAlterativePrice = p_priceToUse.SelectedIndex - 1;
                _job.MarkJobDone(dp_done.Date);
            }

            _job.MarkJobPaid(amount, method);
        }

        return true;
    }
}
