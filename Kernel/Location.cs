using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public class Location
    {
        public string PropertyNameNumber { get; set; }
        public string Postcode { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string Area { get; set; }
        public Vector3 GPS_Location { get; set; }
        
        static Location _garbaeCollectorLimiter;
        public Location DeepCopy()
        {
            _garbaeCollectorLimiter = new Location()
            {
                PropertyNameNumber = PropertyNameNumber,
                Postcode = Postcode,
                Street = Street,
                City = City,
                Area = Area,
                GPS_Location = GPS_Location,
            };
            return _garbaeCollectorLimiter;
        }

        public static Location None = new Location() { PropertyNameNumber = String.Empty, Postcode = String.Empty, Street = String.Empty };

        public override string ToString()
        {
            return $"{PropertyNameNumber} {Street} {City} {Area}";
        }
    }
}
