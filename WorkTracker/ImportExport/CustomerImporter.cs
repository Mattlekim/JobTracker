using Kernel;

namespace UiInterface.ImportExport
{
    public class ImportResult
    {
        public int Created;
        public int Updated;
        public int MissingPrice;
        /// <summary>notes that said to text the customer the night before</summary>
        public int TnbFromNotes;
        /// <summary>phone numbers moved out of notes into the phone field</summary>
        public int PhonesFound;
        /// <summary>front only prices stored as an alternative price</summary>
        public int FrontPrices;
        /// <summary>jobs put on the round that was picked for the sheet</summary>
        public int RoundSet;
        /// <summary>customers whose balance was cleared, with a record kept</summary>
        public int BalancesCleared;
        /// <summary>jobs given the one due date that was picked for the sheet</summary>
        public int DueDatesSet;
        /// <summary>jobs left where they were because a day is booked in for them</summary>
        public int DueDatesLeftBooked;
        /// <summary>houses the file gave no repeat for, imported as one offs</summary>
        public int OneOffs;
        /// <summary>email addresses the file carried</summary>
        public int EmailsFound;
        /// <summary>jobs put on a round the file itself named, rather than the one picked</summary>
        public int RoundsFromFile;
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// What is being asked of one import, past the file itself.
    ///
    /// A round sheet says where the houses are and what they cost and
    /// nothing else, so the three things it cannot say are asked once for
    /// the whole sheet rather than typed in a house at a time afterwards:
    /// which round the work is on, whether anybody starts out owing
    /// anything, and when it is all first due.
    /// </summary>
    public class ImportOptions
    {
        /// <summary>
        /// the sheet has streets but no town. an export from another app
        /// usually does say, so this is what is used **where the file does
        /// not** rather than what is put on everybody: overwriting a town
        /// the file got right would be worse than the gap it fills
        /// </summary>
        public string City = string.Empty;
        public string Area = string.Empty;

        /// <summary>
        /// the round every job off this sheet goes on. blank asks for
        /// nothing - work already on a round keeps it, and new work starts
        /// on none - because a sheet is usually one round and a blank
        /// answer is the answer of somebody who has not got rounds
        /// </summary>
        public string Round = string.Empty;

        /// <summary>
        /// start everybody on the sheet at nothing owed. what a sheet
        /// carries is the work, not the ledger, so a round taken on from
        /// somebody else's spreadsheet usually starts square. each one
        /// cleared leaves a <see cref="BalanceAdjustment"/> behind it,
        /// like every other balance changed by hand
        /// </summary>
        public bool ZeroBalances;

        /// <summary>
        /// the day all of it is first due. null works each house out from
        /// the last clean ticked on the sheet, which is what a sheet that
        /// has been kept up to date is for; a date is for one that has not
        /// </summary>
        public DateTime? DueDate;

        /// <summary>why a balance cleared by an import says it was cleared</summary>
        public const string ClearedReason = "Cleared on spreadsheet import";
    }

    /// <summary>
    /// Maps rows parsed from a round spreadsheet onto customers and jobs.
    /// Creates customers that do not exist yet (matched on house number +
    /// street) and updates price / frequency / TNB on ones that do.
    /// </summary>
    public static class CustomerImporter
    {
        public static ImportResult Import(Stream xlsxStream, ImportOptions options)
        {
            options = options ?? new ImportOptions();
            return Import(RoundSheetParser.Parse(xlsxStream), options);
        }

        /// <summary>
        /// The mapping half, on rows somebody else has read.
        ///
        /// A round sheet and an export from another app are two different
        /// files and want two different readers, but what is done with what
        /// comes out of them - matching a house that is already here,
        /// creating one that is not, putting the work on a round, clearing
        /// the balances - is the same job, and two copies of it would drift.
        /// </summary>
        public static ImportResult Import(List<ImportedCustomerRow> rows, ImportOptions options)
        {
            options = options ?? new ImportOptions();
            rows = rows ?? new List<ImportedCustomerRow>();
            var result = new ImportResult();

            //the round is remembered once for the whole file rather than
            //per row, so the list of rounds cannot end up with the same name
            //on it twice, and so the caller can tell whether the settings
            //need saving
            if (!string.IsNullOrWhiteSpace(options.Round))
                Job.RememberRound(options.Round);

            //a file that names a round per house brings its own names with
            //it, and they are remembered the same way
            foreach (string named in rows
                .Select(r => (r.Round ?? string.Empty).Trim())
                .Where(r => r.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase))
                Job.RememberRound(named);

            foreach (ImportedCustomerRow row in rows)
            {
                try
                {
                    ImportRow(row, options, result);
                }
                catch (Exception ex)
                {
                    result.Problems.Add($"Row {row.SourceRow} ({row.HouseNumber} {row.Street}): {ex.Message}");
                }
            }

            Customer.Save();
            Job.Save();
            return result;
        }

