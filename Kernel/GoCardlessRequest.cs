using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Kernel
{
    /// <summary>
    /// where a direct debit request has got to
    /// </summary>
    public enum DirectDebitStatus
    {
        /// <summary>sent to GoCardless, money not collected yet</summary>
        Pending,
        /// <summary>the money has come through</summary>
        Paid,
        /// <summary>the payment failed or was cancelled, a new one can be sent</summary>
        Failed,
    }

    /// <summary>
    /// a record of every direct debit payment request sent to GoCardless.
    ///
    /// A job can only ever have one request outstanding: while a request is
    /// pending the job shows as "DD Pending" rather than paid, and no second
    /// request can be sent for it. The job is only marked paid once
    /// GoCardless confirms the money has actually been collected, and the
    /// payment is then recorded on the date the money left the customer's
    /// bank (the charge date) rather than the day the request was made.
    /// </summary>
    public partial class GoCardlessRequest
    {
        private static int _IdGenerator = 0;

        private static List<GoCardlessRequest> _Requests = new List<GoCardlessRequest>();

        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        public static GoCardlessRequest Add(GoCardlessRequest request)
        {
            request.GenerateId();
            _Requests.Add(request);
            return request;
        }

        public static List<GoCardlessRequest> Query()
        {
            List<GoCardlessRequest> tmp = new List<GoCardlessRequest>();
            tmp.AddRange(_Requests);
            return tmp;
        }

        /// <summary>every request still waiting on GoCardless</summary>
        public static List<GoCardlessRequest> QueryPending()
        {
            return _Requests.FindAll(x => x.Status == DirectDebitStatus.Pending);
        }

        /// <summary>
        /// the request holding up this job, or null when the job is free to
        /// have a payment requested
        /// </summary>
        public static GoCardlessRequest PendingForJob(int jobId)
        {
            //asked once a row through Job.PaymentPending, and direct debits
            //are opt-in and experimental - so on nearly every round the
            //honest answer is "there are none" and it costs a length check
            if (jobId < 0 || _Requests.Count == 0)
                return null;

            foreach (GoCardlessRequest r in _Requests)
                if (r.JobId == jobId && r.Status == DirectDebitStatus.Pending)
                    return r;

            return null;
        }

        /// <summary>
        /// true when a payment request has already been sent for this job and
        /// has not finished yet. a second request must never be sent
        /// </summary>
        public static bool HasPendingForJob(int jobId)
        {
            return PendingForJob(jobId) != null;
        }

        /// <summary>
        /// the request that paid this job, if it was paid by direct debit
        /// </summary>
        public static GoCardlessRequest PaidForJob(int jobId)
        {
            if (jobId < 0)
                return null;
            return _Requests.FirstOrDefault(x => x.JobId == jobId && x.Status == DirectDebitStatus.Paid);
        }

        public static List<GoCardlessRequest> QueryByCustomer(int customerId)
        {
            return _Requests.FindAll(x => x.CustomerId == customerId);
        }

        public static void Remove(int id)
        {
            _Requests.RemoveAll(x => x.Id == id);
        }

        /// <summary>
        /// hands every request made against one customer to another, for
        /// putting two records of the same person back into one
        /// </summary>
        /// <returns>how many were moved</returns>
        public static int MoveToCustomer(int fromCustomerId, int intoCustomerId)
        {
            int moved = 0;
            foreach (GoCardlessRequest r in _Requests)
                if (r.CustomerId == fromCustomerId)
                {
                    r.CustomerId = intoCustomerId;
                    moved++;
                }

            return moved;
        }

        public static void DeleteData()
        {
            _Requests.Clear();
        }

        public int Id { get; set; }

        /// <summary>the job this payment is for. -1 when not job specific</summary>
        public int JobId { get; set; } = -1;

        public int CustomerId { get; set; } = -1;

        /// <summary>the payment id (PMxxxx) at GoCardless</summary>
        public string GoCardlessPaymentId { get; set; } = string.Empty;

        public float Amount { get; set; }

        /// <summary>when the request was sent from this app</summary>
        public DateTime DateRequested { get; set; }

        /// <summary>
        /// the day the money is taken from the customer's bank, as told to us
        /// by GoCardless. this is the date the payment is recorded on
        /// </summary>
        public DateTime ChargeDate { get; set; }

        public DirectDebitStatus Status { get; set; } = DirectDebitStatus.Pending;

        /// <summary>the raw GoCardless status, kept for display</summary>
        public string GoCardlessStatus { get; set; } = string.Empty;

        /// <summary>the payment record created once the money came through</summary>
        public int PaymentId { get; set; } = -1;

        /// <summary>why the payment failed, when it did</summary>
        public string FailureReason { get; set; } = string.Empty;

        [XmlIgnore]
        public bool IsPending { get { return Status == DirectDebitStatus.Pending; } }

        public string FormattedAmount
        {
            get { return $"{Gloable.CurrenceSymbol}{Amount:0.00}"; }
        }

        /// <summary>
        /// short line for the UI, e.g. "£20.00 due 14/08/2026"
        /// </summary>
        public string FormattedSummary
        {
            get
            {
                switch (Status)
                {
                    case DirectDebitStatus.Pending:
                        if (ChargeDate > UsfulFuctions.DateBase)
                            return $"{FormattedAmount} direct debit due {ChargeDate.ToShortDateString()}";
                        return $"{FormattedAmount} direct debit pending";

                    case DirectDebitStatus.Paid:
                        return $"{FormattedAmount} collected {ChargeDate.ToShortDateString()}";

                    default:
                        if (string.IsNullOrWhiteSpace(FailureReason))
                            return $"{FormattedAmount} direct debit failed";
                        return $"{FormattedAmount} direct debit failed ({FailureReason})";
                }
            }
        }

        /// <summary>
        /// mark the money as arrived: records the payment against the
        /// customer on the day it was actually taken and marks the job paid
        /// </summary>
        public void SettleAsPaid(DateTime dateMoneyTaken)
        {
            if (Status == DirectDebitStatus.Paid)
                return;

            ChargeDate = dateMoneyTaken;
            Status = DirectDebitStatus.Paid;

            Payment payment = Payment.Add(CustomerId, Amount, PaymentMethod.GoCardless, GoCardlessPaymentId, dateMoneyTaken);
            PaymentId = payment.Id;

            Job job = Job.Query(QueryType.JobId, JobId).FirstOrDefault();
            if (job != null)
            {
                //the payment above has already taken the money off the
                //balance, so the job just needs linking to it
                job.MarkJobPaidByRecordedPayment(payment.Id);
                job.Refresh();
                job.RefreshColors();
            }
        }

        /// <summary>
        /// the payment will never arrive, so the job is free to be charged
        /// again
        /// </summary>
        public void SettleAsFailed(string reason)
        {
            Status = DirectDebitStatus.Failed;
            FailureReason = reason ?? string.Empty;

            Job job = Job.Query(QueryType.JobId, JobId).FirstOrDefault();
            if (job != null)
            {
                job.Refresh();
                job.RefreshColors();
            }
        }
    }
}
