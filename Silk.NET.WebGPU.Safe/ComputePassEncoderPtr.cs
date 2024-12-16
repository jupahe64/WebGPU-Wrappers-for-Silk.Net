using System;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Safe
{
    public readonly unsafe partial struct ComputePassEncoderPtr
    {
        private readonly WebGPU _wgpu;
        private readonly ComputePassEncoder* _ptr;

        public ComputePassEncoderPtr(WebGPU wgpu, ComputePassEncoder* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator ComputePassEncoder*(ComputePassEncoderPtr ptr) => ptr._ptr;

        public void DispatchWorkgroups(uint workgroupCountX, uint workgroupCountY, uint workgroupCountZ)
        {
            _wgpu.ComputePassEncoderDispatchWorkgroups(_ptr, workgroupCountX, workgroupCountY, workgroupCountZ);
        }

        public void DispatchWorkgroupsIndirect(BufferPtr indirectBuffer, ulong indirectOffset)
        {
            _wgpu.ComputePassEncoderDispatchWorkgroupsIndirect(_ptr, indirectBuffer, indirectOffset);
        }

        public void End()
        {
            _wgpu.ComputePassEncoderEnd(_ptr);
        }

        public void InsertDebugMarker(string markerLabel)
        {
            using var marshalledLabel = new MarshalledString(markerLabel, NativeStringEncoding.UTF8);
            _wgpu.ComputePassEncoderInsertDebugMarker(_ptr, marshalledLabel.Ptr);
        }

        public void PopDebugGroup()
        {
            _wgpu.ComputePassEncoderPopDebugGroup(_ptr);
        }

        public void PushDebugGroup(string groupLabel)
        {
            using var marshalledLabel = new MarshalledString(groupLabel, NativeStringEncoding.UTF8);
            _wgpu.ComputePassEncoderPushDebugGroup(_ptr, marshalledLabel.Ptr);
        }

        public void SetBindGroup(uint groupIndex, BindGroupPtr bindGroup, ReadOnlySpan<uint> dynamicOffsets)
        {
            fixed(uint* ptr = dynamicOffsets)
                _wgpu.ComputePassEncoderSetBindGroup(_ptr, groupIndex, bindGroup, (uint)dynamicOffsets.Length, ptr);
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);
            _wgpu.ComputePassEncoderSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void SetPipeline(ComputePipelinePtr pipeline)
        {
            _wgpu.ComputePassEncoderSetPipeline(_ptr, pipeline);
            
        }

        public void Reference() => _wgpu.ComputePassEncoderReference(_ptr);

        public void Release() => _wgpu.ComputePassEncoderRelease(_ptr);
    }
}
