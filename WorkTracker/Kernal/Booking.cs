using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    /// <summary>
    /// a day's work booked in. there is only ever one booking per day -
    /// booking more work for a day it already has adds to it rather than
    /// starting a second list for the same date
    /// </summary>
    public class Booking
    {
        private static int IdGenerator = -1;

        public static List<Booking> Bookings = new List<Booking>();

        static DateTime DayOf(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day);
        }

        /// <summary>the booking on a day, or null when nothing is booked</summary>
        public static Booking ForDate(DateTime date)
        {
            DateTime day = DayOf(date);
            return Bookings.FirstOrDefault(x => x.Date == day);
        }

        public static void RemoveBooking(DateTime date)
        {
            //the time of day is ignored, so a booking can be removed with
            //whatever form of the date the caller happens to hold
            DateTime day = DayOf(date);
            Bookings.RemoveAll(x => x.Date == day);
        }

        public static void ReseduleBooking(DateTime olddate, DateTime newdate)
        {
            Booking b = ForDate(olddate);
            if (b == null)
                return;

            DateTime day = DayOf(newdate);

            //if that day already has work booked, the two join up
            Booking existing = ForDate(day);
            if (existing != null && existing != b)
            {
                existing.AddJobs(b.Jobs);
                Bookings.Remove(b);
                return;
            }

            b.Date = day;
            foreach (Job j in b.Jobs)
                j.BookInJob(day);
            b.Refresh();
        }

        /// <summary>
        /// books work in for a day. work already booked for that day is kept
        /// and the new work added to it
        /// </summary>
        public static Booking AddBooking(List<Job> jobs, DateTime date)
        {
            DateTime day = DayOf(date);

            Booking existing = ForDate(day);
            if (existing != null)
            {
                existing.AddJobs(jobs);
                return existing;
            }

            Booking booking = new Booking(jobs, day);
            Bookings.Add(booking);
            return booking;
        }

        /// <summary>
        /// takes a single job back out of whatever day it was booked for,
        /// leaving the rest of that day's work booked. the booking goes when
        /// its last job does
        /// </summary>
        /// <returns>true when the job was booked in and has been taken out</returns>
        public static bool RemoveJobFromBooking(Job job)
        {
            if (job == null || !job.IsBookedIn)
                return false;

            Booking b = ForDate(job.DateJobBookinFor);
            job.UnBookInJob();

            if (b == null)
                return true;

            b.Jobs.RemoveAll(x => x.Id == job.Id);

            if (b.Jobs.Count == 0)
                Bookings.Remove(b);
            else
                b.Refresh();

            return true;
        }

        public List<Job> Jobs = new List<Job>();

        /// <summary>the summary row shown at the top of the job list</summary>
        public Job BookingInfo;

        /// <summary>the day this work is booked for</summary>
        public DateTime Date;

        public Booking(List<Job> jobs, DateTime date)
        {
            Date = DayOf(date);

            BookingInfo = new Job()
            {
                Name = "Booking",
                DueColorCode = Colors.Green,
                Id = IdGenerator,
                DisableSwipe = true,
                CustomerId = -1,
            };
            IdGenerator--;
            BookingInfo.Address = new Location();

            AddJobs(jobs);
        }

        /// <summary>
        /// books these jobs in for this day. a job already in this booking is
        /// left alone rather than counted twice
        /// </summary>
        public void AddJobs(List<Job> jobs)
        {
            if (jobs != null)
                foreach (Job j in jobs)
                {
                    if (j == null || Jobs.Any(x => x.Id == j.Id))
                        continue;
                    j.BookInJob(Date);
                    Jobs.Add(j);
                }

            Refresh();
        }

        /// <summary>brings the summary row back in line with the jobs in it</summary>
        public void Refresh()
        {
            float amount = 0;
            float minutes = 0;
            foreach (Job j in Jobs)
            {
                amount += j.EffectivePrice;
                minutes += j.EstimatedTime;
            }

            BookingInfo.Price = amount;
            BookingInfo.DateJobBookinFor = Date;
            BookingInfo.DueDate = Date;
            BookingInfo.EstimatedTime = (int)minutes;
            BookingInfo.Address.Street = $"{Jobs.Count} Jobs Booked In";
        }
    }
}
