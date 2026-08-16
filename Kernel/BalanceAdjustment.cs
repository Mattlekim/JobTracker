using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Kernel
{
    /// <summary>the two ways a balance gets changed by hand</summary>
    public enum BalanceAdjustmentKind
    {
        /// <summary>debt (or credit) cleared without money moving - Settle Up</summary>
        WriteOff = 0,

        /// <summary>the balance typed in - a round taken over from somebody else</summary>
        SetByHand = 1,
    }

    /// <summary>
    /// A balance changed by hand, kept as a record instead of a silent edit.
    ///
    /// The balance is normally the gap between two ledgers - work done and
    /// money received - and both of those keep their history. The two things
    /// that move it outside the ledgers did not: settling up wrote debt off
    /// and remembered nothing, and typing a balance in overwrote the figure
    /// with no trace of what it had been. The next time that customer argued
    /// about money, the one thing the history could not show was the day the
    /// figure was agreed.
    ///
    /// So each one is a row: who, when, what changed and why. They are shown
    /// in the customer's history with the visits and the payments. Nothing
    /// here touches tax - on the cash basis income is the payments, and a
    /// write-off is precisely money that never arrived.
    /// </summary>
    public class BalanceAdjustment
    {
        private static int _IdGenerator = 0;

        private static List<BalanceAdjustment> _Adjustments = new List<BalanceAdjustment>();

        public int Id;
        public int CustomerId;
        public BalanceAdjustmentKind Kind;

        /// <summary>
        /// for a write-off: what was cleared, positive when debt was dropped.
        /// for a balance set by hand: the new balance
        /// </summary>
        public float Amount;

        /// <summary>what the balance said before the change</summary>
        public float BalanceBefore;

        public DateTime Date;

        /// <summary>why, in the round's own words. can be blank</summary>
        public string Reason = string.Empty;

        /// <summary>the record as the history says it</summary>
        [XmlIgnore]
        public string Describe
        {
            get
            {
                if (Kind == BalanceAdjustmentKind.WriteOff)
                    return Amount >= 0
                        ? $"{Gloable.CurrenceSymbol}{Amount:0.00} written off"
                        : $"{Gloable.CurrenceSymbol}{Math.Abs(Amount):0.00} of credit cleared";

                return $"Balance set by hand to {Gloable.CurrenceSymbol}{Amount:0.00} (was {Gloable.CurrenceSymbol}{BalanceBefore:0.00})";
            }
        }

        public static BalanceAdjustment AddWriteOff(int customerId, float amount, float balanceBefore, string reason)
        {
            return Add(BalanceAdjustmentKind.WriteOff, customerId, amount, balanceBefore, reason);
        }

        public static BalanceAdjustment AddSetByHand(int customerId, float newBalance, float balanceBefore, string reason)
        {
            return Add(BalanceAdjustmentKind.SetByHand, customerId, newBalance, balanceBefore, reason);
        }

        private static BalanceAdjustment Add(BalanceAdjustmentKind kind, int customerId, float amount, float balanceBefore, string reason)
        {
            BalanceAdjustment adjustment = new BalanceAdjustment()
            {
                Id = _IdGenerator++,
                CustomerId = customerId,
                Kind = kind,
                Amount = amount,
                BalanceBefore = balanceBefore,
                Date = UsfulFuctions.DateNow,
                Reason = (reason ?? string.Empty).Trim(),
            };

            _Adjustments.Add(adjustment);
            Save();
            return adjustment;
        }

        /// <summary>every adjustment ever made to this customer's balance</summary>
        public static List<BalanceAdjustment> ForCustomer(int customerId)
        {
            return _Adjustments.FindAll(x => x.CustomerId == customerId);
        }

        public static List<BalanceAdjustment> Query()
        {
            return new List<BalanceAdjustment>(_Adjustments);
        }

        /// <summary>a merged duplicate's records follow the payments to the kept customer</summary>
        public static void MoveCustomer(int fromCustomerId, int toCustomerId)
        {
            bool moved = false;

            foreach (BalanceAdjustment a in _Adjustments)
                if (a.CustomerId == fromCustomerId)
                {
                    a.CustomerId = toCustomerId;
                    moved = true;
                }

            if (moved)
                Save();
        }

        public static void DeleteData()
        {
            _Adjustments.Clear();
        }

        //  --------------------------------------------------------  the file
        //
        //  one global file like the expense rules: an adjustment belongs to
        //  a customer, not to a tax year, and it never feeds a tax figure

        private const string _FilePath = "balanceadjustments.rjt";

        public struct SaveData
        {
            public List<BalanceAdjustment> Adjustments;
            public int NextId;
        }

        private static string PathFor(string dir)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(dir))
                folder = Path.Combine(folder, dir);
            return Path.Combine(folder, _FilePath);
        }

        public static void Save(string dir = null)
        {
            SaveData data = new SaveData();
            data.Adjustments = new List<BalanceAdjustment>(_Adjustments);
            data.NextId = _IdGenerator;

            using (FileStream fs = File.Create(PathFor(dir)))
            {
                XmlSerializer xs = new XmlSerializer(typeof(SaveData));
                xs.Serialize(fs, data);
            }
            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            _Adjustments.Clear();
            _IdGenerator = 0;

            try
            {
                using (FileStream fs = File.OpenRead(PathFor(dir)))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(SaveData));
                    SaveData data = (SaveData)xs.Deserialize(fs);

                    if (data.Adjustments != null)
                        _Adjustments.AddRange(data.Adjustments);
                    _IdGenerator = data.NextId;
                }

                foreach (BalanceAdjustment a in _Adjustments)
                    if (a.Id >= _IdGenerator)
                        _IdGenerator = a.Id + 1;
            }
            catch
            {
                //no file yet is a round that has never adjusted a balance
            }
        }
    }
}
