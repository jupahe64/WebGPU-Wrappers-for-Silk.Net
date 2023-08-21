using Silk.NET.WebGPU.Extensions.WGPU;
using System;
using System.Collections.Generic;
using System.Text;

namespace Silk.NET.WebGPU.Safe.Extensions
{
    public unsafe static class WgpuNativeExtension
    {
        public static DevicePtrWgpu GetExtension(this DevicePtr device, Wgpu wgpuExtension) 
        {
            return new(device, wgpuExtension);
        }
    }

    public readonly unsafe struct DevicePtrWgpu
    {
        private readonly Device* _ptr;
        private readonly Wgpu _wgpu;

        public DevicePtrWgpu(Device* ptr, Wgpu wgpuExtension)
        {
            _ptr = ptr;
            _wgpu = wgpuExtension;
        }

        public static implicit operator Device*(DevicePtrWgpu ptr) => ptr._ptr;

        public void Poll(bool wait, (QueuePtr queue, ulong index)? wrappedSubmissionIndex)
        {
            var wsi = new WrappedSubmissionIndex(
                wrappedSubmissionIndex.GetValueOrDefault().queue,
                wrappedSubmissionIndex.GetValueOrDefault().index);

            _wgpu.DevicePoll(_ptr, wait,
                wrappedSubmissionIndex == null ? null : &wsi
                );
        }

        //TODO implement others
    }
}
