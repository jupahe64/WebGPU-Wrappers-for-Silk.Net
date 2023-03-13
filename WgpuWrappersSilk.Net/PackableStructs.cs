using Silk.NET.WebGPU;
using System.Text;
using WGPU = Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
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
            int size = Encoding.UTF8.GetByteCount(EntryPoint)+1;
            size += sizeof(ConstantEntry) * Constants.Length;
            for (int i = 0; i < Constants.Length; i++)
            {
                size += Encoding.UTF8.GetByteCount(Constants[i].key)+1;
            }
            return size;
        }

        internal int PackInto(ref ProgrammableStageDescriptor baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = &payloadBuffer[0])
            {
                var ptr = startPtr;

                var constants = (ConstantEntry*)ptr;
                ptr += sizeof(ConstantEntry) * Constants.Length;

                baseStruct.Module = ShaderModule;
                baseStruct.ConstantCount = (uint)Constants.Length;
                baseStruct.Constants = constants;

                baseStruct.EntryPoint = ptr;
                ptr += Encoding.UTF8.GetBytes(EntryPoint, payloadBuffer[(int)(ptr - startPtr)..])+1;

                for (int i = 0; i < Constants.Length; i++)
                {
                    constants[i].Key = ptr;
                    ptr += Encoding.UTF8.GetBytes(Constants[i].key, payloadBuffer[(int)(ptr - startPtr)..])+1;

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
            int size = Encoding.UTF8.GetByteCount(EntryPoint)+1;
            size += sizeof(ConstantEntry) * Constants.Length;
            for (int i = 0; i < Constants.Length; i++)
                size += Encoding.UTF8.GetByteCount(Constants[i].key)+1;

            size += sizeof(WGPU.VertexBufferLayout) * Buffers.Length;
            for (int i = 0; i < Buffers.Length; i++)
                size += Buffers[i].CalculatePayloadSize();

            return size;
        }

        internal int PackInto(ref WGPU.VertexState baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = &payloadBuffer[0])
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
                ptr += Encoding.UTF8.GetBytes(EntryPoint, payloadBuffer[(int)(ptr - startPtr)..])+1;

                for (int i = 0; i < Constants.Length; i++)
                {
                    constants[i].Key = ptr;
                    ptr += Encoding.UTF8.GetBytes(Constants[i].key, payloadBuffer[(int)(ptr - startPtr)..])+1;

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

            fixed(byte* startPtr = &payloadBuffer[0])
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
            int size = Encoding.UTF8.GetByteCount(EntryPoint)+1;
            size += sizeof(ConstantEntry) * Constants.Length;
            for (int i = 0; i < Constants.Length; i++)
                size += Encoding.UTF8.GetByteCount(Constants[i].key)+1;

            size += sizeof(WGPU.ColorTargetState) * ColorTargets.Length;
            for (int i = 0; i < ColorTargets.Length; i++)
                size += ColorTargets[i].CalculatePayloadSize();

            return size;
        }

        internal int PackInto(ref WGPU.FragmentState baseStruct, Span<byte> payloadBuffer)
        {
            int payloadSize;

            fixed(byte* startPtr = &payloadBuffer[0])
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
                ptr += Encoding.UTF8.GetBytes(EntryPoint, payloadBuffer[(int)(ptr - startPtr)..])+1;

                for (int i = 0; i < Constants.Length; i++)
                {
                    constants[i].Key = ptr;
                    ptr += Encoding.UTF8.GetBytes(Constants[i].key, payloadBuffer[(int)(ptr - startPtr)..])+1;

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

            fixed(byte* startPtr = &payloadBuffer[0])
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
}
