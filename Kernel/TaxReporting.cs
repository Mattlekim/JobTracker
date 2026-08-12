using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    /// <summary>
    /// how income is counted. cash basis (money in and out when it moves) is
    /// the default for sole traders; accruals counts work when it is done
    /// </summary>
    public enum AccountingBasis
    {
        Cash,
        Accruals,
    }

    /// <summary>
    /// the expense boxes HMRC asks for on a self employment return. app
    /// expense categories are grouped into these for quarterly updates
    /// </summary>
    public enum HmrcExpenseCategory
    {
        CostOfGoods,
        CarVanTravel,
        StaffCosts,
        PremisesRunningCosts,
        RepairsAndRenewals,
        AdminCosts,
        Advertising,
        InterestAndFinance,
        ProfessionalFees,
        OtherExpenses,
    }

    /// <summary>
    /// a stretch of time the figures are reported for: one of the four
    /// quarterly updates, or a whole tax year
    /// </summary>
    public class TaxPeriod
    {
        public string Name = string.Empty;
        public DateTime Start;
        /// <summary>the last day of the period, included in the figures</summary>
        public DateTime End;
        /// <summary>when a quarterly update has to be with HMRC</summary>
        public DateTime Due;
        public bool IsWholeYear;

        public bool Contains(DateTime date)
        {
            return date.Date >= Start.Date && date.Date <= End.Date;
        }

        public string FormattedDates
        {
            get { return $"{Start.ToShortDateString()} to {End.ToShortDateString()}"; }
        }
    }

    /// <summary>
    /// the UK tax year (6 April to 5 April) and the quarterly update
    /// periods Making Tax Digital reports against
    /// </summary>
    public static class TaxCalendar
    {
        /// <summary>
        /// the year a tax year starts in, for a date. 5 April 2027 is still
        /// the 2026/27 tax year, 6 April 2027 starts 2027/28
        /// </summary>
        public static int TaxYearOf(DateTime date)
        {
            if (date.Month > 4 || (date.Month == 4 && date.Day >= 6))
                return date.Year;
            return date.Year - 1;
        }

        public static DateTime YearStart(int taxYear)
        {
            return new DateTime(taxYear, 4, 6);
        }

        public static DateTime YearEnd(int taxYear)
        {
            return new DateTime(taxYear + 1, 4, 5);
        }

        /// <summary>the tax year written the way HMRC do, e.g. "2026/27"</summary>
        public static string YearName(int taxYear)
        {
            return $"{taxYear}/{(taxYear + 1) % 100:00}";
        }

        /// <summary>
        /// what a tax year's folder of receipts and statements is called.
        /// a slash cannot go in a folder name, so it is "2026-27"
        /// </summary>
        public static string YearFolderName(int taxYear)
        {
            return $"{taxYear}-{(taxYear + 1) % 100:00}";
        }

        /// <summary>
        /// every tax year there is anything recorded for, oldest first, and
        /// always the one we are in now. this is what the backup and export
        /// pages offer to pick from
        /// </summary>
        public static List<int> YearsWithData()
        {
            HashSet<int> years = new HashSet<int> { TaxYearOf(UsfulFuctions.DateNow) };

            foreach (Payment p in Payment.Query())
                years.Add(TaxYearOf(p.Date));

            foreach (Expense e in Expense.Query())
                years.Add(TaxYearOf(e.Date));

            foreach (StatementRecord s in StatementRecord.Query())
                years.Add(s.TaxYear);

            List<int> sorted = years.Where(y => y >= 2000 && y <= 2100).ToList();
            sorted.Sort();
            return sorted;
        }

        public static TaxPeriod WholeYear(int taxYear)
        {
            return new TaxPeriod
            {
                Name = $"Tax year {YearName(taxYear)}",
                Start = YearStart(taxYear),
                End = YearEnd(taxYear),
                //the year end return is due the following 31 January
                Due = new DateTime(taxYear + 2, 1, 31),
                IsWholeYear = true,
            };
        }

        /// <summary>
        /// the four quarterly update periods. standard periods run to the
        /// 5th; the calendar option runs to month ends instead, which has to
        /// be elected with HMRC
        /// </summary>
        public static List<TaxPeriod> Quarters(int taxYear, bool calendarQuarters)
        {
            List<TaxPeriod> quarters = new List<TaxPeriod>();

            //each quarterly update is due on the 7th of the second month
            //after the quarter ends
            DateTime[] due =
            {
                new DateTime(taxYear, 8, 7),
                new DateTime(taxYear, 11, 7),
                new DateTime(taxYear + 1, 2, 7),
                new DateTime(taxYear + 1, 5, 7),
            };

            DateTime[] starts;
            DateTime[] ends;

            if (calendarQuarters)
            {
                starts = new[]
                {
                    new DateTime(taxYear, 4, 1),
                    new DateTime(taxYear, 7, 1),
                    new DateTime(taxYear, 10, 1),
                    new DateTime(taxYear + 1, 1, 1),
                };
                ends = new[]
                {
                    new DateTime(taxYear, 6, 30),
                    new DateTime(taxYear, 9, 30),
                    new DateTime(taxYear, 12, 31),
                    new DateTime(taxYear + 1, 3, 31),
                };
            }
            else
            {
                starts = new[]
                {
                    new DateTime(taxYear, 4, 6),
                    new DateTime(taxYear, 7, 6),
                    new DateTime(taxYear, 10, 6),
                    new DateTime(taxYear + 1, 1, 6),
                };
                ends = new[]
                {
                    new DateTime(taxYear, 7, 5),
                    new DateTime(taxYear, 10, 5),
                    new DateTime(taxYear + 1, 1, 5),
                    new DateTime(taxYear + 1, 4, 5),
                };
            }

            for (int i = 0; i < 4; i++)
                quarters.Add(new TaxPeriod
                {
                    Name = $"Quarter {i + 1}",
                    Start = starts[i],
                    End = ends[i],
                    Due = due[i],
                });

            return quarters;
        }

        /// <summary>the HMRC box name for an expense category</summary>
        public static string HmrcCategoryName(HmrcExpenseCategory category)
        {
            switch (category)
            {
                case HmrcExpenseCategory.CostOfGoods: return "Cost of goods bought for resale or goods used";
                case HmrcExpenseCategory.CarVanTravel: return "Car, van and travel expenses";
                case HmrcExpenseCategory.StaffCosts: return "Wages, salaries and other staff costs";
                case HmrcExpenseCategory.PremisesRunningCosts: return "Rent, rates, power and insurance costs";
                case HmrcExpenseCategory.RepairsAndRenewals: return "Repairs and renewals of property and equipment";
                case HmrcExpenseCategory.AdminCosts: return "Phone, fax, stationery and other office costs";
                case HmrcExpenseCategory.Advertising: return "Advertising and business entertainment costs";
                case HmrcExpenseCategory.InterestAndFinance: return "Interest and bank, credit card and other financial charges";
                case HmrcExpenseCategory.ProfessionalFees: return "Accountancy, legal and other professional fees";
                default: return "Other business expenses";
            }
        }

        /// <summary>
        /// which HMRC box an app expense category falls into. this is a
        /// sensible starting point rather than tax advice - an accountant
        /// may want some of it moved
        /// </summary>
        public static HmrcExpenseCategory HmrcCategoryFor(ExpenseCategory category)
        {
            switch (category)
            {
                case ExpenseCategory.Fuel:
                case ExpenseCategory.Vehicle:
                    return HmrcExpenseCategory.CarVanTravel;

                case ExpenseCategory.Materials:
                    return HmrcExpenseCategory.CostOfGoods;

                case ExpenseCategory.Equipment:
                    return HmrcExpenseCategory.RepairsAndRenewals;

                case ExpenseCategory.Insurance:
                    return HmrcExpenseCategory.PremisesRunningCosts;

                case ExpenseCategory.BankCharges:
                    return HmrcExpenseCategory.InterestAndFinance;

                default:
                    return HmrcExpenseCategory.OtherExpenses;
            }
        }
    }

    /// <summary>
    /// the income and expense figures for one period, in the shape a
    /// quarterly update asks for
    /// </summary>
    public class TaxSummary
    {
        public TaxPeriod Period;
        public AccountingBasis Basis;

        public float Income;
        public int IncomeCount;

        public float TotalExpenses;
        public int ExpenseCount;

        /// <summary>how much went in each HMRC box</summary>
        public Dictionary<HmrcExpenseCategory, float> ExpensesByCategory = new Dictionary<HmrcExpenseCategory, float>();

        /// <summary>expenses with no receipt photo attached to back them up</summary>
        public int ExpensesWithoutReceipt;

        public float Profit
        {
            get { return Income - TotalExpenses; }
        }

        public string FormattedIncome { get { return $"{Gloable.CurrenceSymbol}{Income:0.00}"; } }
        public string FormattedExpenses { get { return $"{Gloable.CurrenceSymbol}{TotalExpenses:0.00}"; } }
        public string FormattedProfit { get { return $"{Gloable.CurrenceSymbol}{Profit:0.00}"; } }

        /// <summary>
        /// work out the figures for a period. cash basis counts payments
        /// when they were received; accruals counts jobs when they were done
        /// </summary>
        public static TaxSummary Build(TaxPeriod period, AccountingBasis basis)
        {
            TaxSummary summary = new TaxSummary
            {
                Period = period,
                Basis = basis,
            };

            if (basis == AccountingBasis.Cash)
            {
                foreach (Payment p in Payment.Query())
                    if (period.Contains(p.Date))
                    {
                        summary.Income += p.Amount;
                        summary.IncomeCount++;
                    }
            }
            else
            {
                foreach (Job j in Job.Query())
                    if (j.IsCompleted && !j.HaveCanceled && period.Contains(j.DateCompleated))
                    {
                        summary.Income += j.EffectivePrice;
                        summary.IncomeCount++;
                    }
            }

            foreach (Expense e in Expense.Query())
                if (period.Contains(e.Date))
                {
                    summary.TotalExpenses += e.Amount;
                    summary.ExpenseCount++;
                    if (!e.HasReceipt)
                        summary.ExpensesWithoutReceipt++;

                    HmrcExpenseCategory box = TaxCalendar.HmrcCategoryFor(e.Category);
                    if (summary.ExpensesByCategory.ContainsKey(box))
                        summary.ExpensesByCategory[box] += e.Amount;
                    else
                        summary.ExpensesByCategory[box] = e.Amount;
                }

            return summary;
        }

        /// <summary>the four quarterly updates plus the year, for a tax year</summary>
        public static List<TaxSummary> BuildYear(int taxYear, AccountingBasis basis, bool calendarQuarters)
        {
            List<TaxSummary> summaries = new List<TaxSummary>();
            foreach (TaxPeriod q in TaxCalendar.Quarters(taxYear, calendarQuarters))
                summaries.Add(Build(q, basis));
            summaries.Add(Build(TaxCalendar.WholeYear(taxYear), basis));
            return summaries;
        }
    }
}
