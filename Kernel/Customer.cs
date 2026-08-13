using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public partial class Customer
    {
        /// <summary>
        /// the master id number
        /// </summary>
        private static int _IdGenerator = 0;


        /// <summary>
        /// all the customers
        /// </summary>
        private static List<Customer> _Customers = new List<Customer>();

        

        public static ResultType Add(string houseNameNumber, string street)
        {
            return Add(new Customer(houseNameNumber, street));
        }

        public static ResultType Add(Customer customer)
        {
            Console.WriteLine($"Adding new customer {customer.Address.PropertyNameNumber} {customer.Address.Street}");

            customer.GenerateId();
            _Customers.Add(customer);
        //    Console.WriteLine("Failed to add customer");
            return ResultType.Fail;
        }


        private static List<Customer> _tmpQuery = new List<Customer>();

        public static List<Customer> Query(string property, string value)
        {
            Filter f = new Filter(property, value);
            return Query(f);
        }
        public static List<Customer> Query(Filter filter)
        {
            Console.Write("QUERYING RESULTS >> ");
            _tmpQuery = new List<Customer>();
            // foreach (Customer c in _Customers)
            //   _tmpQuery.Add(c.DeepCopy());
            
            _tmpQuery.AddRange(_Customers);

            foreach (FilterItem fi in filter.filters)
                Filter(fi);
            return _tmpQuery;
        }
        public static List<Customer> Query()
        {
            Console.Write("QUERYING RESULTS >> ");
            _tmpQuery = new List<Customer>();
            //  foreach (Customer c in _Customers)
            //    _tmpQuery.Add(c.DeepCopy());
            _tmpQuery.AddRange(_Customers);


            Console.WriteLine();
            return _tmpQuery;
        }

        public static void Delete(int id)
        {
            _Customers.RemoveAll(x => x.Id == id);
        }
        public static void CalculateCustomerBill()
        {
            List<Customer> c;

            foreach (Customer customer in _Customers)
                customer.Balance = 0;


            foreach (Job j in Job.Query())
                if (j.IsCompleted)
                {
                    c = Customer.Query("id", $"{j.CustomerId}");
                    if (c.Count > 0)
                        c[0].Balance += j.Price;
                }

            
            foreach (Payment p in Payment.Query())
            {
                c = Customer.Query("id", $"{p.CustomerId}");
                if (c.Count > 0)
                    c[0].Balance -= p.Amount;
            }
        }

        public static void DeleteData()
        {
            _Customers.Clear();
        }

        /// <summary>
        /// The streets, towns and areas already on the round, for suggesting
        /// as an address is typed. A round is a few streets done over and
        /// over, so what has been typed before is nearly always what is
        /// wanted - and it stops the same street going in three different
        /// ways and splitting itself up in the lists.
        /// </summary>
        public static List<string> KnownStreets()
        {
            return Known(x => x.Address == null ? null : x.Address.Street);
        }

        public static List<string> KnownCities()
        {
            return Known(x => x.Address == null ? null : x.Address.City);
        }

        public static List<string> KnownAreas()
        {
            return Known(x => x.Address == null ? null : x.Address.Area);
        }

        private static List<string> Known(Func<Customer, string> part)
        {
            List<string> known = new List<string>();

            foreach (Customer c in _Customers)
            {
                string value = part(c);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                value = value.Trim();
                if (!known.Exists(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                    known.Add(value);
            }

            known.Sort(StringComparer.CurrentCultureIgnoreCase);
            return known;
        }

        /// <summary>
        /// The town and area a street is in, going by the customers already
        /// on it. A street sits in one town, so once the street is known the
        /// rest of the address usually is too and does not need typing.
        /// Returns null when the street is new.
        /// </summary>
        public static Location AddressForStreet(string street)
        {
            if (string.IsNullOrWhiteSpace(street))
                return null;

            street = street.Trim();

            //the commonest answer rather than the first, in case one was put
            //in wrong at some point
            Dictionary<string, int> counts = new Dictionary<string, int>();
            Dictionary<string, Location> seen = new Dictionary<string, Location>();

            foreach (Customer c in _Customers)
            {
                if (c.Address == null || c.Address.Street == null)
                    continue;

                if (!string.Equals(c.Address.Street.Trim(), street, StringComparison.OrdinalIgnoreCase))
                    continue;

                string key = $"{c.Address.City}|{c.Address.Area}".ToLowerInvariant();
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                seen[key] = c.Address;
            }

            string best = null;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> pair in counts)
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }

            return best == null ? null : seen[best];
        }

        /// <summary>just the numbers, so 07700 900123 and 07700900123 are the same number</summary>
        private static string DigitsOf(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            StringBuilder digits = new StringBuilder();
            foreach (char c in text)
                if (char.IsDigit(c))
                    digits.Append(c);

            return digits.ToString();
        }

        private static void Filter(FilterItem filter)
        {
            string property = filter.Property.ToLower();
            string value = filter.Value.ToLower();
            

            switch (property)
            {
                case "street":
                    Console.Write($"Where property: {property} = {value}   ");
                    if (filter.Absolute)
                        _tmpQuery.RemoveAll(x => x.Address.Street != null && x.Address.Street.ToLower() != value);
                    else
                        _tmpQuery.RemoveAll(x => x.Address.Street != null && !x.Address.Street.ToLower().Contains(value));
                    break;

                case "id":
                    Console.Write($"Where id: {property} = {value}   ");
                    _tmpQuery.RemoveAll(x => x.Id.ToString() != value);
                    break;

                case "name":
                    Console.Write($"Where id: {property} = {value}   ");
                    if (filter.Absolute)
                        _tmpQuery.RemoveAll(x => x.FName.ToLower() != value);
                    else
                        _tmpQuery.RemoveAll(x => !x.FName.ToLower().Contains(value));
                    break;

                case "phone":
                    //a number is written down every way there is - with spaces,
                    //with the code in brackets - so only the digits are matched
                    Console.Write($"Where property: {property} = {value}   ");
                    string wanted = DigitsOf(value);
                    if (wanted.Length == 0)
                    {
                        _tmpQuery.Clear();
                        break;
                    }
                    _tmpQuery.RemoveAll(x => !DigitsOf(x.Phone).Contains(wanted));
                    break;
            }
        }

        public static void ListQueryDebug()
        {
            foreach (Customer c in _tmpQuery)
                Console.WriteLine($"Customer> ID: {c.Id} Name: {c.FName} {c.SName}, Address: {c.Address.PropertyNameNumber} {c.Address.Street}");
        }

        /// <summary>
        /// generate the id number for the current customer
        /// </summary>
        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        /// <summary>
        /// id number for this customer this will be uniqte
        /// </summary>
        public int Id { get; set; }

        public string FName { get; set; } = string.Empty;
        public string SName { get; set; } = string.Empty;

        /// <summary>
        /// the address of the customer
        /// only house and street required
        /// </summary>
        public Location Address { get; set; }

        public string FormattedAddress { get { return Address.ToString(); } }

        /// <summary>
        /// name, phone and email run together. this is for telling two
        /// customers apart in a picker, where there is one line to do it in -
        /// it is not a name, so do not show it as one
        /// </summary>
        public string FormattedOverview
        {
            get
            {
                return $"{FName} {SName} {Phone} {Email}";
            }
        }

        /// <summary>
        /// what the customer is called. falls back to the address, because a
        /// row with nothing on it is no use for picking the right customer
        /// </summary>
        public string FormattedName
        {
            get
            {
                string name = $"{FName} {SName}".Trim();
                return name.Length > 0 ? name : FormattedAddress;
            }
        }

        /// <summary>the phone number and email, for the line under the address</summary>
        public string FormattedContact
        {
            get
            {
                List<string> parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Phone))
                    parts.Add(Phone);
                if (!string.IsNullOrWhiteSpace(Email))
                    parts.Add(Email);
                return string.Join("   ", parts);
            }
        }

        public bool HaveContact
        {
            get { return FormattedContact.Length > 0; }
        }
        /// <summary>
        /// contant number
        /// </summary>
        public string Phone = string.Empty;

        public string Email = string.Empty;

        /// <summary>
        /// internal propertiy for use with the app and some futrure information
        /// </summary>
        public DateTime DateAdded;

        /// <summary>
        /// the current balance of the customer
        /// </summary>
        public float Balance;

        /// <summary>
        /// the date the balance was last checked
        /// </summary>
        public DateTime DateBalanceLastUpdate;

        /// <summary>
        /// the normal way the customer pays
        /// in the future this will be auto populated if left empty
        /// </summary>
        public PaymentMethod NormalPaymentMethord = PaymentMethod.Other;

        /// <summary>
        /// a list of references to link payments to this customer.
        /// </summary>
        public List<string> PaymentRefrences = new List<string>();

        /// <summary>
        /// where a PayPal request goes, when it is not the address they are
        /// emailed everything else at. blank means use their normal email
        /// </summary>
        public string PayPalEmail = string.Empty;

        /// <summary>
        /// the address to send a PayPal payment link to. most people use one
        /// address for everything, so the normal one stands in
        /// </summary>
        public string PayPalContact
        {
            get { return string.IsNullOrWhiteSpace(PayPalEmail) ? Email : PayPalEmail; }
        }

        /// <summary>
        /// this customer is set up to be asked for money through PayPal.
        /// there is nothing to link - it is a link sent to them - so all it
        /// takes is saying that is how they pay
        /// </summary>
        public bool PaysByPayPal()
        {
            return NormalPaymentMethord == PaymentMethod.Paypal;
        }

        /// <summary>
        /// the customer id (CUxxxx) in GoCardless once linked
        /// </summary>
        public string GoCardlessCustomerId = string.Empty;

        /// <summary>
        /// the direct debit mandate id (MDxxxx) used to collect payments
        /// </summary>
        public string GoCardlessMandateId = string.Empty;

        /// <summary>
        /// true when this customer is linked to a GoCardless direct debit
        /// </summary>
        public bool HasGoCardless()
        {
            return !string.IsNullOrWhiteSpace(GoCardlessMandateId);
        }
        public Customer() {
            Address = Location.None;
        }
        public Customer(string houseNameNumber, string street)
        {
            Address = new Location()
            {
                Street = street,
                PropertyNameNumber = houseNameNumber,
            };
        }

        public Customer(string houseNameNumber, string street, string city)
        {
            Address = new Location()
            {
                Street = street,
                PropertyNameNumber = houseNameNumber,
                City = city
            };
        }

        private static Customer _garbaeCollectorLimiter;
        public Customer DeepCopy()
        {
            _garbaeCollectorLimiter = new Customer()
            {
                DateAdded = this.DateAdded,
                Address = this.Address.DeepCopy(),
                Email = this.Email,
                Phone = this.Phone,
                FName = this.FName,
                SName = this.SName,
                Id = this.Id,
                NormalPaymentMethord = this.NormalPaymentMethord,
            };
            return _garbaeCollectorLimiter;
        }
        
    }
}
