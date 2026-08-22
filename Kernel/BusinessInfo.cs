using System;
using System.IO;

namespace Kernel
{
    /// <summary>
    /// Who the invoices are from - the round's own name, address and contact
    /// details, and its logo. It is set once on the settings page and printed
    /// on the top of every invoice.
    ///
    /// The text fields are kept in the settings file, not a file of their own,
    /// because they are how the app is set up rather than something it has
    /// recorded - the same home as the paypal.me name and the default job
    /// duration. Like those two this is a static holder the kernel reads and
    /// <see cref="Settings"/> saves alongside everything else; the kernel does
    /// the invoice building and cannot see the settings page. A field never
    /// filled in reads back as empty and is simply left off the invoice.
    ///
    /// The logo is an image, so it is a file (<see cref="LogoFileName"/>) in
    /// the data folder rather than text in the settings. It sits with the data
    /// so it rides in every backup, and the invoice reads its bytes to embed
    /// it. The settings page is what writes it, scaled down like a receipt
    /// photo.
    /// </summary>
    public static class BusinessInfo
    {
        /// <summary>the business name, printed largest at the top of the invoice</summary>
        public static string Name = string.Empty;

        /// <summary>the business address, one line per line break</summary>
        public static string Address = string.Empty;

        public static string Phone = string.Empty;

        /// <summary>the contact email printed on the invoice</summary>
        public static string Email = string.Empty;

        public static string Website = string.Empty;

        /// <summary>VAT or UTR number, for a business that has one</summary>
        public static string TaxNumber = string.Empty;

        /// <summary>how to pay - bank details, a note about PayPal, whatever suits</summary>
        public static string PaymentDetails = string.Empty;

        /// <summary>the line along the bottom - terms, a thank you</summary>
        public static string FooterNote = string.Empty;

        /// <summary>
        /// true once there is at least a name. an invoice can be made without
        /// it, but the header would be blank, so the settings page nudges for
        /// it first
        /// </summary>
        public static bool IsSetUp
        {
            get { return !string.IsNullOrWhiteSpace(Name); }
        }

        //  --------------------------------------------------------  the logo

        /// <summary>
        /// what the logo is called in the data folder. it is a jpeg, written
        /// scaled down by the settings page the same way a receipt photo is
        /// </summary>
        public const string LogoFileName = "invoicelogo.jpg";

        /// <summary>where the logo lives - in the data folder, so it is backed up</summary>
        public static string LogoPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    LogoFileName);
            }
        }

        public static bool HasLogo
        {
            get
            {
                try
                {
                    return File.Exists(LogoPath);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>the logo's bytes for embedding in an invoice, or null</summary>
        public static byte[] LogoBytes()
        {
            try
            {
                return HasLogo ? File.ReadAllBytes(LogoPath) : null;
            }
            catch
            {
                return null;
            }
        }

        public static void RemoveLogo()
        {
            try
            {
                if (File.Exists(LogoPath))
                    File.Delete(LogoPath);
            }
            catch
            {
            }
        }

        //  ------------------------------------------------  saved with settings

        /// <summary>the text fields, for the settings file to write and read</summary>
        public struct Data
        {
            public string Name;
            public string Address;
            public string Phone;
            public string Email;
            public string Website;
            public string TaxNumber;
            public string PaymentDetails;
            public string FooterNote;
        }

        public static Data Snapshot()
        {
            return new Data()
            {
                Name = Name,
                Address = Address,
                Phone = Phone,
                Email = Email,
                Website = Website,
                TaxNumber = TaxNumber,
                PaymentDetails = PaymentDetails,
                FooterNote = FooterNote,
            };
        }

        /// <summary>
        /// takes the fields back off a settings file. a file written before
        /// this existed reads back with every field null, which is the same
        /// as never having filled any of them in
        /// </summary>
        public static void Restore(Data data)
        {
            Name = data.Name ?? string.Empty;
            Address = data.Address ?? string.Empty;
            Phone = data.Phone ?? string.Empty;
            Email = data.Email ?? string.Empty;
            Website = data.Website ?? string.Empty;
            TaxNumber = data.TaxNumber ?? string.Empty;
            PaymentDetails = data.PaymentDetails ?? string.Empty;
            FooterNote = data.FooterNote ?? string.Empty;
        }
    }
}
