using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Safe;
using Safe = Silk.NET.WebGPU.Safe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Silk.NET.WebGPU.Safe.BindGroupLayoutEntries;
using static Silk.NET.WebGPU.Safe.BindGroupEntries;
using Silk.NET.Maths;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace Picking
{
    internal class Compositor
    {
        public struct LightData
        {
            public Vector3D<float> Position;
            private uint _padding0;
            public Vector3D<float> Color;
            public float Radius;
        }

        private struct Uniforms
        {
            public float Gamma;
            private uint _padding0;
            private uint _padding1;
            private uint _padding2;
        }

        private static BindGroupLayoutEntry[] s_bindGroupLayoutEntries = new BindGroupLayoutEntry[]
        {
            Buffer(0, ShaderStage.Fragment, BufferBindingType.Uniform, (ulong)Marshal.SizeOf<Uniforms>()),
            Texture(1, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, false),
            Texture(2, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, false),
            Texture(3, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, false),
            Texture(4, ShaderStage.Fragment, TextureSampleType.Depth, TextureViewDimension.Dimension2D, false),
            Texture(5, ShaderStage.Fragment, TextureSampleType.Depth, TextureViewDimension.Dimension2D, false)
        };

        private static BindGroupPtr CreateBindGroup(Compositor c, 
            TextureViewPtr inColor, TextureViewPtr inHighlight, TextureViewPtr inOutline,
            TextureViewPtr inDepth, TextureViewPtr inHighlightDepth)
        {
            return c._device.CreateBindGroup(c._textureBindGroupLayout, new BindGroupEntry[]
            {
                Buffer(0, c._uniformBuffer, 0, (ulong)Marshal.SizeOf<Uniforms>()),
                Texture(1, inColor),
                Texture(2, inHighlight),
                Texture(3, inOutline),
                Texture(4, inDepth),
                Texture(5, inHighlightDepth),
            });
        }

        private static string s_shaderCode = """
                struct Varyings {
                    @builtin(position) Position: vec4<f32>,
                    @location(0) TexCoord: vec2<f32>,
                }

                @vertex
                fn vs_main(@builtin(vertex_index) vertex_index : u32) -> Varyings {
                    let a = vec2<f32>(-1.0,  1.0);
                    let b = vec2<f32>( 1.0,  1.0);
                    let c = vec2<f32>(-1.0, -1.0);
                    let d = vec2<f32>( 1.0, -1.0);

                    var pos = array(
                        b, a, c,
                        b, c, d
                    );

                    return Varyings(
                        vec4(pos[vertex_index], 0.0, 1.0),
                        pos[vertex_index] * 0.5 + vec2<f32>(0.5)
                    );
                }

                struct Uniforms {
                    Gamma: f32
                }
                
                @group(0) @binding(0)
                var<uniform> ub: Uniforms;
                @group(0) @binding(1)
                var uColor: texture_2d<f32>;
                @group(0) @binding(2)
                var uHighlight: texture_2d<f32>;
                @group(0) @binding(3)
                var uOutline: texture_2d<f32>;
                @group(0) @binding(4)
                var uDepth: texture_depth_2d;
                @group(0) @binding(5)
                var uHighlightDepth: texture_depth_2d;

                @fragment
                fn fs_main(v: Varyings) -> @location(0) vec4<f32> {
                    let color = textureLoad(uColor, vec2<i32>(floor(v.Position.xy)), 0);
                    var outline = textureLoad(uOutline, vec2<i32>(floor(v.Position.xy)), 0);
                    var highlight = textureLoad(uHighlight, vec2<i32>(floor(v.Position.xy)), 0);
                    let depth = textureLoad(uDepth, vec2<i32>(floor(v.Position.xy)), 0);
                    let highlightDepth = textureLoad(uHighlightDepth, vec2<i32>(floor(v.Position.xy)), 0);

                    let checker = (fract(v.Position.x/20.0)<0.5) != (fract(v.Position.y/20.0)<0.5);

                    let checkerValue = select(0.0, 0.25, checker);

                    highlight.a = select(highlight.a, highlight.a * checkerValue, highlightDepth>depth);

                    var oColor = color;

                    oColor = mix(oColor, vec4(highlight.rgb, 1.0), highlight.a);
                    oColor = mix(oColor, vec4(outline.rgb, 1.0), outline.a);

                    return oColor;
                }
                """;

        private DevicePtr _device;
        private RenderPipelinePtr _pipeline;
        private BindGroupLayoutPtr _textureBindGroupLayout;
        private BufferPtr _uniformBuffer;

        private BindGroupPtr? textureBindGroup;

        private Compositor(DevicePtr device, RenderPipelinePtr pipeline, BindGroupLayoutPtr textureBindGroupLayout, 
            BufferPtr uniformBuffer)
        {
            _device = device;
            _pipeline = pipeline;
            _textureBindGroupLayout = textureBindGroupLayout;
            _uniformBuffer = uniformBuffer;
        }

        public static Compositor Create(DevicePtr device, TextureFormat outputTextureFormat)
        {
            var uniformBuffer = device.CreateBuffer(BufferUsage.CopyDst | BufferUsage.Uniform, (ulong)Marshal.SizeOf<Uniforms>());

            var textureBindGroupLayout = device.CreateBindGroupLayout(s_bindGroupLayoutEntries);

            var module = device.CreateShaderModuleWGSL(
                s_shaderCode,
                compilationHints: null
            );

            var pipelineLayout = device.CreatePipelineLayout(
                new ReadOnlySpan<BindGroupLayoutPtr>(textureBindGroupLayout)
                );

            var pipeline = device.CreateRenderPipeline(
                pipelineLayout,
                vertex: new Safe.VertexState
                {
                    Buffers = Array.Empty<Safe.VertexBufferLayout>(),
                    EntryPoint = "vs_main",
                    Constants = Array.Empty<(string, double)>(),
                    Module = module
                },
                primitive: new Safe.PrimitiveState
                {
                    CullMode = CullMode.Back,
                    FrontFace = FrontFace.Ccw,
                    StripIndexFormat = IndexFormat.Undefined,
                    Topology = PrimitiveTopology.TriangleList
                },
                depthStencil: null,
                multisample: new MultisampleState
                {
                    Count = 1,
                    Mask = uint.MaxValue
                },
                fragment: new Safe.FragmentState
                {
                    Constants = Array.Empty<(string, double)>(),
                    EntryPoint = "fs_main",
                    Module = module,
                    Targets = new Safe.ColorTargetState[]
                    {
                        new Safe.ColorTargetState(outputTextureFormat, 
                        blendState: null, ColorWriteMask.All),
                    }
                }
                );

            return new(device, pipeline, textureBindGroupLayout, uniformBuffer);
        }

        public void Apply(DevicePtr device, QueuePtr queue, 
            TextureViewPtr inColor, TextureViewPtr inHighlight, TextureViewPtr inOutline, 
            TextureViewPtr inDepth, TextureViewPtr inHighlightDepth, TextureViewPtr output)
        {
            textureBindGroup?.Release();

            var uniforms = new Uniforms()
            {
                Gamma = 1
            };

            queue.WriteBuffer(_uniformBuffer, 0, new ReadOnlySpan<Uniforms>(uniforms));

            textureBindGroup = CreateBindGroup(this, inColor, inHighlight, inOutline,
                inDepth, inHighlightDepth);

            var cmd = device.CreateCommandEncoder();
            var pass = cmd.BeginRenderPass(
                new Safe.RenderPassColorAttachment[] 
                { 
                    new Safe.RenderPassColorAttachment(view: output, null, loadOp: LoadOp.Load, storeOp: StoreOp.Store, 
                    clearValue: default)
                }, timestampWrites: null, depthStencilAttachment: null, occlusionQuerySet: null);

            pass.SetPipeline(_pipeline);
            pass.SetBindGroup(0, textureBindGroup.Value, dynamicOffsets: null);
            pass.Draw(6, 1, 0, 0);
            pass.End();

            queue.Submit(new ReadOnlySpan<CommandBufferPtr>(cmd.Finish()));
        }
    }
}
