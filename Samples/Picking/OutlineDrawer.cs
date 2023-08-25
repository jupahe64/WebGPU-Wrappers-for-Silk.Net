using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Safe;
using Safe = Silk.NET.WebGPU.Safe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Silk.NET.WebGPU.Safe.BindGroupLayoutEntries;
using static Silk.NET.WebGPU.Safe.BindGroupEntries;
using Silk.NET.Maths;
using System.Runtime.CompilerServices;

namespace Picking
{
    internal class OutlineDrawer
    {
        private static readonly BindGroupLayoutEntry[] s_bindGroupLayoutEntries = new BindGroupLayoutEntry[]
        {
            Texture(0, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, false),
            Texture(1, ShaderStage.Fragment, TextureSampleType.Uint, TextureViewDimension.Dimension2D, false),
            Texture(2, ShaderStage.Fragment, TextureSampleType.Depth, TextureViewDimension.Dimension2D, false),
            Texture(3, ShaderStage.Fragment, TextureSampleType.Depth, TextureViewDimension.Dimension2D, false)
        };

        private static BindGroupPtr CreateBindGroup(OutlineDrawer c, 
            TextureViewPtr inHighligh, TextureViewPtr inDepth, TextureViewPtr inID, TextureViewPtr inSceneDepth)
        {
            return c._device.CreateBindGroup(c._textureBindGroupLayout, new BindGroupEntry[]
            {
                Texture(0, inHighligh),
                Texture(1, inID),
                Texture(2, inDepth),
                Texture(3, inSceneDepth),
            });
        }

        private static readonly string s_shaderCode = """
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

            @group(0) @binding(0)
            var uColor: texture_2d<f32>;
            @group(0) @binding(1)
            var uId: texture_2d<u32>;
            @group(0) @binding(2)
            var uDepth: texture_depth_2d;
            @group(0) @binding(3)
            var uSceneDepth: texture_depth_2d;

            @fragment
            fn fs_main(v: Varyings) -> @location(0) vec4<f32> {
                let pos0 = vec2<i32>(v.Position.xy);
                let pos1 = pos0 + vec2(-1, 0);
                let pos2 = pos0 + vec2( 0,-1);
                let pos3 = pos0 + vec2( 1, 0);
                let pos4 = pos0 + vec2( 0, 1);

                let i0 = textureLoad(uId, pos0, 0).r;
                let i1 = textureLoad(uId, pos1, 0).r;
                let i2 = textureLoad(uId, pos2, 0).r;
                let i3 = textureLoad(uId, pos3, 0).r;
                let i4 = textureLoad(uId, pos4, 0).r;

                let d0 = textureLoad(uDepth, pos0, 0);
                let d1 = textureLoad(uDepth, pos1, 0);
                let d2 = textureLoad(uDepth, pos2, 0);
                let d3 = textureLoad(uDepth, pos3, 0);
                let d4 = textureLoad(uDepth, pos4, 0);

                //closest position to the camera (x, y, depth)
                var cPos = vec3(vec2<f32>(pos0), d0);
                cPos = select(cPos, vec3(vec2<f32>(pos1), d1), d1 < cPos.z);
                cPos = select(cPos, vec3(vec2<f32>(pos2), d2), d2 < cPos.z);
                cPos = select(cPos, vec3(vec2<f32>(pos3), d3), d3 < cPos.z);
                cPos = select(cPos, vec3(vec2<f32>(pos4), d4), d4 < cPos.z);

                let pos = select(pos0, vec2<i32>(cPos.xy), i0==0u);

                let color = textureLoad(uColor, pos, 0);
                let sceneDepth = textureLoad(uSceneDepth, pos, 0);

                let isEdge = 
                    i0!=i1 ||
                    i0!=i2 ||
                    i0!=i3 ||
                    i0!=i4;

                let alpha = select(0.5, 1.0, cPos.z <= sceneDepth);

                return select(vec4(0.0), vec4(color.rgb, alpha), isEdge);
            }
            """;

        private struct UbBlur
        {
            public float Radius;
            private uint _padding0;
            private uint _padding1;
            private uint _padding2;
        }

