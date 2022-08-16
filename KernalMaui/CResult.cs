using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel
{
    public enum ResultType
    {
        Success,
        Exist,
        Fail
    }
    public class CResult
    {
        public ResultType Type { get; set; }
    }
}
