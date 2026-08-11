using Microsoft.Maui.ApplicationModel.Communication;

namespace UiInterface
{
    /// <summary>
    /// Filling a customer's details in from the phone's own contacts, so
    /// somebody taken down on the doorstep does not have to be typed in twice.
    ///
    /// Only the name, phone and email come across: the contacts API does not
    /// hand over a postal address, so the address is still typed by hand.
    /// </summary>
    public static class ContactFill
    {
        /// <summary>what was taken from a contact. a field left null was not offered</summary>
        public class Details
        {
            public string Name;
            public string Phone;
            public string Email;
        }

        /// <summary>
        /// opens the phone's contact picker and hands back what was chosen.
        /// null when the picker was backed out of, or when the device cannot do
        /// it at all - the reason is put to the user here, so a caller only has
        /// to deal with what it is given
        /// </summary>
        public static async Task<Details> PickAsync(Page page)
        {
            Contact contact;
            try
            {
                contact = await Contacts.Default.PickContactAsync();
            }
            catch (FeatureNotSupportedException)
            {
                await page.DisplayAlert("Contacts",
                    "This device cannot pick a contact, so the details will have to be typed in.", "Ok");
                return null;
            }
            catch (PermissionException)
            {
                await page.DisplayAlert("Contacts",
                    "Work Tracker needs permission to read your contacts before it can fill these in.", "Ok");
                return null;
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Contacts", $"Could not open your contacts: {ex.Message}", "Ok");
                return null;
            }

            //picker was backed out of
            if (contact == null)
                return null;

            //a contact filed under a company or a nickname has no given name,
            //so the display name is used first and the parts only made up from
            //scratch when there is nothing else to go on
            string name = contact.DisplayName;
            if (string.IsNullOrWhiteSpace(name))
                name = $"{contact.GivenName} {contact.FamilyName}".Trim();

            List<string> phones = new List<string>();
            if (contact.Phones != null)
                foreach (ContactPhone p in contact.Phones)
                    phones.Add(p.PhoneNumber);

            List<string> emails = new List<string>();
            if (contact.Emails != null)
                foreach (ContactEmail em in contact.Emails)
                    emails.Add(em.EmailAddress);

            return new Details
            {
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Phone = await ChooseAsync(page, "Phone Number", phones),
                Email = await ChooseAsync(page, "Email", emails),
            };
        }

        /// <summary>
        /// a contact can carry several numbers. one is taken as it stands,
        /// more than one is asked about rather than guessed at
        /// </summary>
        static async Task<string> ChooseAsync(Page page, string title, List<string> values)
        {
            values.RemoveAll(string.IsNullOrWhiteSpace);

            if (values.Count == 0)
                return null;
            if (values.Count == 1)
                return values[0];

            string picked = await page.DisplayActionSheet(title, "Cancel", null, values.ToArray());

            //backing out of the sheet leaves that field alone rather than
            //wiping whatever was already typed
            if (string.IsNullOrWhiteSpace(picked) || picked == "Cancel")
                return null;

            return picked;
        }
    }
}
