using System;
using Silk.NET.Core.Native;
using System.Diagnostics;
using System.Threading.Tasks;
using Silk.NET.WebGPU.Safe.Utils;

namespace Silk.NET.WebGPU.Safe
{
    public readonly unsafe struct AdapterPtr
    {
        private static readonly RentalStorage<(WebGPU, TaskCompletionSource<DevicePtr>)> s_deviceRequests = new();

        private static void DeviceRequestCallback(RequestDeviceStatus status, Device* device, byte* message, void* data)
        {
            var (wgpu, task) = s_deviceRequests.GetAndReturn((int)data);

            if (status != RequestDeviceStatus.Success)
            {
                task.SetException(new WGPUException(
                    $"{status} {SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8)}"));

                return;
            }

            task.SetResult(new DevicePtr(wgpu, device));
        }

        private static readonly PfnRequestDeviceCallback 
            s_DeviceRequestCallback = new(DeviceRequestCallback);

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
            var task = new TaskCompletionSource<DevicePtr>();
            int key = s_deviceRequests.Rent((_wgpu, task));
            _wgpu.AdapterRequestDevice(_ptr, in descriptor, s_DeviceRequestCallback, (void*)key);
            return task.Task;
        }
    }
}
