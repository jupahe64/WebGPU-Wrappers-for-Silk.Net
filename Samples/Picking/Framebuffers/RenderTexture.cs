using Silk.NET.WebGPU;

using Silk.NET.WebGPU.Safe;
using static Picking.Framebuffer;

namespace Picking.Framebuffers
{
    class RenderTexture
    {
        Framebuffer _framebuffer;

        public uint Width => _framebuffer.Width;
        public uint Height => _framebuffer.Height;

        public TextureFormat ColorFormat => _framebuffer.GetFormat(0);

        private RenderTexture(Framebuffer framebuffer)
        {
            _framebuffer = framebuffer;
        }

        public static RenderTexture Create(DevicePtr device,
            TextureFormat colorFormat, uint width = 0, uint height = 0, string? label = null)
        {
            var framebuffer = new Framebuffer(device, new (string name, TextureFormat format)[]
            {
                ("Color", colorFormat),
            }, null, label);

            var renderTexture = new RenderTexture(framebuffer);

            renderTexture.EnsureSize(width, height);

            return renderTexture;
        }

        public bool TryGetTargets(out ColorTargetView color)
        {
            var result = true;

            result &= _framebuffer.TryGetColorTarget(0, out color);

            return result;
        }

        public void EnsureSize(uint width, uint height)
        {
            _framebuffer.EnsureSize(width, height);
        }
    }
}