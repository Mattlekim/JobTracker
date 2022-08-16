using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public partial struct Payment
    {
        private static Action<Payment> PaymentMatchNotFound;

        /// <summary>
        /// the master id number
        /// </summary>
        private static int _IdGenerator = 0;

        /// <summary>
        /// the master list of jobs
        /// </summary>
        private static List<Payment> _Payments = new List<Payment>();

        public static ResultType Add(int customerId, float amount, PaymentMethod method, string reference)
        {
            return Add(customerId, amount,method, reference, DateTime.Now);
        }

        public static ResultType Add(int customerId, float amount, PaymentMethod method, string reference, DateTime date)
        {

            Payment payment = new Payment()
            {
                CustomerId = customerId,
                Amount = amount,
                PaymentMethod = method,
                Date = date,
                CustomerReference = reference,
            };

           // payment.CrossReferenceWithJobs();
            payment.UpdateCustomerBalance();
            return ResultType.Success;
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

            payment.CrossReferenceWithJobs();
            payment.UpdateCustomerBalance();
            _Payments.Add(payment);
            return ResultType.Success;
        }

        public static List<Payment> Query()
        {
            return _Payments;
        }
        /// <summary>
        /// the payment method for this payment
        /// </summary>
        public PaymentMethod PaymentMethod { get; set; }

        /// <summary>
        /// the amount paid
        /// </summary>
        public float Amount;

        /// <summary>
        /// the date of the payment
        /// </summary>
        public DateTime Date;
        
        /// <summary>
        /// the customer id to link this payment too
        /// </summary>
        public int CustomerId;

        /// <summary>
        /// the reference string for this payment
        /// </summary>
        public string CustomerReference;
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
        }
    }
}
