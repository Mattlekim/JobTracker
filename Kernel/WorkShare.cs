using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace Kernel
{
    //  Sending a work list to someone.
    //
    //  A .rwk is a handful of jobs handed to another cleaner - a mate
    //  covering a week off, somebody being tried out on a patch. The sender
    //  picks the jobs and chooses what goes with them (prices, notes, phone
    //  numbers, whether money can be taken), sets a PIN and a name tag for
    //  whoever is doing the work, and the file goes out by whatever the
    //  phone can share with. The receiver opens it in their own copy of the
    //  app, works it under Extra Work, and returns it the same way. When the
    //  return lands back on the sender's phone the work done is written on
    //  to their own jobs, tagged with the worker's name tag.
    //
    //  The file is a small plain header with everything else encrypted:
    //
    //      "RWKSHARE"  8 bytes, what the file is
    //      version     1 byte
    //      kind        1 byte - sent out, or coming back
    //      key         length-prefixed string, a guid in the clear
    //      salt        16 bytes      \
    //      iv          16 bytes       |  the encrypted payload
    //      hmac        32 bytes       |
    //      length + payload          /
    //
    //  The key is deliberately not encrypted. It is how a return finds its
    //  own record on the sender's phone: the sender kept the PIN it was
    //  encrypted with, filed under that key, so a return opens itself
    //  without the PIN being typed again - the whole point of keeping it.
    //  The key says nothing about anybody; the addresses and numbers are
    //  all in the payload.
    //
    //  The payload is the job list as xml, gzipped and then AES encrypted
    //  with keys derived from the PIN (PBKDF2). The HMAC is over the
    //  ciphertext, so a wrong PIN - or a file that has been fiddled with -
    //  is told apart from a right one before anything is trusted.

    /// <summary>whether a .rwk is going out or coming back</summary>
    public enum WorkShareKind : byte
    {
        SentWork = 1,
        ReturnedWork = 2,
    }

    /// <summary>the plain part of a .rwk, readable without the PIN</summary>
    public class WorkShareHeader
    {
        public byte Version;
        public WorkShareKind Kind;
        public string Key = string.Empty;
    }

    /// <summary>
    /// one job as it travels. it carries the sender's job id so the return
    /// can be matched back to the job it came from - the receiver's own ids
    /// mean nothing to anybody else
    /// </summary>
    public class SharedJob
    {
        public int JobId;

        public string House = string.Empty;
        public string Street = string.Empty;
        public string Town = string.Empty;
        public string Area = string.Empty;
        public string Postcode = string.Empty;

        public string JobType = string.Empty;
        public string CustomerName = string.Empty;
        public DateTime DueDate;
        public string Frequency = string.Empty;

        //only filled in when the sender said so
        public float Price;
        public bool HasPrice;
        public string Notes = string.Empty;
        public string Phone = string.Empty;

        //what the worker did with it
        public bool Done;
        public DateTime DoneOn;
        public bool Skipped;
        public bool Paid;
        public float PaidAmount;
        public List<string> Tags = new List<string>();

        [XmlIgnore]
        public string FormattedAddress
        {
            get { return $"{House} {Street} {Town}".Trim(); }
        }

        [XmlIgnore]
        public string FormattedStatus
        {
            get
            {
                List<string> parts = new List<string>();
                if (Done)
                    parts.Add(DoneOn > new DateTime(2000, 1, 1) ? $"Done {DoneOn.ToShortDateString()}" : "Done");
                if (Skipped)
                    parts.Add("Skipped");
                if (Paid)
                    parts.Add($"Paid {Gloable.CurrenceSymbol}{PaidAmount}");
                foreach (string tag in Tags)
                    parts.Add(tag);
                return string.Join(" • ", parts);
            }
        }
    }

    /// <summary>everything inside the encrypted part of a .rwk</summary>
    public class SharedWorkData
    {
        public string Key = string.Empty;
        public string WorkerTag = string.Empty;
        public DateTime SentOn;
        public bool IncludePrices;
        public bool IncludeNotes;
        public bool IncludePhones;
        public bool AllowCollect;
        public List<SharedJob> Jobs = new List<SharedJob>();
    }

    /// <summary>
    /// The sender's memory of a share that went out: the key the file
    /// carries in the clear, and the PIN and worker name tag filed under it.
    /// The PIN is what lets the return open itself; the name tag is what the
    /// updated jobs are tagged with.
    /// </summary>
    public class SentWorkRecord
    {
        public string Key = string.Empty;
        public string Pin = string.Empty;
        public string WorkerTag = string.Empty;
        public DateTime SentOn;
        /// <summary>DateBase until the work has come back</summary>
        public DateTime ReturnedOn = new DateTime(2000, 1, 1);
        public int JobCount;

        [XmlIgnore]
        public bool HasReturned
        {
            get { return ReturnedOn > new DateTime(2000, 1, 1); }
        }
    }

    public class SentWorkSaveData
    {
        public List<SentWorkRecord> Records = new List<SentWorkRecord>();
    }

    public static class WorkShare
    {
        /// <summary>what a shared work list is called</summary>
        public const string Extension = ".rwk";

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RWKSHARE");
        private const byte FormatVersion = 1;
        private const int Pbkdf2Iterations = 100_000;

        /// <summary>is this a shared work list, by its name</summary>
        public static bool LooksLikeShare(string? fileName)
        {
            return !string.IsNullOrWhiteSpace(fileName)
                && fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// the tag put on the sender's own copy of each job that went out, so
        /// the round says which work is with somebody. one definition, because
        /// the return has to take off exactly what the send put on
        /// </summary>
        public static string SentTag(string workerTag)
        {
            return $"Sent To {(workerTag ?? string.Empty).Trim()}".Trim();
        }

        //  --------------------------------------------------  the file itself

        /// <summary>
        /// writes a .rwk. the same call writes a fresh share and a return -
        /// the kind byte and the state carried on the jobs are the difference
        /// </summary>
        public static void WriteFile(string path, SharedWorkData data, string pin, WorkShareKind kind)
        {
            byte[] payload = Serialise(data);

            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] iv = RandomNumberGenerator.GetBytes(16);
            DeriveKeys(pin, salt, out byte[] aesKey, out byte[] hmacKey);

            byte[] cipher;
            using (Aes aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                using ICryptoTransform enc = aes.CreateEncryptor();
                cipher = enc.TransformFinalBlock(payload, 0, payload.Length);
            }

            byte[] mac;
            using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
                mac = hmac.ComputeHash(cipher);

            using FileStream fs = File.Create(path);
            using BinaryWriter w = new BinaryWriter(fs);
            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write((byte)kind);
            w.Write(data.Key ?? string.Empty);
            w.Write(salt);
            w.Write(iv);
            w.Write(mac);
            w.Write(cipher.Length);
            w.Write(cipher);
        }

        /// <summary>
        /// just the plain header, for deciding what to do with a file before
        /// anybody is asked for a PIN. null when it is not one of ours
        /// </summary>
        public static WorkShareHeader? ReadHeader(string path)
        {
            try
            {
                using FileStream fs = File.OpenRead(path);
                using BinaryReader r = new BinaryReader(fs);

                byte[] magic = r.ReadBytes(Magic.Length);
                if (!magic.SequenceEqual(Magic))
                    return null;

                WorkShareHeader header = new WorkShareHeader();
                header.Version = r.ReadByte();
                header.Kind = (WorkShareKind)r.ReadByte();
                header.Key = r.ReadString();
                return header;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// opens the encrypted part with the PIN. null means the wrong PIN or
        /// a file that has been damaged - the HMAC tells the two apart from a
        /// crash, not from each other, and both mean "do not trust it"
        /// </summary>
        public static SharedWorkData? ReadFile(string path, string pin)
        {
            try
            {
                using FileStream fs = File.OpenRead(path);
                using BinaryReader r = new BinaryReader(fs);

                byte[] magic = r.ReadBytes(Magic.Length);
                if (!magic.SequenceEqual(Magic))
                    return null;

                r.ReadByte(); //version
                r.ReadByte(); //kind
                r.ReadString(); //key, already in the header

                byte[] salt = r.ReadBytes(16);
                byte[] iv = r.ReadBytes(16);
                byte[] mac = r.ReadBytes(32);
                int length = r.ReadInt32();
                if (length <= 0 || length > 64 * 1024 * 1024)
                    return null;
                byte[] cipher = r.ReadBytes(length);

                DeriveKeys(pin, salt, out byte[] aesKey, out byte[] hmacKey);

                using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
                {
                    byte[] check = hmac.ComputeHash(cipher);
                    if (!CryptographicOperations.FixedTimeEquals(check, mac))
                        return null;
                }

                byte[] payload;
                using (Aes aes = Aes.Create())
                {
                    aes.Key = aesKey;
                    aes.IV = iv;
                    using ICryptoTransform dec = aes.CreateDecryptor();
                    payload = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                }

                return Deserialise(payload);
            }
            catch
            {
                return null;
            }
        }

        private static void DeriveKeys(string pin, byte[] salt, out byte[] aesKey, out byte[] hmacKey)
        {
            byte[] both = Rfc2898DeriveBytes.Pbkdf2(pin ?? string.Empty, salt, Pbkdf2Iterations,
                HashAlgorithmName.SHA256, 64);
            aesKey = both.Take(32).ToArray();
            hmacKey = both.Skip(32).ToArray();
        }

        private static byte[] Serialise(SharedWorkData data)
        {
            using MemoryStream ms = new MemoryStream();
            using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress, true))
            {
                XmlSerializer xs = new XmlSerializer(typeof(SharedWorkData));
                xs.Serialize(gz, data);
            }
            return ms.ToArray();
        }

        private static SharedWorkData? Deserialise(byte[] payload)
        {
            using MemoryStream ms = new MemoryStream(payload);
            using GZipStream gz = new GZipStream(ms, CompressionMode.Decompress);
            XmlSerializer xs = new XmlSerializer(typeof(SharedWorkData));
            return xs.Deserialize(gz) as SharedWorkData;
        }

        //  ------------------------------------------  what the sender keeps
        //
        //  The keys, PINs and name tags live in sentwork.rjt. It is written
        //  through the app-level scrambling below rather than as plain text -
        //  a PIN somebody chose should not be readable by anything that can
        //  browse the phone's files, even though anything that can run the
        //  app's code can undo it. It is obfuscation, not safety, and it is
        //  labelled as such here so nobody mistakes it for more.

        private const string RecordsFile = "sentwork.rjt";

        //fixed app-level key: this only has to beat "opened it in a text
        //editor", and the real secret - the PIN - never leaves the sender's
        //phone in the clear anyway
        private static readonly byte[] AppKey = SHA256.HashData(Encoding.UTF8.GetBytes("WorkTracker.SentWork.v1"));

        private static List<SentWorkRecord> _records = new List<SentWorkRecord>();
        private static bool _recordsLoaded;

        /// <summary>how long a record is kept once the work has come back</summary>
        private static readonly TimeSpan KeepAfterReturn = TimeSpan.FromDays(91);

        private static string RecordsPath(string? dir = null)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(dir))
                folder = Path.Combine(folder, dir);
            return Path.Combine(folder, RecordsFile);
        }

        public static void LoadRecords()
        {
            if (_recordsLoaded)
                return;
            _recordsLoaded = true;
            _records.Clear();

            try
            {
                string path = RecordsPath();
                if (!File.Exists(path))
                    return;

                byte[] scrambled = File.ReadAllBytes(path);
                byte[] plain = Unscramble(scrambled);

                using MemoryStream ms = new MemoryStream(plain);
                XmlSerializer xs = new XmlSerializer(typeof(SentWorkSaveData));
                SentWorkSaveData? data = xs.Deserialize(ms) as SentWorkSaveData;
                if (data?.Records != null)
                    _records.AddRange(data.Records);

                //a record is only needed until the work has come back and
                //been dealt with, plus three months in case the return has
                //to be opened again. one that has never come back is kept -
                //the work is still out there
                int dropped = _records.RemoveAll(x => x.HasReturned
                    && x.ReturnedOn + KeepAfterReturn < UsfulFuctions.DateNow);
                if (dropped > 0)
                    SaveRecords();
            }
            catch
            {
                //a store that will not read is a lost set of PINs, not a
                //reason to take the app down. returns will ask what to do
            }
        }

        public static void SaveRecords()
        {
            try
            {
                using MemoryStream ms = new MemoryStream();
                XmlSerializer xs = new XmlSerializer(typeof(SentWorkSaveData));
                SentWorkSaveData data = new SentWorkSaveData();
                data.Records.AddRange(_records);
                xs.Serialize(ms, data);

                File.WriteAllBytes(RecordsPath(), Scramble(ms.ToArray()));
            }
            catch
            {
            }
        }

        /// <summary>AES with the app key and a fresh iv on the front of the file</summary>
        private static byte[] Scramble(byte[] plain)
        {
            using Aes aes = Aes.Create();
            aes.Key = AppKey;
            aes.GenerateIV();
            using ICryptoTransform enc = aes.CreateEncryptor();
            byte[] cipher = enc.TransformFinalBlock(plain, 0, plain.Length);

            byte[] output = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, output, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, output, aes.IV.Length, cipher.Length);
            return output;
        }

        private static byte[] Unscramble(byte[] scrambled)
        {
            using Aes aes = Aes.Create();
            aes.Key = AppKey;
            byte[] iv = new byte[16];
            Buffer.BlockCopy(scrambled, 0, iv, 0, 16);
            aes.IV = iv;
            using ICryptoTransform dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(scrambled, 16, scrambled.Length - 16);
        }

        public static SentWorkRecord RememberSentWork(string key, string pin, string workerTag, int jobCount)
        {
            LoadRecords();

            SentWorkRecord record = new SentWorkRecord()
            {
                Key = key,
                Pin = pin,
                WorkerTag = workerTag,
                SentOn = UsfulFuctions.DateNow,
                JobCount = jobCount,
            };
            _records.Add(record);
            SaveRecords();
            return record;
        }

        /// <summary>the record a return's key points at, or null for a stranger's file</summary>
        public static SentWorkRecord? FindRecord(string? key)
        {
            LoadRecords();
            if (string.IsNullOrWhiteSpace(key))
                return null;
            return _records.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static void MarkReturned(SentWorkRecord record)
        {
            record.ReturnedOn = UsfulFuctions.DateNow;
            SaveRecords();
        }

        //  -----------------------------------------  building what goes out

        /// <summary>
        /// the jobs as they will travel. what goes with them is what the
        /// sender ticked - a price, a note or a number that was not ticked is
        /// simply never in the file, rather than in it and hidden
        /// </summary>
        public static SharedWorkData BuildShare(List<Job> jobs, bool prices, bool notes, bool phones,
            bool allowCollect, string workerTag)
        {
            SharedWorkData data = new SharedWorkData()
            {
                Key = Guid.NewGuid().ToString("N"),
                WorkerTag = workerTag ?? string.Empty,
                SentOn = UsfulFuctions.DateNow,
                IncludePrices = prices,
                IncludeNotes = notes,
                IncludePhones = phones,
                AllowCollect = allowCollect,
            };

            foreach (Job j in jobs)
            {
                SharedJob shared = new SharedJob()
                {
                    JobId = j.Id,
                    JobType = j.Name ?? string.Empty,
                    DueDate = j.DueDate,
                    Frequency = DescribeFrequency(j),
                };

                if (j.Address != null)
                {
                    shared.House = j.Address.PropertyNameNumber ?? string.Empty;
                    shared.Street = j.Address.Street ?? string.Empty;
                    shared.Town = j.Address.City ?? string.Empty;
                    shared.Area = j.Address.Area ?? string.Empty;
                    shared.Postcode = j.Address.Postcode ?? string.Empty;
                }

                Customer? c = j.GetCustomer();
                if (c != null)
                    shared.CustomerName = $"{c.FName} {c.SName}".Trim();

                if (prices)
                {
                    shared.Price = j.EffectivePrice;
                    shared.HasPrice = true;
                }

                if (notes)
                    shared.Notes = j.Notes ?? string.Empty;

                if (phones && c != null)
                    shared.Phone = c.Phone ?? string.Empty;

                data.Jobs.Add(shared);
            }

            return data;
        }

        private static string DescribeFrequency(Job j)
        {
            if (j.Frequence <= 0)
                return "One off";

            string unit = j.Frequence_Type switch
            {
                FrequenceType.Day => j.Frequence == 1 ? "day" : "days",
                FrequenceType.Month => j.Frequence == 1 ? "month" : "months",
                FrequenceType.Year => j.Frequence == 1 ? "year" : "years",
                _ => j.Frequence == 1 ? "week" : "weeks",
            };

            return j.Frequence == 1 ? $"Every {unit}" : $"Every {j.Frequence} {unit}";
        }

        //  --------------------------------------  the receiver's extra work
        //
        //  The file that arrived is kept as it came - encrypted, in the data
        //  folder - and opened into memory with the PIN each time Extra Work
        //  is entered. Leaving Extra Work forgets the PIN and the jobs, so
        //  getting back in means typing it again, which is what was asked
        //  for: the list is somebody else's round, and a phone left on a van
        //  seat should not have it one tap away.

        private const string ExtraWorkFile = "extrawork" + Extension;

        /// <summary>the decrypted list while Extra Work is unlocked, else null</summary>
        public static SharedWorkData? OpenedWork { get; private set; }

        /// <summary>the PIN the open list was unlocked with, held to save changes back</summary>
        private static string? _openPin;

        public static string ExtraWorkPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ExtraWorkFile);
        }

        public static bool HaveExtraWork()
        {
            return File.Exists(ExtraWorkPath());
        }

        /// <summary>a .rwk somebody sent becomes this phone's extra work</summary>
        public static void TakeOnExtraWork(string sourcePath)
        {
            File.Copy(sourcePath, ExtraWorkPath(), true);
            Lock();
        }

        /// <summary>
        /// opens the extra work with the PIN. false is the wrong PIN. the PIN
        /// is held while the list is open so changes can be written straight
        /// back into the same file, still encrypted
        /// </summary>
        public static bool Unlock(string pin)
        {
            SharedWorkData? data = ReadFile(ExtraWorkPath(), pin);
            if (data == null)
                return false;

            OpenedWork = data;
            _openPin = pin;
            return true;
        }

        /// <summary>leaving extra work forgets the list and the PIN</summary>
        public static void Lock()
        {
            OpenedWork = null;
            _openPin = null;
        }

        /// <summary>
        /// writes the open list back over the extra work file, marked up as
        /// it now stands. every change goes through here, so the work marked
        /// off is never sitting only in memory
        /// </summary>
        public static void SaveOpenedWork()
        {
            if (OpenedWork == null || _openPin == null)
                return;

            WriteFile(ExtraWorkPath(), OpenedWork, _openPin, WorkShareKind.SentWork);
        }

        /// <summary>
        /// the return: the same list, same key, same PIN, marked as coming
        /// back. written to the path given so it can be shared from the cache
        /// </summary>
        public static bool WriteReturn(string path)
        {
            if (OpenedWork == null || _openPin == null)
                return false;

            WriteFile(path, OpenedWork, _openPin, WorkShareKind.ReturnedWork);
            return true;
        }

        /// <summary>the work is done with and comes off the phone</summary>
        public static void RemoveExtraWork()
        {
            Lock();
            try
            {
                if (File.Exists(ExtraWorkPath()))
                    File.Delete(ExtraWorkPath());
            }
            catch
            {
            }
        }
    }
}
