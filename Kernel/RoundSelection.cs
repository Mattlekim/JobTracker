using System;
using System.Collections.Generic;

namespace Kernel
{
    /// <summary>which part of the round is being asked for</summary>
    public enum RoundPartKind
    {
        Everything,
        Round,
        Area,
    }

    /// <summary>
    /// A part of the round - all of it, one round, or one area - and the
    /// houses that are in it.
    ///
    /// It exists because exporting the whole round is not usually what
    /// somebody wants out of a spreadsheet: they are handing a patch of work
    /// to somebody, or pricing up one village, and a sheet of every house on
    /// the books is a sheet they then have to cut down by hand.
    ///
    /// **A house is in or out as a whole.** The decision is made once per
    /// house and then *every* visit of it travels, finished ones included -
    /// the sheet's Cleaned columns are read off the visits already done, so a
    /// house taken half in would export with no history at all and read as a
    /// house nobody has ever cleaned.
    /// </summary>
    public class RoundPart
    {
        public RoundPartKind Kind = RoundPartKind.Everything;

        /// <summary>
        /// the round or the area asked for. Blank is a real answer and not a
        /// missing one - it is the work that is on no round, or the houses
        /// with no area written against them
        /// </summary>
        public string Name = string.Empty;

        public static RoundPart Everything()
        {
            return new RoundPart { Kind = RoundPartKind.Everything };
        }

        public static RoundPart OnRound(string round)
        {
            return new RoundPart { Kind = RoundPartKind.Round, Name = round ?? string.Empty };
        }

        public static RoundPart InArea(string area)
        {
            return new RoundPart { Kind = RoundPartKind.Area, Name = area ?? string.Empty };
        }

        public bool IsEverything
        {
            get { return Kind == RoundPartKind.Everything; }
        }

        private bool Unnamed
        {
            get { return string.IsNullOrWhiteSpace(Name); }
        }

        /// <summary>what this part is called, for saying out loud</summary>
        public string Describe
        {
            get
            {
                switch (Kind)
                {
                    case RoundPartKind.Round:
                        return Unnamed ? "the work on no round" : $"the {Name} round";

                    case RoundPartKind.Area:
                        return Unnamed ? "the houses with no area" : $"the {Name} area";

                    default:
                        return "the whole round";
                }
            }
        }

        /// <summary>
        /// what the file is called, so a sheet says which part of the round
        /// it holds without being opened. Two exports done on one day would
        /// otherwise land on the same name and the second would ask to
        /// replace the first
        /// </summary>
        public string FileNamePart
        {
            get
            {
                switch (Kind)
                {
                    case RoundPartKind.Round:
                        return Unnamed ? "Round No Round" : $"Round {Name}";

                    case RoundPartKind.Area:
                        return Unnamed ? "Area None" : $"Area {Name}";

                    default:
                        return "Round";
                }
            }
        }

        /// <summary>
        /// the rounds work is actually on, rather than the settings list -
        /// the same rule the work list's round filter follows. A round with
        /// nothing on it is not worth exporting, and a round taken off the
        /// settings list still has its work
        /// </summary>
        public static List<string> RoundsWithWork(List<Job> jobs)
        {
            List<string> rounds = new List<string>();

            foreach (KeyValuePair<string, string> pair in Job.RoundsOfEveryJob(RealHouses(jobs)))
                if (!string.IsNullOrWhiteSpace(pair.Value)
                    && !rounds.Exists(x => string.Equals(x, pair.Value, StringComparison.CurrentCultureIgnoreCase)))
                    rounds.Add(pair.Value);

            rounds.Sort(StringComparer.CurrentCultureIgnoreCase);
            return rounds;
        }

        /// <summary>the areas work is actually in. Areas have no settings list at all</summary>
        public static List<string> AreasWithWork(List<Job> jobs)
        {
            List<string> areas = new List<string>();

            foreach (Job j in RealHouses(jobs))
            {
                string area = j.Address?.Area;
                if (!string.IsNullOrWhiteSpace(area)
                    && !areas.Exists(x => string.Equals(x, area, StringComparison.CurrentCultureIgnoreCase)))
                    areas.Add(area);
            }

            areas.Sort(StringComparer.CurrentCultureIgnoreCase);
            return areas;
        }

