using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WGPU = Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
{
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

        public BindGroupLayoutPtr CreateBindGroupLayout(BindGroupLayoutPtr bindGroupLayout, ReadOnlySpan<BindGroupLayoutEntry> entries, string? label = null)
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
    }
}
