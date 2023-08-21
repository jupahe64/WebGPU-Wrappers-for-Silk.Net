using Silk.NET.Core.Native;
using System.Threading.Tasks;
using Silk.NET.WebGPU.Safe.Utils;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    public readonly unsafe partial struct ShaderModulePtr
    {
        private static readonly RentalStorage<TaskCompletionSource<CompilationInfo>> s_getCompilationInfoTasks = new();

        private static void GetCompilationInfoCallback(CompilationInfoRequestStatus status, WGPU.CompilationInfo* info, void* data)
        {
            var task = s_getCompilationInfoTasks.GetAndReturn((int)data);

            if (status != CompilationInfoRequestStatus.Success)
                task.SetException(new WGPUException(status.ToString()));
            else
                task.SetResult(CompilationInfo.UnpackFrom(info));
        }

        private static readonly PfnCompilationInfoCallback 
            s_GetCompilationInfoCallback = new(GetCompilationInfoCallback);

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

            _wgpu.ShaderModuleGetCompilationInfo(_ptr, s_GetCompilationInfoCallback, (void*)key);

            return task.Task;
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.ShaderModuleSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.ShaderModuleReference(_ptr);
        
        public void Release() => _wgpu.ShaderModuleRelease(_ptr);
    }
}
