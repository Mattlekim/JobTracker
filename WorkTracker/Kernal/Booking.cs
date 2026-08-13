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

        /// <summary>
        /// The days that have work booked on them, oldest first. There is only
        /// ever one booking per day, so a week can be planned out by booking
        /// each day in turn.
        /// </summary>
        public static List<DateTime> BookedDays()
        {
            List<DateTime> days = new List<DateTime>();

            foreach (Job j in Job.Query())
            {
                if (!j.IsBookedIn || j.HaveCanceled)
                    continue;

                DateTime day = j.DateJobBookinFor.Date;
                if (!days.Contains(day))
                    days.Add(day);
            }

            days.Sort();
            return days;
        }

        /// <summary>
        /// A day that has been and gone with work still on it. Those are worth
        /// shouting about - work that was planned for Monday and is still
        /// sitting there on Thursday has been forgotten, not finished.
        /// </summary>
        public static List<DateTime> OverdueDays()
        {
            List<DateTime> overdue = new List<DateTime>();
            DateTime today = UsfulFuctions.DateNow.Date;

            foreach (DateTime day in BookedDays())
            {
                if (day >= today)
                    continue;

                if (JobsOn(day).Exists(x => !x.IsCompleted))
                    overdue.Add(day);
            }

            return overdue;
        }

        /// <summary>the work booked on a day, cancelled jobs left out</summary>
        public static List<Job> JobsOn(DateTime day)
        {
            return Job.Query().FindAll(x => x.IsBookedIn && !x.HaveCanceled
                && x.DateJobBookinFor.Date == day.Date);
        }

        //  ------------------------------------------------------------  tags
        //
        //  A booking is a day's work and nothing else - it is worked out from
        //  the jobs every time and never saved - so it has nowhere of its own
        //  to keep a tag. Tagging a booking therefore means tagging the work
        //  on it, which is also the only thing that would be any use: the tag
        //  is there to say what this visit to a house was like, and that is
        //  what shows up in the customer's history afterwards.

        /// <summary>every tag on a piece of work, each one once</summary>
        public static List<string> TagsOn(List<Job> jobs)
        {
            List<string> tags = new List<string>();

            if (jobs != null)
                foreach (Job j in jobs)
                    foreach (string tag in j.Tags)
                        if (!tags.Exists(x => string.Equals(x, tag, StringComparison.CurrentCultureIgnoreCase)))
                            tags.Add(tag);

            return tags;
        }

        /// <summary>
        /// tags a piece of work. work already carrying the tag is left as it
        /// is rather than tagged twice
        /// </summary>
        /// <returns>how many jobs it was put on</returns>
        public static int TagJobs(List<Job> jobs, string tag)
        {
            int tagged = 0;

            if (jobs != null)
                foreach (Job j in jobs)
                    if (j != null && j.AddTag(tag))
                        tagged++;

            return tagged;
        }

        /// <returns>how many jobs the tag came off</returns>
        public static int UntagJobs(List<Job> jobs, string tag)
        {
            int untagged = 0;

            if (jobs != null)
                foreach (Job j in jobs)
                    if (j != null && j.RemoveTag(tag))
                        untagged++;

            return untagged;
        }

        /// <summary>tags all of this booking's work</summary>
        public int AddTag(string tag)
        {
            return TagJobs(Jobs, tag);
        }

        public int RemoveTag(string tag)
        {
            return UntagJobs(Jobs, tag);
        }

        /// <summary>every tag on this booking's work</summary>
        public List<string> Tags
        {
            get { return TagsOn(Jobs); }
        }

        /// <summary>
        /// Clears away the bookings that have nothing left to say: a day that
        /// has passed with all of its work done. The jobs keep their done mark
        /// and the day they were done on - only the booking goes, because it
        /// is a plan for a day that is over.
        ///
        /// A past day with work still outstanding is left alone. That is not
        /// finished, it is late, and it wants to stay on the board.
        /// </summary>
        /// <returns>how many days were cleared</returns>
        public static int ClearFinishedPastDays()
        {
            int cleared = 0;
            DateTime today = UsfulFuctions.DateNow.Date;

            foreach (DateTime day in BookedDays())
            {
                if (day >= today)
                    continue;

                List<Job> jobs = JobsOn(day);
                if (jobs.Count == 0 || jobs.Exists(x => !x.IsCompleted))
                    continue;

                foreach (Job j in jobs)
                    RemoveJobFromBooking(j);

                cleared++;
            }

            if (cleared > 0)
                Job.Save();

            return cleared;
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

                //what the job counts as taking, the round's usual included,
                //so the summary row agrees with the day it stands for
                minutes += j.Minutes;
            }

            BookingInfo.Price = amount;
            BookingInfo.DateJobBookinFor = Date;
            BookingInfo.DueDate = Date;
            BookingInfo.EstimatedTime = (int)minutes;
            BookingInfo.Address.Street = $"{Jobs.Count} Jobs Booked In";
        }
    }
}
