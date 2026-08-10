using Kernel;

namespace UiInterface.ImportExport
{
    public class ImportResult
    {
        public int Created;
        public int Updated;
        public int MissingPrice;
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

            string notes = BuildNotes(row, result);
            string phone = RoundSheetParser.ExtractPhone(row.Notes);
            (int freqAmount, string freqUnit) = RoundSheetParser.ParseFrequency(row.FrequencyText);
            FrequenceType freqType = freqUnit == "month" ? FrequenceType.Month
                : freqUnit == "day" ? FrequenceType.Day
                : FrequenceType.Week;

            if (customer == null)
            {
                var address = new Location
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
                    TNB = row.Tnb,
                };
                job.SetFrequence(freqAmount, freqType);
                job.DueDate = NextDueDate(row.LastCleaned, freqAmount, freqType);
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
                        TNB = row.Tnb,
                    };
                    job.SetFrequence(freqAmount, freqType);
                    job.DueDate = NextDueDate(row.LastCleaned, freqAmount, freqType);
                    Job.Add(job);
                }
                else
                {
                    if (row.Price.HasValue)
                        job.Price = row.Price.Value;
                    job.TNB = row.Tnb;
                    job.SetFrequence(freqAmount, freqType);
                }
                result.Updated++;
            }
        }

        static string BuildNotes(ImportedCustomerRow row, ImportResult result)
        {
            string notes = row.Notes ?? string.Empty;
            if (!row.Price.HasValue && row.PriceText.Length > 0)
            {
                notes = Append(notes, $"[Import] Price on sheet: '{row.PriceText}' - set price manually");
                result.MissingPrice++;
            }
            else if (!row.Price.HasValue)
            {
                result.MissingPrice++;
            }
            if (row.FrontPriceText.Length > 0)
                notes = Append(notes, $"[Import] Front price: {row.FrontPriceText}");
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
