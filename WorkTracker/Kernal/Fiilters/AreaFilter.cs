using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel.Fiilters
{

    public class AreaFilter : JobFilterBase
    {
        public AreaFilter(List<Job> data) : base(data)
        {
            FilterOptions.Add("All");

            foreach (Job j in data)
            {
                if (j.Address != null)
                    if (j.Address.Area != null && j.Address.Area != String.Empty)
                        if (!FilterOptions.Contains(j.Address.Area))
                            FilterOptions.Add(j.Address.Area);
            }

            FilterName = "Filter Area: ";
        }

        public override void Filter(ref List<Job> jobs)
        {
            if (SelectedIndex <= 0)
                return;

            jobs.RemoveAll(x => x.JobFormattedArea != FilterOptions[SelectedIndex]);
        }
    }
}
