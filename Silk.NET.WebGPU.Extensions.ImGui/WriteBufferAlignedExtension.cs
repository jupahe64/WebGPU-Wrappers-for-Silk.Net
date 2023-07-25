using System;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU.Safe;
using System.Runtime.InteropServices;

namespace Silk.NET.WebGPU.Extensions.ImGui
{
    internal static class WriteBufferAlignedExtension
    {
        /// <summary>
        /// Will make sure total size-in-bytes of the data written is aligned to 16 byte 
        /// <br>
        /// by reading beyond the <paramref name="data"/>-span and 
        /// writing those additional bytes to the <paramref name="buffer"/> if not aligned
        /// </br>
        /// </summary>
        public static unsafe void WriteBufferAligned<T>(this QueuePtr queue, BufferPtr buffer, ulong bufferOffset, ReadOnlySpan<T> data)
            where T : unmanaged
        {
            var structSize = sizeof(T);

            var dataSize = data.Length * structSize;

            queue.WriteBuffer<byte>(buffer, bufferOffset,
                new Span<byte>(
                    Unsafe.AsPointer(ref MemoryMarshal.GetReference(data)),
                    (dataSize + 15) / 16 * 16)
            );
        }
    }
}