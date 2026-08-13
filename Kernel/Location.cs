using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Serialization;

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

        //  ------------------------------------------------------  on screen
        //
        //  The address as it should be *shown*, which is the same as the
        //  address itself unless screenshot mode is on - see
        //  Kernel/ScreenshotMode.cs.
        //
        //  Kept apart from the fields above on purpose. Anything that fills
        //  a form in, saves a file, exports or matches a customer up reads
        //  the real address; only the places that put it on the screen read
        //  these. Mask the fields themselves and the next job edited would
        //  save a made up street.

        [XmlIgnore]
        public string DisplayStreet
        {
            get { return ScreenshotMode.Street(Street); }
        }

        [XmlIgnore]
        public string DisplayCity
        {
            get { return ScreenshotMode.Town(City); }
        }

        [XmlIgnore]
        public string DisplayArea
        {
            get { return ScreenshotMode.Area(Area); }
        }

        [XmlIgnore]
        public string DisplayPostcode
        {
            get { return ScreenshotMode.Postcode(Postcode); }
        }


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

        /// <summary>
        /// the whole address on one line. this is a thing to show somebody,
        /// so it is the shown address - the house number, which is left as
        /// it is, and whatever the road and town are called on screen
        /// </summary>
        public override string ToString()
        {
            return $"{PropertyNameNumber} {DisplayStreet} {DisplayCity} {DisplayArea}";
        }
    }
}
