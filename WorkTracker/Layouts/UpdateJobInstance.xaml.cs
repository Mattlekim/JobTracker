namespace UiInterface.Layouts;

using Kernel;
public partial class UpdateJobInstance : ContentPage
{
	public static Job CurrentJob;

    private bool ignoreCheckedIsCompleated = false;

    public Action OnConfirmed;
    public UpdateJobInstance()
	{
		InitializeComponent();
		if (CurrentJob == null)
			return;

		g_more.BindingContext = CurrentJob;

        Build();
	}

	private void Build()
	{
        l_customerDescription.Text = $"{CurrentJob.JobFormattedStreet} {CurrentJob.JobFormattedCity}";
        p_paymentType.Items.Clear();
        foreach (string s in Enum.GetNames(typeof(PaymentMethod)))
            p_paymentType.Items.Add(s);

        p_paymentType.SelectedItem = "Cash";

        l_jobType.Text = CurrentJob.Name;
        l_jobType.BackgroundColor = Colors.Orange;
        l_jobPrice.Text = $"Price {Gloable.CurrenceSymbol}{CurrentJob.Price}";
        l_jobPrice.BackgroundColor = Colors.Green;

        l_jobOwed.BackgroundColor = CurrentJob.OwedColorCode;
        l_jobOwed.Text = CurrentJob.JobFormattedOwed;

        if (CurrentJob.AlternativePrices == null || CurrentJob.AlternativePrices.Count == 0)
            CurrentJob.UseAlterativePrice = -1;

        if (CurrentJob.UseAlterativePrice < 0)
        {
            l_amoutToPay.Text = $"{CurrentJob.JobFormattedOwedShort}";
            //bnt_removeAlternatePayment.IsVisible = false;
        }
        else
        {
            l_amoutToPay.Text = $"{CurrentJob.AlternativePrices[CurrentJob.UseAlterativePrice].Price}";
            //  bnt_removeAlternatePayment.IsVisible = true;
        }

        l_currencyType.Text = Gloable.CurrenceSymbol;

        ignoreCheckedIsCompleated = true;
        cb_isCompleated.IsChecked = CurrentJob.IsCompleted;

        cb_isPaid.IsChecked = CurrentJob.IsPaidFor;

        if (cb_isPaid.IsChecked)
        {
            p_paymentType.IsEnabled = true;
            l_amoutToPay.IsEnabled = true;
            l_currencyType.TextColor = Colors.White;
        }
        else
        {
            p_paymentType.IsEnabled = false;
            l_amoutToPay.IsEnabled = false;
            l_currencyType.TextColor = Color.FromArgb("4E5151");
        }

        if (cb_isCompleated.IsChecked)
        {
            p_dateCompleated.IsEnabled = true;
            l_dateCompleated.TextColor = Colors.White;

        }
        else
        {
            p_dateCompleated.IsEnabled = false;
            l_dateCompleated.TextColor = Color.FromArgb("4E5151");
        }

        if (CurrentJob.DateCompleated <= UsfulFuctions.DateBase)
            p_dateCompleated.Date = UsfulFuctions.DateNow;
        else
            p_dateCompleated.Date = CurrentJob.DateCompleated;

        if (CurrentJob.AlternativePrices != null && CurrentJob.AlternativePrices.Count > 0)
        {
            p_priceToUse.Items.Clear();
            p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{CurrentJob.Price}");
            for (int i = 0; i < CurrentJob.AlternativePrices.Count; i++)
                p_priceToUse.Items.Add($"{CurrentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{CurrentJob.AlternativePrices[i].Price}");



            p_priceToUse.SelectedIndex = CurrentJob.UseAlterativePrice + 1;
            h_pick_alterativePrice.IsVisible = true;
            bnt_addAlterativePrice.IsVisible = false;
            h_pick_alterativePricebnt.IsVisible = true;
        }
        else
        {
            h_pick_alterativePrice.IsVisible = false;
            bnt_addAlterativePrice.IsVisible = true;
            h_pick_alterativePricebnt.IsVisible = false;
        }
        h_createAlterativePrice.IsVisible = false;
        Payment p = Payment.Get(CurrentJob.PaymentId);
        if (p.Id == -1) //if not valid
            return;

        string tmp = $"{p.PaymentMethod}";
        p_paymentType.SelectedItem = $"{p.PaymentMethod}";
        l_amoutToPay.Text = $"{p.Amount}";
    }



