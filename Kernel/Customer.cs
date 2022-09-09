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

        public string FormattedOverview
        {
            get
            {
                return $"{FName} {SName} {Phone} {Email}";
            }
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

        public void AddBallenceCorrection(string note, float newBallence)
        {
            //calcuate what payment amount we need to add
            float amount = Balance - newBallence;

            Payment.Add(Id, amount, PaymentMethod.BallenceCorrection, "", note);
            Payment.Save();
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
