using System;
using Silk.NET.Core.Native;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.WebGPU.Safe.Utils;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    public delegate void ErrorCallback(ErrorType type, string? message);

    public readonly unsafe partial struct DevicePtr
    {
        private static readonly RentalStorage<(WebGPU, TaskCompletionSource<ComputePipelinePtr>)> s_computePipelineRequests = new();

        private static void CreateComputePipelineCallback(CreatePipelineAsyncStatus status, ComputePipeline* pipeline, byte* message, void* data)
        {
            var (wgpu, task) = s_computePipelineRequests.GetAndReturn((int)data);

            if (status != CreatePipelineAsyncStatus.Success)
            {
                task.SetException(new WGPUException(
                    $"{status} {SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8)}"));

                return;
            }

            task.SetResult(new ComputePipelinePtr(wgpu, pipeline));
        }

        private static readonly RentalStorage<(WebGPU, TaskCompletionSource<RenderPipelinePtr>)> s_renderPipelineRequests = new();

        private static void CreateRenderPipelineCallback(CreatePipelineAsyncStatus status, RenderPipeline* pipeline, byte* message, void* data)
        {
            var (wgpu, task) = s_renderPipelineRequests.GetAndReturn((int)data);

            if (status != CreatePipelineAsyncStatus.Success)
            {
                task.SetException(new WGPUException(
                    $"{status} {SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8)}"));

                return;
            }

            task.SetResult(new RenderPipelinePtr(wgpu, pipeline));
        }

        private static readonly List<ErrorCallback> s_uncapturedErrorCallbacks = new();

        private static void UncapturedErrorCallback(ErrorType type, byte* message, void* data)
        {
            s_uncapturedErrorCallbacks[(int)data].Invoke(
                type, 
                SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8)
            );
        }

        private static readonly RentalStorage<TaskCompletionSource<GPUError?>> s_popErrorScopeTasks = new();

        private static void PopErrorScopeCallback(ErrorType type, byte* message, void* data)
        {
            var task = s_popErrorScopeTasks.GetAndReturn((int)data);

            string? messageStr = SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8);

            if (type == ErrorType.DeviceLost)
                task.SetException(new WGPUException($"{type} {messageStr}"));

            task.SetResult(
                type switch
                {
                    ErrorType.NoError => null,
                    ErrorType.Validation => new ValidationError(messageStr ?? ""),
                    ErrorType.OutOfMemory => new OutOfMemoryError(messageStr ?? ""),
                    ErrorType.Internal => new InternalError(messageStr ?? ""),
                    ErrorType.DeviceLost => new DeviceLostError(messageStr ?? ""),
                    ErrorType.Unknown => new UnkownError(messageStr ?? ""),
                    _ => throw new NotImplementedException()
                }
            );
        }

        private static readonly PfnCreateComputePipelineAsyncCallback 
            s_CreateComputePipelineCallback = new(CreateComputePipelineCallback);
        private static readonly PfnCreateRenderPipelineAsyncCallback 
            s_CreateRenderPipelineCallback = new(CreateRenderPipelineCallback);
        private static readonly PfnErrorCallback 
            s_UncapturedErrorCallback = new(UncapturedErrorCallback);
        private static readonly PfnErrorCallback 
            s_PopErrorScopeCallback = new(PopErrorScopeCallback);


        private static readonly delegate*<ErrorType, byte*, void*, void>
            FuncPtr_PopErrorScopeCallback = &PopErrorScopeCallback;

        private readonly WebGPU _wgpu;
        private readonly Device* _ptr;

        public DevicePtr(WebGPU wgpu, Device* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Device*(DevicePtr ptr) => ptr._ptr;

        public BindGroupPtr CreateBindGroup(BindGroupLayoutPtr bindGroupLayout, ReadOnlySpan<BindGroupEntry> entries, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            fixed (BindGroupEntry* entriesPtr = entries)
            {
                var descriptor = new BindGroupDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    Layout = bindGroupLayout,
                    EntryCount = (uint)entries.Length,
                    Entries = entriesPtr
                };

                return new BindGroupPtr(_wgpu, _wgpu.DeviceCreateBindGroup(_ptr, in descriptor));
            }
        }

        public BindGroupLayoutPtr CreateBindGroupLayout(ReadOnlySpan<BindGroupLayoutEntry> entries, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            fixed (BindGroupLayoutEntry* entriesPtr = entries)
            {
                var descriptor = new BindGroupLayoutDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    EntryCount = (uint)entries.Length,
                    Entries = entriesPtr
                };

                return new BindGroupLayoutPtr(_wgpu, _wgpu.DeviceCreateBindGroupLayout(_ptr, in descriptor));
            }
        }

        public BufferPtr CreateBuffer(BufferUsage usage, ulong size, bool mappedAtCreation = false, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new BufferDescriptor
            {
                Label = marshalledLabel.Ptr,
                Usage = usage,
                Size = size,
                MappedAtCreation = mappedAtCreation
            };

            return new BufferPtr(_wgpu, _wgpu.DeviceCreateBuffer(_ptr, in descriptor));

            
        }

        public CommandEncoderPtr CreateCommandEncoder(string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new CommandEncoderDescriptor
            {
                Label = marshalledLabel.Ptr
            };

            return new CommandEncoderPtr(_wgpu, _wgpu.DeviceCreateCommandEncoder(_ptr, in descriptor));
        }

        public ComputePipelinePtr CreateComputePipeline(ProgrammableStage stage, PipelineLayoutPtr? layout, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new ComputePipelineDescriptor 
            {
                Label = marshalledLabel.Ptr,
                Layout = layout ?? (PipelineLayout*)null
            };

            Span<byte> span = stackalloc byte[stage.CalculatePayloadSize()];
            stage.PackInto(ref descriptor.Compute, span);

            return new ComputePipelinePtr(_wgpu, _wgpu.DeviceCreateComputePipeline(_ptr, in descriptor));
        }

        public Task<ComputePipelinePtr> CreateComputePipelineAsync(ProgrammableStage compute, PipelineLayoutPtr? layout, string? label = null)
        {
            var task = new TaskCompletionSource<ComputePipelinePtr>();
            int key = s_computePipelineRequests.Rent((_wgpu, task));

            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new ComputePipelineDescriptor
            {
                Label = marshalledLabel.Ptr,
                Layout = layout ?? (PipelineLayout*)null
            };

            Span<byte> span = stackalloc byte[compute.CalculatePayloadSize()];
            compute.PackInto(ref descriptor.Compute, span);

            _wgpu.DeviceCreateComputePipelineAsync(_ptr, in descriptor, s_CreateComputePipelineCallback, (void*)key);

            return task.Task;
        }

        public PipelineLayoutPtr CreatePipelineLayout(ReadOnlySpan<BindGroupLayoutPtr> bindGroupLayouts, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            BindGroupLayout** bindGroupLayoutPtrs = stackalloc BindGroupLayout*[bindGroupLayouts.Length];

            for (int i = 0; i < bindGroupLayouts.Length; i++)
                bindGroupLayoutPtrs[i] = bindGroupLayouts[i];

            var descriptor = new PipelineLayoutDescriptor
            {
                Label = marshalledLabel.Ptr,
                BindGroupLayouts = bindGroupLayoutPtrs,
                BindGroupLayoutCount = (uint)bindGroupLayouts.Length
            };
            

            return new PipelineLayoutPtr(_wgpu, _wgpu.DeviceCreatePipelineLayout(_ptr, in descriptor));
        }

        public QuerySetPtr CreateQuerySet(QueryType type, uint count, ReadOnlySpan<PipelineStatisticName> statistics = default, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            fixed (PipelineStatisticName* ptr = statistics)
            {
                var descriptor = new QuerySetDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    Type = type,
                    Count = count,
                    PipelineStatisticsCount = (uint)statistics.Length,
                    PipelineStatistics = ptr,
                };

                return new QuerySetPtr(_wgpu, _wgpu.DeviceCreateQuerySet(_ptr, in descriptor));
            }
        }

        public RenderBundleEncoderPtr CreateRenderBundleEncoder(
            ReadOnlySpan<TextureFormat> colorFormats, TextureFormat depthStencilFormat,
            uint sampleCount = 1,
            bool depthReadOnly = false, bool stencilReadOnly = false, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            fixed (TextureFormat* ptr = colorFormats)
            {
                var descriptor = new RenderBundleEncoderDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    ColorFormatsCount = (uint)colorFormats.Length,
                    ColorFormats = ptr,
                    DepthStencilFormat = depthStencilFormat,
                    DepthReadOnly = depthReadOnly,
                    StencilReadOnly = stencilReadOnly,
                    SampleCount = sampleCount
                };

                return new RenderBundleEncoderPtr(_wgpu, _wgpu.DeviceCreateRenderBundleEncoder(_ptr, in descriptor));
            }
        }

        public RenderPipelinePtr CreateRenderPipeline(
            PipelineLayoutPtr? layout, 
            VertexState vertex,
            PrimitiveState primitive,
            DepthStencilState? depthStencil,
            MultisampleState multisample,
            FragmentState? fragment,
            string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            int indexPtr = 0;

            Span<byte> payloadBuffer = stackalloc byte[vertex.CalculatePayloadSize() +
                primitive.CalculatePayloadSize() +
                (fragment.HasValue ? fragment.Value.CalculatePayloadSize() : 0)];

            var vs = default(WGPU.VertexState);
            indexPtr += vertex.PackInto(ref vs, payloadBuffer);

            var ps = default(WGPU.PrimitiveState);
            indexPtr += primitive.PackInto(ref ps, payloadBuffer[indexPtr..]);

            var ds = depthStencil.GetValueOrDefault();

            WGPU.FragmentState* fsPtr = null;
            var fs = default(WGPU.FragmentState);
            if (fragment.HasValue)
            {
                indexPtr += fragment.Value.PackInto(ref fs, payloadBuffer[indexPtr..]);
                fsPtr = &fs;
            }

            var descriptor = new RenderPipelineDescriptor
            {
                Label = marshalledLabel.Ptr,
                Layout = layout ?? (PipelineLayout*)null,
                Vertex = vs,
                Primitive = ps,
                DepthStencil = depthStencil==null ? null : &ds,
                Multisample = multisample,
                Fragment = fsPtr
            };

            return new RenderPipelinePtr(_wgpu, _wgpu.DeviceCreateRenderPipeline(_ptr, in descriptor));
        }

        public Task<RenderPipelinePtr> CreateRenderPipelineAsync(
            PipelineLayoutPtr? layout,
            VertexState vertex,
            PrimitiveState primitive,
            DepthStencilState? depthStencil,
            MultisampleState multisample,
            FragmentState? fragment,
            string? label = null)
        {
            var task = new TaskCompletionSource<RenderPipelinePtr>();
            int key = s_renderPipelineRequests.Rent((_wgpu, task));

            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            int indexPtr = 0;

            Span<byte> payloadBuffer = stackalloc byte[vertex.CalculatePayloadSize() +
                primitive.CalculatePayloadSize() +
                (fragment.HasValue ? fragment.Value.CalculatePayloadSize() : 0)];

            var vs = default(WGPU.VertexState);
            indexPtr += vertex.PackInto(ref vs, payloadBuffer);

            var ps = default(WGPU.PrimitiveState);
            indexPtr += primitive.PackInto(ref ps, payloadBuffer[indexPtr..]);

            var ds = depthStencil.GetValueOrDefault();

            WGPU.FragmentState* fsPtr = null;
            var fs = default(WGPU.FragmentState);
            if (fragment.HasValue)
            {
                indexPtr += fragment.Value.PackInto(ref fs, payloadBuffer[indexPtr..]);
                fsPtr = &fs;
            }

            var descriptor = new RenderPipelineDescriptor
            {
                Label = marshalledLabel.Ptr,
                Layout = layout ?? (PipelineLayout*)null,
                Vertex = vs,
                Primitive = ps,
                DepthStencil = depthStencil == null ? null : &ds,
                Multisample = multisample,
                Fragment = fsPtr
            };

            _wgpu.DeviceCreateRenderPipelineAsync(_ptr, in descriptor, s_CreateRenderPipelineCallback, (void*)key);

            return task.Task;
        }

        public SamplerPtr CreateSampler(
            AddressMode addressModeU, AddressMode addressModeV, AddressMode addressModeW,
            FilterMode magFilter, FilterMode minFilter, MipmapFilterMode mipmapFilter,
            float lodMinClamp, float lodMaxClamp,
            CompareFunction compare,
            ushort maxAnisotropy = 1,
            string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new SamplerDescriptor
            {
                Label = marshalledLabel.Ptr,
                AddressModeU = addressModeU,
                AddressModeV = addressModeV,
                AddressModeW = addressModeW,
                MagFilter = magFilter,
                MinFilter = minFilter,
                MipmapFilter = mipmapFilter,
                LodMinClamp = lodMinClamp,
                LodMaxClamp = lodMaxClamp,
                Compare = compare,
                MaxAnisotropy = maxAnisotropy,
            };

            return new SamplerPtr(_wgpu, _wgpu.DeviceCreateSampler(_ptr, in descriptor));
        }

        

        public ShaderModulePtr CreateShaderModuleWGSL(
            string code,
            ShaderModuleCompilationHint[] compilationHints, 
            string? label = null)
        {
            Span<byte> bytes = new byte[SilkMarshal.GetMaxSizeOf(code, NativeStringEncoding.UTF8)];
            SilkMarshal.StringIntoSpan(code, bytes, NativeStringEncoding.UTF8);
            return CreateShaderModuleWGSL(bytes, compilationHints, label);
        }

        public ShaderModulePtr CreateShaderModuleWGSL(
            ReadOnlySpan<byte> code,
            ShaderModuleCompilationHint[] compilationHints, 
            string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            int payloadBufferSize = 0;
            for (int i = 0; i < compilationHints.Length; i++)
                payloadBufferSize += compilationHints[i].CalculatePayloadSize();

            Span<byte> payloadBuffer = stackalloc byte[payloadBufferSize];
            var compilationHintsPtr = stackalloc WGPU.ShaderModuleCompilationHint[compilationHints.Length];

            int indexPtr = 0;

            for (int i = 0; i < compilationHints.Length; i++)
                indexPtr += compilationHints[i].PackInto(ref compilationHintsPtr[i], payloadBuffer[indexPtr..]);

            fixed(byte* codePtr = code)
            {
                var wgslDescriptor = new ShaderModuleWGSLDescriptor
                {
                    Chain = new ChainedStruct
                    {
                        SType = SType.ShaderModuleWgslDescriptor
                    },
                    Code = codePtr,
                };

                var descriptor = new ShaderModuleDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    HintCount = (uint)compilationHints.Length,
                    Hints = compilationHintsPtr,
                    NextInChain = &wgslDescriptor.Chain
                };

                return new ShaderModulePtr(_wgpu, _wgpu.DeviceCreateShaderModule(_ptr, in descriptor));
            }
        }

        public ShaderModulePtr CreateShaderModuleSPIRV(
            ReadOnlySpan<byte> code,
            ShaderModuleCompilationHint[] compilationHints,
            string? label = null)
        {
            if (code.Length % 4 != 0)
                throw new ArgumentException($"{nameof(code)} is not 32-bit aligned");

            int code32Size = code.Length / 4;

            fixed (byte* code32Ptr = code)
                return CreateShaderModuleSPIRV(
                    new ReadOnlySpan<uint>(code32Ptr, code32Size), 
                    compilationHints, label);
        }

        public ShaderModulePtr CreateShaderModuleSPIRV(
            ReadOnlySpan<uint> code,
            ShaderModuleCompilationHint[] compilationHints, 
            string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            int payloadBufferSize = 0;
            for (int i = 0; i < compilationHints.Length; i++)
                payloadBufferSize += compilationHints[i].CalculatePayloadSize();

            Span<byte> payloadBuffer = stackalloc byte[payloadBufferSize];
            var compilationHintsPtr = stackalloc WGPU.ShaderModuleCompilationHint[compilationHints.Length];

            int indexPtr = 0;

            for (int i = 0; i < compilationHints.Length; i++)
                indexPtr += compilationHints[i].PackInto(ref compilationHintsPtr[i], payloadBuffer[indexPtr..]);

            fixed(uint* codePtr = code)
            {
                var wgslDescriptor = new ShaderModuleSPIRVDescriptor
                {
                    Chain = new ChainedStruct
                    {
                        SType = SType.ShaderModuleSpirvDescriptor
                    },
                    Code = codePtr,
                    CodeSize = (uint)code.Length
                };

                var descriptor = new ShaderModuleDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    HintCount = (uint)compilationHints.Length,
                    Hints = compilationHintsPtr,
                    NextInChain = &wgslDescriptor.Chain
                };

                return new ShaderModulePtr(_wgpu, _wgpu.DeviceCreateShaderModule(_ptr, in descriptor));
            }
        }

        public SwapChainPtr CreateSwapChain(SurfacePtr surface, TextureUsage usage, TextureFormat format, 
            uint width, uint height, PresentMode presentMode, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new SwapChainDescriptor
            {
                Label = marshalledLabel.Ptr,
                Usage = usage,
                Format = format,
                Width = width,
                Height = height,
                PresentMode = presentMode
            };

            return new SwapChainPtr(_wgpu, _wgpu.DeviceCreateSwapChain(_ptr, surface, in descriptor));
        }

        public TexturePtr CreateTexture(TextureUsage usage, TextureDimension dimension,
            Extent3D size, TextureFormat format, uint mipLevelCount, uint sampleCount, 
            ReadOnlySpan<TextureFormat> viewFormats, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            fixed(TextureFormat* viewFormatsPtr = viewFormats)
            {
                var descriptor = new TextureDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    Usage = usage,
                    Dimension = dimension,
                    Size = size,
                    Format = format,
                    MipLevelCount = mipLevelCount,
                    SampleCount = sampleCount,
                    ViewFormatCount = (uint)viewFormats.Length,
                    ViewFormats = viewFormatsPtr,
                };

                return new TexturePtr(_wgpu, _wgpu.DeviceCreateTexture(_ptr, in descriptor));
            }
        }

        public void Destroy() => _wgpu.DeviceDestroy(_ptr);

        public FeatureName[] EnumerateFeatures()
        {
            nuint count = _wgpu.DeviceEnumerateFeatures(_ptr, null);

            Span<FeatureName> span = stackalloc FeatureName[(int)count];
            _wgpu.DeviceEnumerateFeatures(_ptr, span);
            var arr = new FeatureName[count];

            span.CopyTo(arr);
            return arr;
        }

        public Limits GetLimits()
        {
            SupportedLimits limits = default;
            _wgpu.DeviceGetLimits(_ptr, &limits);
            Debug.Assert(limits.NextInChain == null);
            return limits.Limits;
        }

        public QueuePtr GetQueue() => new(_wgpu, _wgpu.DeviceGetQueue(_ptr));

        public bool HasFeature(FeatureName feature) => _wgpu.DeviceHasFeature(_ptr, feature);

        public Task<GPUError?> PopErrorScope()
        {
            var task = new TaskCompletionSource<GPUError?>();
            int key = s_popErrorScopeTasks.Rent(task);
            _wgpu.DevicePopErrorScope(_ptr, s_PopErrorScopeCallback, (void*)key);
            return task.Task;
        }

        public void PushErrorScope(ErrorFilter errorFilter) => _wgpu.DevicePushErrorScope(_ptr, errorFilter);

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);
            _wgpu.DeviceSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void SetUncapturedErrorCallback(ErrorCallback callback)
        {
            int idx = s_uncapturedErrorCallbacks.Count;
            s_uncapturedErrorCallbacks.Add(callback);
            _wgpu.DeviceSetUncapturedErrorCallback(_ptr, s_UncapturedErrorCallback, (void*)idx);
        }

        public void Reference() => _wgpu.DeviceReference(_ptr);

        public void Release() => _wgpu.DeviceRelease(_ptr);
    }
}
