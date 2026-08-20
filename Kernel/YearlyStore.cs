using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Kernel
{
    /// <summary>
    /// The records the taxman cares about - expenses, income, statements -
    /// are kept one file per tax year rather than one file for everything.
    ///
    /// Two reasons. A finished tax year never changes again, so its file
    /// never changes again: the cloud leaves it alone instead of re-uploading
    /// years of history every time this week's fuel receipt is added, and a
    /// backup can be taken of one year on its own. And when the taxman does
    /// ask about 2024/25, there is a single file holding it.
    ///
    /// Everything is still loaded into one list in memory, so nothing else in
    /// the app has to know the files are split up.
    /// </summary>
    public static class YearlyStore
    {
        /// <summary>where the data files live, optionally under a sub folder (backups use one)</summary>
        public static string Folder(string dir = null)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(dir))
                return root;

            string folder = Path.Combine(root, dir);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>the file one tax year of <paramref name="prefix"/> records lives in</summary>
        public static string PathFor(string prefix, int taxYear, string dir = null)
        {
            return Path.Combine(Folder(dir), $"{prefix}-{taxYear}.rjt");
        }

        /// <summary>the tax years there is a file for</summary>
        public static List<int> YearsOnDisk(string prefix, string dir = null)
        {
            List<int> years = new List<int>();
            try
            {
                Regex pattern = new Regex($@"^{Regex.Escape(prefix)}-(\d{{4}})\.rjt$", RegexOptions.IgnoreCase);
                foreach (string path in Directory.GetFiles(Folder(dir), $"{prefix}-*.rjt"))
                {
                    Match m = pattern.Match(Path.GetFileName(path));
                    if (m.Success)
                        years.Add(int.Parse(m.Groups[1].Value));
                }
            }
            catch
            {
            }
            years.Sort();
            return years;
        }

        /// <summary>does this file name hold one tax year of <paramref name="prefix"/> records</summary>
        public static bool IsYearFile(string fileName, string prefix)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
            return Regex.IsMatch(fileName, $@"^{Regex.Escape(prefix)}-\d{{4}}\.rjt$", RegexOptions.IgnoreCase);
        }

        /// <summary>the tax year a data file is for, or -1 if it is not one</summary>
        public static int YearOfFile(string fileName)
        {
            Match m = Regex.Match(fileName ?? string.Empty, @"-(\d{4})\.rjt$", RegexOptions.IgnoreCase);
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        public static byte[] Serialise<T>(T data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                XmlSerializer xs = new XmlSerializer(typeof(T));
                xs.Serialize(ms, data);
                return ms.ToArray();
            }
        }

        public static T Deserialise<T>(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                XmlSerializer xs = new XmlSerializer(typeof(T));
                return (T)xs.Deserialize(fs);
            }
        }

        /// <summary>
        /// the same, out of a stream rather than a file - a data file being
        /// read straight out of a backup without unpacking the zip first
        /// </summary>
        public static T Deserialise<T>(Stream stream)
        {
            XmlSerializer xs = new XmlSerializer(typeof(T));
            return (T)xs.Deserialize(stream);
        }

        /// <summary>
        /// writes the file only when what is in it would actually change.
        /// this is the whole point of splitting the years up: a year that has
        /// not been touched keeps its timestamp, so the cloud sees nothing to
        /// send and leaves it where it is
        /// </summary>
        /// <returns>true when the file was written</returns>
        public static bool WriteIfChanged(string path, byte[] data)
        {
            try
            {
                if (File.Exists(path))
                {
                    byte[] existing = File.ReadAllBytes(path);
                    if (existing.Length == data.Length && existing.SequenceEqual(data))
                        return false;
                }
            }
            catch
            {
                //unreadable - write over it
            }

            File.WriteAllBytes(path, data);
            return true;
        }

        /// <summary>
        /// removes the file for a year that has had its last record taken out
        /// of it, so an empty year does not sit around looking like data
        /// </summary>
        public static void DeleteYear(string prefix, int taxYear, string dir = null)
        {
            try
            {
                string path = PathFor(prefix, taxYear, dir);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        /// <summary>
        /// the old single file, from before the records were split by year.
        /// it is loaded once and then taken away, so its contents cannot come
        /// back and undo anything deleted since
        /// </summary>
        public static string LegacyPath(string fileName, string dir = null)
        {
            return Path.Combine(Folder(dir), fileName);
        }

        public static void RetireLegacyFile(string fileName, string dir = null)
        {
            try
            {
                string path = LegacyPath(fileName, dir);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
