namespace Silk.NET.WebGPU.Safe
{
#pragma warning disable IDE1006 // Naming Styles
    public interface GPUError
#pragma warning restore IDE1006 // Naming Styles
    {
        public string Message { get; }
    }

    public class ValidationError : GPUError
    {
        public ValidationError(string message) { Message = message; }
        public string Message {get; private set;}
    }

    public class OutOfMemoryError : GPUError
    {
        public OutOfMemoryError(string message) { Message = message; }
        public string Message { get; private set; }
    }

    public class InternalError : GPUError
    {
        public InternalError(string message) { Message = message; }
        public string Message { get; private set; }
    }

    public class DeviceLostError : GPUError
    {
        public DeviceLostError(string message) { Message = message; }
        public string Message { get; private set; }
    }
    
    public class UnkownError : GPUError
    {
        public UnkownError(string message) { Message = message; }
        public string Message { get; private set; }
    }
}