        private static readonly BindGroupLayoutEntry[] s_blurBindGroupLayoutEntries = new BindGroupLayoutEntry[]
        {
            Buffer(0, ShaderStage.Fragment, BufferBindingType.Uniform, (uint)Unsafe.SizeOf<UbBlur>()),
            Sampler(1, ShaderStage.Fragment, SamplerBindingType.Filtering),
            Texture(2, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, false),
        };

        private static BindGroupPtr CreateBlurBindGroup(OutlineDrawer c, TextureViewPtr input, SamplerPtr sampler)
        {
            return c._device.CreateBindGroup(c._blurBindGroupLayout, new BindGroupEntry[]
            {
                Buffer(0, c._blurUniformBuffer, 0, (uint)Unsafe.SizeOf<UbBlur>()),
                Sampler(1, sampler),
                Texture(2, input),
            });
        }

        private static readonly string s_blurShaderCode = """
            struct Varyings {
                @builtin(position) Position: vec4<f32>,
                @location(0) TexCoord: vec2<f32>,
            }

            struct UbBlur {
                Radius: f32
            }

            @group(0) @binding(0)
            var<uniform> ubBlur: UbBlur;

            @group(0) @binding(1)
            var uSampler: sampler;

            @group(0) @binding(2)
            var uInput: texture_2d<f32>;



            //adapted from https://www.shadertoy.com/view/ltBXRh

            var<private> mSize: i32 = 25;
            var<private> sigma: f32 = 7.0;
            var<private> kernel: array<f32, 25/*mSize*/>;

            
            fn normpdf(x: f32, sigma: f32) -> f32
            {
            	return 0.39894*exp(-0.5 * x * x / (sigma * sigma)) / sigma;
            }

            @fragment
            fn fs_pass1(v: Varyings) -> @location(0) vec4<f32> {
                let fragCoord = vec2<i32>(v.Position.xy);
                let kSize = (mSize - 1)/2;

                let blurSize = ubBlur.Radius / f32(kSize);

                var res = vec4(0.0);

                var Z = 0.0;
                for (var j = 0; j <= kSize; j++) {
                    let val = normpdf(f32(j), sigma);
                    kernel[kSize+j] = val;
                    kernel[kSize-j] = val;
                }

                for (var j = 0; j < mSize; j++) {
                    Z += kernel[j];
                }

                for (var i=-kSize; i <= kSize; i++) {
                    let color = textureLoad(uInput, fragCoord + vec2(i32(f32(i) * blurSize), 0), 0);
                    res += kernel[kSize+i]*color * floor(color.a); //only blur fully opaque pixels
                }

                return res/Z;
            }
            @fragment
            fn fs_pass2(v: Varyings) -> @location(0) vec4<f32> {
                let fragCoord = vec2<i32>(v.Position.xy);
                let kSize = (mSize - 1)/2;

                let blurSize = ubBlur.Radius / f32(kSize);
            
                var res = vec4(0.0);
            
                var Z = 0.0;
                for (var j = 0; j <= kSize; j++) {
                    let val = normpdf(f32(j), sigma);
                    kernel[kSize+j] = val;
                    kernel[kSize-j] = val;
                }
            
                for (var j = 0; j < mSize; j++) {
                    Z += kernel[j];
                }
            
                for (var i=-kSize; i <= kSize; i++) {
                    res += kernel[kSize+i]*textureLoad(uInput, fragCoord + vec2(0, i32(f32(i) * blurSize)), 0);
                }

                let result = res/Z;

                let alphaFactor = ubBlur.Radius/2.0 + 1.0;

                //convert from premultiplied (which is preserved in the blur) to straight
                //and clip the alpha to get a nice thick outline
                return vec4(result.rgb/result.a, clamp(result.a * 12.0, 0.0, 1.0));
            }
            """;

        private DevicePtr _device;
        private RenderPipelinePtr _pipeline;
        private (RenderPipelinePtr pass1, RenderPipelinePtr pass2) _blurPipelines;
        private BindGroupLayoutPtr _textureBindGroupLayout;
        private BindGroupLayoutPtr _blurBindGroupLayout;
        private BufferPtr _blurUniformBuffer;

