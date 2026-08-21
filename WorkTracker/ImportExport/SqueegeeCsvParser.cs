using System.Globalization;
using System.Text.RegularExpressions;
using Kernel;

namespace UiInterface.ImportExport
{
    /// <summary>
    /// What one read of a Squeegee export came to, past the rows themselves.
    ///
    /// The counts are the point. A Squeegee export is not a round - it is a
    /// list of invoices or of jobs, with several rows for the same house and
    /// rows that are not work at all - so an import that quietly turned
    /// 2,000 rows into 300 houses would leave nobody any way of knowing
    /// whether it had understood the file or thrown most of it away.
    /// </summary>
    public class SqueegeeImport
    {
        /// <summary>one per house, which is what the importer wants</summary>
        public List<ImportedCustomerRow> Rows = new List<ImportedCustomerRow>();

        /// <summary>headings the file had that were understood, as "Address -> address"</summary>
        public List<string> ColumnsUsed = new List<string>();

        /// <summary>headings the file had that mean nothing here</summary>
        public List<string> ColumnsIgnored = new List<string>();

        public int RowsRead;
        /// <summary>voided invoices, and the negative twin every void has</summary>
        public int VoidsSkipped;
        /// <summary>rows with nothing that could be read as an address</summary>
        public int NoAddress;
        /// <summary>rows that were another go at a house already read</summary>
        public int DuplicatesFolded;
        /// <summary>houses whose row carried no readable price</summary>
        public int NoPrice;
        /// <summary>houses the file gave no repeat for - imported as one offs</summary>
        public int OneOffs;

        /// <summary>the file has to have said where the work is to be any use</summary>
        public bool HasAddress;
    }

    /// <summary>
    /// Reads a Squeegee export (Reporting -> download csv) into the same
    /// <see cref="ImportedCustomerRow"/> list a round spreadsheet is read
    /// into, so <see cref="CustomerImporter"/> does the mapping onto
    /// customers and jobs for both and the two cannot drift apart.
    ///
    /// **Columns are found by their heading, never by where they sit**, the
    /// same rule <see cref="PayPalStatement"/> follows and for the same
    /// reason: a bank has its columns pointed out once and remembered
    /// because it is one layout for ever, and an export from another app
    /// names its own and can rename them in the next release. Nothing about
    /// the layout is saved.
    ///
    /// Squeegee has more than one report that can be downloaded and they do
    /// not carry the same columns - the invoices report has addresses,
    /// prices, frequencies and rounds but no phone numbers - so the headings
    /// are aliased rather than demanded, and what was understood and what
    /// was ignored is handed back to be said out loud before anything is
    /// imported.
    /// </summary>
    public static class SqueegeeCsvParser
    {
        //--------------------------------------------------------------
        // which heading is which
        //--------------------------------------------------------------

        //the whole address in one cell - "91 Crate Close, Oldbury"
        static readonly string[] AddressNames =
            { "address", "job address", "site address", "property address", "customer address", "full address" };

        //or in pieces, which is what a fuller export gives
        static readonly string[] Address1Names = { "address line 1", "addressline1", "address 1", "street" };
        static readonly string[] Address2Names = { "address line 2", "addressline2", "address 2" };
        static readonly string[] TownNames = { "town", "city", "town/city", "post town" };
        static readonly string[] AreaNames = { "area", "county", "district", "region" };
        static readonly string[] PostcodeNames = { "postcode", "post code", "zip", "zip code" };

        static readonly string[] NameNames = { "customer", "customer name", "client", "client name", "name" };
        static readonly string[] RefNames = { "customer ref", "customer reference", "client ref", "customer id", "ref" };
        static readonly string[] FrequencyNames = { "frequency", "recurrence", "schedule", "repeats", "interval" };
        static readonly string[] RoundColumnNames = { "round", "rounds", "run", "work round" };
        static readonly string[] PhoneNames = { "phone", "telephone", "mobile", "phone number", "telephone number", "contact number" };
        static readonly string[] EmailNames = { "email", "e-mail", "email address" };
        static readonly string[] NotesNames = { "notes", "note", "description", "job description", "comments", "instructions" };

        //gross first: it is what the customer actually pays, so a balance
        //cleared against it comes out to the penny. the same rule the PayPal
        //import follows over taking the net and leaving everybody a few
        //pence short for ever
        static readonly string[] GrossNames = { "amount", "total", "gross", "price", "job price", "cost", "value", "charge" };
        static readonly string[] NetNames = { "subtotal", "sub total", "net", "net amount", "amount excluding tax" };

