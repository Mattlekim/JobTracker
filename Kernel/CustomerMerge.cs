using System;
using System.Collections.Generic;

namespace Kernel
{
    /// <summary>
    /// What to do with the money on the customer being merged away.
    /// </summary>
    public enum MergeBalance
    {
        /// <summary>leave the one being kept as it is</summary>
        Keep,

        /// <summary>take the figure off the one being merged away</summary>
        Take,

        /// <summary>add the two together</summary>
        Add,
    }

    /// <summary>
    /// Putting two customer records back together.
    ///
    /// Editing a job's details used to make a whole new customer for a job
    /// that already had one and point the job at it, leaving the original
    /// behind with the balance and the payments but no work. That is fixed,
    /// but the records it made are still on the books - and they still count
    /// towards the money owed on the work list, because that adds up every
    /// customer in credit or in debt whether they have work or not.
    ///
    /// So this is a tidy up rather than a feature: it finds the records with
    /// no work against them, says which customer looks like the same person,
    /// and puts the two back into one.
    /// </summary>
    public partial class Customer
    {
        /// <summary>
        /// the customers with no work against them at all - no jobs on the
        /// round, and no quotes waiting either
        /// </summary>
        public static List<Customer> WithoutWork()
        {
            HashSet<int> working = new HashSet<int>();

            foreach (Job j in Job.Query())
                working.Add(j.CustomerId);

            foreach (Job q in Job.QueryQuotes())
                working.Add(q.CustomerId);

            List<Customer> spare = new List<Customer>();
            foreach (Customer c in _Customers)
                if (!working.Contains(c.Id))
                    spare.Add(c);

            return spare;
        }

        /// <summary>
        /// the customers that look like the same person as this one, best
        /// match first.
        ///
        /// the duplicate was made from the job being edited, so it carries
        /// that job's address - which is why the address is what is matched
        /// on first. a name or a phone number is worth a look after that,
        /// because someone can be on two addresses.
        /// </summary>
        public static List<Customer> LooksLikeSameAs(Customer c)
        {
            List<Customer> matches = new List<Customer>();
            if (c == null)
                return matches;

            List<Customer> others = new List<Customer>();
            foreach (Customer o in _Customers)
                if (o.Id != c.Id)
                    others.Add(o);

            foreach (Customer o in others)
                if (SameAddress(c, o))
                    matches.Add(o);

            foreach (Customer o in others)
                if (!matches.Contains(o) && SamePhone(c, o))
                    matches.Add(o);

            foreach (Customer o in others)
                if (!matches.Contains(o) && SameName(c, o))
                    matches.Add(o);

            return matches;
        }

        private static bool Same(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            return string.Equals(a.Trim(), b.Trim(), StringComparison.CurrentCultureIgnoreCase);
        }

        private static bool SameAddress(Customer a, Customer b)
        {
            if (a.Address == null || b.Address == null)
                return false;

            return Same(a.Address.PropertyNameNumber, b.Address.PropertyNameNumber)
                && Same(a.Address.Street, b.Address.Street);
        }

        private static bool SamePhone(Customer a, Customer b)
        {
            string one = DigitsOf(a.Phone);
            return one.Length >= 6 && one == DigitsOf(b.Phone);
        }

        private static bool SameName(Customer a, Customer b)
        {
            return Same($"{a.FName} {a.SName}", $"{b.FName} {b.SName}");
        }

        /// <summary>
        /// how much of the record would be lost by simply deleting this
        /// customer rather than merging them - payments taken, direct debits
        /// and a balance are all things worth keeping
        /// </summary>
        public bool HoldsRecords()
        {
            return Payment.Query(QueryType.CustomerId, Id).Count > 0
                || GoCardlessRequest.QueryByCustomer(Id).Count > 0
                || Math.Abs(Balance) > 0.005f
                || HasGoCardless();
        }

        /// <summary>
        /// puts <paramref name="from"/> into <paramref name="into"/> and
        /// deletes it.
        ///
        /// everything that points at the old record is moved rather than
        /// dropped: its payments, its direct debit requests, its bank
        /// references, and anything the record being kept has not got - a
        /// surname, a phone number, a mandate. nothing is written over,
        /// because the record being kept is the one in use.
        /// </summary>
        /// <returns>false when there is nothing sensible to merge</returns>
        public static bool Merge(Customer from, Customer into, MergeBalance balance)
        {
            if (from == null || into == null || from.Id == into.Id)
                return false;

            //work first, so a merge is safe even on a customer that has some.
            //by the time this is offered they have none, but that is the
            //caller's doing rather than something to rely on here
            foreach (Job j in Job.Query())
                if (j.CustomerId == from.Id)
                    j.CustomerId = into.Id;

            foreach (Job q in Job.QueryQuotes())
                if (q.CustomerId == from.Id)
                    q.CustomerId = into.Id;

            Payment.MoveToCustomer(from.Id, into.Id);
            GoCardlessRequest.MoveToCustomer(from.Id, into.Id);

            //the write-offs and hand-set balances are part of the money
            //story, and the story follows the customer that is kept
            BalanceAdjustment.MoveCustomer(from.Id, into.Id);

            if (string.IsNullOrWhiteSpace(into.FName))
                into.FName = from.FName;

            if (string.IsNullOrWhiteSpace(into.SName))
                into.SName = from.SName;

            if (string.IsNullOrWhiteSpace(into.Phone))
                into.Phone = from.Phone;

            if (string.IsNullOrWhiteSpace(into.Email))
                into.Email = from.Email;

            if (string.IsNullOrWhiteSpace(into.GoCardlessCustomerId))
                into.GoCardlessCustomerId = from.GoCardlessCustomerId;

            if (string.IsNullOrWhiteSpace(into.GoCardlessMandateId))
                into.GoCardlessMandateId = from.GoCardlessMandateId;

            if (into.NormalPaymentMethord == PaymentMethod.Other)
                into.NormalPaymentMethord = from.NormalPaymentMethord;

            //the bank references are how a payment off a statement finds its
            //way to a customer, so both sets are worth keeping
            if (from.PaymentRefrences != null)
            {
                if (into.PaymentRefrences == null)
                    into.PaymentRefrences = new List<string>();

                foreach (string r in from.PaymentRefrences)
                    if (!into.PaymentRefrences.Exists(x => Same(x, r)))
                        into.PaymentRefrences.Add(r);
            }

            //the older record is the one that has been on the books, so its
            //date is the one that means anything
            if (from.DateAdded != default(DateTime) && (into.DateAdded == default(DateTime) || from.DateAdded < into.DateAdded))
                into.DateAdded = from.DateAdded;

            switch (balance)
            {
                case MergeBalance.Take:
                    into.Balance = from.Balance;
                    break;

                case MergeBalance.Add:
                    into.Balance += from.Balance;
                    break;
            }

            Delete(from.Id);

            Customer.Save();
            Payment.Save();
            Job.Save();
            GoCardlessRequest.Save();
            return true;
        }
    }
}
