namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Putting duplicate customer records back into one.
///
/// Editing a job's details used to make a whole new customer for a job that
/// already had one, and point the job at it. The original was left behind
/// with the balance and the payments but no work. That is fixed, but the
/// records it made are still on the books - and they still count towards the
/// money owed on the work list, because that adds up every customer in debt
/// whether they have work or not.
///
/// So this lists the customers with nothing to do, says which customer looks
/// like the same person, and merges the two on one tap. It is a tidy up, not
/// a feature: once the books are straight there is nothing here to see.
/// </summary>
public partial class TidyCustomers : ContentPage
{
    public TidyCustomers()
    {
        InitializeComponent();
        Build();
    }

    private void Build()
    {
        vsl_list.Clear();

        List<Customer> spare = Customer.WithoutWork();

        l_empty.IsVisible = spare.Count == 0;
        bnt_mergeAll.IsVisible = false;

        //what these records are doing to the figure on the work list, which
        //is the reason to bother with any of this
        float owed = 0;
        foreach (Customer c in spare)
            if (c.Balance > 0)
                owed += c.Balance;

        l_owedNote.IsVisible = owed > 0.005f;
        l_owedNote.Text = $"These are adding {Gloable.CurrenceSymbol}{owed:0.00} to the money owed on the work list.";

        int clear = 0;
        foreach (Customer c in spare)
        {
            List<Customer> looks = Customer.LooksLikeSameAs(c);
            Customer match = looks.Count > 0 ? looks[0] : null;

            if (match != null && looks.Count == 1)
                clear++;

            vsl_list.Add(Card(c, match, looks.Count));
        }

        if (clear > 1)
        {
            bnt_mergeAll.IsVisible = true;
            bnt_mergeAll.Text = clear == spare.Count
                ? $"Merge All {clear}"
                : $"Merge The {clear} With One Clear Match";
        }
    }

