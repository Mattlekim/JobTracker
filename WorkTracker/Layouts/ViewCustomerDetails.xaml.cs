namespace UiInterface.Layouts;

using Kernel;
public partial class ViewCustomerDetails : ContentPage
{
	List<Job> _customerJobs = new List<Job>();

	public static Job CurrentJob;

    public Action<Job> OnJobDetialsUpdated;

    public ViewCustomerDetails()
    {
        InitializeComponent();
        if (CurrentJob == null)
            return;

        if (CurrentJob.HaveCanceled)
            tbi_cancelJob.Text = "Resume Job";
        else
            tbi_cancelJob.Text = "Cancel Job";

        Job job = Job.Query(QueryType.CustomerId, CurrentJob.CustomerId).FirstOrDefault();

        //visited guard: a corrupt JobNextId pointing back at an earlier job
        //would otherwise loop forever
        HashSet<int> seenJobs = new HashSet<int>();
        while (job != null && seenJobs.Add(job.Id))
        {
            _customerJobs.Add(job);
            job = Job.Query(QueryType.JobId, job.JobNextId).FirstOrDefault();
        }

     

       

        List<History> history = new List<History>();

        foreach (Job j in _customerJobs)
            history.Add(new History(j));

        List<Payment> payments = Payment.Query(QueryType.CustomerId, CurrentJob.CustomerId);
        foreach (Payment p in payments)
            history.Add(new History(p));

        history = history.OrderByDescending(x => x.SortDate).ToList();


        bool altColour = false;
        for (int i = 0; i < history.Count; i++)
        {
            if (Application.Current.PlatformAppTheme == AppTheme.Dark)
            {
                if (altColour)
                    history[i].AltColour = WorkPlanner.MainColorDark;
                else
                    history[i].AltColour = WorkPlanner.altColorDark;
            }
            else
            {
                if (altColour)
                    history[i].AltColour = WorkPlanner.MainColor;
                else
                    history[i].AltColour = WorkPlanner.altColor;
            }

            altColour = !altColour;
            //_jobsToAddFrom = i;
        }

        cv_jobList.ItemsSource = history;

        Customer c = CurrentJob.GetCustomer();

        History h = new History(CurrentJob);
        l_owing.BindingContext = h;
        l_creditDebit.BindingContext = h;
       
        l_customerName.Text = $"{c.FName} {c.SName}";
        //the shown address: the house number as it is, and the road, town and
        //postcode as screenshot mode leaves them
        l_customerAddressl1.Text = $"{c.Address.PropertyNameNumber} {c.Address.DisplayStreet}";
        l_customerAddressl2.Text = $"{c.Address.DisplayCity}";
        if (c.Address.Area == null || c.Address.Area == string.Empty)
            l_customerAddressl3.IsVisible = false;
        else
            l_customerAddressl3.IsVisible = true;
        l_customerAddressl3.Text = $"{c.Address.DisplayArea}";

        if (c.Address.Postcode == null || c.Address.Postcode == string.Empty)
            l_customerAddressl4.IsVisible = false;
        else
            l_customerAddressl4.IsVisible = true;
        l_customerAddressl4.Text = $"{c.Address.DisplayPostcode}";

        l_phone.Text = c.Phone;
        l_email.Text = c.Email;

        ShowHowTheyPay();



    }


    //  ------------------------------------------------------  how they pay
    //
    //  What is set here is what marking their work done then acts on, so it
    //  is on this page rather than buried in the edit form: it is a thing you
    //  change about a customer, not a thing you fill in once when they join.

    /// <summary>true while the card is being filled in, so setting a picker
    /// to what it already says does not count as a change</summary>
    private bool _fillingInPayment = false;

    private void ShowHowTheyPay()
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
            return;

        _fillingInPayment = true;

        if (p_paysBy.Items.Count == 0)
            foreach (string name in Enum.GetNames(typeof(PaymentMethod)))
                p_paysBy.Items.Add(name);

        p_paysBy.SelectedItem = c.NormalPaymentMethord.ToString();
        e_payPalEmail.Text = c.PayPalEmail;

        _fillingInPayment = false;

        vsl_payPal.IsVisible = c.NormalPaymentMethord == PaymentMethod.Paypal;
        vsl_goCardless.IsVisible = c.NormalPaymentMethord == PaymentMethod.GoCardless;

