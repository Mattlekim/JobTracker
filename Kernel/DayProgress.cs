using System;
using System.Collections.Generic;

namespace Kernel
{
    /// <summary>
    /// How a day of work is going: how much of it is done, what that comes to
    /// in money, and roughly how long the rest will take.
    ///
    /// The booked work page and the calendar both ask this about a day, and
    /// both used to work it out for themselves - two copies of the same loop
    /// with two copies of the same wording under them. They are the same day
    /// looked at two ways and must not be able to disagree about how far
    /// through it you are, so the counting and the words for it live here,
    /// in the kernel with the work.
    ///
    /// **A clean that was done counts even though the job has been cancelled
    /// since**, which is why completed is tested before cancelled - the same
    /// order the calendar's month totals, the stats page and the tax figures
    /// go in. Cancelling says the house is not being cleaned any more; it does
    /// not say that clean never happened, and the money for it is real. A
    /// cancelled visit that never happened is not work and is left out either
    /// way.
    /// </summary>
    public class DayProgress
    {
        /// <summary>visits written up</summary>
        public int Done;

        /// <summary>visits still to do</summary>
        public int Left;

        /// <summary>roughly how long what is left will take</summary>
        public int MinutesLeft;

        /// <summary>what the work done comes to</summary>
        public float ValueDone;

        /// <summary>what the work still to do comes to</summary>
        public float ValueLeft;

        public int Total
        {
            get { return Done + Left; }
        }

        public bool HaveWork
        {
            get { return Total > 0; }
        }

        public bool AllDone
        {
            get { return Total > 0 && Left == 0; }
        }

        /// <summary>what the whole day comes to, done and not</summary>
        public float Value
        {
            get { return ValueDone + ValueLeft; }
        }

        /// <summary>
        /// how a day's work stands. The time a job takes is asked of the job
        /// (<see cref="Job.Minutes"/>), so a house with no estimate of its own
        /// counts as the round's usual rather than as nothing - the figure
        /// would be quietly optimistic on a round that has never timed
        /// anything
        /// </summary>
        public static DayProgress For(IEnumerable<Job> jobs)
        {
            DayProgress day = new DayProgress();

            if (jobs == null)
                return day;

            foreach (Job job in jobs)
            {
                if (job == null)
                    continue;

                //done first, and deliberately so - see the note above
                if (job.IsCompleted)
                {
                    day.Done++;
                    day.ValueDone += job.EffectivePrice;
                    continue;
                }

                if (job.HaveCanceled)
                    continue;

                day.Left++;
                day.ValueLeft += job.EffectivePrice;
                day.MinutesLeft += job.Minutes;
            }

            return day;
        }

        /// <summary>how many are done - "3 of 12 done, 9 left"</summary>
        public string CountText
        {
            get
            {
                if (!HaveWork)
                    return string.Empty;

                if (Left == 0)
                    return Total == 1 ? "Done" : $"All {Total} done";

                return $"{Done} of {Total} done, {Left} left";
            }
        }

        /// <summary>
        /// What the day is worth and how much of it has been earned -
        /// "£45.00 of £120.00 done".
        ///
        /// This is the question the counts cannot answer: eight houses done
        /// out of twelve is not two thirds of the day's money when the four
        /// left are the expensive ones.
        /// </summary>
        public string ValueText
        {
            get
            {
                if (!ShowValue)
                    return string.Empty;

                if (Done == 0)
                    return $"{Money(Value)} to do";

                if (Left == 0)
                    return $"{Money(ValueDone)} done";

                return $"{Money(ValueDone)} of {Money(Value)} done";
            }
        }

        /// <summary>
        /// a day whose work is worth nothing has nothing to say here, and a
        /// chip reading "£0.00 to do" is worse than no chip
        /// </summary>
        public bool ShowValue
        {
            get { return Value > 0; }
        }

        public string TimeLeftText
        {
            get { return ShowTimeLeft ? $"About {ShortMinutes(MinutesLeft)} left" : string.Empty; }
        }

        /// <summary>
        /// no times filled in anywhere - better to say nothing than "0m left"
        /// </summary>
        public bool ShowTimeLeft
        {
            get { return MinutesLeft > 0; }
        }

        private static string Money(float amount)
        {
            return $"{Gloable.CurrenceSymbol}{amount:0.00}";
        }

        /// <summary>
        /// minutes as a person would say them, short - 2h 30m, 45m, 3h.
        ///
        /// Deliberately not <see cref="Job.SpellMinutes"/>, which says the
        /// same figure as "2 hrs 30 mins": that one is a tag on a row and is
        /// read on its own, this one rides in a chip on a day heading beside
        /// two others and has to be got in at a glance.
        /// </summary>
        public static string ShortMinutes(int minutes)
        {
            int hours = minutes / 60;
            int rest = minutes % 60;

            if (hours == 0)
                return $"{rest}m";
            if (rest == 0)
                return $"{hours}h";

            return $"{hours}h {rest}m";
        }
    }
}
