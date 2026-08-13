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
    /// </summary>
    public class RoundStats
    {
        /// <summary>how many houses are still to do</summary>
        public int HousesLeft;

        /// <summary>and what they come to</summary>
        public float ValueLeft;

        /// <summary>and how long they are reckoned to take, in minutes</summary>
        public int MinutesLeft;

        /// <summary>houses that were due before today rather than on it</summary>
        public int HousesOverdue;

        /// <summary>everything on the round, done or not</summary>
        public int HousesOnTheRound;

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
            RoundStats stats = new RoundStats();

            //a copy, because the queries below hand back a buffer they share
            List<Job> jobs = new List<Job>(Job.Query());

            Dictionary<string, MonthOfWork> byMonth = new Dictionary<string, MonthOfWork>();
            DateTime earliest = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));

            foreach (Job j in jobs)
            {
                if (j.HaveCanceled)
                    continue;

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

                //everything from here is work still waiting
                stats.HousesOnTheRound++;
                stats.ValuePerMonth += PerMonth(j);

                if (j.DueDate.Date > today.Date)
                    continue;

                stats.HousesLeft++;
                stats.ValueLeft += j.EffectivePrice;
                //a house with no estimate of its own still takes the round's
                //usual, which is what the calendar counts it as
                stats.MinutesLeft += j.Minutes;

                if (j.DueDate.Date < today.Date)
                    stats.HousesOverdue++;
            }

            stats.Months = byMonth.Values
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToList();

            foreach (Customer c in Customer.Query())
                if (c.Balance > 0)
                {
                    stats.MoneyOwed += c.Balance;
                    stats.CustomersOwing++;
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