    private void bnt_cancelAlterativePrice(object sender, EventArgs e)
    {
        h_createAlterativePrice.IsVisible = false;
        if (p_priceToUse.Items.Count > 1)
        {

            h_pick_alterativePrice.IsVisible = true;
            h_pick_alterativePricebnt.IsVisible = true;
        }
        else
            bnt_addAlterativePrice.IsVisible = true;
    }

    private void bnt_addAlterativePrice_Clicked(object sender, EventArgs e)
    {
        h_createAlterativePrice.IsVisible = true;
        bnt_addAlterativePrice.IsVisible = false;
        h_pick_alterativePrice.IsVisible = false;
        h_pick_alterativePricebnt.IsVisible = false;
    }

    private void bnt_saveAlterativePrice(object sender, EventArgs e)
    {

        try
        {
            if (CurrentJob.AlternativePrices == null)
                CurrentJob.AlternativePrices = new List<AlternativePrice>();

            CurrentJob.AlternativePrices.Add(new AlternativePrice()
            {
                Description = e_alterativeName.Text,
                Price = (float)Convert.ToDouble(e_alterativePrice.Text)
            });
            p_priceToUse.Items.Clear();
            p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{CurrentJob.Price}");

            for (int i = 0; i < CurrentJob.AlternativePrices.Count; i++)
                p_priceToUse.Items.Add($"{CurrentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{CurrentJob.AlternativePrices[i].Price}");

            p_priceToUse.SelectedIndex = CurrentJob.AlternativePrices.Count;
            h_createAlterativePrice.IsVisible = false;
            h_pick_alterativePrice.IsVisible = true;
            h_pick_alterativePricebnt.IsVisible = true;
            Job.Save();
        }
        catch
        {
            p_priceToUse.Items.Clear();
            p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{CurrentJob.Price}");
            for (int i = 0; i < CurrentJob.AlternativePrices.Count; i++)
                p_priceToUse.Items.Add($"{CurrentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{CurrentJob.AlternativePrices[i].Price}");
            DisplayAlert("Error", "Invalid information for alternative price", "Ok");
            h_createAlterativePrice.IsVisible = false;
        }
    }

    private void p_priceToUse_SelectedIndexChanged(object sender, EventArgs e)
    {

        if (p_priceToUse.SelectedIndex == 0)
            bnt_removeAlternatePayment.IsVisible = false;
        else
            bnt_removeAlternatePayment.IsVisible = true;

        if (cb_isCompleated.IsChecked)
        {
            cb_isCompleated.IsChecked = !cb_isCompleated.IsChecked;
            cb_isCompleated.IsChecked = !cb_isCompleated.IsChecked;
        }
    }

    private void bnt_deleteAlternativePrice_Clicked(object sender, EventArgs e)
    {
        CurrentJob.AlternativePrices.RemoveAt(p_priceToUse.SelectedIndex - 1);

        p_priceToUse.Items.Clear();
        p_priceToUse.Items.Add($"Normal {Gloable.CurrenceSymbol}{CurrentJob.Price}");

        for (int i = 0; i < CurrentJob.AlternativePrices.Count; i++)
            p_priceToUse.Items.Add($"{CurrentJob.AlternativePrices[i].Description} {Gloable.CurrenceSymbol}{CurrentJob.AlternativePrices[i].Price}");

        p_priceToUse.SelectedIndex = 0;


    }

