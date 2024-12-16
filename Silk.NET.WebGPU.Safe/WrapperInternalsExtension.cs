﻿using System;
using System.Collections.Generic;
using System.Text;
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToAutoProperty

//generated by the following code:
/*
var typeNames = new string[]{
    "Adapter",
    "BindGroup",
    "BindGroupLayout",
    "Buffer",
    "CommandBuffer",
    "CommandEncoder",
    "ComputePassEncoder",
    "ComputePipeline",
    "Device",
    "Instance",
    "PipelineLayout",
    "QuerySet",
    "Queue",
    "RenderBundle",
    "RenderBundleEncoder",
    "RenderPassEncoder",
    "RenderPipeline",
    "Sampler",
    "ShaderModule",
    "Surface",
    "Texture",
    "TextureView",
};


Console.WriteLine("public unsafe static class WrapperInternalsExtension");
Console.WriteLine("{");
foreach(string typeName in typeNames){
    Console.WriteLine($$"""
        public static WebGPU GetAPI(this {{typeName}}Ptr wrapper)
        {
            return wrapper.API;
        }
        public static {{typeName}}* GetPtr(this {{typeName}}Ptr wrapper)
        {
            return wrapper.Ptr;
        }
        public static IntPtr GetIntPtr(this {{typeName}}Ptr wrapper)
        {
            return (nint)wrapper.Ptr;
        }
    """);
    Console.WriteLine();
}
Console.WriteLine("}");

foreach(string typeName in typeNames){
    Console.WriteLine($$"""
    public unsafe partial struct {{typeName}}Ptr
    {
        internal WebGPU API => _wgpu;
        internal {{typeName}}* Ptr => _ptr;
    }
    """);
    Console.WriteLine();
    Console.WriteLine();
}
*/

namespace Silk.NET.WebGPU.Safe;

