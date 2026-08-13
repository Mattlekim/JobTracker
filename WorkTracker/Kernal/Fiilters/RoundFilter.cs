using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel.Fiilters
{
    /// <summary>
    /// Cuts the list down to one round.
    ///
    /// The rounds offered are the ones work is actually on rather than the
    /// list on the settings page: a round with nothing on it is not worth
    /// filtering to, and a round taken off that list still has its work.
    ///
    /// "No Round" is offered as well, because the work nobody has organised
    /// yet is exactly what somebody organising it wants to see.
    /// </summary>
    public class RoundFilter : JobFilterBase
    {
        public const string NoRound = "No Round";

        public RoundFilter(List<Job> data) : base(data)
        {
            FilterOptions.Add("All");

            bool anyWithout = false;

            foreach (Job j in data)
            {
                if (!j.HaveRound)
                {
                    anyWithout = true;
                    continue;
                }

                if (!FilterOptions.Exists(x => string.Equals(x, j.Round, StringComparison.CurrentCultureIgnoreCase)))
                    FilterOptions.Add(j.Round);
            }

            if (anyWithout)
                FilterOptions.Add(NoRound);

            FilterName = "Filter Round: ";
        }

        public override void Filter(ref List<Job> jobs)
        {
            if (SelectedIndex <= 0 || SelectedIndex >= FilterOptions.Count)
                return;

            string wanted = FilterOptions[SelectedIndex];

            if (wanted == NoRound)
            {
                jobs.RemoveAll(x => x.HaveRound);
                return;
            }

            jobs.RemoveAll(x => !string.Equals(x.Round ?? string.Empty, wanted, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
