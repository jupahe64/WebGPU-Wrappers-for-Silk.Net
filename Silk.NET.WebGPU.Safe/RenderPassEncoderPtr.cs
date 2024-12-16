using System;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Safe
{
    public readonly unsafe partial struct RenderPassEncoderPtr
    {
        private readonly WebGPU _wgpu;
        private readonly RenderPassEncoder* _ptr;

        public RenderPassEncoderPtr(WebGPU wgpu, RenderPassEncoder* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator RenderPassEncoder*(RenderPassEncoderPtr ptr) => ptr._ptr;

        public void BeginOcclusionQuery(uint queryIndex)
        {
            _wgpu.RenderPassEncoderBeginOcclusionQuery(_ptr, queryIndex);
        }

        public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        {
            _wgpu.RenderPassEncoderDraw(_ptr, vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
        {
            _wgpu.RenderPassEncoderDrawIndexed(_ptr, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
        }

        public void DrawIndexedIndirect(BufferPtr indirectBuffer, ulong indirectOffset)
        {
            _wgpu.RenderPassEncoderDrawIndexedIndirect(_ptr, indirectBuffer, indirectOffset);
        }

        public void DrawIndirect(BufferPtr indirectBuffer, ulong indirectOffset)
        {
            _wgpu.RenderPassEncoderDrawIndirect(_ptr, indirectBuffer, indirectOffset);
        }

        public void End()
        {
            _wgpu.RenderPassEncoderEnd(_ptr);
        }

        public void EndOcclusionQuery()
        {
            _wgpu.RenderPassEncoderEndOcclusionQuery(_ptr);
        }

        public void ExecuteBundles(ReadOnlySpan<RenderBundlePtr> bundles)
        {
            RenderBundle** bundlesPtr = stackalloc RenderBundle*[bundles.Length];

            for (int i = 0; i < bundles.Length; i++)
                bundlesPtr[i] = bundles[i];

            _wgpu.RenderPassEncoderExecuteBundles(_ptr, (uint)bundles.Length, bundlesPtr);
        }

        public void InsertDebugMarker(string markerLabel)
        {
            using var marshalledLabel = new MarshalledString(markerLabel, NativeStringEncoding.UTF8);
            _wgpu.RenderPassEncoderInsertDebugMarker(_ptr, marshalledLabel.Ptr);
        }

        public void PopDebugGroup()
        {
            _wgpu.RenderPassEncoderPopDebugGroup(_ptr);
        }

        public void PushDebugGroup(string groupLabel)
        {
            using var marshalledLabel = new MarshalledString(groupLabel, NativeStringEncoding.UTF8);
            _wgpu.RenderPassEncoderPushDebugGroup(_ptr, marshalledLabel.Ptr);
        }

        public void SetBindGroup(uint groupIndex, BindGroupPtr bindGroup, ReadOnlySpan<uint> dynamicOffsets)
        {
            fixed (uint* ptr = dynamicOffsets)
                _wgpu.RenderPassEncoderSetBindGroup(_ptr, groupIndex, bindGroup, (uint)dynamicOffsets.Length, ptr);
        }

        public void SetBlendConstant(Color color)
        {
            _wgpu.RenderPassEncoderSetBlendConstant(_ptr, in color);
        }

        public void SetIndexBuffer(BufferPtr buffer, IndexFormat format, ulong offset, ulong size)
        {
            _wgpu.RenderPassEncoderSetIndexBuffer(_ptr, buffer, format, offset, size);
            
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);
            _wgpu.RenderPassEncoderSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void SetPipeline(RenderPipelinePtr pipeline)
        {
            _wgpu.RenderPassEncoderSetPipeline(_ptr, pipeline);
            
        }

        public void SetScissorRect(uint x, uint y, uint width, uint height)
        {
            _wgpu.RenderPassEncoderSetScissorRect(_ptr, x, y, width, height);
        }

        public void SetStencilReference(uint reference)
        {
            _wgpu.RenderPassEncoderSetStencilReference(_ptr, reference);
        }

        public void SetVertexBuffer(uint slot, BufferPtr buffer, ulong offset, ulong size)
        {
            _wgpu.RenderPassEncoderSetVertexBuffer(_ptr, slot, buffer, offset, size);
        }

        public void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
        {
            _wgpu.RenderPassEncoderSetViewport(_ptr, x, y, width, height, minDepth, maxDepth);
        }

        public void Reference() => _wgpu.RenderPassEncoderReference(_ptr);

        public void Release() => _wgpu.RenderPassEncoderRelease(_ptr);
    }
}
