using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WgpuWrappersSilk.Net
{
#pragma warning disable IDE1006 // Naming Styles
    public interface GPUError
#pragma warning restore IDE1006 // Naming Styles
    {
        public string Message { get; }
    }

    public record ValidationError(string Message) : GPUError;
    public record OutOfMemoryError(string Message) : GPUError;
    public record InternalError(string Message) : GPUError;
}
