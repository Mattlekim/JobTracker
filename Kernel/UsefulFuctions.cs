using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public class Vector3
    {
        public float X;
        public float Y;
        public float Z;
    }

    public static class UsfulFuctions
    {

        /// <summary>
        /// today, worked out fresh every time it is read. this used to be a
        /// field filled in once when the class first loaded, which is fine on a
        /// desktop that gets shut down but not on a phone, where the app is left
        /// running for days - work marked done was stamped with whatever date
        /// the app happened to have been started on
        /// </summary>
        public static DateTime DateNow => DateTime.Now.Date;

        public static DateTime DateBase = new DateTime(2000, 1, 1);

        private static int tmp;

        public static int Difference(DateTime one, DateTime two)
        {
            return Math.Abs((one.DayOfYear + one.Year * 400) - (two.DayOfYear + two.Year * 400));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <returns>if number is bigger than 0 first date is bigger than second date</returns>
        public static int DifferenceSigned(DateTime one, DateTime two)
        {
            return (one.DayOfYear + one.Year * 400) - (two.DayOfYear + two.Year * 400);
        }
        public static DateTime StringToDateTime(string input)
        {
            tmp = input.IndexOf("/");

            if (tmp == -1)
                return new DateTime(2000, 1, 1);
            int day;
            int month = 0;
            int year = 0;

            try
            {
               
                day = Convert.ToInt32(input.Substring(0, tmp));
                input = input.Substring(tmp + 1);
                tmp = input.IndexOf("/");
                month = Convert.ToInt32(input.Substring(0, tmp));
                input = input.Substring(tmp + 1);
                year = Convert.ToInt32(input);
                return new DateTime(year, month, day);
            }
            catch
            {
                return new DateTime(2000, 1, 1);
            }

            return DateTime.Now;
        }

        public static string GetFirstLettersFromWord(String word)
        {
            word = word.Insert(0, " ");
            List<char> output = new List<char>();
            
            for (int i = 0; i < word.Length - 1; i++)
            {
                if (word[i] == ' ' && word[i+1] != ' ')
                {
                    output.Add(word[i+1]);
                    i++; //performance skip
                }
            }

            return new string(output.ToArray());
        }
    }
}
