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
        /// second "is this right?" over the top of the first.
        ///
        /// when the asking started is kept as well. this is one flag for the
        /// whole app, so anything that never finished used to turn the button
        /// off everywhere until the app was restarted - which is exactly what
        /// happened: it filled an address in once and then did nothing at all
        /// for the rest of the session. asking that has been going longer than
        /// the whole thing can possibly take is treated as finished.
        /// </summary>
        private static bool _asking = false;
        private static DateTime _askingSince = DateTime.MinValue;

        /// <summary>
        /// past this, whatever was asking is not coming back and the button
        /// is free again. it only has to be longer than a fix and a lookup
        /// together, both of which give up on their own below
        /// </summary>
        private static readonly TimeSpan AskingGoesStale = TimeSpan.FromMinutes(2);

        /// <summary>
        /// how long the phone is given to work out where it is, and how long
        /// the address lookup is given after that. neither of them promises
        /// to come back on its own
        /// </summary>
        private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(20);

        public static async Task<Found> AskAsync(Page page)
        {
            if (_asking && DateTime.UtcNow - _askingSince < AskingGoesStale)
            {
                //saying so beats a button that looks broken
                await Say(page, "Still working out where you are. Give it a moment.");
                return null;
            }

            _asking = true;
            _askingSince = DateTime.UtcNow;
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
                //the lookup goes off to the network and there is nothing in it
                //that promises to come back, so it is given only so long
                Task<IEnumerable<Placemark>> lookup = Geocoding.Default.GetPlacemarksAsync(where);
                if (await Task.WhenAny(lookup, Task.Delay(LookupTimeout)) != lookup)
                {
                    await Say(page, "Turning where you are into an address is taking too long. Try again, or type it in.");
                    return null;
                }

                IEnumerable<Placemark> places = await lookup;
                place = places == null ? null : places.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await Say(page, $"Could not turn where you are into an address: {ex.Message}");
                return null;
            }

            if (place == null)
            {
                await Say(page, "Nothing could be found for where you are standing, so the address will have to be typed in.");
                return null;
            }

            Found found = Read(place);

            if (!found.Anything)
            {
                await Say(page, "Where you are came back without a street or a town on it, so there is nothing to fill in.");
                return null;
            }

            //a phone can be a few doors out, and the address it gives is not
            //always the one you are standing at - so it is shown before it
            //goes anywhere near the form
            if (!await Confirm(page,
                    $"{found.Summary}\n\nThe house number is not filled in - a phone is not accurate enough to trust with it."))
                return null;

            return found;
        }

        /// <summary>
        /// says something about the location, as long as there is still a page
        /// there to say it on. an alert put up on a page that has been left
        /// behind never comes back, and whatever was waiting on it waits for
        /// ever
        /// </summary>
        private static async Task Say(Page page, string message)
        {
            if (page == null || page.Handler == null)
                return;

            await page.DisplayAlert("Location", message, "Ok");
        }

        /// <summary>the same, for the one question that is asked</summary>
        private static async Task<bool> Confirm(Page page, string message)
        {
            if (page == null || page.Handler == null)
                return false;

            return await page.DisplayAlert("Is This Right?", message, "Use It", "Cancel");
        }

        /// <summary>where the phone is, or null with the reason already given</summary>
        private static async Task<Microsoft.Maui.Devices.Sensors.Location> WhereAsync(Page page)
        {
            try
            {
                //asked for up front so a refusal says so, rather than coming
                //back out of the fix as an exception with no wording of its own
                PermissionStatus permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (permission != PermissionStatus.Granted)
                    permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (permission != PermissionStatus.Granted)
                {
                    await Say(page, "Work Tracker needs permission to use your location before it can fill an address in.");
                    return null;
                }

                //asked for properly rather than taken from the last known
                //fix, which can be from the other side of the round. the last
                //known one is only a fall back for when nothing comes back in
                //time - standing between houses is exactly where a phone is
                //slowest to get a fix
                GeolocationRequest request = new GeolocationRequest(GeolocationAccuracy.Best, FixTimeout);

                Microsoft.Maui.Devices.Sensors.Location where = null;

                //the timeout on the request is not enough on its own: asking a
                //second time can leave the phone listening for a fix that
                //never comes, which is what left this working once per run.
                //the token is what actually gets it back
                using (CancellationTokenSource giveUp = new CancellationTokenSource(FixTimeout))
                {
                    try
                    {
                        where = await Geolocation.Default.GetLocationAsync(request, giveUp.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        //no fix in time. the last one the phone had is better
                        //than nothing when it is a street being filled in
                    }
                }

                if (where == null)
                    where = await Geolocation.Default.GetLastKnownLocationAsync();

                if (where == null)
                    await Say(page, "Your phone could not work out where it is. Out in the open it usually only takes a moment.");

                return where;
            }
            catch (FeatureNotSupportedException)
            {
                await Say(page, "This device cannot find where it is, so the address will have to be typed in.");
            }
            catch (FeatureNotEnabledException)
            {
                await Say(page, "Location is turned off on this device. Turn it on and try again.");
            }
            catch (PermissionException)
            {
                await Say(page, "Work Tracker needs permission to use your location before it can fill an address in.");
            }
            catch (Exception ex)
            {
                await Say(page, $"Could not find where you are: {ex.Message}");
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
