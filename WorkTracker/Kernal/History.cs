using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public class History
    {
        public Job TheJob;
        public Payment ThePayment;
        public BalanceAdjustment TheAdjustment;

        public string Notes { get; set; }
        public DateTime SortDate;

        public Color DateColour { get; private set; }
        public Color TextDateColour { get; private set; }

        public bool IsJob { get; private set; }

        /// <summary>a write-off or a balance set by hand, in with the money it explains</summary>
        public bool IsAdjustment { get; private set; }

        public bool IsPayment
        {
            get
            {
                return !IsJob && !IsAdjustment;
            }
        }
        public Color AltColour { get; set; }

        /// <summary>
        /// the tags on this visit, as one line. tags belong to the one time
        /// the job was done, so this is what says which of the visits in the
        /// history were front only, or had nobody in, or whatever else
        /// </summary>
        public string TagsText
        {
            get { return TheJob == null ? string.Empty : TheJob.TagsText; }
        }

        public bool HaveTags
        {
            get { return TheJob != null && TheJob.HaveTags; }
        }

        public History(Job theJob)
        {
            TheJob = theJob;
            Notes = TheJob.Notes;
            _customer = TheJob.GetCustomer();
            if (TheJob.IsCompleted)
            {
                SortDate = TheJob.DateCompleated;
                DateColour = Colors.Gray;
                TextDateColour = Colors.Black;
            }
            else
            {
                DateColour = Color.FromArgb("005C9E");
                SortDate = TheJob.DueDate;
                TextDateColour = Colors.White;
            }

            IsJob = true;

            

            if (_customer == null)
                return;

            if (_customer.Balance > 0)
                OwingColour = Colors.Red;
            else
            if (_customer.Balance < 0)
                OwingColour = Colors.Green;
            else
                OwingColour = Colors.Grey;
        }

        public History(Payment thePayment)
        {
            ThePayment = thePayment;

            SortDate = thePayment.Date;

            //the method's own colour rather than green for all of them, so a
            //customer who pays cash and a customer on a direct debit do not
            //read the same. it is the same colour the payments page uses,
            //because it is the same property answering
            DateColour = thePayment.MethodColour;
            TextDateColour = thePayment.MethodTextColour;
            IsJob = false;
        }

        /// <summary>
        /// a balance changed outside the ledgers - debt written off, or a
        /// figure typed in. it sits in the history with the visits and the
        /// payments because it is the missing line that makes them add up
        /// </summary>
        public History(BalanceAdjustment theAdjustment)
        {
            TheAdjustment = theAdjustment;

            SortDate = theAdjustment.Date;

            //its own colour: it is not work and it is not money that moved
            DateColour = Color.FromArgb("#6A1B9A");
            TextDateColour = Colors.White;
            IsJob = false;
            IsAdjustment = true;
        }

        public string AdjustmentDate
        {
            get
            {
                if (TheAdjustment == null)
                    return string.Empty;

                return TheAdjustment.Kind == BalanceAdjustmentKind.WriteOff
                    ? $"Written off {TheAdjustment.Date.ToShortDateString()}"
                    : $"Balance changed {TheAdjustment.Date.ToShortDateString()}";
            }
        }

        private Customer _customer;

        public Color OwingColour { get; private set; } = Colors.Grey;

        public string CreditDebit
        {
            get
            {
                if (_customer == null)
                    return String.Empty;

                if (_customer.Balance > 0)
                    return " in debt";
                else
                    return " in credit";
            }
        }
        public string Owing
        {
            get
            {
                _customer = TheJob.GetCustomer();
                if (_customer == null)
                {
                    return $"{Gloable.CurrenceSymbol}0.00";
                }

                if (_customer.Balance > 0)
                {
                 
                    return $"{Gloable.CurrenceSymbol}{Math.Abs(_customer.Balance)}";
                }

                if (_customer.Balance < 0)
                {
                    return $"{Gloable.CurrenceSymbol}{Math.Abs(_customer.Balance)}";
                }

                return $"{Gloable.CurrenceSymbol}{Math.Abs(_customer.Balance)}";
            }
        }
        public string JobDate
        {
            get
            {
                if (TheJob == null)
                    return String.Empty;

                if (TheJob.IsCompleted)
                    return $"{TheJob.JobFormattedDueTime} {TheJob.FormattedData}";
                
                return $"{TheJob.JobFormattedDueTime} {TheJob.DueDate.ToShortDateString()}";
            }
        }

        public string PaymentDate
        {
            get
            {
                if (ThePayment == null)
                    return String.Empty;

                //the date on its own once it is old enough for "N days ago"
                //to mean nothing, and both while it still says something.
                //ThePayment.Date on its own printed the midnight behind it
                if (ThePayment.ShowAge)
                    return $"Payment received {ThePayment.PaymentDate} ({ThePayment.PaymentDaysAgo})";

                return $"Payment received {ThePayment.PaymentDate}";
            }
        }

        /// <summary>
        /// the picture of how it was paid, white stroked so it reads on the
        /// banner it sits on. empty for anything that is not a payment
        /// </summary>
        public string PaymentIcon
        {
            get { return ThePayment == null ? string.Empty : ThePayment.MethodIcon; }
        }

        /// <summary>
        /// the second line is the price of a visit, the reason behind an
        /// adjustment and the bank's reference on a payment - and cash has no
        /// reference, so on those there is no second line rather than a chip
        /// saying NA
        /// </summary>
        public bool HaveLine2
        {
            get { return IsJob || IsAdjustment || (ThePayment != null && ThePayment.HasReference); }
        }

        public string FormattedLine1
        {
        
            get {

                if (IsJob)
                    return $"Job Address: {TheJob.Address.PropertyNameNumber} {TheJob.Address.DisplayStreet} {TheJob.Address.DisplayCity}";

                if (IsAdjustment)
                    return TheAdjustment.Describe;

                return $"Paid by {ThePayment.MethodName} {Gloable.CurrenceSymbol}{ThePayment.Amount}";
            }
        }
        public string FormattedLine2
        {
            get {
                if (IsJob)
                {
                    if (TheJob.IsCompleted)
                    {
                        AlternativePrice chosen = TheJob.ChosenAlternativePrice;
                        if (chosen == null)
                            return $"Normal Job. Price {Gloable.CurrenceSymbol}{TheJob.Price}";

                        return $"{chosen.Description}. Price {Gloable.CurrenceSymbol}{chosen.Price}";
                    }
                    else
                        return "NA";
                }

                //the why is the whole worth of the record - said plainly when
                //there is one, and honestly when there is not
                if (IsAdjustment)
                    return TheAdjustment.Reason.Length > 0
                        ? $"Reason: {TheAdjustment.Reason}"
                        : "No reason noted";

                //whatever the money came in through can carry a reference,
                //not just a bank transfer, and the line is hidden altogether
                //when there is none - see HaveLine2
                return $"Payment reference: {ThePayment.CustomerReference}";

            }
        }
    }
}
