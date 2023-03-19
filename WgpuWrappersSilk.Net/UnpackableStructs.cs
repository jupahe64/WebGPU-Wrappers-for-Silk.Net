using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using WGPU = Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
{
    public unsafe struct CompilationInfo
    {
        public CompilationMessage[] Messages;

        internal static CompilationInfo UnpackFrom(WGPU.CompilationInfo* native)
        {
            var messages = new CompilationMessage[native->MessageCount];

            for (int i = 0; i < native->MessageCount; i++)
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
