using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WgpuWrappersSilk.Net
{
    public class WGPUException : Exception
    {
        public WGPUException(string message) : base(message) { }
    }
}
