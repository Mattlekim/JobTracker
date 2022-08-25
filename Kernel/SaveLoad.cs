using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Serialization;

using System.Collections.ObjectModel;
using System.ComponentModel;

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


        public static void Save(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            CustomerSaveData csd = new CustomerSaveData()
            {

            };
            csd.Customers = new List<Customer>();
            csd.Customers.AddRange(_Customers);
            csd.NextCustomerId = _IdGenerator;
            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(CustomerSaveData));
                xs.Serialize(fs, csd);

            }
        }
        public static void Load(string dir = null)
        {
            try
            {
                string fileLocation = string.Empty;
                if (dir != null && dir != string.Empty)
                {
                    fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                    fileLocation = Path.Combine(fileLocation, _FilePath);
                }
                else
                    fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);


                CustomerSaveData csd = new CustomerSaveData()
                {

                };

                using (FileStream fs = File.OpenRead(fileLocation))
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

    public partial class Job: INotifyPropertyChanged
    {
        private static string _FilePath = "jobs.rjt";

        public static void Save(string dir = null)
        {
            JobSaveData csd = new JobSaveData()
            {

            };

            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            csd.Jobs = new List<Job>();
            csd.Jobs.AddRange(_Jobs);
            csd.NextJobId = _IdGenerator;

            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
                xs.Serialize(fs, csd);

            }

        }

        public static bool _Loaded = false;

        public static void Reset()
        {
            _Loaded = false;
        }

        private static void FixBaseIdBug()
        {
            foreach (Job j in _Jobs)
                if (j.BaseJobId == 0)
                {
                    if (j.JobNextId == -1)
                    {
                        List<Job> linkedJobs = new List<Job>();
                        linkedJobs.Add(j);
                        int jobId = j.Id;

                        while (true)
                        {
                            Job job = _Jobs.FirstOrDefault(x => x.JobNextId == jobId);
                            if (job == null) //no matching job ie the first job in the list
                            {
                                foreach (Job jj in linkedJobs)
                                {
                                    jj.BaseJobId = jobId;
                                }
                                break;
                            }

                            linkedJobs.Add(job);
                            jobId = job.Id;
                        }

                    }
                }

        }
        public static void Load(string dir = null)
        {
            if (_Loaded)
                return;

            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            JobSaveData csd = new JobSaveData()
            {

            };

            try
            {
                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(JobSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    csd = (JobSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.

                    foreach (Job j in csd.Jobs)
                    {
                        j.DateCompleated = new DateTime(j.DateCompleated.Year, j.DateCompleated.Month, j.DateCompleated.Day);
                        j.DueDate = new DateTime(j.DueDate.Year, j.DueDate.Month, j.DueDate.Day);
                        if (j.Address.Street == null)
                            j.Address.Street = String.Empty;
                        else
                            j.Address.Street = j.Address.Street.Trim();

                        if (j.Address.City == null)
                            j.Address.City = String.Empty;
                        else
                            j.Address.City = j.Address.City.Trim();

                        if (j.Address.Area == null)
                            j.Address.Area = String.Empty;
                        else
                            j.Address.Area = j.Address.Area.Trim();


                    }

                    _Jobs.Clear();
                    _Jobs.AddRange(csd.Jobs);
                    _IdGenerator = csd.NextJobId;
                }
                FixBaseIdBug();
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
        public List<string> paymentsToIgnore;
    }

    public partial class Payment
    {
        private static string _FilePath = "payment.rjt";
        public static void Save(string dir = null)
        {

            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            PaymentSaveData psd = new PaymentSaveData()
            {

            };


            psd.Payments = new List<Payment>();
            psd.Payments.AddRange(_Payments);
            psd.NextPaymentId = _IdGenerator;
            psd.paymentsToIgnore = new List<string>();
            psd.paymentsToIgnore.AddRange(Payment.IgnorePaymentList);

            using (FileStream fs = File.Create(fileLocation))
            {
                XmlSerializer xs = new XmlSerializer(typeof(PaymentSaveData));
                xs.Serialize(fs, psd);

            }

        }

        public static void Load(string dir = null)
        {
            string fileLocation = string.Empty;
            if (dir != null && dir != string.Empty)
            {
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
                fileLocation = Path.Combine(fileLocation, _FilePath);
            }
            else
                fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _FilePath);

            PaymentSaveData csd = new PaymentSaveData()
            {

            };
            try
            {
                using (FileStream fs = File.OpenRead(fileLocation))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(PaymentSaveData));
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    csd = (PaymentSaveData)xs.Deserialize(fs);
#pragma warning restore CS8605 // Unboxing a possibly null value.


                    _Payments.Clear();
                    _Payments.AddRange(csd.Payments);
                    _IdGenerator = csd.NextPaymentId;

                    Payment.IgnorePaymentList = new List<string>();
                    Payment.IgnorePaymentList.AddRange(csd.paymentsToIgnore);
                }
            }
            catch
            {

            }
        }
    }

}