        static void ImportRow(ImportedCustomerRow row, ImportOptions options, ImportResult result)
        {
            Customer customer = Customer.Query().FirstOrDefault(c => c.Address != null
                && string.Equals(c.Address.PropertyNameNumber?.Trim(), row.HouseNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Address.Street?.Trim(), row.Street, StringComparison.OrdinalIgnoreCase));

            //anything the notes cell was really holding (text night before,
            //a phone number, a front only price) is stored properly and
            //taken back out of the notes
            RoundSheetParser.ParsedNotes parsed = RoundSheetParser.ParseNotes(row.Notes);

            bool tnb = row.Tnb || parsed.Tnb;
            if (parsed.Tnb)
                result.TnbFromNotes++;

            string phone = parsed.Phone;
            if (phone.Length > 0)
                result.PhonesFound++;

            //a file with a phone column of its own beats a number dug out
            //of a note. only the note counts towards PhonesFound - that
            //figure is about numbers moved out of free text, not about how
            //many customers ended up with one
            if (row.Phone.Trim().Length > 0)
                phone = row.Phone.Trim();

            //the sheet's own Front column wins over one written in a note
            float? frontPrice = RoundSheetParser.ParsePrice(row.FrontPriceText) ?? parsed.FrontPrice;
            if (frontPrice.HasValue)
                result.FrontPrices++;

            string notes = BuildNotes(row, parsed.Remaining, result);
            (int freqAmount, FrequenceType freqType) = FrequencyFor(row);
            if (row.OneOff)
                result.OneOffs++;

            if (customer == null)
            {
                var address = new Kernel.Location
                {
                    PropertyNameNumber = row.HouseNumber,
                    Street = row.Street,
                    City = Pick(row.City, options.City),
                    Area = Pick(row.Area, options.Area),
                    Postcode = (row.Postcode ?? string.Empty).Trim(),
                };

                customer = new Customer { Address = address };
                if (row.Name.Length > 0)
                    customer.FName = row.Name;
                if (phone.Length > 0)
                    customer.Phone = phone;
                if (row.Email.Trim().Length > 0)
                {
                    customer.Email = row.Email.Trim();
                    result.EmailsFound++;
                }
                customer.NormalPaymentMethord = PaymentMethodFrom(row.PaymentType);
                Customer.Add(customer);

                var job = new Job
                {
                    CustomerId = customer.Id,
                    Address = address,
                    Price = row.Price ?? 0,
                    Notes = notes,
                    TNB = tnb,
                };
                job.SetFrequence(freqAmount, freqType);
                job.DueDate = DueDateFor(options, row, freqAmount, freqType, result);
                ApplyFrontPrice(job, frontPrice);
                Job.Add(job);
                PutOnRound(job, row, options, result);
                result.Created++;
            }
            else
            {
                if (customer.Phone.Length == 0 && phone.Length > 0)
                    customer.Phone = phone;
                TopUpCustomer(customer, row, result);

                Job job = Job.Query(QueryType.CustomerId, customer.Id)
                    .OrderBy(j => j.Id)
                    .LastOrDefault(j => j.JobNextId == -1)
                    ?? Job.Query(QueryType.CustomerId, customer.Id).LastOrDefault();
                if (job == null)
                {
                    job = new Job
                    {
                        CustomerId = customer.Id,
                        Address = customer.Address,
                        Price = row.Price ?? 0,
                        Notes = notes,
                        TNB = tnb,
                    };
                    job.SetFrequence(freqAmount, freqType);
                    job.DueDate = DueDateFor(options, row, freqAmount, freqType, result);
                    ApplyFrontPrice(job, frontPrice);
                    Job.Add(job);
                    PutOnRound(job, row, options, result);
                }
                else
                {
                    if (row.Price.HasValue)
                        job.Price = row.Price.Value;
                    job.TNB = tnb;
                    job.SetFrequence(freqAmount, freqType);
                    ApplyFrontPrice(job, frontPrice);
                    CleanExistingNotes(job, customer, result);

                    //a job already here has other visits behind it, so the
                    //round goes on the job rather than on this one visit
                    PutOnRound(job, row, options, result);
                    ReDate(job, options, result);
                }
                result.Updated++;
            }

            ClearBalance(customer, options, result);
        }