        static readonly string[] StatusNames = { "status", "state", "invoice status" };

        //the day the work is next wanted. **"due date" is deliberately not
        //one of these**: on the invoices report that column is the day the
        //invoice wants paying, which has nothing to do with when the house
        //is next cleaned, and reading it as a due date would put a round's
        //worth of work on the wrong days without anything looking wrong
        static readonly string[] NextDueNames =
            { "next due", "next due date", "next visit", "next clean", "planned date", "scheduled date", "next planned" };

        //the day the row itself is about, used to tell one go at a house
        //from an older one
        static readonly string[] DateNames = { "date", "invoice date", "job date", "completed date", "date completed" };

        public static SqueegeeImport Parse(CSVFile file)
        {
            SqueegeeImport read = new SqueegeeImport();
            if (file == null || file.Header == null || file.data == null)
                return read;

            var header = file.Header;
            int colAddress = Column(header, AddressNames);
            int colAddress1 = Column(header, Address1Names);
            int colAddress2 = Column(header, Address2Names);
            int colTown = Column(header, TownNames);
            int colArea = Column(header, AreaNames);
            int colPostcode = Column(header, PostcodeNames);
            int colName = Column(header, NameNames);
            int colRef = Column(header, RefNames);
            int colFreq = Column(header, FrequencyNames);
            int colRound = Column(header, RoundColumnNames);
            int colPhone = Column(header, PhoneNames);
            int colEmail = Column(header, EmailNames);
            int colNotes = Column(header, NotesNames);
            int colGross = Column(header, GrossNames);
            int colNet = Column(header, NetNames);
            int colStatus = Column(header, StatusNames);
            int colNextDue = Column(header, NextDueNames);
            int colDate = Column(header, DateNames);

            var used = new HashSet<int>();
            Note(read, header, colAddress, "address", used);
            Note(read, header, colAddress1, "address", used);
            Note(read, header, colAddress2, "address", used);
            Note(read, header, colTown, "town", used);
            Note(read, header, colArea, "area", used);
            Note(read, header, colPostcode, "postcode", used);
            Note(read, header, colName, "customer name", used);
            Note(read, header, colRef, "customer reference", used);
            Note(read, header, colFreq, "how often", used);
            Note(read, header, colRound, "round", used);
            Note(read, header, colPhone, "phone", used);
            Note(read, header, colEmail, "email", used);
            Note(read, header, colNotes, "notes", used);
            Note(read, header, colGross, "price", used);
            Note(read, header, colNet, "price if there is no total", used);
            Note(read, header, colStatus, "status", used);
            Note(read, header, colNextDue, "next due", used);
            Note(read, header, colDate, "date of the row", used);

            for (int i = 0; i < header.Length; i++)
                if (!used.Contains(i) && !string.IsNullOrWhiteSpace(header[i]))
                    read.ColumnsIgnored.Add(header[i].Trim());

            read.HasAddress = colAddress >= 0 || colAddress1 >= 0;
            if (!read.HasAddress)
                return read;

            //one house at a time, newest row winning. keyed on the customer
            //reference **and** the address rather than on either alone: a
            //landlord's three houses share one reference, and two customers
            //with the same name are two customers
            var houses = new Dictionary<string, (ImportedCustomerRow row, DateTime when)>();
            var order = new List<string>();

            foreach (string[] cells in file.data)
            {
                if (cells == null || cells.Length == 0)
                    continue;
                if (cells.All(string.IsNullOrWhiteSpace))
                    continue;

                read.RowsRead++;

                //a voided invoice is money that was never asked for, and
                //every void comes as a pair - the original and a negative
                //twin. neither says anything about the round
                string status = At(cells, colStatus);
                if (status.Trim().Equals("void", StringComparison.OrdinalIgnoreCase))
                {
                    read.VoidsSkipped++;
                    continue;
                }

                string address = colAddress >= 0
                    ? At(cells, colAddress)
                    : Join(At(cells, colAddress1), At(cells, colAddress2));

                Split split = SplitAddress(address);
                if (colTown >= 0)
                    split.Town = At(cells, colTown).Trim();
                if (colArea >= 0)
                    split.Area = At(cells, colArea).Trim();
                if (colPostcode >= 0)
                    split.Postcode = At(cells, colPostcode).Trim();

                if (split.Street.Length == 0 && split.Number.Length == 0)
                {
                    read.NoAddress++;
                    continue;
                }

                (int amount, FrequenceType type, bool oneOff) = ReadFrequency(At(cells, colFreq));

                var row = new ImportedCustomerRow
                {
                    SourceRow = read.RowsRead + 1,   //+1 for the heading row
                    HouseNumber = split.Number,
                    Street = split.Street,
                    City = split.Town,
                    Area = split.Area,
                    Postcode = split.Postcode,
                    Name = At(cells, colName).Trim(),
                    ExternalRef = At(cells, colRef).Trim(),
                    Phone = At(cells, colPhone).Trim(),
                    Email = At(cells, colEmail).Trim(),
                    Notes = At(cells, colNotes).Trim(),
                    Round = At(cells, colRound).Trim(),
                    FrequencyText = At(cells, colFreq).Trim(),
                    FrequencyAmount = amount,
                    FrequencyType = type,
                    OneOff = oneOff,
                    NextDue = ReadDate(At(cells, colNextDue)),
                };

                //gross is what the customer pays; the net column is only a
                //fallback for an export that has no total on it
                row.PriceText = At(cells, colGross).Trim();
                row.Price = ReadPrice(row.PriceText);
                if (!row.Price.HasValue && colNet >= 0)
                {
                    row.PriceText = At(cells, colNet).Trim();
                    row.Price = ReadPrice(row.PriceText);
                }

                DateTime when = ReadDate(At(cells, colDate)) ?? DateTime.MinValue;
                string key = Key(row);

                if (houses.TryGetValue(key, out var already))
                {
                    read.DuplicatesFolded++;
                    //an older row cannot tell us anything the newer one has
                    //not already said about what the house costs now
                    if (when < already.when)
                        continue;
                    houses[key] = (Fill(row, already.row), when);
                    continue;
                }

                houses[key] = (row, when);
                order.Add(key);
            }

            foreach (string key in order)
            {
                ImportedCustomerRow row = houses[key].row;
                if (!row.Price.HasValue)
                    read.NoPrice++;
                if (row.OneOff)
                    read.OneOffs++;
                read.Rows.Add(row);
            }

            return read;
        }

