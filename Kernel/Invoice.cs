using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Kernel
{
    /// <summary>
    /// One line on an invoice - what it is, how many, and what each one comes
    /// to. The line total is worked out rather than stored, so the two can
    /// never disagree, and the invoice totals itself off these.
    /// </summary>
    public class InvoiceLine
    {
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// the day this line is for - the clean it is billing for. optional
        /// (MinValue = none), because a line like "conservatory roof" is not
        /// about a day. When several cleans are owed, each is a line with its
        /// own date.
        /// </summary>
        public DateTime Date { get; set; } = DateTime.MinValue;

        /// <summary>how many. one by default - most lines are a single job</summary>
        public float Quantity { get; set; } = 1;

        /// <summary>what one of them costs</summary>
        public float UnitPrice { get; set; } = 0;

        /// <summary>whether this line carries a date</summary>
        [XmlIgnore]
        public bool HasDate
        {
            get { return Date > DateTime.MinValue; }
        }

        /// <summary>quantity times the price. never stored - see the class summary</summary>
        [XmlIgnore]
        public float LineTotal
        {
            get { return Quantity * UnitPrice; }
        }

        public InvoiceLine DeepCopy()
        {
            return new InvoiceLine()
            {
                Description = Description,
                Date = Date,
                Quantity = Quantity,
                UnitPrice = UnitPrice,
            };
        }
    }

    /// <summary>
    /// A bill handed to a customer: a header of who it is from and who it is
    /// to, a list of lines, and a total worked out off them.
    ///
    /// An invoice is a **record** and is kept as one (`invoices.rjt`, one
    /// global file like the balance adjustments and the day notes - an invoice
    /// belongs to a customer, not to a tax year, and it is not itself a tax
    /// figure). It rides in every backup with the rest of the round.
    ///
    /// Two things are deliberately a **snapshot** taken when the invoice is
    /// made rather than read live: who it is billed to (name and address) and
    /// the price on each line. An invoice is what the customer was actually
    /// sent, so a price rise agreed afterwards, an address corrected, or the
    /// customer being merged away must not quietly rewrite a bill already
    /// handed over. The customer is still linked by <see cref="CustomerId"/>
    /// so their invoices can be found, but nothing on the invoice is read back
    /// off them.
    ///
    /// The <see cref="Number"/> is the sequential invoice number the customer
    /// sees. It is handed out in order and never reused, so the counter is
    /// kept in the file alongside the invoices - a gap in the numbers is
    /// something an accountant asks about.
    /// </summary>
    public class Invoice
    {
        private static int _IdGenerator = 0;

        /// <summary>the next invoice number to hand out - see the class summary</summary>
        private static int _NextNumber = 1;

        private static List<Invoice> _Invoices = new List<Invoice>();

        /// <summary>the id used to find this invoice - not the one shown</summary>
        public int Id { get; set; }

        /// <summary>the invoice number the customer sees, handed out in order</summary>
        public int Number { get; set; }

        /// <summary>who it is for, or -1 for an invoice written from scratch</summary>
        public int CustomerId { get; set; } = -1;

        public DateTime Date { get; set; }

        /// <summary>when payment is due, or MinValue for none</summary>
        public DateTime DueDate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// who the invoice is billed to, taken down when it is made so a later
        /// change to the customer does not rewrite a bill already sent
        /// </summary>
        public string BillToName { get; set; } = string.Empty;

        /// <summary>the bill-to address, one line per line break, same reason</summary>
        public string BillToAddress { get; set; } = string.Empty;

        public List<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

        /// <summary>anything to say on it - a thank you, when it is due, how to pay</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// whether it has been paid. false is awaiting payment, which is what
        /// an invoice starts as and what an older file reads back as
        /// </summary>
        public bool Paid { get; set; } = false;

        /// <summary>when it was marked paid, or MinValue</summary>
        public DateTime DatePaid { get; set; } = DateTime.MinValue;

        [XmlIgnore]
        public string StatusText
        {
            get { return Paid ? "Paid" : "Awaiting payment"; }
        }

        /// <summary>the invoice totalled up off its lines</summary>
        [XmlIgnore]
        public float Total
        {
            get
            {
                float total = 0;
                if (Lines != null)
                    foreach (InvoiceLine line in Lines)
                        total += line.LineTotal;
                return total;
            }
        }

        /// <summary>the number the way it is shown - INV-0001</summary>
        [XmlIgnore]
        public string FormattedNumber
        {
            get { return $"INV-{Number:0000}"; }
        }

        [XmlIgnore]
        public string FormattedTotal
        {
            get { return $"{Gloable.CurrenceSymbol}{Total:0.00}"; }
        }

        [XmlIgnore]
        public string FormattedDate
        {
            get { return Date.ToString("d MMM yyyy"); }
        }

        /// <summary>one line for a list row: number, date, who it is for, total</summary>
        [XmlIgnore]
        public string Summary
        {
            get
            {
                string who = string.IsNullOrWhiteSpace(BillToName) ? "No name" : BillToName.Trim();
                return $"{FormattedNumber}  -  {who}  -  {FormattedTotal}";
            }
        }

        //  ----------------------------------------------------  the invoices

        /// <summary>
        /// the next number that would be handed out, for showing on a new
        /// invoice before it is saved. it is not taken until the invoice is
        /// actually added, so backing out of one does not leave a gap
        /// </summary>
        public static int PeekNextNumber()
        {
            return _NextNumber;
        }

        /// <summary>
        /// adds a new invoice, giving it its id and the next invoice number.
        /// the number is taken here rather than when the editor opens, so an
        /// invoice started and abandoned does not use one up.
        /// </summary>
        public static Invoice Add(Invoice invoice)
        {
            if (invoice == null)
                return null;

            invoice.Id = _IdGenerator++;
            invoice.Number = _NextNumber++;
            if (invoice.Lines == null)
                invoice.Lines = new List<InvoiceLine>();

            _Invoices.Add(invoice);
            Save();
            return invoice;
        }

        /// <summary>
        /// writes changes to an invoice already saved - the same object is
        /// edited in place, so this only puts it on disk
        /// </summary>
        public static void Update(Invoice invoice)
        {
            if (invoice == null)
                return;

            //the editor works on its own object, so an existing invoice is
            //matched by id and swapped rather than trusting reference equality
            int at = _Invoices.FindIndex(x => x.Id == invoice.Id);
            if (at >= 0)
                _Invoices[at] = invoice;
            else
                _Invoices.Add(invoice);

            Save();
        }

        public static Invoice ById(int id)
        {
            return _Invoices.Find(x => x.Id == id);
        }

        /// <summary>every invoice, newest first, as a copy of the list</summary>
        public static List<Invoice> Query()
        {
            return _Invoices.OrderByDescending(x => x.Number).ToList();
        }

        /// <summary>this customer's invoices, newest first</summary>
        public static List<Invoice> ForCustomer(int customerId)
        {
            return _Invoices.FindAll(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.Number).ToList();
        }

        public static void Delete(int id)
        {
            int before = _Invoices.Count;
            _Invoices.RemoveAll(x => x.Id == id);
            if (_Invoices.Count != before)
                Save();
        }

        /// <summary>a merged duplicate's invoices follow it to the kept customer</summary>
        public static void MoveCustomer(int fromCustomerId, int toCustomerId)
        {
            bool moved = false;

            foreach (Invoice invoice in _Invoices)
                if (invoice.CustomerId == fromCustomerId)
                {
                    invoice.CustomerId = toCustomerId;
                    moved = true;
                }

            if (moved)
                Save();
        }

        /// <summary>
        /// After money has come in, mark a customer's awaiting invoices paid
        /// when they now owe nothing.
        ///
        /// "If all the money has been paid" is read off the balance: a
        /// customer's balance is the running total of work done minus money
        /// received, so a balance at or below zero (a half penny of float
        /// slack) means everything billed to them has been covered. This is
        /// called after bank reconciliation records the payments, so a
        /// statement that clears what somebody owed marks their invoice paid
        /// without it being ticked by hand.
        ///
        /// Only invoices attached to a customer are touched - one written from
        /// scratch (<see cref="CustomerId"/> -1) has no customer to clear.
        /// </summary>
        /// <returns>how many invoices were marked paid</returns>
        public static int MarkPaidForClearedCustomers(IEnumerable<int> customerIds)
        {
            if (customerIds == null)
                return 0;

            HashSet<int> seen = new HashSet<int>();
            int changed = 0;

            foreach (int id in customerIds)
            {
                if (id < 0 || !seen.Add(id))
                    continue;

                Customer c = Customer.ById(id);
                if (c == null || c.Balance > 0.005f)
                    continue;

                foreach (Invoice invoice in _Invoices)
                    if (invoice.CustomerId == id && !invoice.Paid)
                    {
                        invoice.Paid = true;
                        invoice.DatePaid = UsfulFuctions.DateNow;
                        changed++;
                    }
            }

            if (changed > 0)
                Save();

            return changed;
        }

        public static void DeleteData()
        {
            _Invoices.Clear();
            _IdGenerator = 0;
            _NextNumber = 1;
        }

        //  --------------------------------------------------------  the file

        private const string _FilePath = "invoices.rjt";

        public struct SaveData
        {
            public List<Invoice> Invoices;
            public int NextId;

            /// <summary>the next invoice number to hand out - see the class summary</summary>
            public int NextNumber;
        }

        private static string PathFor(string dir)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(dir))
                folder = Path.Combine(folder, dir);
            return Path.Combine(folder, _FilePath);
        }

        public static void Save(string dir = null)
        {
            SaveData data = new SaveData();
            data.Invoices = new List<Invoice>(_Invoices);
            data.NextId = _IdGenerator;
            data.NextNumber = _NextNumber;

            if (YearlyStore.WriteIfChanged(PathFor(dir), YearlyStore.Serialise(data)))
                DataStamp.Touch(DataStamp.Invoices, dir);

            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            _Invoices.Clear();
            _IdGenerator = 0;
            _NextNumber = 1;

            try
            {
                using (FileStream fs = File.OpenRead(PathFor(dir)))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(SaveData));
                    SaveData data = (SaveData)xs.Deserialize(fs);

                    if (data.Invoices != null)
                        _Invoices.AddRange(data.Invoices);
                    _IdGenerator = data.NextId;
                    _NextNumber = data.NextNumber;
                }

                //the counters must always be ahead of what is on the file: an
                //id handed out twice muddles two invoices together, and a
                //number reused is a duplicate bill. an older file with no
                //NextNumber in it reads back as 0, so it is worked out here
                foreach (Invoice invoice in _Invoices)
                {
                    if (invoice.Lines == null)
                        invoice.Lines = new List<InvoiceLine>();
                    if (invoice.Id >= _IdGenerator)
                        _IdGenerator = invoice.Id + 1;
                    if (invoice.Number >= _NextNumber)
                        _NextNumber = invoice.Number + 1;
                }

                if (_NextNumber < 1)
                    _NextNumber = 1;
            }
            catch
            {
                //no file yet is a round that has never made an invoice
            }
        }
    }
}
