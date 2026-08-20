namespace UiInterface.Layouts;

using Kernel;

/// <summary>
/// Every house on the round with something wrong with the way it is set up,
/// and what that is.
///
/// This is the *seeing* half of Verify Data on the settings page; the finding
/// is all in <see cref="DataCheck"/>, in the kernel with the work, so what
/// counts as wrong is one rule and the wording for it is the same here as in
/// the summary the button puts up.
///
/// The whole page is read-only on purpose. Nothing here can be put right by
/// guessing - a missing price is not a price this could work out, and a
/// missing phone number is on a scrap of paper somewhere, not in the app - so
/// a row is a way in to the house rather than something to press. Tapping one
/// opens <c>Layouts/ViewCustomerDetails</c>, which is where every one of these
/// is actually mended: the price and the time have a Change of their own
/// there, and Edit Details covers the rest.
///
/// It builds again every time it is navigated to, so a house put right and
/// come back from has dropped off the list rather than sitting there fixed.
/// </summary>
public partial class DataIssues : ContentPage
{
    public DataIssues()
    {
        InitializeComponent();
        NavigatedTo += (s, e) => Build();
    }

    /// <summary>the classic pull down: the round gone over again</summary>
    private void rv_issues_Refreshing(object sender, EventArgs e)
    {
        try
        {
            Build();
        }
        finally
        {
            rv_issues.IsRefreshing = false;
        }
    }

    private void Build()
    {
        List<DataProblem> problems = DataCheck.Run();

        //the money and what is owed are read off the customer as the row is
        //drawn, and this page is as likely as not being come back to from
        //having changed one of them
        foreach (DataProblem problem in problems)
        {
            problem.Job.Refresh();
            problem.Job.RefreshColors();
        }

        cv_issues.ItemsSource = problems;

        bool found = problems.Count > 0;

        if (found)
            l_total.Text = problems.Count == 1
                ? "1 house needs putting right"
                : $"{problems.Count} houses need putting right";
        else
            l_total.Text = "Nothing to put right";

        //a house with three things wrong with it is on three of these lines,
        //because each line is a job of work to go and do - so the lines can
        //add up to more than the houses, and that is not a miscount
        l_summary.Text = DataCheck.Summarise(problems);
        l_summary.IsVisible = found;

        //the two share the row, so one of them is always the one on screen
        rv_issues.IsVisible = found;
        l_empty.IsVisible = !found;
    }

    private void cv_issues_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DataProblem problem = cv_issues.SelectedItem as DataProblem;

        //cleared straight away, so the same house can be opened twice running
        cv_issues.SelectedItem = null;

        if (problem == null || problem.Job == null)
            return;

        WorkPlanner.ShowJobInfo(problem.Job, this);
    }
}
