using Silk.NET.WebGPU;

using Silk.NET.WebGPU.Safe;

namespace DeferredRendering
{
    class GBuffer
    {
        Framebuffer _framebuffer;

        public uint Width => _framebuffer.Width;
        public uint Height => _framebuffer.Height;

        public TextureFormat AlbedoChannelFormat => _framebuffer.GetFormat(0);
        public TextureFormat NormalChannelFormat => _framebuffer.GetFormat(1);
        public TextureFormat LightChannelFormat => _framebuffer.GetFormat(2);
        public TextureFormat DepthStencilFormat => _framebuffer.DepthStencilFormat!.Value;

        private GBuffer(Framebuffer framebuffer)
        {
            _framebuffer = framebuffer;
        }

        public static GBuffer Create(DevicePtr device,
            TextureFormat albedoFormat, TextureFormat normalFormat, TextureFormat lightFormat,
            TextureFormat depthStencilFormat, uint width = 0, uint height = 0, string? label = null)
        {
            var framebuffer = new Framebuffer(device, new (string name, TextureFormat format)[]
            {
                ("Albedo", albedoFormat),
                ("Normal", normalFormat),
                ("Light", lightFormat),
            }, depthStencilFormat, label);

            var gbuffer = new GBuffer(framebuffer);

            gbuffer.EnsureSize(width, height);

            return gbuffer;
        }

        public bool TryGetViews(out TextureViewPtr albedoView, out TextureViewPtr normalView, out TextureViewPtr lightView,
            out TextureViewPtr depthStencilView, out TextureViewPtr depthOnlyView)
        {
            var result = true;

            result &= _framebuffer.TryGetColorView(0, out albedoView);
            result &= _framebuffer.TryGetColorView(1, out normalView);
            result &= _framebuffer.TryGetColorView(2, out lightView);
            result &= _framebuffer.TryGetDepthViews(out var dsv, out var dov);
            depthStencilView = dsv!.Value;
            depthOnlyView = dov!.Value;

            return result;
        }

        public void EnsureSize(uint width, uint height)
        {
            _framebuffer.EnsureSize(width, height);
        }
    }
}