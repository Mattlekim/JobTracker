using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public class Location
    {
        //empty rather than null: a half filled address used to leave nulls
        //here, which then blew up anything comparing them
        public string PropertyNameNumber { get; set; } = string.Empty;
        public string Postcode { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
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
