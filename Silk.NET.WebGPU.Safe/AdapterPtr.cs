using System;
using Silk.NET.Core.Native;
using System.Diagnostics;
using System.Threading.Tasks;
using Silk.NET.WebGPU.Safe.Utils;
using System.Collections.Generic;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    public delegate void DeviceLostCallback(DeviceLostReason reason, string? message);

    public readonly unsafe partial struct AdapterPtr
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

        private static readonly List<DeviceLostCallback> s_deviceLostCallbacks = new();

        private static void DeviceLostCallback(DeviceLostReason reason, byte* message, void* data)
        {
            s_deviceLostCallbacks[(int)data].Invoke(
                reason,
                SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8)
            );
        }

        private static readonly PfnRequestDeviceCallback 
            s_DeviceRequestCallback = new(DeviceRequestCallback);

        private static readonly PfnDeviceLostCallback s_DeviceLostCallback = new(DeviceLostCallback);

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
            WGPU.AdapterProperties properties = default;
            _wgpu.AdapterGetProperties(_ptr, &properties);
            return AdapterProperties.UnpackFrom(&properties);
        }

        public bool HasFeature(FeatureName feature)
        {
            return _wgpu.AdapterHasFeature(_ptr, feature);
        }

        public Task<DevicePtr> RequestDevice(
            Limits? requiredLimits, FeatureName[]? requiredFeatures,
            DeviceLostCallback? deviceLostCallback, string? defaultQueueLabel, string ? label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);
            using var marshalledDefaultQueueLabel = new MarshalledString(defaultQueueLabel, NativeStringEncoding.UTF8);

            int idx = 0;

            if (deviceLostCallback is not null)
            {
                idx = s_deviceLostCallbacks.Count;
                s_deviceLostCallbacks.Add(deviceLostCallback);
            }

            RequiredLimits _requiredLimits = default;

            if(requiredLimits is not null)
            {
                _requiredLimits.Limits = requiredLimits.Value;
            }

            requiredFeatures ??= Array.Empty<FeatureName>();

            fixed (FeatureName* requiredFeaturesPtr = requiredFeatures)
            {
                DeviceDescriptor descriptor = new()
                {
                    DeviceLostCallback = deviceLostCallback == null ? default : s_DeviceLostCallback,
                    DeviceLostUserdata = (void*)idx,
                    DefaultQueue = new QueueDescriptor(label: marshalledDefaultQueueLabel.Ptr),
                    RequiredLimits = requiredLimits == null ? null : &_requiredLimits,
                    RequiredFeatures = requiredFeaturesPtr,
                    RequiredFeaturesCount = (uint)requiredFeatures.Length,
                    Label = marshalledLabel.Ptr
                };

                var task = new TaskCompletionSource<DevicePtr>();
                int key = s_deviceRequests.Rent((_wgpu, task));
                _wgpu.AdapterRequestDevice(_ptr, in descriptor, s_DeviceRequestCallback, (void*)key);
                return task.Task;
            }
        }

        public void Reference() => _wgpu.AdapterReference(_ptr);

        public void Release() => _wgpu.AdapterRelease(_ptr);
    }
}
