using Kernel;

namespace UiInterface
{
    /// <summary>
    /// Raised when customers/jobs change in bulk outside the normal page flows
    /// (excel import, backup restore, delete all data) so list pages know to
    /// rebuild themselves the next time they are shown.
    /// </summary>
    public static class DataRefreshNotifier
    {
        public static event Action DataChanged;

        public static void NotifyDataChanged()
        {
            RebuildBookings();
            DataChanged?.Invoke();
        }

        /// <summary>
        /// Booking.Bookings is a static cache, so it has to be built from the
        /// jobs rather than added to - otherwise it keeps rows pointing at
        /// jobs that have changed or gone, and the same day can end up listed
        /// more than once.
        /// </summary>
        public static void RebuildBookings()
        {
            Booking.Bookings.Clear();

            var jobsByDate = new Dictionary<DateTime, List<Job>>();
            foreach (Job j in Job.Query())
            {
                if (!j.IsBookedIn)
                    continue;
                DateTime date = j.DateJobBookinFor.Date;
                if (!jobsByDate.TryGetValue(date, out List<Job> jobs))
                {
                    jobs = new List<Job>();
                    jobsByDate[date] = jobs;
                }
                jobs.Add(j);
            }

            foreach (KeyValuePair<DateTime, List<Job>> pair in jobsByDate)
                Booking.AddBooking(pair.Value, pair.Key);
        }
    }
}
