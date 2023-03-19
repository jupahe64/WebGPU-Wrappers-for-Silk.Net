using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WgpuWrappersSilk.Net.Utils;
using WGPU = Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
{
    public readonly unsafe struct ShaderModulePtr
    {
        private static readonly RentalStorage<TaskCompletionSource<CompilationInfo>> s_getCompilationInfoTasks = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void GetCompilationInfoCallback(CompilationInfoRequestStatus status, WGPU.CompilationInfo* info, void* data)
        {
            var task = s_getCompilationInfoTasks.GetAndReturn((int)data);

            if (status != CompilationInfoRequestStatus.Success)
                task.SetException(new WGPUException(status.ToString()));

            task.SetResult(CompilationInfo.UnpackFrom(info));
        }

        private readonly WebGPU _wgpu;
        private readonly ShaderModule* _ptr;

        public ShaderModulePtr(WebGPU wgpu, ShaderModule* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator ShaderModule*(ShaderModulePtr ptr) => ptr._ptr;

        public Task<CompilationInfo> GetCompilationInfo()
        {
            var task = new TaskCompletionSource<CompilationInfo>();
            var key = s_getCompilationInfoTasks.Rent(task);

            _wgpu.ShaderModuleGetCompilationInfo(_ptr, new(&GetCompilationInfoCallback), (void*)key);

            return task.Task;
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.ShaderModuleSetLabel(_ptr, marshalledLabel.Ptr);
        }
    }
}
