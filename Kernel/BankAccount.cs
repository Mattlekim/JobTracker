using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernel
{
    /// <summary>
    /// A bank account statements are imported from.
    ///
    /// Each account keeps its own remembered column layouts - csv and pdf
    /// apart, exactly as when there was only one of each - so statements from
    /// two different banks can both be imported without one bank's layout
    /// overwriting the other's.
    ///
    /// The id is what everything else tracks against: a kept statement
    /// carries it, and the reference an expense is given when it comes off a
    /// statement carries it too, so the same amount to the same payee on the
    /// same day out of two accounts is two transactions rather than a
    /// re-import. An account is never deleted and its id is never reused,
    /// because a dropped id would orphan everything recorded against it -
    /// rename an account that has changed rather than replacing it.
    /// </summary>
    public partial class BankAccount
    {
        private static List<BankAccount> _Accounts = new List<BankAccount>();

        private static int _IdGenerator = 0;

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        //the remembered csv layout. -1 is a column that has not been chosen
        public int Date { get; set; } = -1;
        public int Ref { get; set; } = -1;
        public int Amount { get; set; } = -1;
        public int Debit { get; set; } = -1;
        public bool DebitAndCreditTogether { get; set; }

        //the same bank's pdf statement has different columns to its csv, so
        //the two are remembered apart - as they always were
        public int PdfDate { get; set; } = -1;
        public int PdfRef { get; set; } = -1;
        public int PdfAmount { get; set; } = -1;
        public int PdfDebit { get; set; } = -1;
        public bool PdfDebitAndCreditTogether { get; set; }

        /// <summary>
        /// true on the account made out of the days when the app had one
        /// layout for everything. expense references written back then carry
        /// no account id in them, and they belong to this account - see
        /// Expense.FindFromStatement
        /// </summary>
        public bool InheritsLegacyReferences { get; set; }

        public static BankAccount Add(string name)
        {
            BankAccount account = new BankAccount()
            {
                Id = _IdGenerator++,
                Name = name ?? string.Empty,
            };
            _Accounts.Add(account);
            return account;
        }

        public static BankAccount Get(int id)
        {
            return _Accounts.FirstOrDefault(x => x.Id == id);
        }

        public static List<BankAccount> Query()
        {
            List<BankAccount> tmp = new List<BankAccount>();
            tmp.AddRange(_Accounts);
            return tmp;
        }

        public static int Count
        {
            get { return _Accounts.Count; }
        }

        public static void DeleteData()
        {
            _Accounts.Clear();
            _IdGenerator = 0;
        }

        /// <summary>
        /// the account an import belongs to while there is nothing to choose
        /// between - the one account there is, made on the spot when the app
        /// has none at all. a round with one bank account is never asked
        /// which account a statement is from
        /// </summary>
        public static BankAccount FirstOrMake()
        {
            if (_Accounts.Count == 0)
            {
                Add("My Bank");
                Save();
            }

            return _Accounts[0];
        }

        /// <summary>
        /// true when the name is already an account's, so two accounts are
        /// never called the same thing - the import question offers accounts
        /// by name
        /// </summary>
        public static bool NameTaken(string name, int exceptId = -1)
        {
            return _Accounts.Exists(x => x.Id != exceptId
                && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        //the one-layout-for-everything columns out of an old settings file,
        //held here between Settings.Load reading them and Load turning them
        //into the first account. see EnsureLegacyAccount
        public static int LegacyDate = -1, LegacyRef = -1, LegacyAmount = -1, LegacyDebit = -1;
        public static bool LegacyDebitAndCreditTogether;

        public static int LegacyPdfDate = -1, LegacyPdfRef = -1, LegacyPdfAmount = -1, LegacyPdfDebit = -1;
        public static bool LegacyPdfDebitAndCreditTogether;

        /// <summary>
        /// turns the layout the app used to keep in the settings file into
        /// the first account, so nobody re-teaches columns they already
        /// taught. runs at the end of Load and does nothing once any account
        /// exists, so it can only ever run once per data set
        /// </summary>
        public static void EnsureLegacyAccount(string dir = null)
        {
            if (_Accounts.Count > 0)
                return;

            //0,0,0 is what a settings file written before statement imports
            //existed reads back as, not a chosen layout - no bank prints the
            //date, the reference and the amount in the same column
            bool haveCsv = LegacyDate != -1 && LegacyRef != -1 && LegacyAmount != -1
                && !(LegacyDate == 0 && LegacyRef == 0 && LegacyAmount == 0);

            bool havePdf = LegacyPdfDate != -1 && LegacyPdfRef != -1 && LegacyPdfAmount != -1
                && !(LegacyPdfDate == 0 && LegacyPdfRef == 0 && LegacyPdfAmount == 0);

            if (!haveCsv && !havePdf)
                return;

            BankAccount account = Add("My Bank");
            account.InheritsLegacyReferences = true;

            if (haveCsv)
            {
                account.Date = LegacyDate;
                account.Ref = LegacyRef;
                account.Amount = LegacyAmount;
                account.Debit = LegacyDebit;
                account.DebitAndCreditTogether = LegacyDebitAndCreditTogether;
            }

            if (havePdf)
            {
                account.PdfDate = LegacyPdfDate;
                account.PdfRef = LegacyPdfRef;
                account.PdfAmount = LegacyPdfAmount;
                account.PdfDebit = LegacyPdfDebit;
                account.PdfDebitAndCreditTogether = LegacyPdfDebitAndCreditTogether;
            }

            Save(dir);
        }
    }
}