        private BindGroupPtr? _textureBindGroup = null;
        private BindGroupPtr? _blurPassABindGroup = null;
        private BindGroupPtr? _blurPassBBindGroup = null;
        private TexturePtr? _tempTexture;

        public OutlineDrawer(DevicePtr device, RenderPipelinePtr pipeline, 
            (RenderPipelinePtr pass1, RenderPipelinePtr pass2) blurPipelines, 
            BindGroupLayoutPtr textureBindGroupLayout, BindGroupLayoutPtr blurBindGroupLayout, 
            BufferPtr blurUniformBuffer)
        {
            _device = device;
            _pipeline = pipeline;
            _blurPipelines = blurPipelines;
            _textureBindGroupLayout = textureBindGroupLayout;
            _blurBindGroupLayout = blurBindGroupLayout;
            _blurUniformBuffer = blurUniformBuffer;
        }

        public static OutlineDrawer Create(DevicePtr device, TextureFormat outputTextureFormat)
        {
            var textureBindGroupLayout = device.CreateBindGroupLayout(s_bindGroupLayoutEntries);
            var blurBindGroupLayout = device.CreateBindGroupLayout(s_blurBindGroupLayoutEntries);

            var blurUniformBuffer = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, 
                (uint)Unsafe.SizeOf<UbBlur>());

            var module = device.CreateShaderModuleWGSL(
                s_shaderCode,
                compilationHints: null
            );

            var blurShaderModule = device.CreateShaderModuleWGSL(
                s_blurShaderCode,
                compilationHints: null
            );

            var vertexState = new Safe.VertexState
            {
                Buffers = Array.Empty<Safe.VertexBufferLayout>(),
                EntryPoint = "vs_main",
                Constants = Array.Empty<(string, double)>(),
                Module = module
            };


            var primitiveState = new Safe.PrimitiveState
            {
                CullMode = CullMode.Back,
                FrontFace = FrontFace.Ccw,
                StripIndexFormat = IndexFormat.Undefined,
                Topology = PrimitiveTopology.TriangleList
            };

            var multisampleState = new MultisampleState
            {
                Count = 1,
                Mask = uint.MaxValue
            };

            var pipelineLayout = device.CreatePipelineLayout(
                new ReadOnlySpan<BindGroupLayoutPtr>(textureBindGroupLayout)
                );

