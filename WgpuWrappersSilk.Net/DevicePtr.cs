using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using WGPU = Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
{
    public delegate void ErrorCallback(ErrorType type, string? message);

    public readonly unsafe struct DevicePtr
    {
        private static readonly List<(WebGPU, TaskCompletionSource<ComputePipelinePtr>)> s_computePipelineRequests = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void CreateComputePipelineCallback(CreatePipelineAsyncStatus status, ComputePipeline* pipeline, byte* message, void* data)
        {
            var (wgpu, task) = s_computePipelineRequests[(int)data];
            task.SetResult(new ComputePipelinePtr(wgpu, pipeline));
        }

        private static readonly List<(WebGPU, TaskCompletionSource<RenderPipelinePtr>)> s_renderPipelineRequests = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void CreateRenderPipelineCallback(CreatePipelineAsyncStatus status, RenderPipeline* pipeline, byte* message, void* data)
        {
            var (wgpu, task) = s_renderPipelineRequests[(int)data];
            task.SetResult(new RenderPipelinePtr(wgpu, pipeline));
        }

        private static readonly List<ErrorCallback> s_errorCallbacks = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void UncapturedErrorCallback(ErrorType type, byte* message, void* data)
        {
            s_errorCallbacks[(int)data].Invoke(
                type, 
                SilkMarshal.PtrToString((nint)message, NativeStringEncoding.UTF8)
            );
        }

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

            fixed (BindGroupEntry* entriesPtr = &entries[0])
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

            fixed (BindGroupLayoutEntry* entriesPtr = &entries[0])
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

        public ComputePipelinePtr CreateComputePipeline(ProgrammableStage stage, PipelineLayoutPtr layout, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new ComputePipelineDescriptor 
            {
                Label = marshalledLabel.Ptr,
                Layout = layout
            };

            Span<byte> span = stackalloc byte[stage.CalculatePayloadSize()];
            stage.PackInto(ref descriptor.Compute, span);

            return new ComputePipelinePtr(_wgpu, _wgpu.DeviceCreateComputePipeline(_ptr, in descriptor));
        }

        public Task<ComputePipelinePtr> CreateComputePipelineAsync(ProgrammableStage stage, PipelineLayoutPtr layout, string? label = null)
        {
            int idx = s_computePipelineRequests.Count;
            var task = new TaskCompletionSource<ComputePipelinePtr>();

            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new ComputePipelineDescriptor
            {
                Label = marshalledLabel.Ptr,
                Layout = layout
            };

            Span<byte> span = stackalloc byte[stage.CalculatePayloadSize()];
            stage.PackInto(ref descriptor.Compute, span);

            _wgpu.DeviceCreateComputePipelineAsync(_ptr, in descriptor, new(&CreateComputePipelineCallback), (void*)idx);

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

        public QuerySetPtr CreateQuerySet(QueryType type, int count, ReadOnlySpan<PipelineStatisticName> statistics = default, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            fixed (PipelineStatisticName* ptr = &statistics[0])
            {
                var descriptor = new QuerySetDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    Type = type,
                    Count = (uint)count,
                    PipelineStatisticsCount = (uint)statistics.Length,
                    PipelineStatistics = ptr,
                };

                return new QuerySetPtr(_wgpu, _wgpu.DeviceCreateQuerySet(_ptr, in descriptor));
            }
        }

        public RenderBundleEncoderPtr CreateRenderBundleEncoder(
            ReadOnlySpan<TextureFormat> colorFormats, TextureFormat depthFormat, 
            bool depthReadOnly, bool stencilReadOnly, uint sampleCount, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            fixed (TextureFormat* ptr = &colorFormats[0])
            {
                var descriptor = new RenderBundleEncoderDescriptor
                {
                    Label = marshalledLabel.Ptr,
                    ColorFormatsCount = (uint)colorFormats.Length,
                    ColorFormats = ptr,
                    DepthStencilFormat = depthFormat,
                    DepthReadOnly = depthReadOnly,
                    StencilReadOnly = stencilReadOnly,
                    SampleCount = sampleCount
                };

                return new RenderBundleEncoderPtr(_wgpu, _wgpu.DeviceCreateRenderBundleEncoder(_ptr, in descriptor));
            }
        }

        public RenderPipelinePtr CreateRenderPipeline(
            PipelineLayoutPtr layout, 
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
                (fragment.HasValue ? fragment.Value.CalculatePayloadSize() : 0)];

            var vs = default(WGPU.VertexState);
            indexPtr += vertex.PackInto(ref vs, payloadBuffer);

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
                Layout = layout,
                Vertex = vs,
                Primitive = primitive,
                DepthStencil = &ds,
                Multisample = multisample,
                Fragment = fsPtr
            };

            return new RenderPipelinePtr(_wgpu, _wgpu.DeviceCreateRenderPipeline(_ptr, in descriptor));
        }

        public Task<RenderPipelinePtr> CreateRenderPipelineAsync(
            PipelineLayoutPtr layout,
            VertexState vertex,
            PrimitiveState primitive,
            DepthStencilState? depthStencil,
            MultisampleState multisample,
            FragmentState? fragment,
            string? label = null)
        {

            int idx = s_computePipelineRequests.Count;
            var task = new TaskCompletionSource<RenderPipelinePtr>();

            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            int indexPtr = 0;

            Span<byte> payloadBuffer = stackalloc byte[vertex.CalculatePayloadSize() +
                (fragment.HasValue ? fragment.Value.CalculatePayloadSize() : 0)];

            var vs = default(WGPU.VertexState);
            indexPtr += vertex.PackInto(ref vs, payloadBuffer);

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
                Layout = layout,
                Vertex = vs,
                Primitive = primitive,
                DepthStencil = &ds,
                Multisample = multisample,
                Fragment = fsPtr
            };

            _wgpu.DeviceCreateRenderPipelineAsync(_ptr, in descriptor, new(&CreateRenderPipelineCallback), (void*)idx);

            return task.Task;
        }

        public SamplerPtr CreateSampler(
            AddressMode addressModeU, AddressMode addressModeV, AddressMode addressModeW,
            FilterMode magFilter, FilterMode minFilter, MipmapFilterMode mipmapFilter,
            float lodMinClamp, float lodMaxClamp,
            CompareFunction compare,
            ushort maxAnisotropy,
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
            Span<byte> bytes = new byte[Encoding.UTF8.GetByteCount(code)+1];
            Encoding.UTF8.GetBytes(code, bytes);
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

            fixed(byte* codePtr = &code[0])
            {
                var wgslDescriptor = new ShaderModuleWGSLDescriptor
                {
                    Chain = new ChainedStruct
                    {
                        SType = SType.ShaderModuleWgsldescriptor
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

            fixed (byte* code32Ptr = &code[0])
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

            fixed(uint* codePtr = &code[0])
            {
                var wgslDescriptor = new ShaderModuleSPIRVDescriptor
                {
                    Chain = new ChainedStruct
                    {
                        SType = SType.ShaderModuleSpirvdescriptor
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

        public void SetUncapturedErrorCallback(ErrorCallback callback)
        {
            int idx = s_errorCallbacks.Count;
            s_errorCallbacks.Add(callback);
            _wgpu.DeviceSetUncapturedErrorCallback(_ptr, new PfnErrorCallback(&UncapturedErrorCallback), (void*)idx);
        }
    }
}
