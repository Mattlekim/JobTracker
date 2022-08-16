using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    /// <summary>
    /// the payment method for the payment
    /// </summary>
    public enum PaymentMethod
    {
        Cash,
        Card,
        Paypal,
        Bank,
        Check,
        GoCardless,
        Other,
    }
}
