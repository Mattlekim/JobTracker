using System;
using System.Collections.Generic;

namespace Kernel
{
    /// <summary>
    /// What can be wrong with a house on the round.
    ///
    /// Every one of these is a job that will look perfectly normal on every
    /// list in the app right up until the moment it matters - the night the
    /// texts go out, the evening the week is planned, the day the money is
    /// added up. That is why they are worth going looking for rather than
    /// waiting to be noticed.
    /// </summary>
    public enum DataIssue
    {
        /// <summary>nothing is charged for the work</summary>
        NoPrice,

        /// <summary>no idea how long it takes, and none set for the round either</summary>
        NoTime,

        /// <summary>text the night before, with no number to text</summary>
        TextNightBeforeNoPhone,

        /// <summary>email the night before, with no address to email</summary>
        EmailNightBeforeNoEmail,

        /// <summary>text when it is done, with no number to text</summary>
        TextWhenDoneNoPhone,

        /// <summary>email when it is done, with no address to email</summary>
        EmailWhenDoneNoEmail,

        /// <summary>the job is pointed at a customer who is not there any more</summary>
        NoCustomer,
    }

    /// <summary>
    /// one house and everything found wrong with it.
    ///
    /// Properties rather than fields because the page binds to them, and a
    /// binding cannot see a field - it would draw a row with nothing on it
    /// and say nothing about why
    /// </summary>
    public class DataProblem
    {
        public Job Job { get; set; }

        public List<DataIssue> Issues { get; set; } = new List<DataIssue>();

        /// <summary>
        /// everything wrong with this house in one line, in the order the
        /// issues were found, which is the order they are declared in
        /// </summary>
        public string Says
        {
            get
            {
                List<string> said = new List<string>();

                foreach (DataIssue issue in Issues)
                    said.Add(DataCheck.Say(issue));

                return string.Join(" • ", said);
            }
        }
    }

    /// <summary>
    /// Going over the round looking for the work that is quietly not set up
    /// properly.
    ///
    /// A round is added to a house at a time, over years, at a gate in the
    /// rain, and the things that go missing are the ones nothing complains
    /// about: a job saved with no price on it is still on every list and
    /// still gets cleaned, it just never asks anybody for money. A house
    /// ticked to be texted the night before with no phone number against it
    /// is silently left out of the night's messages - `TextCustomers` sends
    /// to whoever it can and says nothing about the rest. A job with no time
    /// on it and no usual set for the round makes a day's planning add up to
    /// nothing.
    ///
    /// None of that shows up on a page, because every one of those pages is
    /// showing what it was told. So this is asked for outright, from the
    /// settings page, and the answer is a list of houses to go and put right.
    ///
    /// It is in the kernel with the data for the usual reason: what counts as
    /// wrong is a rule about the work, the wording for it has to be the same
    /// in the summary and on the list, and it can then be run by
    /// KernelDebugger without the app.
    ///
    /// **It changes nothing.** There is deliberately no "fix them all": what
    /// a missing price or a missing phone number should be is not something
    /// this can know, and quietly filling one in would be worse than the gap.
    /// </summary>
    public static class DataCheck
    {
        /// <summary>
        /// how one issue is said. One wording, used by the list, the summary
        /// and the self test, so a house cannot be told about differently in
        /// two places
        /// </summary>
        public static string Say(DataIssue issue)
        {
            switch (issue)
            {
                case DataIssue.NoPrice:
                    return "No price";

                case DataIssue.NoTime:
                    return "No time set";

                case DataIssue.TextNightBeforeNoPhone:
                    return "Text night before, no phone number";

                case DataIssue.EmailNightBeforeNoEmail:
                    return "Email night before, no email address";

                case DataIssue.TextWhenDoneNoPhone:
                    return "Text when done, no phone number";

                case DataIssue.EmailWhenDoneNoEmail:
                    return "Email when done, no email address";
            }

            //the NoCustomer answer rather than a case of its own, so an issue
            //added to the enum and forgotten here still says something
            return "No customer record";
        }

