using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WgpuWrappersSilk.Net.Utils;
using Buffer = Silk.NET.WebGPU.Buffer;
using WGPU = Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
{
    public readonly unsafe struct BufferPtr
    {
        private static readonly RentalStorage<TaskCompletionSource> s_bufferMapTasks = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void BufferMapAsyncCallback(BufferMapAsyncStatus status, void* data)
        {
            var task = s_bufferMapTasks.GetAndReturn((int)data);

            if (status != BufferMapAsyncStatus.Success)
                task.SetException(new WGPUException(status.ToString()));

            task.SetResult();
        }

        private readonly WebGPU _wgpu;
        private readonly Buffer* _ptr;

        public BufferPtr(WebGPU wgpu, Buffer* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Buffer*(BufferPtr ptr) => ptr._ptr;

        public void Destroy() => _wgpu.BufferDestroy(_ptr);

        public Span<T> GetConstMappedRange<T>(nuint offset, nuint size)
            where T : unmanaged
        {
            return new(_wgpu.BufferGetConstMappedRange(_ptr, offset, size), (int)size/sizeof(T));
        }

        public Span<T> GetMappedRange<T>(nuint offset, nuint size)
            where T : unmanaged
        {
            return new(_wgpu.BufferGetMappedRange(_ptr, offset, size), (int)size/sizeof(T));
        }

        public ulong GetSize() => _wgpu.BufferGetSize(_ptr);

        public BufferUsage GetUsage() => _wgpu.BufferGetUsage(_ptr);

        public Task MapAsync(MapMode mode, nuint offset, nuint size)
        {
            var task = new TaskCompletionSource();
            var key = s_bufferMapTasks.Rent(task);

            _wgpu.BufferMapAsync(_ptr, mode, offset, size, new(&BufferMapAsyncCallback), (void*)key);

            return task.Task;
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.BufferSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Unmap() => _wgpu.BufferUnmap(_ptr);
    }
}
