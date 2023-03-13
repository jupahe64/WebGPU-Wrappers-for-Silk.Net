using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WgpuWrappersSilk.Net
{
    public unsafe struct InstancePtr
    {
        private static readonly List<(WebGPU, TaskCompletionSource<AdapterPtr>)> s_adapterRequests = new();

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void AdapterRequestCallback(RequestAdapterStatus status, Adapter* adapter, byte* message, void* data)
        {
            var (wgpu, task) = s_adapterRequests[(int)data];
            task.SetResult(new AdapterPtr(wgpu, adapter));
        }

        public static InstancePtr Create(WebGPU wgpu, in InstanceDescriptor descriptor) 
            => new(wgpu, wgpu.CreateInstance(in descriptor));

        private readonly WebGPU _wgpu;
        private readonly Instance* _ptr;

        public InstancePtr(WebGPU wgpu, Instance* ptr)
        {
            _wgpu = wgpu;
            _ptr = ptr;
        }

        public static implicit operator Instance*(InstancePtr ptr) => ptr._ptr;

        public Task<AdapterPtr> RequestAdapter(in RequestAdapterOptions options)
        {
            int idx = s_adapterRequests.Count;
            var task = new TaskCompletionSource<AdapterPtr>();
            s_adapterRequests.Add((_wgpu, task));
            _wgpu.InstanceRequestAdapter(_ptr, in options, new(&AdapterRequestCallback), (void*)idx);

            return task.Task;
        }

        #region CreateSurface
        public SurfacePtr CreateSurfaceFromAndroidNativeWindow(IntPtr nativeWindow, string? label = null)
        {
            var descriptor = new SurfaceDescriptorFromAndroidNativeWindow
            {
                Chain = new ChainedStruct(sType: SType.SurfaceDescriptorFromAndroidNativeWindow),
                Window = (void*)nativeWindow
            };
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            return new SurfacePtr(_wgpu, _wgpu.InstanceCreateSurface(_ptr, new SurfaceDescriptor(
                label: marshalledLabel.Ptr,
                nextInChain: &descriptor.Chain
                )));
        }

        public SurfacePtr CreateSurfaceFromHTMLCanvas(string selector, string? label = null)
        {
            using var marshalledSelector = new MarshalledString(selector, NativeStringEncoding.UTF8);

            var descriptor = new SurfaceDescriptorFromCanvasHTMLSelector
            {
                Chain = new ChainedStruct(sType: SType.SurfaceDescriptorFromCanvasHtmlselector),
                Selector = marshalledSelector.Ptr
            };
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            return new SurfacePtr(_wgpu, _wgpu.InstanceCreateSurface(_ptr, new SurfaceDescriptor(
                label: marshalledLabel.Ptr,
                nextInChain: &descriptor.Chain
                )));
        }

        public SurfacePtr CreateSurfaceFromMetalLayer(IntPtr layer, string? label = null)
        {
            var descriptor = new SurfaceDescriptorFromMetalLayer
            {
                Chain = new ChainedStruct(sType: SType.SurfaceDescriptorFromMetalLayer),
                Layer = (void*)layer
            };
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            return new SurfacePtr(_wgpu, _wgpu.InstanceCreateSurface(_ptr, new SurfaceDescriptor(
                label: marshalledLabel.Ptr,
                nextInChain: &descriptor.Chain
                )));
        }

        public SurfacePtr CreateSurfaceFromWaylandSurface(IntPtr display, IntPtr surface, string? label = null)
        {
            var descriptor = new SurfaceDescriptorFromWaylandSurface
            {
                Chain = new ChainedStruct(sType: SType.SurfaceDescriptorFromWaylandSurface),
                Display = (void*)display, 
                Surface = (void*)surface
            };
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            return new SurfacePtr(_wgpu, _wgpu.InstanceCreateSurface(_ptr, new SurfaceDescriptor(
                label: marshalledLabel.Ptr,
                nextInChain: &descriptor.Chain
                )));
        }

        public SurfacePtr CreateSurfaceFromWindowsHWND(IntPtr hwnd, string? label = null)
        {
            var descriptor = new SurfaceDescriptorFromWindowsHWND
            {
                Chain = new ChainedStruct(sType: SType.SurfaceDescriptorFromWindowsHwnd), 
                Hwnd = (void*)hwnd
            };
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            return new SurfacePtr(_wgpu, _wgpu.InstanceCreateSurface(_ptr, new SurfaceDescriptor(
                label: marshalledLabel.Ptr,
                nextInChain: &descriptor.Chain
                )));
        }

        public SurfacePtr CreateSurfaceFromXcbWindow(IntPtr connection, uint window, string? label = null)
        {
            var descriptor = new SurfaceDescriptorFromXcbWindow
            {
                Chain = new ChainedStruct(sType: SType.SurfaceDescriptorFromXcbWindow),
                Connection = (void*)connection, 
                Window = window
            };
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            return new SurfacePtr(_wgpu, _wgpu.InstanceCreateSurface(_ptr, new SurfaceDescriptor(
                label: marshalledLabel.Ptr,
                nextInChain: &descriptor.Chain
                )));
        }

        public SurfacePtr CreateSurfaceFromXlibWindow(IntPtr display, uint window, string? label = null)
        {
            var descriptor = new SurfaceDescriptorFromXlibWindow
            {
                Chain = new ChainedStruct(sType: SType.SurfaceDescriptorFromXlibWindow),
                Display = (void*)display, 
                Window = window
            };
            using var marshalledLabel = new MarshalledString(label, NativeStringEncoding.UTF8);

            return new SurfacePtr(_wgpu, _wgpu.InstanceCreateSurface(_ptr, new SurfaceDescriptor(
                label: marshalledLabel.Ptr,
                nextInChain: &descriptor.Chain
                )));
        }
        #endregion

        public void ProcessEvents()
        {
            _wgpu.InstanceProcessEvents(_ptr);
        }
    }
}
