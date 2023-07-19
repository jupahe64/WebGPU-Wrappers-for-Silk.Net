using System;
using Silk.NET.Core.Native;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    public readonly unsafe struct CommandEncoderPtr
    {
        private readonly WebGPU _wgpu;
        private readonly CommandEncoder* _ptr;

        public CommandEncoderPtr(WebGPU wgpu, CommandEncoder* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator CommandEncoder*(CommandEncoderPtr ptr) => ptr._ptr;

        public ComputePassEncoderPtr BeginComputePass(ReadOnlySpan<ComputePassTimestampWrite> timestampWrites, string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var timestampWritesPtr = stackalloc WGPU.ComputePassTimestampWrite[timestampWrites.Length];

            for (int i = 0; i < timestampWrites.Length; i++)
                timestampWritesPtr[i] = timestampWrites[i].Pack();

            var descriptor = new ComputePassDescriptor
            {
                Label = marshalledLabel.Ptr,
                TimestampWriteCount = (uint)timestampWrites.Length,
                TimestampWrites = timestampWritesPtr
            };

            return new(_wgpu, _wgpu.CommandEncoderBeginComputePass(_ptr, in descriptor));
        }

        public RenderPassEncoderPtr BeginRenderPass(
            ReadOnlySpan<RenderPassColorAttachment> colorAttachments, 
            ReadOnlySpan<RenderPassTimestampWrite> timestampWrites, 
            RenderPassDepthStencilAttachment? depthStencilAttachment,
            QuerySetPtr? occlusionQuerySet,
            uint? maxDrawCount = null,
            string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var timestampWritesPtr = stackalloc WGPU.RenderPassTimestampWrite[timestampWrites.Length];

            for (int i = 0; i < timestampWrites.Length; i++)
                timestampWritesPtr[i] = timestampWrites[i].Pack();

            var colorAttachmentsPtr = stackalloc WGPU.RenderPassColorAttachment[colorAttachments.Length];

            for (int i = 0; i < colorAttachments.Length; i++)
                colorAttachmentsPtr[i] = colorAttachments[i].Pack();

            WGPU.RenderPassDepthStencilAttachment* depthStencilAttachmentPtr = null;

            if(depthStencilAttachment.HasValue)
            {
                var tmp = depthStencilAttachment.Value.Pack();
                depthStencilAttachmentPtr = &tmp;
            }

            var maxDrawCountStruct = new RenderPassDescriptorMaxDrawCount
            {
                Chain = new ChainedStruct { SType = SType.RenderPassDescriptorMaxDrawCount },
                MaxDrawCount = maxDrawCount.GetValueOrDefault()
            };
            
            var descriptor = new RenderPassDescriptor
            {
                NextInChain = maxDrawCount.HasValue ? 
                    (ChainedStruct*)&maxDrawCountStruct : null,
                Label = marshalledLabel.Ptr,
                TimestampWriteCount = (uint)timestampWrites.Length,
                TimestampWrites = timestampWritesPtr,
                ColorAttachmentCount = (uint)colorAttachments.Length,
                ColorAttachments = colorAttachmentsPtr,
                DepthStencilAttachment = depthStencilAttachmentPtr,
                OcclusionQuerySet = occlusionQuerySet.GetValueOrDefault()
                
            };

            return new(_wgpu, _wgpu.CommandEncoderBeginRenderPass(_ptr, in descriptor));
        }

        public void ClearBuffer(BufferPtr buffer, ulong offset, ulong size)
        {
            _wgpu.CommandEncoderClearBuffer(_ptr, buffer, offset, size);
        }

        public void CopyBufferToBuffer(BufferPtr source, ulong sourceOffset, 
            BufferPtr destination, ulong destinationOffset, ulong size)
        {
            _wgpu.CommandEncoderCopyBufferToBuffer(_ptr, source, sourceOffset, destination, destinationOffset, size);
        }

        public void CopyBufferToTexture(ImageCopyBuffer source, 
            ImageCopyTexture destination, Extent3D copySize)
        {
            var _source = source.Pack();
            var _destination = destination.Pack();
           _wgpu.CommandEncoderCopyBufferToTexture(_ptr, &_source, &_destination, in copySize);
        }

        public void CopyTextureToBuffer(ImageCopyTexture source, 
            ImageCopyBuffer destination, Extent3D copySize)
        {
            var _source = source.Pack();
            var _destination = destination.Pack();
           _wgpu.CommandEncoderCopyTextureToBuffer(_ptr, &_source, &_destination, in copySize);
        }

        public void CopyTextureToTexture(ImageCopyTexture source, 
            ImageCopyTexture destination, Extent3D copySize)
        {
            var _source = source.Pack();
            var _destination = destination.Pack();
           _wgpu.CommandEncoderCopyTextureToTexture(_ptr, &_source, &_destination, in copySize);
        }

        public CommandBufferPtr Finish(string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new CommandBufferDescriptor
            {
                Label = marshalledLabel.Ptr
            };

            return new(_wgpu, _wgpu.CommandEncoderFinish(_ptr, &descriptor));
        }

        public void InsertDebugMarker(string markerLabel)
        {
            using var marshalledLabel = new MarshalledString(markerLabel, NativeStringEncoding.UTF8);
            _wgpu.CommandEncoderInsertDebugMarker(_ptr, marshalledLabel.Ptr);
        }

        public void PopDebugGroup()
        {
            _wgpu.CommandEncoderPopDebugGroup(_ptr);
        }

        public void PushDebugGroup(string groupLabel)
        {
            using var marshalledLabel = new MarshalledString(groupLabel, NativeStringEncoding.UTF8);
            _wgpu.CommandEncoderPushDebugGroup(_ptr, marshalledLabel.Ptr);
        }

        public void ResolveQuerySet(QuerySetPtr querySet, uint firstQuery, uint queryCount, 
            BufferPtr destination, ulong destinationOffset)
        {
            _wgpu.CommandEncoderResolveQuerySet(_ptr, querySet, firstQuery, queryCount, 
                destination, destinationOffset);
        }

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);
            _wgpu.CommandEncoderSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void WriteTimestamp(QuerySetPtr querySet, uint queryIndex)
        {
            _wgpu.CommandEncoderWriteTimestamp(_ptr, querySet, queryIndex);
        }

        public void Reference() => _wgpu.CommandEncoderReference(_ptr);

        public void Release() => _wgpu.CommandEncoderRelease(_ptr);
    }
}
