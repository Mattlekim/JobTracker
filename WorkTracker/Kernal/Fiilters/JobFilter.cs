using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel.Fiilters
{
    public abstract class JobFilterBase
    {
        public string FilterName { get; protected set; } = string.Empty;

        public string FilterStrring { get; protected set; } = string.Empty;

        public List<string> FilterOptions { get; protected set; } = new List<string>();

        public int SelectedIndex { get; set; } = 0;
        public abstract void Filter(ref List<Job> jobs);

        public JobFilterBase(List<Job> data)
        {

        }
    }
}
