using System;

namespace Silk.NET.WebGPU.Safe
{
    public class WGPUException : Exception
    {
        public WGPUException(string message) : base(message) { }
    }
}
