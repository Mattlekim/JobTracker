using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public struct FilterItem
    {
        public string Property;
        public string Value;
        public bool Absolute;
    }
    public struct Filter
    {
        public List<FilterItem> filters;
        

        public Filter(string property, string value, bool absolute = true)
        {
            filters = new List<FilterItem>();
            Add(property, value, absolute);
        }
        public void Add(string property, string value, bool absolute = true)
        {
            filters.Add(new FilterItem()
            {
                Property = property,
                Value = value,
                Absolute = absolute,
            });
        }
    }

    public enum JobFilter
    {
        Compleated,
        NotCompleated,
        None
    }
}
