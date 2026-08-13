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

        public string Notes { get; set; }
        public DateTime SortDate;

        public Color DateColour { get; private set; }
        public Color TextDateColour { get; private set; }

        public bool IsJob { get; private set; }

        public bool IsPayment
        {
            get
            {
                return !IsJob;
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

            DateColour = Colors.Green;
            TextDateColour = Colors.White;
            IsJob = false;
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
                return $"Payment recived {ThePayment.PaymentDaysAgo} {ThePayment.Date}";
            }
        }

        public string FormattedLine1
        {
        
            get {

                if (IsJob)
                    return $"Job Address: {TheJob.Address.PropertyNameNumber} {TheJob.Address.Street} {TheJob.Address.City}";
                else
                {
                    return $"Paid by {ThePayment.PaymentType} {Gloable.CurrenceSymbol}{ThePayment.Amount}";

                }
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
                
                if (ThePayment.PaymentMethod == PaymentMethod.Bank)
                    return $"Payment reference: {ThePayment.CustomerReference}"; 
                else
                    return "Payment Reference: NA";

            }
        }
    }
}
