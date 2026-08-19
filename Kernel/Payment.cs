using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public partial class Payment
    {
        private static Action<Payment> PaymentMatchNotFound;

        /// <summary>
        /// the master id number
        /// </summary>
        private static int _IdGenerator = 0;

        /// <summary>
        /// generate the id number for the current customer
        /// </summary>
        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        /// <summary>
        /// the master list of jobs
        /// </summary>
        private static List<Payment> _Payments = new List<Payment>();

        /// <summary>
        /// this exact payment has already been recorded. re-importing the same
        /// statement - or the next one, which overlaps it - has to find it
        /// again rather than take the money twice
        /// </summary>
        public static bool AlreadyRecorded(string paymentRef, float amount, DateTime date)
        {
            return _Payments.Exists(x => x.CustomerReference == paymentRef
                && x.Amount == amount
                && x.Date == date);
        }

        /// <summary>the customer a statement reference belongs to, or null</summary>
        public static Customer CustomerForReference(string paymentRef)
        {
            if (paymentRef == null)
                return null;

            foreach (Customer c in Customer.Query())
                foreach (string s in c.PaymentRefrences)
                    if (s == paymentRef)
                        return c;

            return null;
        }

        public static Payment AddToCustomer(string paymentRef, float amount, DateTime date, PaymentMethod paymentType, out bool found)
        {
            Customer c = CustomerForReference(paymentRef);
            if (c == null)
            {
                found = false;
                return null;
            }

            found = true;
            if (AlreadyRecorded(paymentRef, amount, date))
                return null;

            return Add(c.Id, amount, paymentType, paymentRef, date);
        }
        public static Payment Add(Payment payment, bool DeductBallence)
        {
            payment.GenerateId();
            
            payment.CrossReferenceWithJobs();
            if (DeductBallence)
                payment.UpdateCustomerBalance();
            _Payments.Add(payment);
            return payment;
        }

        public static Payment Add(int customerId, float amount, PaymentMethod method, string reference)
        {
            return Add(customerId, amount,method, reference, DateTime.Now);
        }

        public static Payment Get(int id)
        {
            //walked rather than FindAll'd: this wants one payment, and
            //FindAll built a list of every match to hand back the first
            foreach (Payment p in _Payments)
                if (p.Id == id)
                    return p;

            return new Payment() { Id = -1};
        }
        public static float Remove(int id)
        {
            List<Payment> p = _Payments.FindAll(x => x.Id == id);
            float tmp = 0;
            if (p.Count > 0)
                tmp = p[0].Amount;
            _Payments.RemoveAll(x => x.Id == id);

            return tmp;
        }

        public static void DeleteData()
        {
            _Payments.Clear();
        }

        /// <summary>
        /// hands every payment taken off one customer to another, for putting
        /// two records of the same person back into one
        /// </summary>
        /// <returns>how many were moved</returns>
        public static int MoveToCustomer(int fromCustomerId, int intoCustomerId)
        {
            int moved = 0;
            foreach (Payment p in _Payments)
                if (p.CustomerId == fromCustomerId)
                {
                    p.CustomerId = intoCustomerId;
                    moved++;
                }

            return moved;
        }

        public static Payment Add(int customerId, float amount, PaymentMethod method, string reference, DateTime date)
        {

            Payment payment = new Payment()
            {
                CustomerId = customerId,
                Amount = amount,
                PaymentMethod = method,
                Date = date,
                CustomerReference = reference,
            };
            payment.GenerateId();
            payment.CrossReferenceWithJobs();
            payment.UpdateCustomerBalance();
            _Payments.Add(payment);
            return payment;
        }


        public static ResultType Add(float amount, PaymentMethod method, string reference)
        {
            return Add(amount, method, reference, DateTime.Now);
        }
        public static ResultType Add(float amount, PaymentMethod method, string reference, DateTime date)
        {
            Payment payment = new Payment()
            {
                CustomerId = -1, //set to -1 to flag as no customer found
                Amount = amount,
                PaymentMethod = method,
                Date = date,
                CustomerReference = reference,
            };
            payment.GenerateId();
            payment.CrossReferenceWithJobs();
            payment.UpdateCustomerBalance();
            _Payments.Add(payment);
            return ResultType.Success;
        }

        public static List<Payment> Query()
        {
            return _Payments;
        }

        public static List<Payment> Query(QueryType qtype, object query)
        {
            List<Payment> pay = new List<Payment>();
            pay.AddRange(_Payments);
            switch (qtype)
            {
                case QueryType.CustomerId:
                    pay.RemoveAll(x => x.CustomerId != (int)query);
                    return pay;
            }
            return null;
        }

        private static Customer GetCustomer(int customerId)
        {
            return Customer.ById(customerId);
        }
        /// <summary>
        /// the payment method for this payment
        /// </summary>
        public PaymentMethod PaymentMethod { get; set; }

        /// <summary>
        /// the amount paid
        /// </summary>
        public float Amount { get; set; }

        /// <summary>
        /// the date of the payment
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// the customer id to link this payment too
        /// </summary>
        public int CustomerId;

        public int Id;

        /// <summary>
        /// the reference string for this payment
        /// </summary>
        public string CustomerReference { get; set; }

      

        private Customer _customer;

        /// <summary>
        /// payment references told to stay out of the statement import. these
        /// stick for every statement from now on, so it matters that one
        /// added by mistake can be taken back out again
        /// </summary>
        public static List<string> IgnorePaymentList = new List<string>();

        public static bool IsIgnored(string reference)
        {
            if (IgnorePaymentList == null || reference == null)
                return false;

            return IgnorePaymentList.Contains(reference);
        }

        /// <summary>
        /// stops ignoring a reference, so it comes back on the next statement
        /// import ready to be linked to a customer
        /// </summary>
        public static void StopIgnoring(string reference)
        {
            if (IgnorePaymentList == null || reference == null)
                return;

            IgnorePaymentList.RemoveAll(x => x == reference);
        }

        public static void StopIgnoringEverything()
        {
            if (IgnorePaymentList != null)
                IgnorePaymentList.Clear();
        }
        /// <summary>
        /// who this payment came from, kept hold of once found. The payments
        /// page names the customer on every row, so this is asked once a row
        /// - see <see cref="Job.MatchCustomer"/> for why the id is checked
        /// as well as the cache
        /// </summary>
        public void MatchCustomer()
        {
            if (_customer != null && _customer.Id == CustomerId)
                return;

            _customer = Customer.ById(CustomerId);
        }

        public Customer GetCustomer()
        {
            MatchCustomer();
            return _customer;
        }

        /// <summary>
        /// this function will check this payment and mark it agains any active jobs
        /// </summary>
        public void CrossReferenceWithJobs()
        {
            List<Customer> customers = Customer.Query();

            for (int i =0; i < customers.Count; i++)
                foreach(string s in customers[i].PaymentRefrences)
                    if (s == this.CustomerReference) //if we have found a customer to match this payment to
                    {
                        CustomerId = customers[i].Id; //set the id
                        return;
                    }

            if (PaymentMatchNotFound != null)
                PaymentMatchNotFound(this);
        }

        public void UpdateCustomerBalance()
        {
            if (CustomerId == -1)//if there are no matches for what customer has paid we cannot update the balance
                return;

            Customer c = Customer.ById(CustomerId);
            if (c != null)
                c.Balance -= Amount;

            Save();
            Customer.Save();
        }

        public string PaymentAmount
        {
            get
            {
                return $"Paid {Gloable.CurrenceSymbol}{Amount.ToString()}";
            }
        }

        private static int tmpInt;
        public string PaymentDaysAgo
        {
            get
            {
                tmpInt = UsfulFuctions.Difference(Date, DateTime.Now);

                if (tmpInt == 0)
                    return "Today";

                if (tmpInt == 1)
                    return "Yesterday";

                return $"{tmpInt} days ago";
            }
        }
        public string PaymentDate
        {
            get
            {
                if ((Date - UsfulFuctions.DateNow).Days == 0)
                    return "Today";

                if ((Date - UsfulFuctions.DateNow).Days == -1)
                    return "Yesterday";

                return $"{Date.ToShortDateString()}";
            }
        }

        public string GetCustomerDetails
        {
            get
            {
                Customer c = GetCustomer(CustomerId);
                if (c == null)
                {
                    return "Unidentifyed Payment! Tap Here to link";
                }
                return $"{c.FName} {c.FormattedAddress}";
            }
        }
    }
}
