using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public class Gloable
    {
        public static string CurrenceSymbol = "£";
    }

    /// <summary>
    /// raised whenever customer/job/payment data is saved to disk so the app
    /// layer can push the files to cloud sync without the kernel knowing about it
    /// </summary>
    public static class SyncNotifier
    {
        public static event Action DataSaved;

        public static void NotifySaved()
        {
            DataSaved?.Invoke();
        }
    }
}
