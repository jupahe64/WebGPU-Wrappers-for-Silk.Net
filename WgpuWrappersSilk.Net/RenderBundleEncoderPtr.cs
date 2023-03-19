using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace WgpuWrappersSilk.Net
{
    public readonly unsafe struct RenderBundleEncoderPtr
    {
        private readonly WebGPU _wgpu;
        private readonly RenderBundleEncoder* _ptr;

        public RenderBundleEncoderPtr(WebGPU wgpu, RenderBundleEncoder* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator RenderBundleEncoder*(RenderBundleEncoderPtr ptr) => ptr._ptr;

        public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        {
            _wgpu.RenderBundleEncoderDraw(_ptr, vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
        {
            _wgpu.RenderBundleEncoderDrawIndexed(_ptr, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
        }

        public void DrawIndexedIndirect(BufferPtr indirectBuffer, ulong indirectOffset)
        {
            _wgpu.RenderBundleEncoderDrawIndexedIndirect(_ptr, indirectBuffer, indirectOffset);
        }

        public void DrawIndirect(BufferPtr indirectBuffer, ulong indirectOffset)
        {
            _wgpu.RenderBundleEncoderDrawIndirect(_ptr, indirectBuffer, indirectOffset);
        }

        public RenderBundlePtr Finish(string renderBundleLabel)
        {
            using var marshalledLabel = new MarshalledString(renderBundleLabel, NativeStringEncoding.UTF8);

            RenderBundleDescriptor descriptor = new(
                label: marshalledLabel.Ptr
            );

            return new(_wgpu, _wgpu.RenderBundleEncoderFinish(_ptr, in descriptor));
        }

        public void InsertDebugMarker(string markerLabel)
        {
            using var marshalledLabel = new MarshalledString(markerLabel, NativeStringEncoding.UTF8);
            _wgpu.RenderBundleEncoderInsertDebugMarker(_ptr, marshalledLabel.Ptr);
        }

        public void PopDebugGroup()
        {
            _wgpu.RenderBundleEncoderPopDebugGroup(_ptr);
        }

        public void PushDebugGroup(string groupLabel)
        {
            using var marshalledLabel = new MarshalledString(groupLabel, NativeStringEncoding.UTF8);
            _wgpu.RenderBundleEncoderPushDebugGroup(_ptr, marshalledLabel.Ptr);
        }

        public void SetBindGroup(uint groupIndex, BindGroupPtr bindGroup, ReadOnlySpan<uint> dynamicOffsets)
        {
            _wgpu.RenderBundleEncoderSetBindGroup(_ptr, groupIndex, bindGroup, (uint)dynamicOffsets.Length, in dynamicOffsets[0]);
        }

        public void SetIndexBuffer(uint groupIndex, BufferPtr buffer, IndexFormat format, ulong offset, ulong size)
        {
            _wgpu.RenderBundleEncoderSetIndexBuffer(_ptr, buffer, format, offset, size);
            
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);
            _wgpu.RenderBundleEncoderSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void SetPipeline(RenderPipelinePtr pipeline)
        {
            _wgpu.RenderBundleEncoderSetPipeline(_ptr, pipeline);
            
        }

        public void SetVertexBuffer(uint slot, BufferPtr buffer, ulong offset, ulong size)
        {
            _wgpu.RenderBundleEncoderSetVertexBuffer(_ptr, slot, buffer, offset, size);
        }
    }
}
