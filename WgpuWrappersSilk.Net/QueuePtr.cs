using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WgpuWrappersSilk.Net.Utils;
using WGPU = Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
{
    public readonly unsafe struct QueuePtr
    {
        private static RentalStorage<TaskCompletionSource> s_onSubmittedWorkTasks = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void OnSubmittedWorkDoneCallback(QueueWorkDoneStatus status, void* data)
        {
            var task = s_onSubmittedWorkTasks.GetAndReturn((int)data);

            if (status != QueueWorkDoneStatus.Success)
            {
                task.SetException(new WGPUException(status.ToString()));

                return;
            }

            task.SetResult();
        }

        private readonly WebGPU _wgpu;
        private readonly Queue* _ptr;

        public QueuePtr(WebGPU wgpu, Queue* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Queue*(QueuePtr ptr) => ptr._ptr;

        public Task OnSubmittedWorkDone()
        {
            var task = new TaskCompletionSource();
            int key = s_onSubmittedWorkTasks.Rent(task);
            _wgpu.QueueOnSubmittedWorkDone(_ptr, new(&OnSubmittedWorkDoneCallback), (void*)key);
            return task.Task;
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.QueueSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Submit(ReadOnlySpan<CommandBufferPtr> commands)
        {
            CommandBuffer** commandBufferPtrs = stackalloc CommandBuffer*[commands.Length];
            
            for (int i = 0; i < commands.Length; i++)
                commandBufferPtrs[i] = commands[i];

            _wgpu.QueueSubmit(_ptr, (uint)commands.Length, commandBufferPtrs);
        }

        public void WriteBuffer<T>(BufferPtr buffer, ulong bufferOffset, ReadOnlySpan<T> data)
            where T : unmanaged
        {
            _wgpu.QueueWriteBuffer<T>(_ptr, buffer, bufferOffset, data, (uint)(sizeof(T)*data.Length));
        }

        public void WriteTexture<T>(in ImageCopyTexture destination, ReadOnlySpan<T> data, 
        in TextureDataLayout dataLayout, in Extent3D writeSize)
            where T : unmanaged
        {
            WGPU.ImageCopyTexture _destination = destination.Pack();

            fixed (TextureDataLayout* dataLayoutPtr = &dataLayout)
            fixed (Extent3D* writeSizePtr = &writeSize)
            {
                _wgpu.QueueWriteTexture<T>(_ptr, &_destination, data, (uint)(sizeof(T) * data.Length), 
                    dataLayoutPtr, writeSizePtr);
            }
        }
    }
}