        /// <summary>
        /// puts the imported work on the round that was picked for the sheet.
        ///
        /// a blank round asks for nothing rather than taking work off the
        /// round it is on: a sheet is one round's worth of houses, and
        /// somebody who has not got rounds leaves the question alone.
        /// </summary>
        static void PutOnRound(Job job, ImportedCustomerRow row, ImportOptions options, ImportResult result)
        {
            //a round named against the house itself beats the one picked
            //for the whole file: a file that says which round each house is
            //on knows something the question could not ask
            string round = (row?.Round ?? string.Empty).Trim();
            bool fromFile = round.Length > 0;
            if (!fromFile)
                round = (options.Round ?? string.Empty).Trim();

            if (round.Length == 0 || job == null)
                return;

            //SetRound puts it on every visit of the job, which is what a
            //round is about - where the house is does not change between one
            //clean and the next
            job.SetRound(round);
            result.RoundSet++;
            if (fromFile)
                result.RoundsFromFile++;
        }

        /// <summary>
        /// when the sheet is being given one due date, work already on the
        /// round is moved to it as well - the point of answering it is to
        /// start the whole lot together.
        ///
        /// Three sorts of visit are left where they are. A clean already
        /// written up keeps the day it was done on, because that day is what
        /// a month's takings are read off, and a cancelled visit is not work.
        /// A day booked in is an arrangement with somebody: the calendar puts
        /// booked work on the day it is booked for rather than the day it is
        /// due, so moving the due date under it would say one thing on the
        /// calendar and another on the round. Anything left behind is
        /// counted rather than passed over quietly.
        /// </summary>
        static void ReDate(Job job, ImportOptions options, ImportResult result)
        {
            if (!options.DueDate.HasValue || job == null || job.IsCompleted || job.HaveCanceled)
                return;

            if (job.IsBookedIn)
            {
                result.DueDatesLeftBooked++;
                return;
            }

            job.DueDate = options.DueDate.Value;
            job.Refresh();
            job.RefreshColors();
            result.DueDatesSet++;
        }

        /// <summary>
        /// the day a house imported off the sheet is first wanted - the one
        /// date picked for the whole sheet, or worked out from the last clean
        /// ticked on it
        /// </summary>
        static DateTime DueDateFor(ImportOptions options, ImportedCustomerRow row,
            int freqAmount, FrequenceType freqType, ImportResult result)
        {
            if (options.DueDate.HasValue)
            {
                result.DueDatesSet++;
                return options.DueDate.Value;
            }

            //a file that says when the house is next wanted has the answer
            //already. a round sheet has not, and has it worked out from the
            //last clean ticked on it and how often it comes round
            if (row.NextDue.HasValue)
                return row.NextDue.Value.Date;

            return NextDueDate(row.LastCleaned, freqAmount, freqType);
        }

        /// <summary>
        /// how often the house comes round. a reader that has already
        /// worked it out is believed - it had the file's own wording in
        /// front of it - and a round sheet, which leaves it at -1, has its
        /// Freq cell read here exactly as it always was.
        /// </summary>
        static (int amount, FrequenceType type) FrequencyFor(ImportedCustomerRow row)
        {
            if (row.OneOff)
                return (0, FrequenceType.Week);
            if (row.FrequencyAmount >= 0)
                return (row.FrequencyAmount, row.FrequencyType);

            (int amount, string unit) = RoundSheetParser.ParseFrequency(row.FrequencyText);
            return (amount, unit == "month" ? FrequenceType.Month
                : unit == "day" ? FrequenceType.Day
                : FrequenceType.Week);
        }

        /// <summary>the file's answer where it has one, the page's otherwise</summary>
        static string Pick(string fromFile, string fromPage)
        {
            string file = (fromFile ?? string.Empty).Trim();
            return file.Length > 0 ? file : (fromPage ?? string.Empty).Trim();
        }

        /// <summary>
        /// fills in what a customer already here has not got, and leaves
        /// alone what they have. a town or a postcode on a record was put
        /// there by somebody who knows the round; an import writing over it
        /// with whatever another app was holding would be a change nobody
        /// asked for and nobody would see.
        /// </summary>
        static void TopUpCustomer(Customer customer, ImportedCustomerRow row, ImportResult result)
        {
            if (customer.Email.Length == 0 && row.Email.Trim().Length > 0)
            {
                customer.Email = row.Email.Trim();
                result.EmailsFound++;
            }

            if (customer.Address == null)
                return;

            if ((customer.Address.Postcode ?? string.Empty).Length == 0 && row.Postcode.Trim().Length > 0)
                customer.Address.Postcode = row.Postcode.Trim();
            if ((customer.Address.City ?? string.Empty).Length == 0 && row.City.Trim().Length > 0)
                customer.Address.City = row.City.Trim();
            if ((customer.Address.Area ?? string.Empty).Length == 0 && row.Area.Trim().Length > 0)
                customer.Address.Area = row.Area.Trim();
        }

