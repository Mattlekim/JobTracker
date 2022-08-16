using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Serialization;

namespace Kernel
{
    public class WorkDay
    {
        /// <summary>
        /// the master id number
        /// </summary>
        private static int _IdGenerator = 0;

        /// <summary>
        /// generate the id number for the current customer
        /// </summary>
        private void GenerateId()
        {
            Id = _IdGenerator;
            _IdGenerator++;
        }

        public static List<WorkDay> days = new List<WorkDay>();

        public int Id;

        public string Name;
        public string Description;
        public string Notes;

        public List<int> JobsIds;

        public DateTime StartDay;
        public DateTime EndDay;
        public bool IsCompleated = false;

        [XmlIgnore]
        public List<Job> JobsToDo;

        public static void Add(WorkDay workDay)
        {
            workDay.GenerateId();
        }

        public WorkDay(string name, string desc, string notes)
        {
            Name = name;
            Description = desc;
            Notes = notes;
            JobsIds = new List<int>();
            JobsToDo = new List<Job>();
        }

        public void GetJobsLinkedToThisDay()
        {

        }
        public void AddJob()
        {

        }
    }
}