        ShowPayPalState(c);
        ShowGoCardlessState(c);
        ShowWhatHappensOnDone(c);
    }

    private void ShowPayPalState(Customer c)
    {
        if (!PayPal.IsSetUp)
        {
            l_payPalState.Text = "Put your paypal.me name in on the settings page before this can send anything.";
            return;
        }

        l_payPalState.Text = string.IsNullOrWhiteSpace(c.PayPalContact)
            ? "No email for them, so the link would have to be texted or copied."
            : $"The link would go to {c.PayPalContact}.";
    }

    private void ShowGoCardlessState(Customer c)
    {
        bool linked = c.HasGoCardless();

        bnt_gcUnlink.IsVisible = linked;
        bnt_gcLink.Text = linked ? "Link To Somebody Else" : "Find In GoCardless";

        if (!GoCardless.IsConnected)
        {
            l_gcState.Text = "GoCardless is not connected yet - do that on the settings page, or type the mandate in by hand.";
            return;
        }

        l_gcState.Text = linked
            ? $"Linked to mandate {c.GoCardlessMandateId}."
            : "Not linked to a direct debit yet.";
    }

    /// <summary>
    /// what marking this customer's work done will now do by itself, said out
    /// loud so it is never a surprise
    /// </summary>
    private void ShowWhatHappensOnDone(Customer c)
    {
        switch (c.NormalPaymentMethord)
        {
            case PaymentMethod.GoCardless:
                l_onDone.Text = c.HasGoCardless()
                    ? "Marking their work done will offer to collect what they owe by direct debit."
                    : "Link them to a direct debit and marking their work done will offer to collect it.";
                return;

            case PaymentMethod.Paypal:
                l_onDone.Text = PayPal.IsSetUp
                    ? "Marking their work done will offer to send them a PayPal link for what they owe."
                    : "Set your paypal.me name up and marking their work done will offer to send them a link.";
                return;

            default:
                l_onDone.Text = "Marking their work done does nothing about the money - it goes on their balance to be settled however you like.";
                return;
        }
    }

    private void p_paysBy_Changed(object sender, EventArgs e)
    {
        if (_fillingInPayment)
            return;

        Customer c = CurrentJob?.GetCustomer();
        if (c == null || p_paysBy.SelectedItem == null)
            return;

        c.NormalPaymentMethord = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)p_paysBy.SelectedItem);
        Customer.Save();

        ShowHowTheyPay();
    }

    private void e_payPalEmail_Changed(object sender, TextChangedEventArgs e)
    {
        if (_fillingInPayment)
            return;

        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
            return;

        c.PayPalEmail = e.NewTextValue == null ? string.Empty : e.NewTextValue.Trim();
        Customer.Save();

        ShowPayPalState(c);
    }

    private async void bnt_gcLink_Clicked(object sender, EventArgs e)
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
            return;

        if (!GoCardless.IsConnected)
        {
            await DisplayAlert("GoCardless", "GoCardless is not connected yet. Connect it on the settings page, or type the mandate in by hand.", "Ok");
            return;
        }

        await LinkToGoCardless(c);
        ShowHowTheyPay();
    }

    /// <summary>
    /// the ids typed in rather than picked, for when they are already known -
    /// off the GoCardless dashboard, or from a round taken over from somebody
    /// who was already collecting
    /// </summary>
    private async void bnt_gcByHand_Clicked(object sender, EventArgs e)
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
            return;

        string mandate = await DisplayPromptAsync("Direct Debit Mandate",
            "The mandate id that collects from this customer. It starts MD.", "Save", "Cancel",
            initialValue: c.GoCardlessMandateId);
        if (mandate == null)
            return;

        string customerId = await DisplayPromptAsync("GoCardless Customer",
            "Their customer id in GoCardless. It starts CU. Leave it blank if you do not know it - the mandate is what collects.", "Save", "Cancel",
            initialValue: c.GoCardlessCustomerId);
        if (customerId == null)
            return;

        c.GoCardlessMandateId = mandate.Trim();
        c.GoCardlessCustomerId = customerId.Trim();
        Customer.Save();

        ShowHowTheyPay();
    }

    private async void bnt_gcUnlink_Clicked(object sender, EventArgs e)
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
            return;

        if (!await DisplayAlert("GoCardless",
            "Unlink this customer from their direct debit? The direct debit itself is not cancelled - this app just stops collecting on it.", "Unlink", "Cancel"))
            return;

        c.GoCardlessCustomerId = string.Empty;
        c.GoCardlessMandateId = string.Empty;
        Customer.Save();

        ShowHowTheyPay();
    }

    private async void l_emailClicked(object sender, EventArgs e)
    {
        List<Job> jobs = new List<Job>();
        jobs.Add(CurrentJob);
        //this customer was picked on purpose, so their night before setting
        //should not decide whether the email goes
        await WorkPlanner.EmailCustomers(jobs, DateTime.Now, string.Empty, this, false);
    }

    private async void l_phoneClicked(object sender, EventArgs e)
    {
        List<Job> jobs = new List<Job>();
        jobs.Add(CurrentJob);
        await WorkPlanner.TextCustomers(jobs, DateTime.Now, string.Empty, this, false);
    }

    private void tbi_EditDetails_Clicked(object sender, EventArgs e)
    {
        NewJob.AddNewJob = false;
        NewJob.JobToAdd = CurrentJob;

        NewJob nj = new NewJob();
        nj.OnJobUpdated += (j) =>
        {
            if (OnJobDetialsUpdated != null)
                OnJobDetialsUpdated(CurrentJob);
        };

        Navigation.PushAsync(nj);
    }

    /// <summary>
    /// Sends the customer a paypal.me link with the amount already in it.
    ///
    /// It does not mark anything paid: the link has been sent, that is all.
    /// The money is recorded when it actually lands, off the PayPal statement
    /// - marking a job paid before the money is there is how a round ends up
    /// chasing people who have paid and not chasing people who have not.
    /// </summary>
    private async void tbi_PayPal_Clicked(object sender, EventArgs e)
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
        {
            await DisplayAlert("PayPal", "This job has no customer linked to it.", "Ok");
            return;
        }

        if (!PayPal.IsSetUp)
        {
            await DisplayAlert("PayPal",
                "Put your paypal.me name in on the settings page first, under PayPal. That is all it needs - there is nothing to connect.", "Ok");
            return;
        }

        //what they owe, or this job's price when the account is clear
        float suggested = c.Balance > 0 ? c.Balance : CurrentJob.EffectivePrice;

        string amountText = await DisplayPromptAsync("Ask For Payment",
            $"How much to ask {c.FName} {c.SName} for ({Gloable.CurrenceSymbol})", "Next", "Cancel",
            initialValue: suggested.ToString("0.00"), keyboard: Keyboard.Numeric);
        if (amountText == null)
            return;

        float amount;
        try
        {
            amount = (float)Convert.ToDouble(amountText);
        }
        catch
        {
            await DisplayAlert("PayPal", "That is not a valid amount.", "Ok");
            return;
        }

        //copying it covers everything else a round is sent on - WhatsApp,
        //Messenger, or reading it out over the gate
        string how = await DisplayActionSheet($"Send the link for {Gloable.CurrenceSymbol}{amount:0.00}", "Cancel", null,
            "Text It", "Email It", "Copy The Link");
        if (how == null || how == "Cancel")
            return;

        string message = PayPal.MessageFor(amount);
        List<Job> jobs = new List<Job>() { CurrentJob };

        switch (how)
        {
            case "Text It":
                await WorkPlanner.TextCustomers(jobs, DateTime.Now, message, this, false);
                break;

            case "Email It":
                //to their PayPal address when they have given one
                await WorkPlanner.EmailPayPalLink(c, message, this);
                break;

            case "Copy The Link":
                await Clipboard.Default.SetTextAsync(PayPal.LinkFor(amount));
                await DisplayAlert("Copied", $"{PayPal.LinkFor(amount)}\n\nPaste it wherever you send them things.", "Ok");
                break;
        }
    }

    private async void tbi_GoCardless_Clicked(object sender, EventArgs e)
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
        {
            await DisplayAlert("GoCardless", "This job has no customer linked to it.", "Ok");
            return;
        }

        if (!GoCardless.IsConnected)
        {
            await DisplayAlert("GoCardless", "GoCardless is not connected yet. Go to Settings and connect it with your access token first.", "Ok");
            return;
        }

        if (!c.HasGoCardless())
        {
            await LinkToGoCardless(c);
            return;
        }

        //a job with a request already on its way must never be charged again
        GoCardlessRequest outstanding = GoCardlessRequest.PendingForJob(CurrentJob.Id);
        if (outstanding != null)
        {
            string check = await DisplayActionSheet(
                $"Waiting on {outstanding.FormattedSummary}", "Close", null, "Check If It Has Been Paid");
            if (check == "Check If It Has Been Paid")
            {
                string result = await GoCardless.RefreshPendingAsync();
                await DisplayAlert("GoCardless", result, "Ok");
            }
            return;
        }

        float suggested = c.Balance > 0 ? c.Balance : CurrentJob.Price;
        string action = await DisplayActionSheet($"GoCardless (Experimental) - {c.FName} {c.SName}", "Cancel", null,
            $"Request Payment ({Gloable.CurrenceSymbol}{suggested:0.00})",
            "Check Pending Payments",
            "Unlink Direct Debit");
        if (action == null)
            return;

        if (action.StartsWith("Request Payment"))
        {
            string amountText = await DisplayPromptAsync("Request Payment",
                $"Amount to collect ({Gloable.CurrenceSymbol})", "Request", "Cancel",
                initialValue: suggested.ToString("0.00"), keyboard: Keyboard.Numeric);
            if (amountText == null)
                return;

            float amount;
            try
            {
                amount = (float)Convert.ToDouble(amountText);
            }
            catch
            {
                await DisplayAlert("GoCardless", "That is not a valid amount.", "Ok");
                return;
            }

            if (!await DisplayAlert("Request Payment",
                $"Collect {Gloable.CurrenceSymbol}{amount:0.00} from {c.FName} {c.SName} by direct debit?\n\nThe job stays unpaid until the money actually comes through.", "Yes", "No"))
                return;

            try
            {
                GoCardlessRequest request = await GoCardless.RequestJobPaymentAsync(CurrentJob, amount);

                string when = request.ChargeDate > UsfulFuctions.DateBase
                    ? $" It should leave their bank on {request.ChargeDate.ToShortDateString()}."
                    : string.Empty;
                await DisplayAlert("Payment Requested",
                    $"{request.FormattedAmount} has been requested from {c.FName} {c.SName} by direct debit.{when}\n\n" +
                    "The job will be marked paid automatically once the money comes through.", "Ok");
            }
            catch (Exception ex)
            {
                await DisplayAlert("GoCardless", ex.Message, "Ok");
            }
        }
        else if (action == "Check Pending Payments")
        {
            string result = await GoCardless.RefreshPendingAsync();
            await DisplayAlert("GoCardless", result, "Ok");
        }
        else if (action == "Unlink Direct Debit")
        {
            if (!await DisplayAlert("GoCardless", "Unlink this customer from their GoCardless direct debit? The direct debit itself is not cancelled.", "Unlink", "Cancel"))
                return;
            c.GoCardlessCustomerId = string.Empty;
            c.GoCardlessMandateId = string.Empty;
            Customer.Save();
        }
    }

    /// <summary>
    /// match this customer up with a customer in the GoCardless account and
    /// remember their direct debit mandate
    /// </summary>
    private async Task LinkToGoCardless(Customer c)
    {
        List<GoCardless.GcCustomer> gcCustomers;
        try
        {
            gcCustomers = await GoCardless.ListCustomersAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("GoCardless", ex.Message, "Ok");
            return;
        }

        if (gcCustomers.Count == 0)
        {
            await DisplayAlert("GoCardless", "There are no customers in your GoCardless account yet. The customer needs to sign up to a direct debit first - send them your GoCardless payment request link.", "Ok");
            return;
        }

        //best guess first: same email address
        List<GoCardless.GcCustomer> candidates = new List<GoCardless.GcCustomer>();
        if (!string.IsNullOrWhiteSpace(c.Email))
            candidates = gcCustomers.FindAll(x => string.Equals(x.Email, c.Email.Trim(), StringComparison.OrdinalIgnoreCase));

        //then by surname
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(c.SName))
            candidates = gcCustomers.FindAll(x => x.FamilyName.ToLower().Contains(c.SName.Trim().ToLower()));

        if (candidates.Count == 0)
            candidates = gcCustomers;

        //an action sheet cannot hold hundreds of entries - narrow down first
        if (candidates.Count > 25)
        {
            string search = await DisplayPromptAsync("Find Customer",
                $"There are {candidates.Count} customers in GoCardless. Type part of their name or email to search:", "Search", "Cancel");
            if (search == null)
                return;
            candidates = candidates.FindAll(x => x.Display.ToLower().Contains(search.Trim().ToLower()));
            if (candidates.Count == 0)
            {
                await DisplayAlert("GoCardless", "No customers matched that search.", "Ok");
                return;
            }
            if (candidates.Count > 25)
                candidates = candidates.GetRange(0, 25);
        }

        string picked = await DisplayActionSheet("Which GoCardless customer is this?", "Cancel", null,
            candidates.Select(x => x.Display).ToArray());
        if (picked == null)
            return;

        GoCardless.GcCustomer gc = candidates.FirstOrDefault(x => x.Display == picked);
        if (gc == null)
            return;

        try
        {
            string mandate = await GoCardless.FindUsableMandateAsync(gc.Id);
            if (mandate == null)
            {
                await DisplayAlert("GoCardless", $"{gc.Display} has no usable direct debit mandate. They need to complete the direct debit sign up first.", "Ok");
                return;
            }

            c.GoCardlessCustomerId = gc.Id;
            c.GoCardlessMandateId = mandate;
            Customer.Save();
            await DisplayAlert("GoCardless", $"Linked to {gc.Display}. You can now take payments from this customer by direct debit.", "Ok");
        }
        catch (Exception ex)
        {
            await DisplayAlert("GoCardless", ex.Message, "Ok");
        }
    }

    /// <summary>
    /// the money in rarely matches the amount charged to the penny once a
    /// fee or a customer rounding down is involved, so this marks the job
    /// paid and clears the odds and ends left owing
    /// </summary>
    private async void tbi_Settle_Clicked(object sender, EventArgs e)
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
        {
            await DisplayAlert("Settle Up", "This job has no customer linked to it.", "Ok");
            return;
        }

        if (c.Balance == 0)
        {
            await DisplayAlert("Settle Up", "This customer does not owe anything.", "Ok");
            return;
        }

        string question = c.Balance > 0
            ? $"{c.FName} {c.SName} still shows {Gloable.CurrenceSymbol}{c.Balance:0.00} owing.\n\n" +
              "Clear it and mark the job paid? The difference is written off - it is not counted as income, because you never received it."
            : $"{c.FName} {c.SName} shows {Gloable.CurrenceSymbol}{Math.Abs(c.Balance):0.00} in credit.\n\n" +
              "Clear it and mark the job paid?";

        if (!await DisplayAlert("Settle Up", question, "Clear It", "Cancel"))
            return;

        float writtenOff = CurrentJob.SettleBalance();

        if (OnJobDetialsUpdated != null)
            OnJobDetialsUpdated(CurrentJob);

        await DisplayAlert("Settled",
            writtenOff > 0
                ? $"{Gloable.CurrenceSymbol}{writtenOff:0.00} written off. Nothing is owing now."
                : "Balance cleared. Nothing is owing now.", "Ok");
    }

    private void tbi_Cancel_Job_Clicked(object sender, EventArgs e)
    {
        if (!CurrentJob.HaveCanceled)
            CurrentJob.CancelJob();
        else
            CurrentJob.UnCancelJob();

        if (CurrentJob.HaveCanceled)
            tbi_cancelJob.Text = "Resume Job";
        else
            tbi_cancelJob.Text = "Cancel Job";
    }
    /// <summary>
    /// types in what the customer owes. the balance normally looks after
    /// itself, but a round taken over from somebody else starts with whatever
    /// was written down before
    /// </summary>
    private async void bnt_changeBalance_Clicked(object sender, EventArgs e)
    {
        Customer c = CurrentJob?.GetCustomer();
        if (c == null)
            return;

        if (!await CustomerBalance.ChangeAsync(c, this))
            return;

        //the balance labels read off a History built when the page opened, so
        //a fresh one is what makes them say the new figure
        History h = new History(CurrentJob);
        l_owing.BindingContext = h;
        l_creditDebit.BindingContext = h;

        if (OnJobDetialsUpdated != null)
            OnJobDetialsUpdated(CurrentJob);
    }

}

