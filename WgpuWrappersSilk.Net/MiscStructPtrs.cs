using Microsoft.VisualBasic;
using Silk.NET.Core.Attributes;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace WgpuWrappersSilk.Net
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

    public readonly unsafe struct SurfacePtr
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
    }
    
    public readonly unsafe struct BindGroupLayoutPtr
    {
        private readonly WebGPU _wgpu;
        private readonly BindGroupLayout* _ptr;

        public BindGroupLayoutPtr(WebGPU wgpu, BindGroupLayout* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator BindGroupLayout*(BindGroupLayoutPtr ptr) => ptr._ptr;
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

    public readonly unsafe struct BindGroupPtr
    {
        private readonly WebGPU _wgpu;
        private readonly BindGroup* _ptr;

        public BindGroupPtr(WebGPU wgpu, BindGroup* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator BindGroup*(BindGroupPtr ptr) => ptr._ptr;
    }

    public unsafe static class BindGroupEntries
    {
        public static BindGroupEntry Buffer(uint binding, Buffer* buffer, ulong offset, ulong size) => 
            new(binding: binding, buffer: buffer, offset: offset, size: size);

        public static BindGroupEntry Sampler(uint binding, Sampler* sampler) =>
            new(binding: binding, sampler: sampler);

        public static BindGroupEntry Texture(uint binding, TextureView* textureView) =>
            new(binding: binding, textureView: textureView);
    }

    public readonly unsafe struct BufferPtr
    {
        private readonly WebGPU _wgpu;
        private readonly Buffer* _ptr;

        public BufferPtr(WebGPU wgpu, Buffer* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Buffer*(BufferPtr ptr) => ptr._ptr;
    }

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
    }

    public readonly unsafe struct ComputePipelinePtr
    {
        private readonly WebGPU _wgpu;
        private readonly ComputePipeline* _ptr;

        public ComputePipelinePtr(WebGPU wgpu, ComputePipeline* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator ComputePipeline*(ComputePipelinePtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct RenderPipelinePtr
    {
        private readonly WebGPU _wgpu;
        private readonly RenderPipeline* _ptr;

        public RenderPipelinePtr(WebGPU wgpu, RenderPipeline* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator RenderPipeline*(RenderPipelinePtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct ShaderModulePtr
    {
        private readonly WebGPU _wgpu;
        private readonly ShaderModule* _ptr;

        public ShaderModulePtr(WebGPU wgpu, ShaderModule* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator ShaderModule*(ShaderModulePtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct PipelineLayoutPtr
    {
        private readonly WebGPU _wgpu;
        private readonly PipelineLayout* _ptr;

        public PipelineLayoutPtr(WebGPU wgpu, PipelineLayout* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator PipelineLayout*(PipelineLayoutPtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct QuerySetPtr
    {
        private readonly WebGPU _wgpu;
        private readonly QuerySet* _ptr;

        public QuerySetPtr(WebGPU wgpu, QuerySet* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator QuerySet*(QuerySetPtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct RenderBundlePtr
    {
        private readonly WebGPU _wgpu;
        private readonly RenderBundle* _ptr;

        public RenderBundlePtr(WebGPU wgpu, RenderBundle* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator RenderBundle*(RenderBundlePtr ptr) => ptr._ptr;
    }

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
    }

    public readonly unsafe struct SamplerPtr
    {
       private readonly WebGPU _wgpu;
       private readonly Sampler* _ptr;

       public SamplerPtr(WebGPU wgpu, Sampler* ptr)
       {
           _wgpu = wgpu;
           _ptr = ptr;
       }

       public static implicit operator Sampler*(SamplerPtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct TexturePtr
    {
       private readonly WebGPU _wgpu;
       private readonly Texture* _ptr;

       public TexturePtr(WebGPU wgpu, Texture* ptr)
       {
           _wgpu = wgpu;
           _ptr = ptr;
       }

       public static implicit operator Texture*(TexturePtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct SwapChainPtr
    {
       private readonly WebGPU _wgpu;
       private readonly SwapChain* _ptr;

       public SwapChainPtr(WebGPU wgpu, SwapChain* ptr)
       {
           _wgpu = wgpu;
           _ptr = ptr;
       }

       public static implicit operator SwapChain*(SwapChainPtr ptr) => ptr._ptr;
    }

    public readonly unsafe struct QueuePtr
    {
       private readonly WebGPU _wgpu;
       private readonly Queue* _ptr;

       public QueuePtr(WebGPU wgpu, Queue* ptr)
       {
           _wgpu = wgpu;
           _ptr = ptr;
       }

       public static implicit operator Queue*(QueuePtr ptr) => ptr._ptr;
    }
    
    // public readonly unsafe struct ComputePipelinePtr
    // {
    //    private readonly WebGPU _wgpu;
    //    private readonly ComputePipeline* _ptr;

    //    public ComputePipelinePtr(WebGPU wgpu, ComputePipeline* ptr)
    //    {
    //        _wgpu = wgpu;
    //        _ptr = ptr;
    //    }

    //    public static implicit operator ComputePipeline*(ComputePipelinePtr ptr) => ptr._ptr;
    // }
}
