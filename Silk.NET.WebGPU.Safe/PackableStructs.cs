using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Silk.NET.Core.Attributes;
using Silk.NET.Core.Native;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    internal unsafe struct PayloadSizeTracker
    {
        private int _payloadSize;
        private int _stringPoolSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddStruct<T>() where T : unmanaged
        {
            _payloadSize += sizeof(T);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOptional<T>(bool isPresent) where T : unmanaged
        {
            _payloadSize += isPresent ? sizeof(T) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOptional<T>(T? value) where T : unmanaged
        {
            _payloadSize += value.HasValue ? sizeof(T) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddArray<T>(int count) where T : unmanaged
        {
            _payloadSize += sizeof(T) * count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddString(string? str, NativeStringEncoding encoding)
        {
            _stringPoolSize += SilkMarshal.GetMaxSizeOf(str, encoding);
        }

        public readonly void GetSize(out int size, out int stringPoolOffset)
        {
            size = _payloadSize + _stringPoolSize;
            stringPoolOffset = _payloadSize;
        }
    }

    internal unsafe struct PayloadAllocator
    {
        private byte* _payloadPtr;
        private byte* _stringPoolPtr;
        private byte* _payloadEnd;

        public PayloadAllocator(int totalSize, byte* payloadPtr, byte* stringPoolPtr) : this()
        {
            _payloadPtr = payloadPtr;
            _stringPoolPtr = stringPoolPtr;
            _payloadEnd = payloadPtr + totalSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* AddStruct<T>() where T : unmanaged
        {
            var ptr = (T*)_payloadPtr;
            _payloadPtr += sizeof(T);
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* AddOptional<T>(in T? value) where T : unmanaged
        {
            if (value is null)
                return null;

            var ptr = (T*)_payloadPtr;
            *ptr = value!.Value;
            _payloadPtr += sizeof(T);
            return ptr;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* AddArray<T>(int count) where T : unmanaged
        {
            var ptr = (T*)_payloadPtr;
            _payloadPtr += sizeof(T) * count;
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* AddString(string? str, NativeStringEncoding encoding)
        {
            var ptr = _stringPoolPtr;

            SilkMarshal.StringIntoSpan(str, 
                new Span<byte>(_stringPoolPtr, (int)(_payloadEnd - _stringPoolPtr)));
            _stringPoolPtr += SilkMarshal.GetMaxSizeOf(str, encoding);

            return ptr;
        }
    }

    public unsafe struct ProgrammableStage
    {
        public ShaderModulePtr Module;
        public string EntryPoint;
        public (string key, double value)[] Constants;

        public ProgrammableStage(ShaderModulePtr shaderModule, string entryPoint,
            (string key, double value)[] constants)
        {
            Module = shaderModule;
            EntryPoint = entryPoint;
            Constants = constants;
        }
        
        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddString(EntryPoint, NativeStringEncoding.UTF8);
            payloadSize.AddArray<ConstantEntry>(Constants.Length);
            for (int i = 0; i < Constants.Length; i++)
            {
                payloadSize.AddString(Constants[i].key, NativeStringEncoding.UTF8);
            }
        }

        internal readonly void PackInto(ref ProgrammableStageDescriptor baseStruct, ref PayloadAllocator payload)
        {
            baseStruct.Module = Module;
            baseStruct.ConstantCount = (uint)Constants.Length;
            baseStruct.Constants = payload.AddArray<ConstantEntry>(Constants.Length);

            baseStruct.EntryPoint = payload.AddString(EntryPoint, NativeStringEncoding.UTF8);
                

            for (int i = 0; i < Constants.Length; i++)
            {
                baseStruct.Constants[i].Key = payload.AddString(Constants[i].key, NativeStringEncoding.UTF8);
                baseStruct.Constants[i].Value = Constants[i].value;
            }
        }
    }

    public unsafe struct VertexState
    {
        public ShaderModulePtr Module;
        public string EntryPoint;
        public (string key, double value)[] Constants;
        public VertexBufferLayout[] Buffers;

        public VertexState(ShaderModulePtr module, string entryPoint,
            (string key, double value)[] constants, VertexBufferLayout[] buffers)
        {
            Module = module;
            EntryPoint = entryPoint;
            Constants = constants;
            Buffers = buffers;
        }

        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddString(EntryPoint, NativeStringEncoding.UTF8);
            payloadSize.AddArray<ConstantEntry>(Constants.Length);
            for (int i = 0; i < Constants.Length; i++)
                payloadSize.AddString(Constants[i].key, NativeStringEncoding.UTF8);

            payloadSize.AddArray<WGPU.VertexBufferLayout>(Buffers.Length);
            for (int i = 0; i < Buffers.Length; i++)
                Buffers[i].CalculatePayloadSize(ref payloadSize);
        }

        internal readonly void PackInto(ref WGPU.VertexState baseStruct, ref PayloadAllocator payload)
        {
            baseStruct.Module = Module;
            baseStruct.ConstantCount = (uint)Constants.Length;
            baseStruct.Constants = payload.AddArray<ConstantEntry>(Constants.Length);
            baseStruct.BufferCount = (uint)Buffers.Length;
            baseStruct.Buffers = payload.AddArray<WGPU.VertexBufferLayout>(Buffers.Length);

            baseStruct.EntryPoint = payload.AddString(EntryPoint, NativeStringEncoding.UTF8);

            for (int i = 0; i < Constants.Length; i++)
            {
                baseStruct.Constants[i].Key = payload.AddString(Constants[i].key, NativeStringEncoding.UTF8);
                baseStruct.Constants[i].Value = Constants[i].value;
            }

            for (int i = 0; i < Buffers.Length; i++)
            {
                Buffers[i].PackInto(ref baseStruct.Buffers[i], ref payload);
            }
        }
    }

    public unsafe struct PrimitiveState
    {
        public PrimitiveTopology Topology;
        public IndexFormat StripIndexFormat;
        public FrontFace FrontFace;
        public CullMode CullMode;
        public bool UnclippedDepth;

        public PrimitiveState(PrimitiveTopology topology, 
            IndexFormat stripIndexFormat, FrontFace frontFace, 
            CullMode cullMode, bool unclippedDepth)
        {
            Topology = topology;
            StripIndexFormat = stripIndexFormat;
            FrontFace = frontFace;
            CullMode = cullMode;
            UnclippedDepth = unclippedDepth;
        }

        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddStruct<PrimitiveDepthClipControl>();
        }

        internal readonly void PackInto(ref Silk.NET.WebGPU.PrimitiveState baseStruct, ref PayloadAllocator payload)
        {
            var clipControl = payload.AddStruct<PrimitiveDepthClipControl>();
            clipControl->UnclippedDepth = UnclippedDepth;
            clipControl->Chain.SType = SType.PrimitiveDepthClipControl;

            baseStruct.Topology = Topology;
            baseStruct.StripIndexFormat = StripIndexFormat;
            baseStruct.FrontFace = FrontFace;
            baseStruct.CullMode = CullMode;

            baseStruct.NextInChain = (ChainedStruct*)clipControl;
        }
    }

    public unsafe struct VertexBufferLayout
    {
        public ulong ArrayStride;
        public VertexStepMode StepMode;
        public VertexAttribute[] Attributes;

        public VertexBufferLayout(ulong arrayStride, VertexStepMode stepMode, VertexAttribute[] attributes)
        {
            this.ArrayStride = arrayStride;
            this.StepMode = stepMode;
            this.Attributes = attributes;
        }

        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddArray<VertexAttribute>(Attributes.Length);
        }

        internal readonly void PackInto(ref Silk.NET.WebGPU.VertexBufferLayout baseStruct, ref PayloadAllocator payload)
        {
            baseStruct.ArrayStride = ArrayStride;
            baseStruct.StepMode = StepMode;
            baseStruct.AttributeCount = (uint)Attributes.Length;
            baseStruct.Attributes = payload.AddArray<VertexAttribute>(Attributes.Length);

            for (int i = 0; i < Attributes.Length; i++)
                baseStruct.Attributes[i] = Attributes[i];
        }
    }

    public unsafe struct FragmentState
    {
        public ShaderModulePtr Module;
        public string EntryPoint;
        public (string key, double value)[] Constants;
        public ColorTargetState[] Targets;

        public FragmentState(ShaderModulePtr module, string entryPoint,
            (string key, double value)[] constants, ColorTargetState[] colorTargets)
        {
            Module = module;
            EntryPoint = entryPoint;
            Constants = constants;
            Targets = colorTargets;
        }

        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddString(EntryPoint, NativeStringEncoding.UTF8);
            payloadSize.AddArray<ConstantEntry>(Constants.Length);
            for (int i = 0; i < Constants.Length; i++)
                payloadSize.AddString(Constants[i].key, NativeStringEncoding.UTF8);

            payloadSize.AddArray<WGPU.ColorTargetState>(Targets.Length);
            for (int i = 0; i < Targets.Length; i++)
                Targets[i].CalculatePayloadSize(ref payloadSize);
        }

        internal readonly void PackInto(ref WGPU.FragmentState baseStruct, ref PayloadAllocator payload)
        {
            baseStruct.Module = Module;
            baseStruct.ConstantCount = (uint)Constants.Length;
            baseStruct.Constants = payload.AddArray<ConstantEntry>(Constants.Length);
            baseStruct.TargetCount = (uint)Targets.Length;
            baseStruct.Targets = payload.AddArray<WGPU.ColorTargetState>(Targets.Length);

            baseStruct.EntryPoint = payload.AddString(EntryPoint, NativeStringEncoding.UTF8);

            for (int i = 0; i < Constants.Length; i++)
            {
                baseStruct.Constants[i].Key = payload.AddString(Constants[i].key, NativeStringEncoding.UTF8);
                baseStruct.Constants[i].Value = Constants[i].value;
            }

            for (int i = 0; i < Targets.Length; i++)
            {
                Targets[i].PackInto(ref baseStruct.Targets[i], ref payload);
            }
        }
    }

    public unsafe struct ColorTargetState
    {
        public TextureFormat Format;
        public (BlendComponent color, BlendComponent alpha)? BlendState;
        public ColorWriteMask WriteMask;


        public ColorTargetState(TextureFormat format, (BlendComponent color, BlendComponent alpha)? blendState, ColorWriteMask writeMask)
        {
            Format = format;
            BlendState = blendState;
            WriteMask = writeMask;
        }

        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddOptional<BlendState>(BlendState.HasValue);
        }

        internal readonly void PackInto(ref Silk.NET.WebGPU.ColorTargetState baseStruct, ref PayloadAllocator payload)
        {
            baseStruct.Format = Format;
                baseStruct.WriteMask = WriteMask;

            if (BlendState.HasValue)
            {
                var blendState = payload.AddStruct<BlendState>();
                blendState->Color = BlendState.Value.color;
                blendState->Alpha = BlendState.Value.alpha;
                baseStruct.Blend = blendState;
            }
        }
    }

    public unsafe struct ShaderModuleCompilationHint
    {
        public string EntryPoint;
        public PipelineLayoutPtr PipelineLayout;

        public ShaderModuleCompilationHint(string entryPoint, PipelineLayoutPtr pipelineLayout)
        {
            EntryPoint = entryPoint;
            PipelineLayout = pipelineLayout;
        }

        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddString(EntryPoint, NativeStringEncoding.UTF8);
        }

        internal readonly void PackInto(ref WGPU.ShaderModuleCompilationHint baseStruct, ref PayloadAllocator payload)
        {
            baseStruct.Layout = PipelineLayout;
            baseStruct.EntryPoint = payload.AddString(EntryPoint, NativeStringEncoding.UTF8);
        }
    }

    public unsafe struct RenderPipelineDescriptor
    {
        public PipelineLayoutPtr? Layout;
        public VertexState Vertex;
        public PrimitiveState Primitive;
        public DepthStencilState? DepthStencil;
        public MultisampleState Multisample;
        public FragmentState? Fragment;
        public string? Label;

        public RenderPipelineDescriptor(PipelineLayoutPtr? layout, 
            VertexState vertex, PrimitiveState primitive, DepthStencilState? depthStencil, 
            MultisampleState multisample, FragmentState? fragment, string? label = null)
        {
            Layout = layout;
            Vertex = vertex;
            Primitive = primitive;
            DepthStencil = depthStencil;
            Multisample = multisample;
            Fragment = fragment;
            Label = label;
        }

        internal readonly void CalculatePayloadSize(ref PayloadSizeTracker payloadSize)
        {
            payloadSize.AddString(Label, NativeStringEncoding.UTF8);

            Vertex.CalculatePayloadSize(ref payloadSize);
            Primitive.CalculatePayloadSize(ref payloadSize);
            payloadSize.AddOptional(DepthStencil);

            if (Fragment.HasValue)
            {
                payloadSize.AddStruct<WGPU.FragmentState>();
                Fragment.Value.CalculatePayloadSize(ref payloadSize);
            }
        }

        internal readonly void PackInto(ref WGPU.RenderPipelineDescriptor baseStruct, ref PayloadAllocator payload)
        {
            baseStruct.Label = payload.AddString(Label, NativeStringEncoding.UTF8);
            baseStruct.Layout = Layout ?? (PipelineLayout*)null;
            baseStruct.Multisample = Multisample;

            Vertex.PackInto(ref baseStruct.Vertex, ref payload);
            Primitive.PackInto(ref baseStruct.Primitive, ref payload);

            baseStruct.DepthStencil = payload.AddOptional(in DepthStencil);


            if (Fragment.HasValue)
            {
                baseStruct.Fragment = payload.AddStruct<WGPU.FragmentState>();

                Fragment.Value.PackInto(ref *baseStruct.Fragment, ref payload);
            }
        }
    }

    internal unsafe static class ShaderModuleDescriptor
    {
        internal static void CalculatePayloadSize(ref PayloadSizeTracker payloadSize, 
            string? label, ReadOnlySpan<Safe.ShaderModuleCompilationHint> compilationHints)
        {
            payloadSize.AddString(label, NativeStringEncoding.UTF8);

            payloadSize.AddArray<WGPU.ShaderModuleCompilationHint>(compilationHints.Length);
            for (int i = 0; i < compilationHints.Length; i++)
            {
                compilationHints[i].CalculatePayloadSize(ref payloadSize);
            }
        }

        internal static void PackInto(ref WGPU.ShaderModuleDescriptor baseStruct, ref PayloadAllocator payload,
            string? label, ReadOnlySpan<Safe.ShaderModuleCompilationHint> compilationHints)
        {
            baseStruct.Label = payload.AddString(label, NativeStringEncoding.UTF8);

            baseStruct.HintCount = (uint)compilationHints.Length;
            baseStruct.Hints = payload.AddArray<WGPU.ShaderModuleCompilationHint>(compilationHints.Length);
            for (int i = 0; i < compilationHints.Length; i++)
            {
                compilationHints[i].PackInto(ref baseStruct.Hints[i], ref payload);
            }
        }
    }

    public unsafe struct ImageCopyBuffer
    {
        public BufferPtr Buffer;
        
        public TextureDataLayout Layout;

        public ImageCopyBuffer(BufferPtr buffer, TextureDataLayout layout)
        {
            Buffer = buffer;
            Layout = layout;
        }

        internal WGPU.ImageCopyBuffer Pack()
        {
            return new WGPU.ImageCopyBuffer 
            {
                Buffer = Buffer,
                Layout = Layout,
            };
        }
    }

    public unsafe struct ImageCopyTexture
    {
        public TexturePtr Texture;

        public uint MipLevel;

        public Origin3D Origin;

        public TextureAspect Aspect;

        public ImageCopyTexture(TexturePtr texture, uint mipLevel, Origin3D origin, TextureAspect aspect)
        {
            Texture = texture;
            MipLevel = mipLevel;
            Origin = origin;
            Aspect = aspect;
        }

        internal WGPU.ImageCopyTexture Pack()
        {
            return new WGPU.ImageCopyTexture 
            {
                Texture = Texture,
                MipLevel = MipLevel,
                Origin = Origin,
                Aspect = Aspect,
            };
        }
    }

    public unsafe struct ComputePassTimestampWrite
    {
        public ComputePassTimestampLocation Location;
        public uint QueryIndex;
        public QuerySetPtr QuerySet;

        public ComputePassTimestampWrite(ComputePassTimestampLocation location, uint queryIndex, QuerySetPtr querySet)
        {
            Location = location;
            QueryIndex = queryIndex;
            QuerySet = querySet;
        }

        internal WGPU.ComputePassTimestampWrite Pack()
        {
            return new WGPU.ComputePassTimestampWrite
            {
                Location = Location,
                QueryIndex = QueryIndex,
                QuerySet = QuerySet
            };
        }
    }

    public unsafe struct RenderPassTimestampWrite
    {
        public RenderPassTimestampLocation Location;
        public uint QueryIndex;
        public QuerySetPtr QuerySet;

        public RenderPassTimestampWrite(RenderPassTimestampLocation location, uint queryIndex, QuerySetPtr querySet)
        {
            Location = location;
            QueryIndex = queryIndex;
            QuerySet = querySet;
        }

        internal WGPU.RenderPassTimestampWrite Pack()
        {
            return new WGPU.RenderPassTimestampWrite
            {
                Location = Location,
                QueryIndex = QueryIndex,
                QuerySet = QuerySet
            };
        }
    }

    public unsafe partial struct RenderPassColorAttachment
    {
        public TextureViewPtr View;
        public TextureViewPtr? ResolveTarget;
        public LoadOp LoadOp;
        public StoreOp StoreOp;
        public Color ClearValue;

        public RenderPassColorAttachment(TextureViewPtr view, TextureViewPtr? resolveTarget, LoadOp loadOp, StoreOp storeOp, Color clearValue)
        {
            View = view;
            ResolveTarget = resolveTarget;
            LoadOp = loadOp;
            StoreOp = storeOp;
            ClearValue = clearValue;
        }

        internal WGPU.RenderPassColorAttachment Pack()
        {
            return new WGPU.RenderPassColorAttachment
            {
                View = View,
                ResolveTarget = ResolveTarget.GetValueOrDefault(),
                LoadOp = LoadOp,
                StoreOp = StoreOp,
                ClearValue = ClearValue
            };
        }
    }

    public unsafe partial struct RenderPassDepthStencilAttachment
    {       
        public TextureViewPtr View;
        public LoadOp DepthLoadOp;
        public StoreOp DepthStoreOp;
        public float DepthClearValue;
        public bool DepthReadOnly;
        public LoadOp StencilLoadOp;
        public StoreOp StencilStoreOp;
        public uint StencilClearValue;
        public bool StencilReadOnly;

        public RenderPassDepthStencilAttachment(TextureViewPtr view, 
            LoadOp depthLoadOp, StoreOp depthStoreOp, float depthClearValue, bool depthReadOnly, 
            LoadOp stencilLoadOp, StoreOp stencilStoreOp, uint stencilClearValue, bool stencilReadOnly)
        {
            View = view;
            DepthLoadOp = depthLoadOp;
            DepthStoreOp = depthStoreOp;
            DepthClearValue = depthClearValue;
            DepthReadOnly = depthReadOnly;
            StencilLoadOp = stencilLoadOp;
            StencilStoreOp = stencilStoreOp;
            StencilClearValue = stencilClearValue;
            StencilReadOnly = stencilReadOnly;
        }

        internal WGPU.RenderPassDepthStencilAttachment Pack()
        {
            return new WGPU.RenderPassDepthStencilAttachment
            {
                View = View,
                DepthLoadOp = DepthLoadOp,
                DepthStoreOp = DepthStoreOp,
                DepthClearValue = DepthClearValue,
                DepthReadOnly = DepthReadOnly,
                StencilLoadOp = StencilLoadOp,
                StencilStoreOp = StencilStoreOp,
                StencilClearValue = StencilClearValue,
                StencilReadOnly = StencilReadOnly
            };
        }
    }
}