    private void cb_IsCompleated_Changed(object sender, CheckedChangedEventArgs e)
    {
        CheckBox cb = sender as CheckBox;

        if (cb.IsChecked)
        {
            p_dateCompleated.IsEnabled = true;
            l_dateCompleated.TextColor = Colors.White;

            float ballence = 0;

            if (CurrentJob.GetCustomer() != null)
                ballence = CurrentJob.GetCustomer().Balance;

            if (CurrentJob.IsCompleted)
            {
                if (CurrentJob.UseAlterativePrice == -1)
                    ballence -= CurrentJob.Price;
                else
                    ballence -= CurrentJob.AlternativePrices[CurrentJob.UseAlterativePrice].Price;
            }
            if (p_priceToUse.SelectedIndex - 1 < 0)
                ballence += CurrentJob.Price;
            else
                ballence += CurrentJob.AlternativePrices[p_priceToUse.SelectedIndex - 1].Price;

            l_amoutToPay.Text = $"{ballence}";
        }
        else
        {
            p_dateCompleated.IsEnabled = false;
            l_dateCompleated.TextColor = Colors.DarkGray;

        }
    }

    private void on_isPaid_Changed(object sender, CheckedChangedEventArgs e)
    {
        if (cb_isPaid.IsChecked)
        {
            p_paymentType.IsEnabled = true;
            l_amoutToPay.IsEnabled = true;
        }
        else
        {
            p_paymentType.IsEnabled = false;
            l_amoutToPay.IsEnabled = false;
        }
    }

    private void bnt_cancel_clicked(object sender, EventArgs e)
	{
        Navigation.PopAsync();
    }

	private void bnt_confirm_clicked(object sender, EventArgs e)
	{
        if (CurrentJob == null)
            return;

       
        if (CurrentJob.IsPaidFor && cb_isPaid.IsChecked) //if still paid we need to check that there is no differnce in payment details
        {
            //payment code looking for difference in payment
            Payment p = Payment.Get(CurrentJob.PaymentId);
            if (p.Id != -1)
            {
                p.PaymentMethod = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)p_paymentType.SelectedItem);
                try
                {
                    float diff = (float)Convert.ToDouble(l_amoutToPay.Text) - p.Amount;
                    p.Amount = (float)Convert.ToDouble(l_amoutToPay.Text);
                    CurrentJob.AddToBalenceCredit(diff);
                }
                catch
                {
                    DisplayAlert("Error", "Invalid price entered", "Ok");
                    return;
                }
            }
        }

        if (CurrentJob.IsCompleted && !cb_isCompleated.IsChecked)
            CurrentJob.UnMarkJobDone(true);


        int paymentRequired = p_priceToUse.SelectedIndex - 1;

        if (CurrentJob.IsCompleted && cb_isCompleated.IsChecked) //if job is still compleated
        {
            //price for done checking for a difference.
            if (paymentRequired != CurrentJob.UseAlterativePrice)
            {
                CurrentJob.UnMarkJobDone(true);
                CurrentJob.UseAlterativePrice = paymentRequired;
                CurrentJob.MarkJobDone(true);
            }
        }

        CurrentJob.UseAlterativePrice = p_priceToUse.SelectedIndex - 1;

        if (!CurrentJob.IsCompleted && cb_isCompleated.IsChecked)
            CurrentJob.MarkJobDone(p_dateCompleated.Date);

        if (CurrentJob.IsPaidFor && !cb_isPaid.IsChecked)
            CurrentJob.UnMarkJobPaid();

        if (!CurrentJob.IsPaidFor && cb_isPaid.IsChecked)
            CurrentJob.MarkJobPaid((float)Convert.ToDouble(l_amoutToPay.Text), (PaymentMethod)Enum.Parse(typeof(PaymentMethod), (string)p_paymentType.SelectedItem));

        if (CurrentJob.IsCompleted && cb_isCompleated.IsChecked)
            CurrentJob.DateCompleated = p_dateCompleated.Date;

        CurrentJob.Refresh();
        CurrentJob.RefreshColors();

        
        //RefreshPage();
        Job.Save();
        Payment.Save();
        Customer.Save();

        if (OnConfirmed != null)
            OnConfirmed();

        Navigation.PopAsync();
    }
}