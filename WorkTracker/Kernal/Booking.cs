using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public class Booking
    {
        private static int IdGenerator = -1;

        public static List<Booking> Bookings = new List<Booking>();

        public static void RemoveBooking(DateTime date)
        {
            Bookings.RemoveAll(x => x.BookingInfo.DateJobBookinFor == date);
        }

        public static void ReseduleBooking(DateTime olddate, DateTime newdate)
        {
            Booking b =  Bookings.FirstOrDefault(x => x.BookingInfo.DateJobBookinFor == olddate);
            if (b != null)
            {
                b.BookingInfo.DateJobBookinFor = newdate;
                b.BookingInfo.DueDate = newdate;
                foreach (Job j in b.Jobs)
                    j.DateJobBookinFor = newdate;
            }
           
        }
        public static void AddBooking(List<Job> jobs, DateTime date)
        {
            Bookings.Add(new Booking(jobs, date));
        }

        public List<Job> Jobs = new List<Job>();
        public Job BookingInfo;

        private float Amount = 0;
        private float Time = 0;
        public Booking(List<Job> jobs, DateTime date)
        {
            Jobs = jobs;

            int c = 0;
            foreach (Job j in jobs)
            {
                j.BookInJob(date);
                Amount += j.Price;
                Time += j.EstimatedTime;
                c++;
            }
            Time = Time / 60f;
            Time = (float)Math.Round(Time, 1);
            string timetoDisplay = "Unknown time to complete work";

            if (jobs.Count > c)
                timetoDisplay = $"More than {Time} hours of work";
            if (Time > 0)
                timetoDisplay = $"About {Time} hours of work";

            BookingInfo = new Job()
            {
                Name = "Booking",
                DueColorCode = Colors.Green,
                Price = Amount,
                Id = IdGenerator,
                DisableSwipe = true,
                CustomerId = -1,
                DateJobBookinFor = new DateTime(date.Year, date.Month, date.Day),
                DueDate = new DateTime(date.Year, date.Month, date.Day),
            };
            IdGenerator--;
            BookingInfo.Address = new Location()
            {
                Street = $"{jobs.Count} Jobs Booked In"
            };
            
        }
    }
}