        /// <summary>
        /// Everything wrong with the round, a house at a time.
        ///
        /// **One visit per house, the one next due** - the same
        /// <see cref="Job.SameJobKey"/> and <see cref="Job.NextDue"/> pair
        /// Layouts/AllJobs and RoundStats pick a house with, so the three
        /// cannot disagree about how many houses there are. The job list
        /// keeps every visit of every house ever done, so checking the lot
        /// would report the same missing price twenty times over.
        ///
        /// A clean already written up and a cancelled one are left out for
        /// the same reason they are left off the round everywhere else:
        /// neither is work anybody is going to turn up for, and a price that
        /// was wrong on a visit already paid for cannot be put right now.
        /// </summary>
        public static List<DataProblem> Run(List<Job> jobs = null)
        {
            List<DataProblem> problems = new List<DataProblem>();

            foreach (Job j in OneVisitPerHouse(jobs ?? Job.Query()))
            {
                DataProblem problem = Check(j);

                if (problem != null)
                    problems.Add(problem);
            }

            return problems;
        }

        /// <summary>
        /// one house looked over, or null when there is nothing wrong with it
        /// </summary>
        public static DataProblem Check(Job j)
        {
            if (j == null)
                return null;

            DataProblem problem = new DataProblem() { Job = j };

            //what the house is charged. EffectivePrice rather than Price, so
            //a house on an alternative price is judged on what it actually
            //comes to. Negative is as wrong as nothing
            if (j.EffectivePrice <= 0)
                problem.Issues.Add(DataIssue.NoPrice);

            //Minutes is the job's own estimate or the round's usual, so this
            //only fires when there is no answer at all - a house with nothing
            //of its own on a round that has a usual set is not a problem, it
            //is the usual
            if (j.Minutes <= 0)
                problem.Issues.Add(DataIssue.NoTime);

            Customer c = j.GetCustomer();

            if (c == null)
            {
                problem.Issues.Add(DataIssue.NoCustomer);

                //nothing else can be asked about somebody who is not there.
                //The four questions below are all "have they got a number",
                //and the honest answer is that there is nobody to ask
                return problem;
            }

            bool phone = !string.IsNullOrWhiteSpace(c.Phone);
            bool email = !string.IsNullOrWhiteSpace(c.Email);

            //ticked to be told and no way of telling them. This is the one
            //that goes unnoticed longest: the night's messages simply go to
            //everybody else
            if (j.TNB && !phone)
                problem.Issues.Add(DataIssue.TextNightBeforeNoPhone);

            if (j.ENB && !email)
                problem.Issues.Add(DataIssue.EmailNightBeforeNoEmail);

            if (j.TAC && !phone)
                problem.Issues.Add(DataIssue.TextWhenDoneNoPhone);

            if (j.EAC && !email)
                problem.Issues.Add(DataIssue.EmailWhenDoneNoEmail);

            return problem.Issues.Count > 0 ? problem : null;
        }

        /// <summary>
        /// how many houses each kind of problem is on, in the enum's order so
        /// the summary reads the same way every time
        /// </summary>
        public static Dictionary<DataIssue, int> Counts(List<DataProblem> problems)
        {
            Dictionary<DataIssue, int> counts = new Dictionary<DataIssue, int>();

            if (problems == null)
                return counts;

            foreach (DataProblem problem in problems)
                foreach (DataIssue issue in problem.Issues)
                {
                    int had;
                    counts.TryGetValue(issue, out had);
                    counts[issue] = had + 1;
                }

            return counts;
        }

        /// <summary>
        /// what was found, a line per kind of problem. A house with three
        /// things wrong with it is counted on all three lines, because each
        /// line is a job of work to go and do
        /// </summary>
        public static string Summarise(List<DataProblem> problems)
        {
            Dictionary<DataIssue, int> counts = Counts(problems);

            if (counts.Count == 0)
                return string.Empty;

            List<string> lines = new List<string>();

            foreach (DataIssue issue in Enum.GetValues(typeof(DataIssue)))
            {
                int on;
                if (!counts.TryGetValue(issue, out on) || on == 0)
                    continue;

                lines.Add($"{on} {(on == 1 ? "house" : "houses")}: {Say(issue)}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// The round: one visit per house, the one next due, and only the
        /// houses still being cleaned.
        ///
        /// The same rule and the same two calls Layouts/AllJobs picks its
        /// rows with, including leaving out the booking summary rows - those
        /// are a day's total wearing a job's clothes, not a house, and one
        /// has no price, no time and no customer by definition.
        /// </summary>
        private static List<Job> OneVisitPerHouse(List<Job> all)
        {
            Dictionary<string, Job> houses = new Dictionary<string, Job>();

            foreach (Job j in all)
            {
                if (j.IsCompleted || j.HaveCanceled || j.CustomerId == -1)
                    continue;

                Job kept;
                houses.TryGetValue(j.SameJobKey, out kept);
                houses[j.SameJobKey] = Job.NextDue(kept, j);
            }

            return new List<Job>(houses.Values);
        }
    }
}
