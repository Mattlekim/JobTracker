using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Kernel.Fiilters
{
    public class CityFilter : JobFilterBase
    {
        public CityFilter(List<Job> data) : base(data)
        {
            FilterOptions.Add("All");

            foreach (Job j in data)
            {
                if (j.Address != null)
                    if (j.Address.City != null && j.Address.City != String.Empty)
                        if (!FilterOptions.Contains(j.Address.City))
                            FilterOptions.Add(j.Address.City);
            }

            FilterName = "Filter City: ";
        }

        public override void Filter(ref List<Job> jobs)
        {
            if (SelectedIndex == 0)
                return;

            jobs.RemoveAll(x => x.JobFormattedCity != FilterOptions[SelectedIndex]);
        }


    }
}
