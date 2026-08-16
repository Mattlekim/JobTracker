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
        /// Booking.Bookings is a cache built from the jobs, and Booking owns
        /// the building now - this is kept as a way in for the pages that
        /// already call it here
        /// </summary>
        public static void RebuildBookings()
        {
            Booking.Rebuild();
        }
    }
}
