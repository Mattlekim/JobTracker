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

        private static int index;
        private static int startChar;
        private static List<String> tmpRow = new List<string>();
        private static string[] ReadRow(string row)
        {

            index = 0;
            startChar = 0;
            tmpRow.Clear();
   

            while (index != -1) //make sure we can find the correct index
            {
                index = row.IndexOf(',', startChar);
                if (index == -1)
                {
                    if (startChar < row.Length)
                        tmpRow.Add(row.Substring(startChar));

                    return tmpRow.ToArray(); //return as end of row
                }

                tmpRow.Add(row.Substring(startChar, index - startChar));
                startChar = index + 1;
            }
           
            return tmpRow.ToArray();
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
