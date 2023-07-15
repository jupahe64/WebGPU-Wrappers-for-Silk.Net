using System;
using System.Text;
using Silk.NET.Core.Native;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    public unsafe struct ProgrammableStage
    {
        public ShaderModulePtr ShaderModule;
        public string EntryPoint;
        public (string key, double value)[] Constants;

        public ProgrammableStage(ShaderModulePtr shaderModule, string entryPoint,
            (string key, double value)[] constants)
        {
            ShaderModule = shaderModule;
            EntryPoint = entryPoint;
            Constants = constants;
        }
        
        internal int CalculatePayloadSize()
        {
            int size = SilkMarshal.GetMaxSizeOf(EntryPoint, NativeStringEncoding.UTF8)+1;
            size += sizeof(ConstantEntry) * Constants.Length;
            for (int i = 0; i < Constants.Length; i++)
            {
                size += SilkMarshal.GetMaxSizeOf(Constants[i].key, NativeStringEncoding.UTF8);
            }
            return size;
        }

        internal int PackInto(ref ProgrammableStageDescriptor baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = payloadBuffer)
            {
                var ptr = startPtr;

                var constants = (ConstantEntry*)ptr;
                ptr += sizeof(ConstantEntry) * Constants.Length;

                baseStruct.Module = ShaderModule;
                baseStruct.ConstantCount = (uint)Constants.Length;
                baseStruct.Constants = constants;

                baseStruct.EntryPoint = ptr;
                
                ptr += SilkMarshal.StringIntoSpan(EntryPoint, payloadBuffer[(int) (ptr - startPtr)..], 
                    NativeStringEncoding.UTF8);
                

                for (int i = 0; i < Constants.Length; i++)
                {
                    constants[i].Key = ptr;
                    ptr += SilkMarshal.StringIntoSpan(Constants[i].key, payloadBuffer[(int)(ptr - startPtr)..], 
                        NativeStringEncoding.UTF8);

                    constants[i].Value = Constants[i].value;
                }

                payloadSize = (int)(ptr - startPtr);
            }
            return payloadSize;
        }
    }

    public unsafe struct VertexState
    {
        public ShaderModulePtr ShaderModule;
        public string EntryPoint;
        public (string key, double value)[] Constants;
        public VertexBufferLayout[] Buffers;

        public VertexState(ShaderModulePtr shaderModule, string entryPoint,
            (string key, double value)[] constants, VertexBufferLayout[] buffers)
        {
            ShaderModule = shaderModule;
            EntryPoint = entryPoint;
            Constants = constants;
            Buffers = buffers;
        }

        internal int CalculatePayloadSize()
        {
            int size = SilkMarshal.GetMaxSizeOf(EntryPoint, NativeStringEncoding.UTF8);
            size += sizeof(ConstantEntry) * Constants.Length;
            for (int i = 0; i < Constants.Length; i++)
                size += SilkMarshal.GetMaxSizeOf(Constants[i].key, NativeStringEncoding.UTF8);

            size += sizeof(WGPU.VertexBufferLayout) * Buffers.Length;
            for (int i = 0; i < Buffers.Length; i++)
                size += Buffers[i].CalculatePayloadSize();

            return size;
        }

        internal int PackInto(ref WGPU.VertexState baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = payloadBuffer)
            {
                var ptr = startPtr;

                var constants = (ConstantEntry*)ptr;
                ptr += sizeof(ConstantEntry) * Constants.Length;

                var buffers = (WGPU.VertexBufferLayout*)ptr;
                ptr += sizeof(WGPU.VertexBufferLayout) * Buffers.Length;

                baseStruct.Module = ShaderModule;
                baseStruct.ConstantCount = (uint)Constants.Length;
                baseStruct.Constants = constants;
                baseStruct.BufferCount = (uint)Buffers.Length;
                baseStruct.Buffers = buffers;

                baseStruct.EntryPoint = ptr;
                ptr += SilkMarshal.StringIntoSpan(EntryPoint, payloadBuffer[(int)(ptr - startPtr)..], 
                    NativeStringEncoding.UTF8);

                for (int i = 0; i < Constants.Length; i++)
                {
                    constants[i].Key = ptr;
                    ptr += SilkMarshal.StringIntoSpan(Constants[i].key, payloadBuffer[(int)(ptr - startPtr)..], 
                        NativeStringEncoding.UTF8);

                    constants[i].Value = Constants[i].value;
                }

                for (int i = 0; i < Buffers.Length; i++)
                {
                    var subBuffer = payloadBuffer.Slice((int)(ptr - startPtr));
                    ptr += Buffers[i].PackInto(ref buffers[i], subBuffer);
                }

                payloadSize = (int)(ptr - startPtr);
            }
            return payloadSize;
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

        internal int CalculatePayloadSize()
        {
            return sizeof(VertexAttribute) * Attributes.Length;
        }

        internal int PackInto(ref Silk.NET.WebGPU.VertexBufferLayout baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = payloadBuffer)
            {
                var ptr = startPtr;

                var attributes = (VertexAttribute*)ptr;
                ptr += sizeof(VertexAttribute) * Attributes.Length;

                baseStruct.ArrayStride = ArrayStride;
                baseStruct.StepMode = StepMode;
                baseStruct.AttributeCount = (uint)Attributes.Length;
                baseStruct.Attributes = attributes;

                var attribute = attributes;
                for (int i = 0; i < Attributes.Length; i++)
                    attributes[i] = Attributes[i];

                payloadSize = (int)(ptr - startPtr);
            }
            return payloadSize;
        }
    }

    public unsafe struct FragmentState
    {
        public ShaderModulePtr ShaderModule;
        public string EntryPoint;
        public (string key, double value)[] Constants;
        public ColorTargetState[] ColorTargets;

        public FragmentState(ShaderModulePtr shaderModule, string entryPoint,
            (string key, double value)[] constants, ColorTargetState[] colorTargets)
        {
            ShaderModule = shaderModule;
            EntryPoint = entryPoint;
            Constants = constants;
            ColorTargets = colorTargets;
        }

        internal int CalculatePayloadSize()
        {
            int size = SilkMarshal.GetMaxSizeOf(EntryPoint, NativeStringEncoding.UTF8);
            size += sizeof(ConstantEntry) * Constants.Length;
            for (int i = 0; i < Constants.Length; i++)
                size += SilkMarshal.GetMaxSizeOf(Constants[i].key, NativeStringEncoding.UTF8);

            size += sizeof(WGPU.ColorTargetState) * ColorTargets.Length;
            for (int i = 0; i < ColorTargets.Length; i++)
                size += ColorTargets[i].CalculatePayloadSize();

            return size;
        }

        internal int PackInto(ref WGPU.FragmentState baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = payloadBuffer)
            {
                var ptr = startPtr;

                var constants = (ConstantEntry*)ptr;
                ptr += sizeof(ConstantEntry) * Constants.Length;

                var targets = (WGPU.ColorTargetState*)ptr;
                ptr += sizeof(WGPU.ColorTargetState) * ColorTargets.Length;

                baseStruct.Module = ShaderModule;
                baseStruct.ConstantCount = (uint)Constants.Length;
                baseStruct.Constants = constants;
                baseStruct.TargetCount = (uint)ColorTargets.Length;
                baseStruct.Targets = targets;

                baseStruct.EntryPoint = ptr;
                ptr += SilkMarshal.StringIntoSpan(EntryPoint, payloadBuffer[(int)(ptr - startPtr)..], 
                    NativeStringEncoding.UTF8);

                for (int i = 0; i < Constants.Length; i++)
                {
                    constants[i].Key = ptr;
                    ptr += SilkMarshal.StringIntoSpan(Constants[i].key, payloadBuffer[(int)(ptr - startPtr)..], 
                        NativeStringEncoding.UTF8);

                    constants[i].Value = Constants[i].value;
                }

                for (int i = 0; i < ColorTargets.Length; i++)
                {
                    var subBuffer = payloadBuffer.Slice((int)(ptr - startPtr));
                    ptr += ColorTargets[i].PackInto(ref targets[i], subBuffer);
                }

                payloadSize = (int)(ptr - startPtr);
            }
            return payloadSize;
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

        internal int CalculatePayloadSize()
        {
            return BlendState.HasValue ? sizeof(BlendState): 0;
        }

        internal int PackInto(ref Silk.NET.WebGPU.ColorTargetState baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = payloadBuffer)
            {
                var ptr = startPtr;

                baseStruct.Format = Format;
                baseStruct.WriteMask = WriteMask;

                if (BlendState.HasValue)
                {
                    var blendState = (BlendState*)ptr;
                    ptr += sizeof(BlendState);
                    blendState[0].Color = BlendState.Value.color;
                    blendState[0].Alpha = BlendState.Value.alpha;
                    baseStruct.Blend = blendState;
                }

                payloadSize = (int)(ptr - startPtr);
            }
            return payloadSize;
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

        internal int CalculatePayloadSize()
        {
            return SilkMarshal.GetMaxSizeOf(EntryPoint, NativeStringEncoding.UTF8);
        }

        internal int PackInto(ref WGPU.ShaderModuleCompilationHint baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = payloadBuffer)
            {
                baseStruct.Layout = PipelineLayout;

                var ptr = startPtr;

                baseStruct.EntryPoint = ptr;
                ptr += SilkMarshal.StringIntoSpan(EntryPoint, payloadBuffer[(int)(ptr - startPtr)..], 
                    NativeStringEncoding.UTF8);

                payloadSize = (int)(ptr - startPtr);
            }
            return payloadSize;
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
