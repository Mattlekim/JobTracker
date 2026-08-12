namespace UiInterface
{
    /// <summary>
    /// Filling an address in from where the phone is standing.
    ///
    /// New work is taken down at the door, so the street, the town and the
    /// postcode are already known to the phone - and they are the parts that
    /// are worth not typing, because they have to match what is already on
    /// the round or the house lands in a group of its own in every list.
    ///
    /// The house number is deliberately left alone. A phone is accurate to a
    /// few doors at best, so a number filled in from it would be wrong often
    /// enough to be worse than useless - and it is the one part of the
    /// address that is quick to type anyway.
    /// </summary>
    public static class AddressFromLocation
    {
        /// <summary>
        /// what was found. a part left null was not offered, and should be
        /// left as it is rather than emptied
        /// </summary>
        public class Found
        {
            public string Street;
            public string City;
            public string Area;
            public string Postcode;

            /// <summary>the whole thing on one line, for asking "is this right?"</summary>
            public string Summary
            {
                get
                {
                    List<string> parts = new List<string>();
                    foreach (string p in new string[] { Street, Area, City, Postcode })
                        if (!string.IsNullOrWhiteSpace(p))
                            parts.Add(p.Trim());

                    return string.Join(", ", parts);
                }
            }

            public bool Anything
            {
                get { return Summary.Length > 0; }
            }
        }

        /// <summary>
        /// works out where the phone is and turns it into an address, asking
        /// first whether it is the right one.
        ///
        /// null when there is nothing to fill in - the reason is put to the
        /// user here, so a caller only has to deal with what it is given.
        /// </summary>
        /// <summary>
        /// getting a fix takes a few seconds, and the button stays there to
        /// be pressed again while it does. a second press would put up a
        /// second "is this right?" over the top of the first
        /// </summary>
        private static bool _asking = false;

        public static async Task<Found> AskAsync(Page page)
        {
            if (_asking)
                return null;

            _asking = true;
            try
            {
                return await FindAsync(page);
            }
            finally
            {
                _asking = false;
            }
        }

        private static async Task<Found> FindAsync(Page page)
        {
            Microsoft.Maui.Devices.Sensors.Location where = await WhereAsync(page);
            if (where == null)
                return null;

            Placemark place;
            try
            {
                IEnumerable<Placemark> places = await Geocoding.Default.GetPlacemarksAsync(where);
                place = places == null ? null : places.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Location", $"Could not turn where you are into an address: {ex.Message}", "Ok");
                return null;
            }

            if (place == null)
            {
                await page.DisplayAlert("Location",
                    "Nothing could be found for where you are standing, so the address will have to be typed in.", "Ok");
                return null;
            }

            Found found = Read(place);

            if (!found.Anything)
            {
                await page.DisplayAlert("Location",
                    "Where you are came back without a street or a town on it, so there is nothing to fill in.", "Ok");
                return null;
            }

            //a phone can be a few doors out, and the address it gives is not
            //always the one you are standing at - so it is shown before it
            //goes anywhere near the form
            if (!await page.DisplayAlert("Is This Right?",
                    $"{found.Summary}\n\nThe house number is not filled in - a phone is not accurate enough to trust with it.",
                    "Use It", "Cancel"))
                return null;

            return found;
        }

        /// <summary>where the phone is, or null with the reason already given</summary>
        private static async Task<Microsoft.Maui.Devices.Sensors.Location> WhereAsync(Page page)
        {
            try
            {
                //asked for properly rather than taken from the last known
                //fix, which can be from the other side of the round. the last
                //known one is only a fall back for when nothing comes back in
                //time - standing between houses is exactly where a phone is
                //slowest to get a fix
                GeolocationRequest request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15));

                Microsoft.Maui.Devices.Sensors.Location where = await Geolocation.Default.GetLocationAsync(request);
                if (where == null)
                    where = await Geolocation.Default.GetLastKnownLocationAsync();

                if (where == null)
                    await page.DisplayAlert("Location",
                        "Your phone could not work out where it is. Out in the open it usually only takes a moment.", "Ok");

                return where;
            }
            catch (FeatureNotSupportedException)
            {
                await page.DisplayAlert("Location",
                    "This device cannot find where it is, so the address will have to be typed in.", "Ok");
            }
            catch (FeatureNotEnabledException)
            {
                await page.DisplayAlert("Location",
                    "Location is turned off on this device. Turn it on and try again.", "Ok");
            }
            catch (PermissionException)
            {
                await page.DisplayAlert("Location",
                    "Work Tracker needs permission to use your location before it can fill an address in.", "Ok");
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Location", $"Could not find where you are: {ex.Message}", "Ok");
            }

            return null;
        }

        private static Found Read(Placemark place)
        {
            Found found = new Found()
            {
                Street = Clean(place.Thoroughfare),
                City = Clean(place.Locality),
                Area = Clean(place.SubLocality),
                Postcode = Clean(place.PostalCode),
            };

            //a lot of places come back with no district on them. the wider
            //one is worth offering when it is actually saying something the
            //town has not already said
            if (found.Area == null && !string.Equals(Clean(place.SubAdminArea), found.City, StringComparison.CurrentCultureIgnoreCase))
                found.Area = Clean(place.SubAdminArea);

            //a village with no town above it comes back the other way round
            if (found.City == null)
                found.City = Clean(place.SubAdminArea) ?? Clean(place.AdminArea);

            //and then the district must not repeat it
            if (found.Area != null && string.Equals(found.Area, found.City, StringComparison.CurrentCultureIgnoreCase))
                found.Area = null;

            return found;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