            var pipeline = device.CreateRenderPipeline(
                pipelineLayout,
                vertex: vertexState,
                primitive: primitiveState,
                depthStencil: null,
                multisample: multisampleState,
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

            pipelineLayout = device.CreatePipelineLayout(
                new ReadOnlySpan<BindGroupLayoutPtr>(blurBindGroupLayout)
                );

            var blurPass1Pipeline = device.CreateRenderPipeline(
                pipelineLayout,
                vertex: vertexState,
                primitive: primitiveState,
                depthStencil: null,
                multisample: multisampleState,
                fragment: new Safe.FragmentState
                {
                    Constants = Array.Empty<(string, double)>(),
                    EntryPoint = "fs_pass1",
                    Module = blurShaderModule,
                    Targets = new Safe.ColorTargetState[]
                    {
                        new Safe.ColorTargetState(TextureFormat.Rgba8Unorm,
                        blendState: null, ColorWriteMask.All),
                    }
                }
            );

            var blurPass2Pipeline = device.CreateRenderPipeline(
                pipelineLayout,
                vertex: vertexState,
                primitive: primitiveState,
                depthStencil: null,
                multisample: multisampleState,
                fragment: new Safe.FragmentState
                {
                    Constants = Array.Empty<(string, double)>(),
                    EntryPoint = "fs_pass2",
                    Module = blurShaderModule,
                    Targets = new Safe.ColorTargetState[]
                    {
                        new Safe.ColorTargetState(outputTextureFormat,
                        blendState: (
                            new BlendComponent(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                            new BlendComponent(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha)
                            ), ColorWriteMask.All),
                    }
                }
            );

            return new(device, pipeline, (blurPass1Pipeline, blurPass2Pipeline), 
                textureBindGroupLayout, blurBindGroupLayout, blurUniformBuffer);
        }

        public void Apply(DevicePtr device, QueuePtr queue, TextureViewPtr inOutlineColor, TextureViewPtr inDepth,
            TextureViewPtr inID, TextureViewPtr inSceneDepth, Vector2D<uint> framebufferSize, TextureViewPtr output, float thickness = 2)
        {
            var sampler = device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, AddressMode.ClampToEdge, 
                FilterMode.Linear, FilterMode.Linear, MipmapFilterMode.Linear, lodMinClamp: 0, lodMaxClamp: 1, CompareFunction.Undefined);

            _textureBindGroup?.Release();
            _textureBindGroup = CreateBindGroup(this, inOutlineColor, inDepth, inID, inSceneDepth);

            float radius = (thickness - 2) / 2;

            bool isThickOutline = radius > 0;

            TextureViewPtr tempTextureView = default;

            if (isThickOutline)
            {
                queue.WriteBuffer(_blurUniformBuffer, 0, new ReadOnlySpan<UbBlur>(
                    new UbBlur
                    {
                        Radius = radius+0.25f
                    }
                ));

                _blurPassABindGroup?.Release();
                _blurPassABindGroup = CreateBlurBindGroup(this, output, sampler);

                _tempTexture?.Destroy();

                _tempTexture = device.CreateTexture(TextureUsage.TextureBinding | TextureUsage.RenderAttachment, TextureDimension.Dimension2D,
                    new Extent3D(framebufferSize.X, framebufferSize.Y, 1), TextureFormat.Rgba8Unorm, mipLevelCount: 1, sampleCount: 1,
                    new ReadOnlySpan<TextureFormat>(TextureFormat.Rgba8Unorm));

                tempTextureView = _tempTexture.Value.CreateView(TextureFormat.Rgba8Unorm, TextureViewDimension.Dimension2D, TextureAspect.All,
                    baseMipLevel: 0, mipLevelCount: 1, baseArrayLayer: 0, arrayLayerCount: 1);

                _blurPassBBindGroup?.Release();
                _blurPassBBindGroup = CreateBlurBindGroup(this, tempTextureView, sampler);
            }

            var cmd = device.CreateCommandEncoder();
            var pass = cmd.BeginRenderPass(
                new Safe.RenderPassColorAttachment[] 
                { 
                    new Safe.RenderPassColorAttachment(view: output, null, loadOp: LoadOp.Load, storeOp: StoreOp.Store, 
                    clearValue: default)
                }, timestampWrites: null, depthStencilAttachment: null, occlusionQuerySet: null);

            pass.SetPipeline(_pipeline);
            pass.SetBindGroup(0, _textureBindGroup.Value, dynamicOffsets: null);
            pass.Draw(6, 1, 0, 0);
            pass.End();

            if (isThickOutline)
            {
                pass = cmd.BeginRenderPass(
                new Safe.RenderPassColorAttachment[]
                {
                    new Safe.RenderPassColorAttachment(view: tempTextureView, null, loadOp: LoadOp.Load, storeOp: StoreOp.Store,
                    clearValue: default)
                }, timestampWrites: null, depthStencilAttachment: null, occlusionQuerySet: null);

                pass.SetPipeline(_blurPipelines.pass1);
                pass.SetBindGroup(0, _blurPassABindGroup!.Value, dynamicOffsets: null);
                pass.Draw(6, 1, 0, 0);
                pass.End();

                pass = cmd.BeginRenderPass(
                    new Safe.RenderPassColorAttachment[]
                    {
                    new Safe.RenderPassColorAttachment(view: output, null, loadOp: LoadOp.Load, storeOp: StoreOp.Store,
                    clearValue: default)
                    }, timestampWrites: null, depthStencilAttachment: null, occlusionQuerySet: null);

                pass.SetPipeline(_blurPipelines.pass2);
                pass.SetBindGroup(0, _blurPassBBindGroup!.Value, dynamicOffsets: null);
                pass.Draw(6, 1, 0, 0);
                pass.End();
            }

            queue.Submit(new ReadOnlySpan<CommandBufferPtr>(cmd.Finish()));
        }
    }
}