    private Border Card(Customer c, Customer match, int matchCount)
    {
        VerticalStackLayout inner = new VerticalStackLayout() { Spacing = 4 };

        inner.Add(new Label()
        {
            Text = c.FormattedName,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
        });

        inner.Add(Caption(c.FormattedAddress));

        List<string> holds = new List<string>();
        if (Math.Abs(c.Balance) > 0.005f)
            holds.Add(c.Balance > 0
                ? $"owes {Gloable.CurrenceSymbol}{c.Balance:0.00}"
                : $"in credit {Gloable.CurrenceSymbol}{Math.Abs(c.Balance):0.00}");

        int payments = Payment.Query(QueryType.CustomerId, c.Id).Count;
        if (payments > 0)
            holds.Add(payments == 1 ? "1 payment" : $"{payments} payments");

        if (c.HasGoCardless())
            holds.Add("direct debit");

        if (!string.IsNullOrWhiteSpace(c.Phone))
            holds.Add(c.Phone);

        if (holds.Count > 0)
            inner.Add(Caption(string.Join("   ", holds)));

        if (match == null)
            inner.Add(new Label()
            {
                Text = "No other customer looks like this one.",
                FontSize = 12,
                TextColor = Color.FromArgb("#EF6C00"),
                Margin = new Thickness(0, 4, 0, 0),
            });
        else
        {
            string more = matchCount > 1 ? $"  (+{matchCount - 1} more)" : string.Empty;
            inner.Add(new Label()
            {
                Text = $"Looks like {match.FormattedName} - {match.FormattedAddress}{more}",
                FontSize = 12,
                TextColor = Color.FromArgb("#1E88E5"),
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        HorizontalStackLayout buttons = new HorizontalStackLayout()
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
        };

        if (match != null)
        {
            Button merge = Action("Merge", "#2E7D32");
            merge.Clicked += async (s, e) => await MergeRow(c);
            buttons.Add(merge);
        }

        Button delete = Action("Delete", "#E53935");
        delete.Clicked += async (s, e) => await DeleteRow(c);
        buttons.Add(delete);

        inner.Add(buttons);

        return new Border()
        {
            Style = (Style)Resources["Card"],
            Content = inner,
        };
    }

    private Label Caption(string text)
    {
        return new Label()
        {
            Text = text,
            FontSize = 12,
            TextColor = Color.FromArgb("#6B7280"),
        };
    }

    private Button Action(string text, string colour)
    {
        return new Button()
        {
            Text = text,
            FontSize = 13,
            Padding = new Thickness(14, 6),
            CornerRadius = 8,
            BorderWidth = 2,
            BorderColor = Color.FromArgb(colour),
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb(colour),
        };
    }

    //  ---------------------------------------------------------------  merge

    private async Task MergeRow(Customer spare)
    {
        List<Customer> looks = Customer.LooksLikeSameAs(spare);
        if (looks.Count == 0)
            return;

        Customer into = looks[0];

        //more than one could be the same person, so which is not this page's
        //to decide
        if (looks.Count > 1)
        {
            List<string> choices = new List<string>();
            foreach (Customer o in looks)
                choices.Add($"{o.FormattedName} - {o.FormattedAddress}");

            string picked = await DisplayActionSheet("Merge into which customer?", "Cancel", null, choices.ToArray());
            int index = choices.IndexOf(picked);
            if (index < 0)
                return;

            into = looks[index];
        }

        MergeBalance? balance = await BalanceChoice(spare, into);
        if (balance == null)
            return;

        if (!await Confirm(spare, into))
            return;

        Customer.Merge(spare, into, balance.Value);
        DataRefreshNotifier.NotifyDataChanged();
        Build();
    }

    /// <summary>
    /// which balance the merged customer ends up on. only worth asking when
    /// both records carry one - the duplicate was made with a copy of the
    /// figure, so adding them together would charge for the same work twice
    /// </summary>
    /// <returns>null when the question was asked and backed out of</returns>
    private async Task<MergeBalance?> BalanceChoice(Customer spare, Customer into)
    {
        bool spareHas = Math.Abs(spare.Balance) > 0.005f;
        bool intoHas = Math.Abs(into.Balance) > 0.005f;

        if (!spareHas)
            return MergeBalance.Keep;

        if (!intoHas)
            return MergeBalance.Take;

        if (Math.Abs(spare.Balance - into.Balance) < 0.005f)
            return MergeBalance.Keep;

        string keep = $"Keep {Money(into.Balance)} (on the one with the work)";
        string take = $"Use {Money(spare.Balance)} (on this one)";
        string add = $"Add them up - {Money(into.Balance + spare.Balance)}";

        string picked = await DisplayActionSheet(
            $"{spare.FormattedName}: both records have a balance. Which is right?",
            "Cancel", null, keep, take, add);

        if (picked == take)
            return MergeBalance.Take;

        if (picked == add)
            return MergeBalance.Add;

        if (picked == keep)
            return MergeBalance.Keep;

        //cancelled, or the sheet was dismissed
        return null;
    }

    private async Task<bool> Confirm(Customer spare, Customer into)
    {
        return await DisplayAlert("Merge Customers",
            $"{spare.FormattedName} at {spare.FormattedAddress} will be merged into " +
            $"{into.FormattedName} at {into.FormattedAddress}, and this record will go.",
            "Merge", "Cancel");
    }

    private async void bnt_mergeAll_Clicked(object sender, EventArgs e)
    {
        //only the ones there is nothing to ask about: exactly one customer
        //they could be, and no argument over the balance
        List<Customer> doing = new List<Customer>();
        foreach (Customer c in Customer.WithoutWork())
        {
            List<Customer> looks = Customer.LooksLikeSameAs(c);
            if (looks.Count != 1)
                continue;

            if (Math.Abs(c.Balance) > 0.005f
                && Math.Abs(looks[0].Balance) > 0.005f
                && Math.Abs(c.Balance - looks[0].Balance) > 0.005f)
                continue;

            doing.Add(c);
        }

        if (doing.Count == 0)
        {
            await DisplayAlert("Nothing To Do", "The ones left need a decision, so they have to be done one at a time.", "Ok");
            return;
        }

        if (!await DisplayAlert("Merge Customers",
                $"{doing.Count} customers will be merged into the customer that has their work. Each one keeps its payments and its balance.",
                $"Merge {doing.Count}", "Cancel"))
            return;

        int done = 0;
        foreach (Customer c in doing)
        {
            List<Customer> looks = Customer.LooksLikeSameAs(c);
            if (looks.Count != 1)
                continue;

            //the one with nothing on it takes the figure from the one that has
            MergeBalance balance = Math.Abs(looks[0].Balance) > 0.005f
                ? MergeBalance.Keep
                : MergeBalance.Take;

            if (Customer.Merge(c, looks[0], balance))
                done++;
        }

        DataRefreshNotifier.NotifyDataChanged();
        Build();

        await DisplayAlert("Merged", done == 1 ? "1 customer merged." : $"{done} customers merged.", "Ok");
    }

    //  --------------------------------------------------------------  delete

    private async Task DeleteRow(Customer spare)
    {
        string warning = spare.HoldsRecords()
            ? "\n\nThis record still has money or payments on it. Deleting it loses them - merging keeps them."
            : string.Empty;

        if (!await DisplayAlert("Delete Customer",
                $"{spare.FormattedName} at {spare.FormattedAddress} will be deleted.{warning}",
                "Delete", "Cancel"))
            return;

        Customer.Delete(spare.Id);
        Customer.Save();
        DataRefreshNotifier.NotifyDataChanged();
        Build();
    }

    private string Money(float amount)
    {
        return $"{Gloable.CurrenceSymbol}{amount:0.00}";
    }
}
