using Silk.NET.WebGPU;

using Silk.NET.WebGPU.Safe;
using System;
using System.Linq;

namespace DeferredRendering
{
    class Framebuffer
    {
        private record struct ColorTarget(string Name, TextureFormat Format, TexturePtr? Tex, TextureViewPtr? View);
        private record struct DepthStencilTarget(TextureFormat Format, TexturePtr? Tex, TextureViewPtr? View, TextureViewPtr? DepthOnlyView);
        public uint Width => _width;
        public uint Height => _height;

        public TextureFormat? DepthStencilFormat => _depthStencil?.Format;

        public TextureFormat GetFormat(int index) => _colorTargets[index].Format;

        private uint _width;
        private uint _height;

        private readonly DevicePtr _device;
        private readonly string? label;
        private ColorTarget[] _colorTargets;
        private DepthStencilTarget? _depthStencil = null;

        public Framebuffer(DevicePtr device,
            (string name, TextureFormat format)[] colorTargets, TextureFormat? depthStencilFormat, string? label)
        {
            _width = 0;
            _height = 0;
            _device = device;
            this.label = label;

            _colorTargets = Array.ConvertAll<(string name, TextureFormat format), ColorTarget>(
                colorTargets, 
                x => new(x.name, x.format, null, null)
            );

            if(depthStencilFormat is TextureFormat format)
                _depthStencil = new(format, null, null, null);
        }



        public bool TryGetViews(Span<TextureViewPtr> colorTargetViewsDest,
            out TextureViewPtr? depthStencilView, out TextureViewPtr? depthOnlyView)
        {
            if (_width * _height == 0)
            {
                depthStencilView = default;
                depthOnlyView = default;
                return false;
            }

            for (int i = 0; i < _colorTargets.Length; i++)
                colorTargetViewsDest[i] = _colorTargets[i].View!.Value;

            depthStencilView = _depthStencil?.View;
            depthOnlyView = _depthStencil?.DepthOnlyView;

            return true;
        }

        public void EnsureSize(uint width, uint height)
        {
            if (width == _width && height == _height)
                return;

            _width = width;
            _height = height;

            TextureUsage usageFlags = TextureUsage.RenderAttachment | TextureUsage.TextureBinding;

            for (int i = 0; i < _colorTargets.Length; i++)
                _colorTargets[i].Tex?.Destroy();

            _depthStencil?.Tex?.Destroy();

            if (width * height == 0)
                return;

            for (int i = 0; i < _colorTargets.Length; i++)
            {
                ref var target = ref _colorTargets[i];

                target.Tex = _device.CreateTexture(usageFlags, TextureDimension.Dimension2D,
                    new Extent3D(width, height, 1),
                    target.Format, 1, 1, new ReadOnlySpan<TextureFormat>(target.Format),
                    label: label == null ? null : $"{label}:{target.Name}");

                target.View = target.Tex?.CreateView(target.Format, TextureViewDimension.Dimension2D,
                    TextureAspect.All, 0, 1, 0, 1,
                    label: label == null ? null : $"{label}:{target.Name} - View");
            }

                
            if (_depthStencil is not null)
            {
                var target = _depthStencil.Value;
                target.Tex = _device.CreateTexture(usageFlags, TextureDimension.Dimension2D,
                    new Extent3D(width, height, 1),
                    target.Format, 1, 1, new ReadOnlySpan<TextureFormat>(target.Format),
                    label: label == null ? null : $"{label}:DepthStencil");

                target.View = target.Tex?.CreateView(target.Format,
                    TextureViewDimension.Dimension2D,
                    TextureAspect.All, 0, 1, 0, 1,
                    label: label == null ? null : $"{label}:DepthStencil - View");

                target.DepthOnlyView = target.Tex?.CreateView(target.Format,
                    TextureViewDimension.Dimension2D,
                    TextureAspect.DepthOnly, 0, 1, 0, 1,
                    label: label == null ? null : $"{label}:DepthStencil - View(Depth)");

                _depthStencil = target;
            }
            
        }
    }

    class GBuffer
    {
        Framebuffer _framebuffer;

        public uint Width => _framebuffer.Width;
        public uint Height => _framebuffer.Height;

        public TextureFormat AlbedoFormat => _framebuffer.GetFormat(0);
        public TextureFormat NormalFormat => _framebuffer.GetFormat(1);
        public TextureFormat DepthStencilFormat => _framebuffer.DepthStencilFormat!.Value;

        private GBuffer(DevicePtr device,
            TextureFormat albedoFormat, TextureFormat normalFormat, TextureFormat depthStencilFormat, string? label)
        {
            _framebuffer = new Framebuffer(device, new (string name, TextureFormat format)[]
            {
                ("Albedo", albedoFormat),
                ("Normal", normalFormat),
            }, depthStencilFormat, label);
        }

        public static GBuffer Create(DevicePtr device, TextureFormat albedoFormat, TextureFormat normalFormat, TextureFormat depthStencilFormat,
            uint width = 0, uint height = 0, string? label = null)
        {
            var framebuffer = new GBuffer(device, albedoFormat, normalFormat, depthStencilFormat, label);

            framebuffer.EnsureSize(width, height);

            return framebuffer;
        }

        public bool TryGetViews(out TextureViewPtr albedoView, out TextureViewPtr normalView,
            out TextureViewPtr depthStencilView, out TextureViewPtr depthOnlyView)
        {
            var colorTargetViews = new TextureViewPtr[2];
            if (!_framebuffer.TryGetViews(colorTargetViews, 
                out TextureViewPtr? _depthStencilView, out TextureViewPtr? _depthOnlyView))
            {
                albedoView = default;
                normalView = default;
                depthStencilView = default;
                depthOnlyView = default;
                return false;
            }

            albedoView = colorTargetViews[0];
            normalView = colorTargetViews[1];
            depthStencilView = _depthStencilView!.Value;
            depthOnlyView = _depthOnlyView!.Value;

            return true;
        }

        public void EnsureSize(uint width, uint height)
        {
            _framebuffer.EnsureSize(width, height);
        }
    }

    class RenderTexture
    {
        Framebuffer _framebuffer;

        public uint Width => _framebuffer.Width;
        public uint Height => _framebuffer.Height;

        public TextureFormat ColorTargetFormat => _framebuffer.GetFormat(0);
        public TextureFormat? DepthStencilFormat => _framebuffer.DepthStencilFormat;

        private RenderTexture(DevicePtr device,
            TextureFormat colorTargetFormat, TextureFormat? depthStencilFormat, string? label)
        {
            _framebuffer = new Framebuffer(device, new (string name, TextureFormat format)[]
            {
                ("Color", colorTargetFormat),
            }, depthStencilFormat, label);
        }

        public static RenderTexture Create(DevicePtr device, TextureFormat colorFormat, TextureFormat? depthStencilFormat,
            uint width = 0, uint height = 0, string? label = null)
        {
            var framebuffer = new RenderTexture(device, colorFormat, depthStencilFormat, label);

            framebuffer.EnsureSize(width, height);

            return framebuffer;
        }

        public bool TryGetViews(out TextureViewPtr colorTargetView,
            out TextureViewPtr? depthStencilView, out TextureViewPtr? depthOnlyView)
        {
            var colorTargetViews = new TextureViewPtr[1];
            if (!_framebuffer.TryGetViews(colorTargetViews, out depthStencilView, out depthOnlyView))
            {
                colorTargetView = default;
                return false;
            }

            colorTargetView = colorTargetViews[0];

            return true;
        }

        public void EnsureSize(uint width, uint height)
        {
            _framebuffer.EnsureSize(width, height);
        }
    }
}