        /// <summary>
        /// starts a customer off owing nothing.
        ///
        /// The record is the point: a balance changed outside the ledgers
        /// with nothing written down is exactly what left the last argument
        /// about money with a history that did not add up. A customer who
        /// already owes nothing has nothing to write down.
        /// </summary>
        static void ClearBalance(Customer customer, ImportOptions options, ImportResult result)
        {
            if (!options.ZeroBalances || customer == null || customer.Balance == 0)
                return;

            float was = customer.Balance;
            customer.Balance = 0;
            customer.DateBalanceLastUpdate = UsfulFuctions.DateNow;
            BalanceAdjustment.AddWriteOff(customer.Id, was, was, ImportOptions.ClearedReason);

            //what a customer owes shows against every job they have, and
            //those rows are only redrawn when the job says something changed
            foreach (Job j in Job.Query(QueryType.CustomerId, customer.Id))
            {
                j.Refresh();
                j.RefreshColors();
            }

            result.BalancesCleared++;
        }

        /// <summary>
        /// a job imported before these details were understood can still be
        /// holding them as note text, so the same tidy up is applied to what
        /// is already there
        /// </summary>
        static void CleanExistingNotes(Job job, Customer customer, ImportResult result)
        {
            if (string.IsNullOrWhiteSpace(job.Notes))
                return;

            RoundSheetParser.ParsedNotes parsed = RoundSheetParser.ParseNotes(job.Notes);
            if (!parsed.Tnb && parsed.Phone.Length == 0 && !parsed.FrontPrice.HasValue)
                return;

            if (parsed.Tnb)
            {
                job.TNB = true;
                result.TnbFromNotes++;
            }
            if (parsed.Phone.Length > 0)
            {
                if (customer.Phone.Length == 0)
                    customer.Phone = parsed.Phone;
                result.PhonesFound++;
            }
            if (parsed.FrontPrice.HasValue)
            {
                ApplyFrontPrice(job, parsed.FrontPrice);
                result.FrontPrices++;
            }

            job.Notes = parsed.Remaining;
        }

        /// <summary>the name a front only price is stored under</summary>
        public const string FrontOnlyDescription = "Front Only";

        static void ApplyFrontPrice(Job job, float? frontPrice)
        {
            if (!frontPrice.HasValue || frontPrice.Value <= 0)
                return;

            if (job.AlternativePrices == null)
                job.AlternativePrices = new List<AlternativePrice>();

            AlternativePrice existing = job.AlternativePrices.FirstOrDefault(x =>
                string.Equals(x.Description, FrontOnlyDescription, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                existing.Price = frontPrice.Value;
            else
                job.AlternativePrices.Add(new AlternativePrice
                {
                    Description = FrontOnlyDescription,
                    Price = frontPrice.Value,
                });
        }

        static string BuildNotes(ImportedCustomerRow row, string remainingNotes, ImportResult result)
        {
            //the front price and anything else understood has already been
            //taken out; only a price that could not be read is worth saying
            string notes = remainingNotes ?? string.Empty;
            if (!row.Price.HasValue && row.PriceText.Length > 0)
            {
                notes = Append(notes, $"[Import] Price on sheet: '{row.PriceText}' - set price manually");
                result.MissingPrice++;
            }
            else if (!row.Price.HasValue)
            {
                result.MissingPrice++;
            }
            return notes;
        }

        static string Append(string notes, string extra)
            => notes.Length == 0 ? extra : notes + "\n" + extra;

        static PaymentMethod PaymentMethodFrom(string pt)
        {
            string t = (pt ?? string.Empty).Trim().ToUpperInvariant();
            if (t == "B") return PaymentMethod.Bank;
            if (t == "C") return PaymentMethod.Cash;
            if (t == "PP") return PaymentMethod.Paypal;
            return PaymentMethod.Other; // mixed values like "C or B"
        }

        static DateTime NextDueDate(DateTime? lastCleaned, int freqAmount, FrequenceType type)
        {
            if (lastCleaned == null)
                return DateTime.Now.Date;
            DateTime last = lastCleaned.Value;
            return type switch
            {
                FrequenceType.Day => last.AddDays(freqAmount),
                FrequenceType.Month => last.AddMonths(freqAmount),
                FrequenceType.Year => last.AddYears(freqAmount),
                _ => last.AddDays(freqAmount * 7),
            };
        }
    }
}
