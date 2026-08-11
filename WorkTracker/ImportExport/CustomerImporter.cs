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
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// Maps rows parsed from a round spreadsheet onto customers and jobs.
    /// Creates customers that do not exist yet (matched on house number +
    /// street) and updates price / frequency / TNB on ones that do.
    /// </summary>
    public static class CustomerImporter
    {
        public static ImportResult Import(Stream xlsxStream, string city, string area)
        {
            List<ImportedCustomerRow> rows = RoundSheetParser.Parse(xlsxStream);
            var result = new ImportResult();

            foreach (ImportedCustomerRow row in rows)
            {
                try
                {
                    ImportRow(row, city, area, result);
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

        static void ImportRow(ImportedCustomerRow row, string city, string area, ImportResult result)
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
                    City = city ?? string.Empty,
                    Area = area ?? string.Empty,
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
                job.DueDate = NextDueDate(row.LastCleaned, freqAmount, freqType);
                ApplyFrontPrice(job, frontPrice);
                Job.Add(job);
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
                    job.DueDate = NextDueDate(row.LastCleaned, freqAmount, freqType);
                    ApplyFrontPrice(job, frontPrice);
                    Job.Add(job);
                }
                else
                {
                    if (row.Price.HasValue)
                        job.Price = row.Price.Value;
                    job.TNB = tnb;
                    job.SetFrequence(freqAmount, freqType);
                    ApplyFrontPrice(job, frontPrice);
                    CleanExistingNotes(job, customer, result);
                }
                result.Updated++;
            }
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
