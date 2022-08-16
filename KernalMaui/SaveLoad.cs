using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Kernel
{
    public struct CustomerSaveData
    {
        public List<Customer> Customers;
        public int NextCustomerId;
    }


    public partial class Customer
    {

        private static string _FilePath = "customers.rjt";
        private static bool _FilePathSet = false;
        public static void Save()
        {
            if (!_FilePathSet)
            {
                _FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);
                _FilePathSet = true;
            }

            CustomerSaveData csd = new CustomerSaveData()
            {

            };
            csd.Customers = new List<Customer>();
            csd.Customers.AddRange(_Customers);
            csd.NextCustomerId = _IdGenerator;
            using (FileStream fs = File.Create(_FilePath))
            {
                XmlSerializer xs = new XmlSerializer(typeof(CustomerSaveData));
                xs.Serialize(fs, csd);

            }
        }
        public static void Load()
        {
            try
            {
                if (!_FilePathSet)
                {
                    _FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);
                    _FilePathSet = true;
                }

                CustomerSaveData csd = new CustomerSaveData()
                {

                };

                using (FileStream fs = File.OpenRead(_FilePath))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(CustomerSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    csd = (CustomerSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.


                    _Customers.Clear();
                    _Customers.AddRange(csd.Customers);
                    _IdGenerator = csd.NextCustomerId;
                }
            }
            catch
            {
            }
        }

    }

    public struct JobSaveData
    {
        public List<Job> Jobs;
        public int NextJobId;
    }

    public partial class Job
    {
        private static string _FilePath = _FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jobs.rjt"); 

        public static void Save()
        {
            JobSaveData csd = new JobSaveData()
            {

            };
            csd.Jobs = new List<Job>();
            csd.Jobs.AddRange(_Jobs);
            csd.NextJobId = _IdGenerator;

            using (FileStream fs = File.Create(_FilePath))
            {
                XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
                xs.Serialize(fs, csd);

            }

        }

        public static bool _Loaded = false;
        public static void Load()
        {
            if (_Loaded)
                return;

            
            JobSaveData csd = new JobSaveData()
            {

            };

            try
            {
                using (FileStream fs = File.OpenRead(_FilePath))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    csd = (JobSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.


                    _Jobs.Clear();
                    _Jobs.AddRange(csd.Jobs);
                    _IdGenerator = csd.NextJobId;
                }
                _Loaded = true;
            }
            catch (Exception ex)
            { }

        }
    }

    public struct PaymentSaveData
    {
        public List<Payment> Payments;
        public int NextPaymentId;
    }

    public partial struct Payment
    {
        private static string _FilePath = _FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "payment.rjt");
        public static void Save()
        {
            PaymentSaveData psd = new PaymentSaveData()
            {

            };
            psd.Payments = new List<Payment>();
            psd.Payments.AddRange(_Payments);
            psd.NextPaymentId = _IdGenerator;

            using (FileStream fs = File.Create(_FilePath))
            {
                XmlSerializer xs = new XmlSerializer(typeof(PaymentSaveData));
                xs.Serialize(fs, psd);

            }

        }

        public static void Load()
        {
            PaymentSaveData csd = new PaymentSaveData()
            {

            };

            using (FileStream fs = File.OpenRead(_FilePath))
            {
                XmlSerializer xs = new XmlSerializer(typeof(PaymentSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                csd = (PaymentSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.


                _Payments.Clear();
                _Payments.AddRange(csd.Payments);
                _IdGenerator = csd.NextPaymentId;
            }

        }
    }
}
