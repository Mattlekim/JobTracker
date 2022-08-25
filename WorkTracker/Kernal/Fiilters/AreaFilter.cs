using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Kernel.Fiilters
{
    public class AreaFilter : JobFilterBase
    {
        public bool ListAllJobs = false;

        public AreaFilter(List<Job> data) : base(data)
        {
        }

        public override void Filter(ref List<Job> jobs)
        {
            if (ListAllJobs)
                return;

            jobs.RemoveAll(x => x.JobFormattedArea != FilterStrring);
        }
    }
}
