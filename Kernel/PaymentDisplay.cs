using System;
using System.Xml.Serialization;
using Microsoft.Maui.Graphics;

namespace Kernel
{
    //  The half of Payment that exists for the screen.
    //
    //  Same rule as Job.cs / JobDisplay.cs: if deleting a member could only
    //  ever break a screen it belongs here; if it could break a figure, a
    //  file or a rule about the money it belongs in Payment.cs, and nothing
    //  in Payment.cs should ever need a colour.
    //
    //  Cash, a card, PayPal, a bank transfer, a cheque and a direct debit
    //  are not the same thing, and a list of payments that says so in one
    //  word each is a list that has to be read a line at a time. A colour
    //  and a picture per method is what lets the page be glanced at - the
    //  cash of a Saturday round is one colour running down the page, and
    //  the one bank transfer among it stands out without being looked for.
    //
    //  The wording, the colour and the icon are all worked out from the one
    //  switch on the method, so a method added to the enum shows up here as
    //  Other rather than as a blank chip.
    public partial class Payment
    {
        /// <summary>
        /// how the method is said to somebody. the enum's own names are what
        /// is saved and what the pickers parse back, so they are not touched:
        /// this is the reading version, where Bank is a bank transfer and
        /// GoCardless is the direct debit it actually is
        /// </summary>
        [XmlIgnore]
        public string MethodName
        {
            get { return NameFor(PaymentMethod); }
        }

        /// <summary>
        /// the same wording asked about a method on its own rather than about
        /// a payment. the filter chips on the payments page have to name a
        /// method they may have no payment for, and a second switch written
        /// over there is how the two would come to disagree
        /// </summary>
        public static string NameFor(PaymentMethod method)
        {
            switch (method)
            {
                case PaymentMethod.Cash: return "Cash";
                case PaymentMethod.Card: return "Card Payment";
                case PaymentMethod.Paypal: return "PayPal";
                case PaymentMethod.Bank: return "Bank Transfer";
                case PaymentMethod.Check: return "Cheque";
                case PaymentMethod.GoCardless: return "Direct Debit";
            }

            return "Other";
        }

        /// <summary>
        /// the colour of the method's chip and disc. these are backgrounds
        /// with white on them, so they are the deep end of each colour - a
        /// pale one cannot carry white text or a white icon
        /// </summary>
        [XmlIgnore]
        public Color MethodColour
        {
            get { return ColourFor(PaymentMethod); }
        }

        /// <summary>the same colour, asked about a method on its own - see
        /// <see cref="NameFor"/> for why these are static</summary>
        public static Color ColourFor(PaymentMethod method)
        {
            switch (method)
            {
                case PaymentMethod.Cash: return Color.FromArgb("#2E7D32");       //notes and coins, green
                case PaymentMethod.Card: return Color.FromArgb("#6A1B9A");       //purple
                case PaymentMethod.Paypal: return Color.FromArgb("#0070BA");     //paypal's own blue
                case PaymentMethod.Bank: return Color.FromArgb("#00838F");       //teal
                case PaymentMethod.Check: return Color.FromArgb("#8D6E63");      //paper, brown
                case PaymentMethod.GoCardless: return Color.FromArgb("#D84315"); //orange
            }

            return Color.FromArgb("#546E7A"); //other, slate
        }

        /// <summary>
        /// what is written and drawn on <see cref="MethodColour"/>. white on
        /// every one of them, and said here rather than in the page so a
        /// colour ever changed to a pale one changes this with it
        /// </summary>
        [XmlIgnore]
        public Color MethodTextColour
        {
            get { return Colors.White; }
        }

        /// <summary>
        /// the icon for the method, white stroked so it reads on the disc.
        /// the svgs are turned into pngs at build, which is why these are
        /// named .png - see the toolbar icons for the same trick
        /// </summary>
        [XmlIgnore]
        public string MethodIcon
        {
            get { return IconFor(PaymentMethod); }
        }

        /// <summary>the same icon, asked about a method on its own - see
        /// <see cref="NameFor"/> for why these are static</summary>
        public static string IconFor(PaymentMethod method)
        {
            switch (method)
            {
                case PaymentMethod.Cash: return "paycash.png";
                case PaymentMethod.Card: return "paycard.png";
                case PaymentMethod.Paypal: return "paypaypal.png";
                case PaymentMethod.Bank: return "paybank.png";
                case PaymentMethod.Check: return "paycheque.png";
                case PaymentMethod.GoCardless: return "paydebit.png";
            }

            return "payother.png";
        }

        /// <summary>
        /// how long ago it was, and only when that is worth saying:
        /// <see cref="PaymentDaysAgo"/> and <see cref="PaymentDate"/> both
        /// say "Today" today, and a row saying it twice reads as a mistake
        /// </summary>
        [XmlIgnore]
        public bool ShowAge
        {
            get { return UsfulFuctions.Difference(Date, DateTime.Now) > 1; }
        }

        /// <summary>
        /// the reference the bank put on the payment, or nothing at all for
        /// cash handed over at the door. an empty chip on every cash payment
        /// is a column of blanks down the page
        /// </summary>
        [XmlIgnore]
        public bool HasReference
        {
            get { return !string.IsNullOrWhiteSpace(CustomerReference); }
        }

        /// <summary>
        /// true while the payment is not linked to anybody, which is what the
        /// page colours the customer line off
        /// </summary>
        [XmlIgnore]
        public bool IsUnidentified
        {
            get { return GetCustomer(CustomerId) == null; }
        }

        /// <summary>
        /// red while nobody owns the payment, quiet grey once somebody does
        /// </summary>
        [XmlIgnore]
        public Color CustomerTextColour
        {
            get { return IsUnidentified ? Color.FromArgb("#C62828") : Color.FromArgb("#6B7280"); }
        }
    }
}
