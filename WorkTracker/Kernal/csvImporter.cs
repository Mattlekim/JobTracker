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
            return Parse(File.ReadAllText(filePath));
        }

        /// <summary>
        /// A whole csv into its heading row and its rows.
        ///
        /// This walks the text rather than the lines, because **a line and
        /// a row are not the same thing**: a field in quotes may hold a
        /// newline, and an export written by another app puts one there
        /// wherever somebody typed a note on two lines. Split on the
        /// newlines first and that one note becomes two half rows, with
        /// every column on both of them out of step - which does not fail,
        /// it imports rubbish. A file with no newline inside a field reads
        /// exactly as it always did.
        /// </summary>
        public static CSVFile Parse(string text)
        {
            CSVFile csv = new CSVFile();
            List<string[]> rows = ReadRows(text ?? string.Empty);

            if (rows.Count == 0)
            {
                csv.Header = new string[0];
                csv.data = new string[0][];
                return csv;
            }

            csv.Header = rows[0];
            csv.data = rows.Skip(1).ToArray();
            return csv;
        }

        /// <summary>
        /// splits the text into rows, keeping a newline that is inside a
        /// quoted field as part of that field
        /// </summary>
        private static List<string[]> ReadRows(string text)
        {
            List<string[]> rows = new List<string[]>();
            StringBuilder row = new StringBuilder();
            bool inQuotes = false;

            //a quote only opens a field when it is the first thing in one,
            //which is the rule ReadRow reads the row back with. Toggling on
            //any quote at all would let a stray one - 6" pole, written in a
            //note - swallow the rest of the file
            bool atFieldStart = true;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    if (inQuotes)
                    {
                        //a doubled quote is a quote inside the field and
                        //does not close anything
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            row.Append("\"\"");
                            i++;
                            continue;
                        }
                        inQuotes = false;
                    }
                    else if (atFieldStart)
                        inQuotes = true;

                    row.Append(c);
                    atFieldStart = false;
                    continue;
                }

                if (!inQuotes && (c == '\n' || c == '\r'))
                {
                    //\r\n is one ending, not two
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    AddRow(rows, row.ToString());
                    row.Clear();
                    atFieldStart = true;
                    continue;
                }

                row.Append(c);
                atFieldStart = !inQuotes && c == ',';
            }

            AddRow(rows, row.ToString());
            return rows;
        }

        /// <summary>
        /// a blank line is not a row. csv files routinely end with one, and
        /// counting it would hand every reader an empty row to trip over
        /// </summary>
        private static void AddRow(List<string[]> rows, string line)
        {
            if (line.Trim().Length == 0)
                return;
            rows.Add(ReadRow(line));
        }
    }
}
