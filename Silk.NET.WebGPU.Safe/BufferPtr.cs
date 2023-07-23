using System;
using Silk.NET.Core.Native;
using System.Threading.Tasks;
using Silk.NET.WebGPU.Safe.Utils;

namespace Silk.NET.WebGPU.Safe
{
    public readonly unsafe partial struct BufferPtr
    {
        private record struct Void();
        private static readonly RentalStorage<TaskCompletionSource<Void>> s_bufferMapTasks = new();

        private static void BufferMapAsyncCallback(BufferMapAsyncStatus status, void* data)
        {
            var task = s_bufferMapTasks.GetAndReturn((int)data);

            if (status != BufferMapAsyncStatus.Success)
                task.SetException(new WGPUException(status.ToString()));

            task.SetResult(default);
        }

        private static readonly PfnBufferMapCallback 
            s_BufferMapAsyncCallback = new(BufferMapAsyncCallback);

        private readonly WebGPU _wgpu;
        private readonly Buffer* _ptr;

        public BufferPtr(WebGPU wgpu, Buffer* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Buffer*(BufferPtr ptr) => ptr._ptr;

        public void Destroy() => _wgpu.BufferDestroy(_ptr);

        public ReadOnlySpan<T> GetConstMappedRange<T>(nuint offset, nuint size)
            where T : unmanaged
        {
            return new(_wgpu.BufferGetConstMappedRange(_ptr, offset, size), (int)size/sizeof(T));
        }

        public BufferMapState GetMapState() => _wgpu.BufferGetMapState(_ptr);

        public Span<T> GetMappedRange<T>(nuint offset, nuint size)
            where T : unmanaged
        {
            return new(_wgpu.BufferGetMappedRange(_ptr, offset, size), (int)size/sizeof(T));
        }

        public ulong GetSize() => _wgpu.BufferGetSize(_ptr);

        public BufferUsage GetUsage() => _wgpu.BufferGetUsage(_ptr);

        public Task MapAsync(MapMode mode, nuint offset, nuint size)
        {
            var task = new TaskCompletionSource<Void>();
            var key = s_bufferMapTasks.Rent(task);

            _wgpu.BufferMapAsync(_ptr, mode, offset, size, s_BufferMapAsyncCallback, (void*)key);

            return task.Task;
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.BufferSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Unmap() => _wgpu.BufferUnmap(_ptr);

        public void Reference() => _wgpu.BufferReference(_ptr);

        public void Release() => _wgpu.BufferRelease(_ptr);
    }
}
