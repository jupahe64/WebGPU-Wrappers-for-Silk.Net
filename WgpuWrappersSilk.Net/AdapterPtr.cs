using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WgpuWrappersSilk.Net
{
    public readonly unsafe struct AdapterPtr
    {
        private static readonly List<(WebGPU, TaskCompletionSource<DevicePtr>)> s_deviceRequests = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void DeviceRequestCallback(RequestDeviceStatus status, Device* device, byte* message, void* data)
        {
            var (wgpu, task) = s_deviceRequests[(int)data];

            if (status != RequestDeviceStatus.Success)
            {
                task.SetException(new WGPUException(
                    $"{status} {SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8)}"));

                return;
            }

            task.SetResult(new DevicePtr(wgpu, device));
        }

        private readonly WebGPU _wgpu;
        private readonly Adapter* _ptr;

        public AdapterPtr(WebGPU wgpu, Adapter* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Adapter*(AdapterPtr ptr) => ptr._ptr;

        public FeatureName[] EnumerateFeatures()
        {
            nuint count = _wgpu.AdapterEnumerateFeatures(_ptr, null);

            Span<FeatureName> span = stackalloc FeatureName[(int)count];
            _wgpu.AdapterEnumerateFeatures(_ptr, span);
            var arr = new FeatureName[count];

            span.CopyTo(arr);
            return arr;
        }

        public Limits GetLimits()
        {
            SupportedLimits limits = default;
            _wgpu.AdapterGetLimits(_ptr, &limits);
            Debug.Assert(limits.NextInChain == null);
            return limits.Limits;
        }

        public AdapterProperties GetProperties()
        {
            AdapterProperties properties = default;
            _wgpu.AdapterGetProperties(_ptr, &properties);
            Debug.Assert(properties.NextInChain == null);
            return properties;
        }

        public bool HasFeature(FeatureName feature)
        {
            return _wgpu.AdapterHasFeature(_ptr, feature);
        }

        public Task<DevicePtr> RequestDevice(in DeviceDescriptor descriptor)
        {
            int idx = s_deviceRequests.Count;
            var task = new TaskCompletionSource<DevicePtr>();
            s_deviceRequests.Add((_wgpu, task));
            _wgpu.AdapterRequestDevice(_ptr, in descriptor, new(&DeviceRequestCallback), (void*)idx);
            return task.Task;
        }
    }
}
