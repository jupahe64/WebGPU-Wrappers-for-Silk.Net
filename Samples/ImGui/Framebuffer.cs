using Silk.NET.WebGPU;

using Silk.NET.WebGPU.Safe;
using System;

namespace ImGuiDemo
{
    class Framebuffer
    {
        public uint Width => _width;
        public uint Height => _height;

        private uint _width;
        private uint _height;

        private readonly DevicePtr _device;
        private readonly string? label;
        private (TextureFormat format, TexturePtr? tex, TextureViewPtr? view) _color0;
        private (TextureFormat format, TexturePtr? tex, TextureViewPtr? view, TextureViewPtr? depthOnlyView)? _depthStencil;

        private Framebuffer(uint width, uint height, DevicePtr device,
            TextureFormat color0Format, TextureFormat? depthStencilFormat, string? label)
        {
            _width = width;
            _height = height;
            _device = device;
            this.label = label;
            _color0 = (color0Format, null, null);

            if (depthStencilFormat is not null)
                _depthStencil = (depthStencilFormat.Value, null, null, null);
            else
                _depthStencil = null;
        }

        public static Framebuffer Create(DevicePtr device, TextureFormat color0Format, TextureFormat? depthStencilFormat = null,
            uint width = 0, uint height = 0, string? label = null)
        {
            var framebuffer = new Framebuffer(0, 0, device, color0Format, depthStencilFormat, label);

            framebuffer.EnsureSize(width, height);

            return framebuffer;
        }

        public bool TryGetViews(out TextureViewPtr color0View, out TextureViewPtr? depthStencilView, out TextureViewPtr? depthOnlyView)
        {
            if (_width * _height == 0)
            {
                color0View = default;
                depthStencilView = default;
                depthOnlyView = default;
                return false;
            }

            color0View = _color0.view!.Value;
            depthStencilView = _depthStencil?.view;
            depthOnlyView = _depthStencil?.depthOnlyView;

            return true;
        }

        public void EnsureSize(uint width, uint height)
        {
            if (width == _width && height == _height)
                return;

            _width = width;
            _height = height;

            TextureUsage usageFlags = TextureUsage.RenderAttachment | TextureUsage.TextureBinding;


            _color0.tex?.Destroy();
            _depthStencil?.tex?.Destroy();

            if (width * height == 0)
                return;

            _color0.tex = _device.CreateTexture(usageFlags, TextureDimension.Dimension2D,
                    new Extent3D(width, height, 1),
                    _color0.format, 1, 1, new ReadOnlySpan<TextureFormat>(_color0.format),
                    label: label == null ? null : $"{label}:Color0");

            _color0.view = _color0.tex?.CreateView(_color0.format, TextureViewDimension.Dimension2D,
                TextureAspect.All, 0, 1, 0, 1,
                    label: label == null ? null : $"{label}:Color0 - View");

            if (_depthStencil is not null)
            {
                var depthStencil = _depthStencil.Value;
                depthStencil.tex = _device.CreateTexture(usageFlags, TextureDimension.Dimension2D,
                    new Extent3D(width, height, 1),
                    depthStencil.format, 1, 1, new ReadOnlySpan<TextureFormat>(depthStencil.format),
                    label: label == null ? null : $"{label}:DepthStencil");

                depthStencil.view = depthStencil.tex?.CreateView(depthStencil.format,
                    TextureViewDimension.Dimension2D,
                    TextureAspect.All, 0, 1, 0, 1,
                    label: label == null ? null : $"{label}:DepthStencil - View");

                depthStencil.depthOnlyView = depthStencil.tex?.CreateView(depthStencil.format,
                    TextureViewDimension.Dimension2D,
                    TextureAspect.DepthOnly, 0, 1, 0, 1,
                    label: label == null ? null : $"{label}:DepthStencil - View(Depth)");

                _depthStencil = depthStencil;
            }
        }
    }
}