        //--------------------------------------------------------------
        // the address
        //--------------------------------------------------------------

        public class Split
        {
            public string Number = string.Empty;
            public string Street = string.Empty;
            public string Town = string.Empty;
            public string Area = string.Empty;
            public string Postcode = string.Empty;
        }

        static readonly Regex PostcodeRegex = new Regex(
            @"^[A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        //"91", "91a", "91-93", "Flat 2"
        static readonly Regex LeadingNumberRegex = new Regex(
            @"^\s*(?<n>(?:flat|apt|apartment|unit)?\s*\d+[a-z]?(?:\s*[-/]\s*\d+[a-z]?)?)\s+(?<s>\S.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Takes "91 Crate Close, Oldbury" apart into the pieces a
        /// <see cref="Kernel.Location"/> is kept in.
        ///
        /// **This is the part of the import that can go wrong quietly.**
        /// Customers are matched on house number plus street, so a street
        /// read wrongly does not fail - it creates a second customer beside
        /// the one already on the round, which is exactly the mess
        /// Layouts/TidyCustomers exists to clear up. The rules are therefore
        /// kept plain and the guesses are the ones a UK address really does
        /// follow, rather than being clever:
        ///
        /// - a postcode at the end is taken off first, so it cannot be read
        ///   as the town
        /// - a first piece with no digits in it at all is the name of the
        ///   house and the piece after it is the street - "Rose Cottage,
        ///   High Street, Oldbury". With only those two there is no town
        ///   rather than a made up one
        /// - otherwise the last piece is the town, the first is the street
        ///   and anything between the two is the area
        /// - one piece on its own is a street with no town, which the
        ///   import page's answer then fills in
        /// </summary>
        public static Split SplitAddress(string address)
        {
            var split = new Split();
            var parts = (address ?? string.Empty)
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();

            if (parts.Count == 0)
                return split;

            if (PostcodeRegex.IsMatch(parts[parts.Count - 1]))
            {
                split.Postcode = parts[parts.Count - 1].ToUpperInvariant();
                parts.RemoveAt(parts.Count - 1);
            }

            if (parts.Count == 0)
                return split;

            string streetLine;
            if (parts.Count == 1)
            {
                //nothing but a street. the town is left for the import page
                //to answer rather than made up out of half an address
                streetLine = parts[0];
            }
            else if (!parts[0].Any(char.IsDigit))
            {
                //a first line with no number anywhere in it is the name of
                //the house rather than the street it stands on - "Rose
                //Cottage, High Street, Oldbury". PropertyNameNumber is a
                //name or a number, so the name goes there and the street
                //stays a street, which is what keeps the houses on it
                //grouping together on every page that reads a round street
                //by street
                split.Number = parts[0];
                split.Street = parts[1];
                if (parts.Count > 2)
                {
                    split.Town = parts[parts.Count - 1];
                    if (parts.Count > 3)
                        split.Area = string.Join(", ", parts.Skip(2).Take(parts.Count - 3));
                }
                return split;
            }
            else
            {
                split.Town = parts[parts.Count - 1];
                parts.RemoveAt(parts.Count - 1);

                streetLine = parts[0];
                if (parts.Count > 1)
                    split.Area = string.Join(", ", parts.Skip(1));
            }

            Match m = LeadingNumberRegex.Match(streetLine);
            if (m.Success)
            {
                split.Number = Tidy(m.Groups["n"].Value);
                split.Street = m.Groups["s"].Value.Trim();
            }
            else
            {
                //no number on it at all - the whole line is the street, and
                //the house is told apart by the customer instead. leaving
                //the street blank and calling the line a number would put
                //the house on a street nobody else is on
                split.Street = streetLine;
            }

            return split;
        }

        static string Tidy(string text) => Regex.Replace(text.Trim(), @"\s+", " ");

        //--------------------------------------------------------------
        // how often, and what it costs
        //--------------------------------------------------------------

        //"on Friday", "on Tuesdays" - the day of the week a round falls on
        static readonly Regex OnADayRegex = new Regex(
            @"\bon\s+(?:mon|tues|wednes|thurs|fri|satur|sun)days?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// "every 4 weeks on Friday" is 4 weeks. A job here does not keep
        /// which day of the week it falls on, so that half is dropped.
        ///
        /// **It is dropped before the unit is looked for, and that is not
        /// tidiness.** Friday has "day" in it. Anything reading the unit
        /// out of the wording while the day name is still there answers
        /// "day" to every Squeegee frequency there is, and a four weekly
        /// house comes in as a four *daily* one - which is not a wrong
        /// figure on a screen, it is a house on the work list seven times a
        /// month for ever. Weeks are then tested before days for the same
        /// reason, so nothing has to be right twice.
        ///
        /// **Nothing said means a one off, not four weekly.** A blank
        /// frequency and "ad hoc" are how Squeegee says the work does not
        /// come round, and a one off never generates a next visit
        /// (Job.IsOneOff). Reading it as four weekly would invent a standing
        /// appointment nobody agreed to and put it on the round for ever,
        /// where a one off costs a house that really does repeat one
        /// frequency typed in by hand.
        /// </summary>
        public static (int amount, FrequenceType type, bool oneOff) ReadFrequency(string text)
        {
            string t = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (t.Length == 0 || t.Contains("ad hoc") || t.Contains("adhoc")
                || t.Contains("one off") || t.Contains("one-off") || t.Contains("oneoff")
                || t.Contains("none") || t.Contains("never"))
                return (0, FrequenceType.Week, true);

            t = OnADayRegex.Replace(t, " ");

            FrequenceType type = UnitIn(t);

            Match m = Regex.Match(t, @"\d+");
            if (!m.Success)
            {
                //the worded ones carry no number of their own
                if (t.Contains("fortnight")) return (2, FrequenceType.Week, false);
                if (t.Contains("quarter")) return (3, FrequenceType.Month, false);
                if (!Said(t)) return (0, FrequenceType.Week, true);
                return (1, type, false);
            }

            if (!int.TryParse(m.Value, out int amount) || amount <= 0)
                return (0, FrequenceType.Week, true);

            return (amount, type, false);
        }

        /// <summary>
        /// weeks unless the wording says otherwise - a round is worked in
        /// weeks and that is what an unrecognised wording is likeliest to
        /// have meant
        /// </summary>
        static FrequenceType UnitIn(string t)
        {
            if (t.Contains("year") || t.Contains("annual")) return FrequenceType.Year;
            if (t.Contains("month")) return FrequenceType.Month;
            if (t.Contains("week") || t.Contains("wk") || t.Contains("fortnight")) return FrequenceType.Week;
            if (t.Contains("day") || t.Contains("daily")) return FrequenceType.Day;
            return FrequenceType.Week;
        }

        /// <summary>whether the wording named a unit at all</summary>
        static bool Said(string t)
            => t.Contains("year") || t.Contains("annual") || t.Contains("month")
            || t.Contains("week") || t.Contains("wk") || t.Contains("fortnight")
            || t.Contains("day") || t.Contains("daily") || t.Contains("quarter");

        /// <summary>
        /// what the row says the house costs. a nought or a credit note is
        /// no price at all - the importer says so and asks for one rather
        /// than putting a house on the round that never asks anybody for
        /// money
        /// </summary>
        public static float? ReadPrice(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string cleaned = text.Replace("£", string.Empty).Replace("$", string.Empty)
                .Replace(",", string.Empty).Trim();
            if (float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out float price) && price > 0)
                return price;
            return null;
        }

        //made once rather than per row: a round's export is thousands of
        //them and building a culture each time is not free
        static readonly CultureInfo Uk = new CultureInfo("en-GB");

        public static DateTime? ReadDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string t = text.Trim();

            //the exports write dates the sortable way round. day-first is
            //tried after it because a british export can be either, and
            //month-first is not tried at all: 05/11 is the 5th of November
            //here and reading it as May would move work by six months
            if (DateTime.TryParseExact(t, new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime iso))
                return iso.Date;

            if (DateTime.TryParse(t, Uk, DateTimeStyles.None, out DateTime uk))
                return uk.Date;

            return null;
        }

        //--------------------------------------------------------------
        // odds and ends
        //--------------------------------------------------------------

        static string Key(ImportedCustomerRow row)
        {
            string address = (row.HouseNumber + "|" + row.Street + "|" + row.City).ToLowerInvariant();
            string who = row.ExternalRef.Length > 0 ? row.ExternalRef.ToLowerInvariant() : row.Name.ToLowerInvariant();
            return who + "||" + address;
        }

        /// <summary>
        /// the newest row wins, but it is topped up from the older one it
        /// replaced: an export can leave a cell empty on one row and filled
        /// on the next, and the newest row having no phone number in it is
        /// not the same as there being no phone number
        /// </summary>
        static ImportedCustomerRow Fill(ImportedCustomerRow newer, ImportedCustomerRow older)
        {
            if (newer.Name.Length == 0) newer.Name = older.Name;
            if (newer.Phone.Length == 0) newer.Phone = older.Phone;
            if (newer.Email.Length == 0) newer.Email = older.Email;
            if (newer.Notes.Length == 0) newer.Notes = older.Notes;
            if (newer.Round.Length == 0) newer.Round = older.Round;
            if (newer.City.Length == 0) newer.City = older.City;
            if (newer.Area.Length == 0) newer.Area = older.Area;
            if (newer.Postcode.Length == 0) newer.Postcode = older.Postcode;
            if (newer.ExternalRef.Length == 0) newer.ExternalRef = older.ExternalRef;
            if (!newer.Price.HasValue && older.Price.HasValue)
            {
                newer.Price = older.Price;
                newer.PriceText = older.PriceText;
            }
            if (newer.OneOff && !older.OneOff)
            {
                newer.OneOff = false;
                newer.FrequencyAmount = older.FrequencyAmount;
                newer.FrequencyType = older.FrequencyType;
                newer.FrequencyText = older.FrequencyText;
            }
            if (!newer.NextDue.HasValue) newer.NextDue = older.NextDue;
            return newer;
        }

        static void Note(SqueegeeImport read, string[] header, int col, string what, HashSet<int> used)
        {
            if (col < 0 || used.Contains(col))
                return;
            used.Add(col);
            read.ColumnsUsed.Add($"{header[col].Trim()} -> {what}");
        }

        static int Column(string[] header, string[] names)
        {
            for (int i = 0; i < header.Length; i++)
            {
                string h = (header[i] ?? string.Empty).Trim().ToLowerInvariant();
                if (h.Length > 0 && names.Contains(h))
                    return i;
            }
            return -1;
        }

        static string At(string[] cells, int col)
            => col < 0 || col >= cells.Length ? string.Empty : (cells[col] ?? string.Empty);

        static string Join(string a, string b)
        {
            a = (a ?? string.Empty).Trim();
            b = (b ?? string.Empty).Trim();
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            return a + ", " + b;
        }
    }
}
