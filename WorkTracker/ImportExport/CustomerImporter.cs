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
        /// <summary>the sheet has streets but no town</summary>
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
            List<ImportedCustomerRow> rows = RoundSheetParser.Parse(xlsxStream);
            var result = new ImportResult();

            //the round is remembered once for the whole sheet rather than
            //per row, so the list of rounds cannot end up with the same name
            //on it twice, and so the caller can tell whether the settings
            //need saving
            if (!string.IsNullOrWhiteSpace(options.Round))
                Job.RememberRound(options.Round);

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

            //the sheet's own Front column wins over one written in a note
            float? frontPrice = RoundSheetParser.ParsePrice(row.FrontPriceText) ?? parsed.FrontPrice;
            if (frontPrice.HasValue)
                result.FrontPrices++;

            string notes = BuildNotes(row, parsed.Remaining, result);
            (int freqAmount, string freqUnit) = RoundSheetParser.ParseFrequency(row.FrequencyText);
            FrequenceType freqType = freqUnit == "month" ? FrequenceType.Month
                : freqUnit == "day" ? FrequenceType.Day
                : FrequenceType.Week;

            if (customer == null)
            {
                var address = new Kernel.Location
                {
                    PropertyNameNumber = row.HouseNumber,
                    Street = row.Street,
                    City = options.City ?? string.Empty,
                    Area = options.Area ?? string.Empty,
                    Postcode = string.Empty,
                };

                customer = new Customer { Address = address };
                if (row.Name.Length > 0)
                    customer.FName = row.Name;
                if (phone.Length > 0)
                    customer.Phone = phone;
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
                PutOnRound(job, options, result);
                result.Created++;
            }
            else
            {
                if (customer.Phone.Length == 0 && phone.Length > 0)
                    customer.Phone = phone;

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
                    PutOnRound(job, options, result);
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
                    PutOnRound(job, options, result);
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
        static void PutOnRound(Job job, ImportOptions options, ImportResult result)
        {
            string round = (options.Round ?? string.Empty).Trim();
            if (round.Length == 0 || job == null)
                return;

            //SetRound puts it on every visit of the job, which is what a
            //round is about - where the house is does not change between one
            //clean and the next
            job.SetRound(round);
            result.RoundSet++;
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
            return NextDueDate(row.LastCleaned, freqAmount, freqType);
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
