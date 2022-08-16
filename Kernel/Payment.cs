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

        public static Payment AddToCustomer(string paymentRef, float amount, DateTime date, PaymentMethod paymentType, out bool found)
        {
            Payment p = new Payment();

            List<Customer> customers = Customer.Query();
            foreach (Customer c in customers)
                foreach (string s in c.PaymentRefrences)
                {
                    if (s == paymentRef)
                    {
                        List<Payment> payments = _Payments.FindAll(x => x.CustomerReference == paymentRef && x.Amount == amount && x.Date == date);

                        found = true;
                        if (payments == null || payments.Count == 0)
                            return Add(c.Id, amount, paymentType, paymentRef, date);
                        return null;
                    }
                }

            found = false;
            return null;
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
            List<Payment> p = _Payments.FindAll(x => x.Id == id);
            float tmp = 0;
            if (p.Count > 0)
                return p[0];

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
            List<Customer> customers = Customer.Query("id",$"{customerId}");
            if (customers.Count > 0)
                return customers[0];

            return null;
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

        public static List<string> IgnorePaymentList = new List<string>();
        public void MatchCustomer()
        {
            if (_customer == null)
            {

                try
                {
                    List<Customer> c = Customer.Query("id", CustomerId.ToString());
                    if (c.Count > 0)
                        _customer = c[0];
                }
                catch
                {
                    return;
                }
            }
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

            List<Customer> c = Customer.Query("id", $"{CustomerId}");
            if (c.Count > 0)
                c[0].Balance -= Amount;

            Save();
            Customer.Save();
        }

        public string PaymentType
        {
            get
            {
                return PaymentMethod.ToString();
            }
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
