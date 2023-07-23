using System;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Safe
{
    internal readonly unsafe struct MarshalledString : IDisposable
    {
        public byte* Ptr => (byte*)_handle;

        private readonly nint _handle;
        private readonly NativeStringEncoding _encoding;

        public MarshalledString(string? str, NativeStringEncoding encoding = NativeStringEncoding.Ansi)
        {
            _handle = SilkMarshal.StringToPtr(str, encoding);
            _encoding = encoding;
        }

        public void Dispose()
        {
            SilkMarshal.FreeString(_handle, _encoding);
        }
    }

    public readonly unsafe partial struct BindGroupPtr
    {
        private readonly WebGPU _wgpu;
        private readonly BindGroup* _ptr;

        public BindGroupPtr(WebGPU wgpu, BindGroup* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator BindGroup*(BindGroupPtr ptr) => ptr._ptr;

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.BindGroupSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.BindGroupReference(_ptr);

        public void Release() => _wgpu.BindGroupRelease(_ptr);
    }

    public unsafe static class BindGroupEntries
    {
        public static BindGroupEntry Buffer(uint binding, BufferPtr buffer, ulong offset, ulong size) => 
            new(binding: binding, buffer: buffer, offset: offset, size: size);

        public static BindGroupEntry Sampler(uint binding, SamplerPtr sampler) =>
            new(binding: binding, sampler: sampler);

        public static BindGroupEntry Texture(uint binding, TextureViewPtr textureView) =>
            new(binding: binding, textureView: textureView);
    }

    public readonly unsafe partial struct BindGroupLayoutPtr
    {
        private readonly WebGPU _wgpu;
        private readonly BindGroupLayout* _ptr;

        public BindGroupLayoutPtr(WebGPU wgpu, BindGroupLayout* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator BindGroupLayout*(BindGroupLayoutPtr ptr) => ptr._ptr;

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.BindGroupLayoutSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.BindGroupLayoutReference(_ptr);

        public void Release() => _wgpu.BindGroupLayoutRelease(_ptr);
    }

    public unsafe static class BindGroupLayoutEntries
    {
        public static BindGroupLayoutEntry Buffer(uint binding, ShaderStage visibility, BufferBindingType bindingType, 
            ulong minBindingSize, bool hasDynamicOffset = false) =>
            new(binding: binding, visibility: visibility,
                buffer: new(type: bindingType, hasDynamicOffset: hasDynamicOffset, minBindingSize: minBindingSize));

        public static BindGroupLayoutEntry Sampler(uint binding, ShaderStage visibility, SamplerBindingType bindingType) =>
            new(binding: binding, visibility: visibility,
                sampler: new(type: bindingType));

        public static BindGroupLayoutEntry Texture(uint binding, ShaderStage visibility,
            TextureSampleType sampleType, TextureViewDimension viewDimension, bool multisampled) =>
            new(binding: binding, visibility: visibility,
                texture: new(sampleType: sampleType, viewDimension: viewDimension, multisampled: multisampled));
    }

    public readonly unsafe partial struct CommandBufferPtr
    {
        private readonly WebGPU _wgpu;
        private readonly CommandBuffer* _ptr;

        public CommandBufferPtr(WebGPU wgpu, CommandBuffer* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator CommandBuffer*(CommandBufferPtr ptr) => ptr._ptr;

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.CommandBufferSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.CommandBufferReference(_ptr);

        public void Release() => _wgpu.CommandBufferRelease(_ptr);
    }

    public readonly unsafe partial struct ComputePipelinePtr
    {
        private readonly WebGPU _wgpu;
        private readonly ComputePipeline* _ptr;

        public ComputePipelinePtr(WebGPU wgpu, ComputePipeline* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator ComputePipeline*(ComputePipelinePtr ptr) => ptr._ptr;

        public BindGroupLayoutPtr GetBindGroupLayout(uint groupIndex) =>
            new(_wgpu, _wgpu.ComputePipelineGetBindGroupLayout(_ptr, groupIndex));

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.ComputePipelineSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.ComputePipelineReference(_ptr);

        public void Release() => _wgpu.ComputePipelineRelease(_ptr);
    }

    public readonly unsafe partial struct PipelineLayoutPtr
    {
        private readonly WebGPU _wgpu;
        private readonly PipelineLayout* _ptr;

        public static PipelineLayoutPtr? Auto => null;

        public PipelineLayoutPtr(WebGPU wgpu, PipelineLayout* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator PipelineLayout*(PipelineLayoutPtr ptr) => ptr._ptr;

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.PipelineLayoutSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.PipelineLayoutReference(_ptr);

        public void Release() => _wgpu.PipelineLayoutRelease(_ptr);
    }

    public readonly unsafe partial struct QuerySetPtr
    {
        private readonly WebGPU _wgpu;
        private readonly QuerySet* _ptr;

        public QuerySetPtr(WebGPU wgpu, QuerySet* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator QuerySet*(QuerySetPtr ptr) => ptr._ptr;

        public void Destroy() => _wgpu.QuerySetDestroy(_ptr);

        public uint GetCount() => _wgpu.QuerySetGetCount(_ptr);

        public QueryType GetQueryType() => _wgpu.QuerySetGetType(_ptr);

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.QuerySetSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.QuerySetReference(_ptr);

        public void Release() => _wgpu.QuerySetRelease(_ptr);
    }

    public readonly unsafe partial struct RenderBundlePtr
    {
        private readonly WebGPU _wgpu;
        private readonly RenderBundle* _ptr;

        public RenderBundlePtr(WebGPU wgpu, RenderBundle* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator RenderBundle*(RenderBundlePtr ptr) => ptr._ptr;

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.RenderBundleSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.RenderBundleReference(_ptr);

        public void Release() => _wgpu.RenderBundleRelease(_ptr);
    }

    public readonly unsafe partial struct RenderPipelinePtr
    {
        private readonly WebGPU _wgpu;
        private readonly RenderPipeline* _ptr;

        public RenderPipelinePtr(WebGPU wgpu, RenderPipeline* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator RenderPipeline*(RenderPipelinePtr ptr) => ptr._ptr;

        public BindGroupLayoutPtr GetBindGroupLayout(uint groupIndex) =>
            new(_wgpu, _wgpu.RenderPipelineGetBindGroupLayout(_ptr, groupIndex));

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.RenderPipelineSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.RenderPipelineReference(_ptr);

        public void Release() => _wgpu.RenderPipelineRelease(_ptr);
    }

    public readonly unsafe partial struct SamplerPtr
    {
        private readonly WebGPU _wgpu;
        private readonly Sampler* _ptr;

        public SamplerPtr(WebGPU wgpu, Sampler* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Sampler*(SamplerPtr ptr) => ptr._ptr;

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);
            _wgpu.SamplerSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.SamplerReference(_ptr);

        public void Release() => _wgpu.SamplerRelease(_ptr);
    }

    public static class SurfaceExtension
    {
        public static unsafe SurfacePtr CreateWebGPUSurface(this INativeWindowSource window, WebGPU wgpu, InstancePtr instance)
        {
            return new(wgpu, window.CreateWebGPUSurface(wgpu, (Instance*)instance));
        }
    }

    public readonly unsafe partial struct SurfacePtr
    {
        private readonly WebGPU _wgpu;
        private readonly Surface* _ptr;

        public SurfacePtr(WebGPU wgpu, Surface* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Surface*(SurfacePtr ptr) => ptr._ptr;

        public TextureFormat GetPreferredFormat(AdapterPtr adapter)
        {
            return _wgpu.SurfaceGetPreferredFormat(_ptr, adapter);
        }

        public void Reference() => _wgpu.SurfaceReference(_ptr);

        public void Release() => _wgpu.SurfaceRelease(_ptr);
    }
    
    public readonly unsafe partial struct SwapChainPtr
    {
        private readonly WebGPU _wgpu;
        private readonly SwapChain* _ptr;

        public SwapChainPtr(WebGPU wgpu, SwapChain* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator SwapChain*(SwapChainPtr ptr) => ptr._ptr;

        public TextureViewPtr GetCurrentTextureView()
        {
            return new(_wgpu, _wgpu.SwapChainGetCurrentTextureView(_ptr));
        }

        public void Present()
        {
            _wgpu.SwapChainPresent(_ptr);
        }

        public void Reference() => _wgpu.SwapChainReference(_ptr);

        public void Release() => _wgpu.SwapChainRelease(_ptr);
    }

    public readonly unsafe partial struct TexturePtr
    {
        private readonly WebGPU _wgpu;
        private readonly Texture* _ptr;

        public TexturePtr(WebGPU wgpu, Texture* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Texture*(TexturePtr ptr) => ptr._ptr;

        public TextureViewPtr CreateView(TextureFormat format, TextureViewDimension dimension,
            TextureAspect aspect, uint baseMipLevel, uint mipLevelCount, 
            uint baseArrayLayer, uint arrayLayerCount,
            string? label = null)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            var descriptor = new TextureViewDescriptor
            {
                ArrayLayerCount = arrayLayerCount,
                Aspect = aspect,
                BaseArrayLayer = baseArrayLayer,
                BaseMipLevel = baseMipLevel,
                Dimension = dimension,
                Format = format,
                Label = marshalledLabel.Ptr,
                MipLevelCount = mipLevelCount
            };

            return new(_wgpu, _wgpu.TextureCreateView(_ptr, in descriptor));
        }

        public void Destroy() => _wgpu.TextureDestroy(_ptr);

        public uint GetDepthOrArrayLayers() => _wgpu.TextureGetDepthOrArrayLayers(_ptr);

        public TextureDimension GetDimension() => _wgpu.TextureGetDimension(_ptr);

        public TextureFormat GetFormat() => _wgpu.TextureGetFormat(_ptr);

        public uint GetHeight() => _wgpu.TextureGetHeight(_ptr);

        public uint GetMipLevelCount() => _wgpu.TextureGetMipLevelCount(_ptr);
        
        public uint GetSampleCount() => _wgpu.TextureGetSampleCount(_ptr);

        public TextureUsage GetUsage() => _wgpu.TextureGetUsage(_ptr);

        public uint GetWidth() => _wgpu.TextureGetWidth(_ptr);

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.TextureSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.TextureReference(_ptr);

        public void Release() => _wgpu.TextureRelease(_ptr);
    }

    public readonly unsafe partial struct TextureViewPtr
    {
        private readonly WebGPU _wgpu;
        private readonly TextureView* _ptr;

        public TextureViewPtr(WebGPU wgpu, TextureView* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator TextureView*(TextureViewPtr ptr) => ptr._ptr;

        public void SetLabel(string label)
        {
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            _wgpu.TextureViewSetLabel(_ptr, marshalledLabel.Ptr);
        }

        public void Reference() => _wgpu.TextureViewReference(_ptr);

        public void Release() => _wgpu.TextureViewRelease(_ptr);
    }
}
