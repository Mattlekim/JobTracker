using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernel
{
    /// <summary>
    /// what a month's work came to
    /// </summary>
    public class MonthOfWork
    {
        public int Year;
        public int Month;

        /// <summary>what was charged for the work done in it</summary>
        public float Value;

        /// <summary>how many houses that was</summary>
        public int Houses;

        public string Name
        {
            get { return new DateTime(Year, Month, 1).ToString("MMMM yyyy"); }
        }

        public string ShortName
        {
            get { return new DateTime(Year, Month, 1).ToString("MMM yy"); }
        }
    }

    /// <summary>
    /// The figures for the round as a whole - what is still to do, what it is
    /// worth, and what has been done month by month.
    ///
    /// It is all worked out here rather than on the page so it can be checked
    /// without the app, and so the same definition of "left to do" is used
    /// everywhere: not done, not cancelled, and due today or before. Work
    /// booked in for a day still counts as left, because it is.
    ///
    /// Two things are counted here and they are counted differently on
    /// purpose. What is *left to do* is a number of visits, the same as the
    /// work list counts it. How big the *round* is is a number of houses,
    /// worked out a job at a time through <see cref="Job.SameJobKey"/> - the
    /// job list keeps every visit of a house, and a house is one house
    /// however many visits of it happen to be outstanding.
    /// </summary>
    public class RoundStats
    {
        /// <summary>what No Round is called when it is put on screen</summary>
        public const string NoRound = "No Round";

        /// <summary>
        /// which round these figures are for. empty on the figures for the
        /// whole round, which is everything added together
        /// </summary>
        public string Round = string.Empty;

        /// <summary>the round's name as it is shown</summary>
        public string RoundName
        {
            get { return string.IsNullOrWhiteSpace(Round) ? NoRound : Round; }
        }

        /// <summary>how many houses are still to do</summary>
        public int HousesLeft;

        /// <summary>and what they come to</summary>
        public float ValueLeft;

        /// <summary>and how long they are reckoned to take, in minutes</summary>
        public int MinutesLeft;

        /// <summary>houses that were due before today rather than on it</summary>
        public int HousesOverdue;

        /// <summary>
        /// how many houses the round is made of. Houses, not visits: the job
        /// list holds every visit of a house, so a house with more than one
        /// visit still outstanding is counted once here even though it is two
        /// jobs to do
        /// </summary>
        public int HousesOnTheRound;

        /// <summary>
        /// what every house on it comes to - one time round, not what is left
        /// to do today. This is the round as a thing you own rather than as a
        /// day's work: how much it is worth to clean the lot once
        /// </summary>
        public float ValueOfTheRound;

        /// <summary>and how long cleaning the lot would take, in minutes</summary>
        public int MinutesForTheRound;

        public string FormattedTimeForTheRound
        {
            get { return FormatMinutes(MinutesForTheRound); }
        }

        /// <summary>what every customer in debt owes, added up</summary>
        public float MoneyOwed;

        /// <summary>and how many of them there are</summary>
        public int CustomersOwing;

        /// <summary>
        /// what the round is worth in a month if everything is done when it
        /// falls due. worked out from how often each job comes round, so a
        /// job done every four weeks counts more than one done every eight.
        /// one offs are left out - they are not coming round again
        /// </summary>
        public float ValuePerMonth;

        /// <summary>the months that had work in them, most recent first</summary>
        public List<MonthOfWork> Months = new List<MonthOfWork>();

        /// <summary>what the months on this list came to on average</summary>
        public float AverageMonth
        {
            get
            {
                if (Months.Count == 0)
                    return 0;

                float total = 0;
                foreach (MonthOfWork m in Months)
                    total += m.Value;

                return total / Months.Count;
            }
        }

        /// <summary>the busiest month on the list, or null when there is none</summary>
        public MonthOfWork BestMonth
        {
            get
            {
                MonthOfWork best = null;
                foreach (MonthOfWork m in Months)
                    if (best == null || m.Value > best.Value)
                        best = m;

                return best;
            }
        }

        public string FormattedTimeLeft
        {
            get { return FormatMinutes(MinutesLeft); }
        }

        /// <summary>minutes as hours and minutes, because 465 means nothing</summary>
        public static string FormatMinutes(int minutes)
        {
            if (minutes <= 0)
                return "0m";

            int hours = minutes / 60;
            int left = minutes % 60;

            if (hours == 0)
                return $"{left}m";

            if (left == 0)
                return $"{hours}h";

            return $"{hours}h {left}m";
        }

        /// <summary>
        /// the figures as they stand.
        /// </summary>
        /// <param name="months">how many months of work done to look back over</param>
        public static RoundStats Now(int months = 12)
        {
            return Now(UsfulFuctions.DateNow, months);
        }

        /// <summary>the figures as they stood on a given day, for testing</summary>
        public static RoundStats Now(DateTime today, int months)
        {
            //a copy, because the queries below hand back a buffer they share
            return Build(new List<Job>(Job.Query()), today, months, string.Empty, true);
        }

        /// <summary>
        /// the same figures, a round at a time.
        ///
        /// A round is a patch of the work, so what is left on one says
        /// nothing about the others: a day's worth still to do in the village
        /// is a different thing from the same figure spread over everywhere.
        /// The rounds come back in the order the work pages use - by name,
        /// with the work nobody has put on a round last. Only rounds that
        /// actually have work on them come back at all.
        /// </summary>
        public static List<RoundStats> ByRound(int months = 12)
        {
            return ByRound(UsfulFuctions.DateNow, months);
        }

        /// <summary>a round at a time, as they stood on a given day</summary>
        public static List<RoundStats> ByRound(DateTime today, int months)
        {
            List<Job> all = new List<Job>(Job.Query());

            //the round of the whole job rather than of each visit of it, so
            //every visit of a house lands in the same group. Splitting a
            //house between two groups is what put a "No Round" made of
            //nothing but finished visits on this page
            Dictionary<string, string> roundOfJob = Job.RoundsOfEveryJob(all);

            Dictionary<string, List<Job>> byRound = new Dictionary<string, List<Job>>(StringComparer.CurrentCultureIgnoreCase);

            foreach (Job j in all)
            {
                string round;
                if (!roundOfJob.TryGetValue(j.SameJobKey, out round))
                    round = j.HaveRound ? j.Round.Trim() : string.Empty;

                List<Job> onIt;
                if (!byRound.TryGetValue(round, out onIt))
                {
                    onIt = new List<Job>();
                    byRound[round] = onIt;
                }

                onIt.Add(j);
            }

            List<RoundStats> rounds = new List<RoundStats>();
            foreach (KeyValuePair<string, List<Job>> pair in byRound)
            {
                RoundStats stats = Build(pair.Value, today, months, pair.Key, false);

                //a round is a patch of work you have. A group with no work
                //left on it is not one - it is the finished and cancelled
                //visits of houses that are not coming round again - and it
                //drew a row saying no houses, no time and no value
                if (stats.HousesOnTheRound == 0)
                    continue;

                rounds.Add(stats);
            }

            return rounds
                .OrderBy(x => string.IsNullOrWhiteSpace(x.Round) ? 1 : 0)
                .ThenBy(x => x.Round, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// works the figures out over one list of jobs.
        /// </summary>
        /// <param name="everyCustomer">
        /// true to count what every customer owes, which is what the round as
        /// a whole is asked for - somebody who owes money with no work left
        /// still owes it. False counts only the customers with work on this
        /// list, because a debt has to be put against the round it came off,
        /// and each of them once however many houses they have on it
        /// </param>
        private static RoundStats Build(List<Job> jobs, DateTime today, int months, string round, bool everyCustomer)
        {
            RoundStats stats = new RoundStats();
            stats.Round = round;

            Dictionary<string, MonthOfWork> byMonth = new Dictionary<string, MonthOfWork>();
            DateTime earliest = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));

            //the houses the round is made of, one visit each. The job list
            //holds every visit of a house, so counting the visits counts the
            //same house twice over wherever more than one of them is still
            //outstanding - and how big a round is is a number of houses. The
            //one kept is the visit due first, which is what the house is
            //next wanted for
            Dictionary<string, Job> houses = new Dictionary<string, Job>();

            foreach (Job j in jobs)
            {
                //A clean that was done was done, and it counts whether or not
                //the job has been cancelled since. Cancelling says the house
                //is not being cleaned any more - it does not say that clean
                //never happened: the customer was charged for it when it was
                //written up and cancelling gives none of it back. Dropping
                //them here is what left a month's takings short of the same
                //days added up on the calendar, which has always counted
                //them (see CalenderView.PopulateDays).
                if (j.IsCompleted)
                {
                    if (j.DateCompleated.Date < earliest.Date)
                        continue;

                    string key = $"{j.DateCompleated.Year}-{j.DateCompleated.Month}";

                    MonthOfWork month;
                    if (!byMonth.TryGetValue(key, out month))
                    {
                        month = new MonthOfWork()
                        {
                            Year = j.DateCompleated.Year,
                            Month = j.DateCompleated.Month,
                        };
                        byMonth[key] = month;
                    }

                    month.Value += j.EffectivePrice;
                    month.Houses++;
                    continue;
                }

                //work that is not going to be done is not work in hand, and
                //the house it is at is not on the round
                if (j.HaveCanceled)
                    continue;

                //everything from here is work still waiting.
                //
                //what is left today is counted a visit at a time, because
                //that is what is left to do: two visits of a house both due
                //are two jobs, and the work list lists them both
                if (j.DueDate.Date <= today.Date)
                {
                    stats.HousesLeft++;
                    stats.ValueLeft += j.EffectivePrice;
                    //a house with no estimate of its own still takes the
                    //round's usual, which is what the calendar counts it as
                    stats.MinutesLeft += j.Minutes;

                    if (j.DueDate.Date < today.Date)
                        stats.HousesOverdue++;
                }

                //the round itself is counted a house at a time
                Job kept;
                houses.TryGetValue(j.SameJobKey, out kept);
                houses[j.SameJobKey] = Job.NextDue(kept, j);
            }

            //the round in full, whether or not it is due today
            foreach (Job j in houses.Values)
            {
                stats.HousesOnTheRound++;
                stats.ValuePerMonth += PerMonth(j);
                stats.ValueOfTheRound += j.EffectivePrice;
                stats.MinutesForTheRound += j.Minutes;
            }

            stats.Months = byMonth.Values
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            if (everyCustomer)
            {
                foreach (Customer c in Customer.Query())
                    if (c.Balance > 0)
                    {
                        stats.MoneyOwed += c.Balance;
                        stats.CustomersOwing++;
                    }
            }
            else
            {
                //the customers with work still on this round, each counted
                //once however many houses of theirs are on it. Read off the
                //work that is left rather than off every visit ever done, so
                //a customer whose houses have since moved to another round is
                //not still counted against this one
                HashSet<int> counted = new HashSet<int>();

                foreach (Job j in houses.Values)
                {
                    if (!counted.Add(j.CustomerId))
                        continue;

                    Customer c = j.GetCustomer();
                    if (c != null && c.Balance > 0)
                    {
                        stats.MoneyOwed += c.Balance;
                        stats.CustomersOwing++;
                    }
                }
            }

            return stats;
        }

        /// <summary>
        /// what one job is worth in a month. a one off is worth nothing per
        /// month - it is not coming round again - and neither is anything
        /// with no frequency to work it out from
        /// </summary>
        private static float PerMonth(Job job)
        {
            if (job.Frequence <= 0)
                return 0;

            //an average month rather than four weeks: thirteen four weekly
            //visits happen in a year, not twelve
            const float weeksInAMonth = 52.1775f / 12f;
            const float daysInAMonth = 365.25f / 12f;

            switch (job.Frequence_Type)
            {
                case FrequenceType.Day:
                    return job.EffectivePrice * (daysInAMonth / job.Frequence);

                case FrequenceType.Week:
                    return job.EffectivePrice * (weeksInAMonth / job.Frequence);

                case FrequenceType.Month:
                    return job.EffectivePrice / job.Frequence;

                case FrequenceType.Year:
                    return job.EffectivePrice / (12f * job.Frequence);
            }

            return 0;
        }
    }
}
