using System;
using System.Collections.Generic;

namespace Kernel
{
    /// <summary>
    /// one month of a Universal Credit claim - an assessment period. It runs
    /// from the day the claim started to the day before the same date the
    /// next month, and the earnings and spending inside it are what has to be
    /// reported at the end of it.
    /// </summary>
    public class UniversalCreditPeriod
    {
        /// <summary>0 is the first month of the claim</summary>
        public int Index;

        public DateTime Start;

        /// <summary>the last day of the month, included in the figures</summary>
        public DateTime End;

        public bool Contains(DateTime date)
        {
            return date.Date >= Start.Date && date.Date <= End.Date;
        }

        /// <summary>the month a claim is on, counting from one</summary>
        public int Number
        {
            get { return Index + 1; }
        }

        /// <summary>the two dates, which is what the month is actually called</summary>
        public string FormattedDates
        {
            get { return $"{Start:d MMM yyyy} to {End:d MMM yyyy}"; }
        }

        /// <summary>
        /// the month being worked through now. A month still running is not
        /// the whole story - the rest of its money is still to come - which
        /// is why the pages mark it rather than showing its figures like the
        /// finished ones
        /// </summary>
        public bool IsCurrent(DateTime today)
        {
            return Contains(today);
        }
    }

    /// <summary>
    /// Universal Credit is worked out a month at a time rather than a tax
    /// year at a time, and the month is the claim's own: it starts on the day
    /// the claim did and every one after it starts on that date again.
    ///
    /// So this is deliberately nothing to do with <see cref="TaxCalendar"/>.
    /// Everything on the tax page is 6 April to 5 April; nothing here lines up
    /// with that, and a figure off one is no answer to the other.
    ///
    /// The figures are what actually moved - money received and money paid
    /// out inside the month - because that is what a claim is reported on.
    /// </summary>
    public static class UniversalCredit
    {
        /// <summary>
        /// the day the claim started, which is what every month is measured
        /// from. Kept here rather than on Settings for the same reason
        /// Job.DefaultDuration is - the sums are here, and the settings own
        /// the figure by writing it down (see Settings.UniversalCreditStart).
        ///
        /// MinValue is nobody having said, which is not the same as a date of
        /// its own: with no start date there are no months to report on at
        /// all, and the page says so instead of guessing at one.
        /// </summary>
        public static DateTime StartDate = DateTime.MinValue;

        public static bool HaveStartDate
        {
            get { return StartDate > new DateTime(2000, 1, 1); }
        }

        /// <summary>
        /// the month at this position, counting the claim's first month as 0.
        ///
        /// Every month is measured from the claim's own start date rather
        /// than from the month before it, which is what keeps a claim that
        /// started on the 31st on the 31st. AddMonths pulls a date back to
        /// the last day of a short month - the 31st of January is the 28th of
        /// February - and stepping a month on from that at a time would leave
        /// the claim on the 28th for ever after. Counted from the start each
        /// time, March is the 31st again, which is what the claim really does.
        /// </summary>
        public static UniversalCreditPeriod Period(DateTime claimStart, int index)
        {
            DateTime start = claimStart.Date.AddMonths(index);
            DateTime end = claimStart.Date.AddMonths(index + 1).AddDays(-1);

            return new UniversalCreditPeriod
            {
                Index = index,
                Start = start,
                End = end,
            };
        }

        /// <summary>the month a date falls in, or null before the claim began</summary>
        public static UniversalCreditPeriod PeriodOn(DateTime claimStart, DateTime date)
        {
            if (date.Date < claimStart.Date)
                return null;

            //close enough to land on or just before the right month, then
            //walked the last step or two: the months are not all the same
            //length, so the arithmetic cannot be trusted to the day
            int guess = ((date.Year - claimStart.Year) * 12) + date.Month - claimStart.Month;
            if (guess < 0)
                guess = 0;

            UniversalCreditPeriod period = Period(claimStart, guess);

            while (date.Date < period.Start.Date && period.Index > 0)
                period = Period(claimStart, period.Index - 1);

            while (date.Date > period.End.Date)
                period = Period(claimStart, period.Index + 1);

            return period;
        }

        /// <summary>
        /// every month from the claim's first up to and including the one the
        /// given day falls in. Newest first is left to whoever is drawing them
        /// </summary>
        public static List<UniversalCreditPeriod> PeriodsTo(DateTime claimStart, DateTime upTo)
        {
            List<UniversalCreditPeriod> periods = new List<UniversalCreditPeriod>();

            if (upTo.Date < claimStart.Date)
                return periods;

            UniversalCreditPeriod last = PeriodOn(claimStart, upTo);
            if (last == null)
                return periods;

            for (int i = 0; i <= last.Index; i++)
                periods.Add(Period(claimStart, i));

            return periods;
        }
    }

    /// <summary>
    /// what went in and what went out over one assessment period.
    ///
    /// It is always the money that actually moved - payments received and
    /// expenses paid inside the month - because that is what a claim is
    /// reported on. There is no accruals option here on purpose: work done
    /// and not paid for is not earnings for a claim, whatever it is for tax.
    /// </summary>
    public class UniversalCreditSummary
    {
        public UniversalCreditPeriod Period;

        /// <summary>money that came in over the month</summary>
        public float Income;
        public int IncomeCount;

        /// <summary>money that went out over the month</summary>
        public float Expenses;
        public int ExpenseCount;

        /// <summary>
        /// what is left. It can be a loss, and it is said as one: a month
        /// that cost more than it took is a real thing to have to explain,
        /// and hiding it behind a nought would only make the figures look
        /// like they had been made up
        /// </summary>
        public float Profit
        {
            get { return Income - Expenses; }
        }

        public bool IsLoss
        {
            get { return Profit < 0; }
        }

        public string FormattedIncome { get { return $"{Gloable.CurrenceSymbol}{Income:0.00}"; } }
        public string FormattedExpenses { get { return $"{Gloable.CurrenceSymbol}{Expenses:0.00}"; } }
        public string FormattedProfit { get { return $"{Gloable.CurrenceSymbol}{Profit:0.00}"; } }

        public static UniversalCreditSummary Build(UniversalCreditPeriod period)
        {
            UniversalCreditSummary summary = new UniversalCreditSummary
            {
                Period = period,
            };

            if (period == null)
                return summary;

            foreach (Payment p in Payment.Query())
                if (period.Contains(p.Date))
                {
                    summary.Income += p.Amount;
                    summary.IncomeCount++;
                }

            foreach (Expense e in Expense.Query())
                if (period.Contains(e.Date))
                {
                    summary.Expenses += e.Amount;
                    summary.ExpenseCount++;
                }

            return summary;
        }

        /// <summary>every month of the claim so far, newest first</summary>
        public static List<UniversalCreditSummary> BuildAll(DateTime claimStart, DateTime upTo)
        {
            List<UniversalCreditSummary> summaries = new List<UniversalCreditSummary>();

            List<UniversalCreditPeriod> periods = UniversalCredit.PeriodsTo(claimStart, upTo);
            for (int i = periods.Count - 1; i >= 0; i--)
                summaries.Add(Build(periods[i]));

            return summaries;
        }
    }
}