        /// <summary>whether anything is on no round, so the option is only offered when it means something</summary>
        public static bool AnyWithNoRound(List<Job> jobs)
        {
            foreach (KeyValuePair<string, string> pair in Job.RoundsOfEveryJob(RealHouses(jobs)))
                if (string.IsNullOrWhiteSpace(pair.Value))
                    return true;

            return false;
        }

        /// <summary>whether any house has no area written against it</summary>
        public static bool AnyWithNoArea(List<Job> jobs)
        {
            foreach (Job j in RealHouses(jobs))
                if (string.IsNullOrWhiteSpace(j.Address?.Area))
                    return true;

            return false;
        }

        /// <summary>
        /// the jobs in this part of the round - every visit of every house
        /// that is in it.
        ///
        /// The whole round is handed back exactly as it came, rather than
        /// rebuilt out of the houses: this only ever takes work out, so
        /// asking for all of it cannot quietly export something different
        /// from what it always did.
        /// </summary>
        public List<Job> Pick(List<Job> jobs)
        {
            if (jobs == null)
                return new List<Job>();

            if (Kind == RoundPartKind.Everything)
                return new List<Job>(jobs);

            Dictionary<string, List<Job>> houses = HouseVisits(jobs);

            //the round belongs to the job rather than to one visit of it, so
            //it is read the way the rest of the app reads it - off the last
            //visit that names one. Read off each visit on its own, a house
            //whose finished visits were left on no round would export half
            //its history under one answer and half under another
            Dictionary<string, string> rounds = Kind == RoundPartKind.Round
                ? Job.RoundsOfEveryJob(RealHouses(jobs))
                : null;

            List<Job> picked = new List<Job>();

            foreach (KeyValuePair<string, List<Job>> house in houses)
                if (Holds(house.Key, house.Value, rounds))
                    picked.AddRange(house.Value);

            return picked;
        }

        private bool Holds(string key, List<Job> visits, Dictionary<string, string> rounds)
        {
            if (Kind == RoundPartKind.Round)
            {
                string round = rounds != null && rounds.TryGetValue(key, out string found)
                    ? found
                    : string.Empty;

                return Same(round, Name);
            }

            Job deciding = DecidingVisit(visits);
            return Same(deciding?.Address?.Area, Name);
        }

        /// <summary>how many houses are in a picked list, however many visits of each came with them</summary>
        public static int CountHouses(List<Job> picked)
        {
            HashSet<string> keys = new HashSet<string>();

            foreach (Job j in RealHouses(picked))
                keys.Add(j.SameJobKey);

            return keys.Count;
        }

        /// <summary>
        /// The visit a house is judged by: the one next due, which is the
        /// same visit AllJobs, RoundStats and DataCheck pick a house with, so
        /// the four cannot disagree about which round a house is on.
        ///
        /// A house with nothing outstanding falls back to its newest visit.
        /// It still says where the house is, and dropping it here would take
        /// a house off an export for the sake of a question it can answer.
        /// </summary>
        private static Job DecidingVisit(List<Job> visits)
        {
            Job chosen = null;

            foreach (Job j in visits)
                if (!j.IsCompleted && !j.HaveCanceled)
                    chosen = Job.NextDue(chosen, j);

            if (chosen != null)
                return chosen;

            foreach (Job j in visits)
                if (chosen == null || j.Id > chosen.Id)
                    chosen = j;

            return chosen;
        }

        /// <summary>every visit of every house, keyed by the house</summary>
        private static Dictionary<string, List<Job>> HouseVisits(List<Job> jobs)
        {
            Dictionary<string, List<Job>> houses = new Dictionary<string, List<Job>>();

            foreach (Job j in RealHouses(jobs))
            {
                if (!houses.TryGetValue(j.SameJobKey, out List<Job> visits))
                {
                    visits = new List<Job>();
                    houses[j.SameJobKey] = visits;
                }

                visits.Add(j);
            }

            return houses;
        }

        /// <summary>
        /// the booking summary rows are not houses (CustomerId -1), the same
        /// test AllJobs and DataCheck make before counting anything
        /// </summary>
        private static List<Job> RealHouses(List<Job> jobs)
        {
            List<Job> real = new List<Job>();

            if (jobs == null)
                return real;

            foreach (Job j in jobs)
                if (j != null && j.CustomerId != -1)
                    real.Add(j);

            return real;
        }

        private static bool Same(string one, string other)
        {
            return string.Equals(one ?? string.Empty, other ?? string.Empty,
                StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
