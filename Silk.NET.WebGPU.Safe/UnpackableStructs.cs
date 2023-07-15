using Silk.NET.Core.Native;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    public unsafe struct CompilationInfo
    {
        public CompilationMessage[] Messages;

        internal static CompilationInfo UnpackFrom(WGPU.CompilationInfo* native)
        {
            var messages = new CompilationMessage[native->MessageCount];

            for (uint i = 0; i < native->MessageCount; i++)
            {
                messages[i] = new CompilationMessage
                {
                    Message = SilkMarshal.PtrToString((nint)native->Messages[i].Message, NativeStringEncoding.UTF8),
                    Type = native->Messages[i].Type,
                    LineNum = native->Messages[i].LineNum,
                    LinePos = native->Messages[i].LinePos,
                    Offset = native->Messages[i].Offset,
                    Length = native->Messages[i].Length
                };
            }
                

            return new CompilationInfo
            {
                Messages = messages
            };
        }
    }

    public record struct CompilationMessage(string? Message, CompilationMessageType Type, 
        ulong LineNum, ulong LinePos, ulong Offset, ulong Length);
}
