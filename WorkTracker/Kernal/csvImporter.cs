using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public class CSVFile
    {
        public string[] Header { get; internal set; }
        public string[][] data { get; internal set; }
    }
    public static class CSV
    {

        /// <summary>
        /// One line of a csv into its columns.
        ///
        /// A field wrapped in quotes keeps whatever is inside it, commas
        /// included, and the quotes themselves are not part of the value. Two
        /// quotes together inside a quoted field are one quote.
        ///
        /// This used to cut the line at every comma it found. A statement
        /// that quotes its fields - PayPal quotes all of them, and plenty of
        /// banks quote the ones with a comma in - came out with the quotes
        /// still stuck to the values, and one payer called "Smith, John" put
        /// every column after it out by one for that row alone. A file with
        /// no quotes in it reads exactly as it always did.
        /// </summary>
        private static string[] ReadRow(string row)
        {
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < row.Length; i++)
            {
                char c = row[i];

                if (inQuotes)
                {
                    if (c != '"')
                    {
                        field.Append(c);
                        continue;
                    }

                    //a doubled quote is one quote, anything else ends the field
                    if (i + 1 < row.Length && row[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                        inQuotes = false;

                    continue;
                }

                if (c == '"' && field.Length == 0)
                {
                    inQuotes = true;
                    continue;
                }

                if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                field.Append(c);
            }

            //the last field, even when it is empty: a line ending in a comma
            //has one, and dropping it would shift nothing but hide a column
            fields.Add(field.ToString());

            return fields.ToArray();
        }
        public static CSVFile Import(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);

            CSVFile csv = new CSVFile();

            List<string> row = new List<string>();

            //read the header
            csv.Header = ReadRow(lines[0]);
            csv.data = new string[lines.Length - 1][];
            bool skipHeader = true;
            int count = 0;
            foreach(string l in lines)
            {
                if (skipHeader)
                {
                    skipHeader = false;
                    continue;
                }

                csv.data[count] = ReadRow(l);

                count++;
            }
            return csv;
        }
    }
}