public static unsafe class WrapperInternalsExtension
{
    public static WebGPU GetAPI(this AdapterPtr wrapper)
    {
        return wrapper.API;
    }
    public static Adapter* GetPtr(this AdapterPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this AdapterPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this BindGroupPtr wrapper)
    {
        return wrapper.API;
    }
    public static BindGroup* GetPtr(this BindGroupPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this BindGroupPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this BindGroupLayoutPtr wrapper)
    {
        return wrapper.API;
    }
    public static BindGroupLayout* GetPtr(this BindGroupLayoutPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this BindGroupLayoutPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this BufferPtr wrapper)
    {
        return wrapper.API;
    }
    public static Buffer* GetPtr(this BufferPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this BufferPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this CommandBufferPtr wrapper)
    {
        return wrapper.API;
    }
    public static CommandBuffer* GetPtr(this CommandBufferPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this CommandBufferPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this CommandEncoderPtr wrapper)
    {
        return wrapper.API;
    }
    public static CommandEncoder* GetPtr(this CommandEncoderPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this CommandEncoderPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this ComputePassEncoderPtr wrapper)
    {
        return wrapper.API;
    }
    public static ComputePassEncoder* GetPtr(this ComputePassEncoderPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this ComputePassEncoderPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this ComputePipelinePtr wrapper)
    {
        return wrapper.API;
    }
    public static ComputePipeline* GetPtr(this ComputePipelinePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this ComputePipelinePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this DevicePtr wrapper)
    {
        return wrapper.API;
    }
    public static Device* GetPtr(this DevicePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this DevicePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this InstancePtr wrapper)
    {
        return wrapper.API;
    }
    public static Instance* GetPtr(this InstancePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this InstancePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this PipelineLayoutPtr wrapper)
    {
        return wrapper.API;
    }
    public static PipelineLayout* GetPtr(this PipelineLayoutPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this PipelineLayoutPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this QuerySetPtr wrapper)
    {
        return wrapper.API;
    }
    public static QuerySet* GetPtr(this QuerySetPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this QuerySetPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this QueuePtr wrapper)
    {
        return wrapper.API;
    }
    public static Queue* GetPtr(this QueuePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this QueuePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this RenderBundlePtr wrapper)
    {
        return wrapper.API;
    }
    public static RenderBundle* GetPtr(this RenderBundlePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this RenderBundlePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this RenderBundleEncoderPtr wrapper)
    {
        return wrapper.API;
    }
    public static RenderBundleEncoder* GetPtr(this RenderBundleEncoderPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this RenderBundleEncoderPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this RenderPassEncoderPtr wrapper)
    {
        return wrapper.API;
    }
    public static RenderPassEncoder* GetPtr(this RenderPassEncoderPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this RenderPassEncoderPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this RenderPipelinePtr wrapper)
    {
        return wrapper.API;
    }
    public static RenderPipeline* GetPtr(this RenderPipelinePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this RenderPipelinePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this SamplerPtr wrapper)
    {
        return wrapper.API;
    }
    public static Sampler* GetPtr(this SamplerPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this SamplerPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this ShaderModulePtr wrapper)
    {
        return wrapper.API;
    }
    public static ShaderModule* GetPtr(this ShaderModulePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this ShaderModulePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this SurfacePtr wrapper)
    {
        return wrapper.API;
    }
    public static Surface* GetPtr(this SurfacePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this SurfacePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this TexturePtr wrapper)
    {
        return wrapper.API;
    }
    public static Texture* GetPtr(this TexturePtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this TexturePtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

    public static WebGPU GetAPI(this TextureViewPtr wrapper)
    {
        return wrapper.API;
    }
    public static TextureView* GetPtr(this TextureViewPtr wrapper)
    {
        return wrapper.Ptr;
    }
    public static IntPtr GetIntPtr(this TextureViewPtr wrapper)
    {
        return (nint)wrapper.Ptr;
    }

}
public unsafe partial struct AdapterPtr
{
    internal WebGPU API => _wgpu;
    internal Adapter* Ptr => _ptr;
}


public unsafe partial struct BindGroupPtr
{
    internal WebGPU API => _wgpu;
    internal BindGroup* Ptr => _ptr;
}


public unsafe partial struct BindGroupLayoutPtr
{
    internal WebGPU API => _wgpu;
    internal BindGroupLayout* Ptr => _ptr;
}


public unsafe partial struct BufferPtr
{
    internal WebGPU API => _wgpu;
    internal Buffer* Ptr => _ptr;
}


public unsafe partial struct CommandBufferPtr
{
    internal WebGPU API => _wgpu;
    internal CommandBuffer* Ptr => _ptr;
}


public unsafe partial struct CommandEncoderPtr
{
    internal WebGPU API => _wgpu;
    internal CommandEncoder* Ptr => _ptr;
}


public unsafe partial struct ComputePassEncoderPtr
{
    internal WebGPU API => _wgpu;
    internal ComputePassEncoder* Ptr => _ptr;
}


public unsafe partial struct ComputePipelinePtr
{
    internal WebGPU API => _wgpu;
    internal ComputePipeline* Ptr => _ptr;
}


public unsafe partial struct DevicePtr
{
    internal WebGPU API => _wgpu;
    internal Device* Ptr => _ptr;
}


public unsafe partial struct InstancePtr
{
    internal WebGPU API => _wgpu;
    internal Instance* Ptr => _ptr;
}


public unsafe partial struct PipelineLayoutPtr
{
    internal WebGPU API => _wgpu;
    internal PipelineLayout* Ptr => _ptr;
}


public unsafe partial struct QuerySetPtr
{
    internal WebGPU API => _wgpu;
    internal QuerySet* Ptr => _ptr;
}


public unsafe partial struct QueuePtr
{
    internal WebGPU API => _wgpu;
    internal Queue* Ptr => _ptr;
}


public unsafe partial struct RenderBundlePtr
{
    internal WebGPU API => _wgpu;
    internal RenderBundle* Ptr => _ptr;
}


public unsafe partial struct RenderBundleEncoderPtr
{
    internal WebGPU API => _wgpu;
    internal RenderBundleEncoder* Ptr => _ptr;
}


public unsafe partial struct RenderPassEncoderPtr
{
    internal WebGPU API => _wgpu;
    internal RenderPassEncoder* Ptr => _ptr;
}


public unsafe partial struct RenderPipelinePtr
{
    internal WebGPU API => _wgpu;
    internal RenderPipeline* Ptr => _ptr;
}


public unsafe partial struct SamplerPtr
{
    internal WebGPU API => _wgpu;
    internal Sampler* Ptr => _ptr;
}


public unsafe partial struct ShaderModulePtr
{
    internal WebGPU API => _wgpu;
    internal ShaderModule* Ptr => _ptr;
}


public unsafe partial struct SurfacePtr
{
    internal WebGPU API => _wgpu;
    internal Surface* Ptr => _ptr;
}


public unsafe partial struct TexturePtr
{
    internal WebGPU API => _wgpu;
    internal Texture* Ptr => _ptr;
}


public unsafe partial struct TextureViewPtr
{
    internal WebGPU API => _wgpu;
    internal TextureView* Ptr => _ptr;
}