using System;
using System.Collections.Generic;

namespace Kernel
{
    /// <summary>
    /// Swaps the road and town names on screen for made up ones, so the round
    /// can be photographed for a listing or a help page without putting a
    /// customer's actual address in front of everybody.
    ///
    /// House numbers are left alone: 14 is 14 wherever it is, and a round
    /// full of houses with no numbers does not look like the app does.
    ///
    /// **This is display only.** Nothing here touches what is saved: the
    /// forms are filled in from the real address, so a job edited while this
    /// is on still saves the address it actually has. The setting itself is
    /// deliberately not saved either - see <see cref="On"/>.
    /// </summary>
    public static class ScreenshotMode
    {
        /// <summary>
        /// Deliberately a plain static and deliberately **not** written to
        /// the settings file. It is on for as long as the screenshots are
        /// being taken and gone the next time the app starts, which is the
        /// only way to be sure a round is never quietly showing made up
        /// addresses months later.
        /// </summary>
        public static bool On = false;

        /// <summary>
        /// The same real name always comes out as the same made up one, and
        /// no two real names share one. That matters for more than looks:
        /// the streets still group as streets and the towns still group as
        /// towns, so a screenshot shows the app behaving exactly as it does
        /// with the real round in it.
        /// </summary>
        private static readonly Dictionary<string, string> _streets = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
        private static readonly Dictionary<string, string> _towns = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
        private static readonly Dictionary<string, string> _areas = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
        private static readonly Dictionary<string, string> _postcodes = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

        private static readonly List<string> MadeUpStreets = new List<string>()
        {
            "Cherry Tree Avenue", "Mill Lane", "Kingsway", "Beech Grove", "Orchard Close",
            "Springfield Road", "The Green", "Hawthorn Drive", "Bramley Way", "Chapel Street",
            "Sycamore Rise", "Windmill Hill", "Foxglove Close", "Meadow View", "Rowan Court",
            "Weavers Walk", "Long Acre", "Bridge Street", "Elder Way", "Primrose Bank",
            "Coronation Terrace", "Wheatfield Road", "Larkspur Close", "Old Forge Lane", "Ash Tree Croft",
            "Brookside", "Cobblers Yard", "Hillcrest Avenue", "Tanners Row", "Juniper Gardens",
        };

        private static readonly List<string> MadeUpTowns = new List<string>()
        {
            "Bramfield", "Wickhampton", "Marsden Bay", "Oakthorpe", "Netherby",
            "Stonebridge", "Kirkhallam", "Ashcombe", "Harleston", "Redmarsh",
            "Thornaby Vale", "Elmsworth", "Danesford", "Whitmoor", "Castleton Green",
        };

        private static readonly List<string> MadeUpAreas = new List<string>()
        {
            "North Side", "Grange Park", "The Rise", "Old Town", "Fieldhead",
            "Woodvale", "Highfields", "Riverside", "Beacon Hill", "The Crofts",
        };

        public static string Street(string real)
        {
            return Swap(real, _streets, MadeUpStreets);
        }

        public static string Town(string real)
        {
            return Swap(real, _towns, MadeUpTowns);
        }

        public static string Area(string real)
        {
            return Swap(real, _areas, MadeUpAreas);
        }

        /// <summary>
        /// a postcode says exactly where a house is, so it is made up too -
        /// built to look like one rather than picked off a list, because
        /// there is no short list of them
        /// </summary>
        public static string Postcode(string real)
        {
            if (!On || string.IsNullOrWhiteSpace(real))
                return real;

            string key = real.Trim();

            string made;
            if (_postcodes.TryGetValue(key, out made))
                return made;

            const string letters = "ABDEFGHJLNPQRSTUWXYZ";
            int hash = StableHash(key);

            made = string.Format("{0}{1}{2} {3}{4}{5}",
                letters[hash % letters.Length],
                letters[(hash / 7) % letters.Length],
                (hash / 3) % 90 + 1,
                (hash / 11) % 9 + 1,
                letters[(hash / 13) % letters.Length],
                letters[(hash / 17) % letters.Length]);

            _postcodes[key] = made;
            return made;
        }

        private static string Swap(string real, Dictionary<string, string> swapped, List<string> madeUp)
        {
            if (!On || string.IsNullOrWhiteSpace(real))
                return real;

            string key = real.Trim();

            string made;
            if (swapped.TryGetValue(key, out made))
                return made;

            //where in the list this name lands is worked out from the name
            //itself, so the same round comes out the same every time the mode
            //is turned on rather than depending on what was looked at first
            int at = StableHash(key) % madeUp.Count;

            for (int i = 0; i < madeUp.Count; i++)
            {
                string candidate = madeUp[(at + i) % madeUp.Count];
                if (!swapped.ContainsValue(candidate))
                {
                    swapped[key] = candidate;
                    return candidate;
                }
            }

            //a round with more streets on it than there are made up names.
            //numbering them keeps them telling apart, which is the one thing
            //that must not break
            made = $"{madeUp[at]} ({swapped.Count})";
            swapped[key] = made;
            return made;
        }

        /// <summary>
        /// always positive, and always the same answer for the same text -
        /// unlike string.GetHashCode, which is deliberately different from
        /// one run of the app to the next
        /// </summary>
        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in text.ToLowerInvariant())
                    hash = (hash ^ c) * 16777619;

                //not Math.Abs: that throws on int.MinValue
                return hash & 0x7FFFFFFF;
            }
        }
    }
}
