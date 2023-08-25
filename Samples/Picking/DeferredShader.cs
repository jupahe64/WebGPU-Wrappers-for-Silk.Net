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
    internal class DeferredShader
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
            public Matrix4X4<float> InverseViewProjection;
            public uint LightCount;
            public uint _padding0;
            public uint _padding1;
            public uint _padding2;
        }

        private DevicePtr _device;
        private RenderPipelinePtr _pipeline;
        private BindGroupLayoutPtr _textureBindGroupLayout;
        private SamplerPtr _sampler;
        private BufferPtr _uniformBuffer;

        private BindGroupPtr? textureBindGroup;

        private DeferredShader(DevicePtr device, RenderPipelinePtr pipeline, BindGroupLayoutPtr textureBindGroupLayout, 
            SamplerPtr sampler, BufferPtr uniformBuffer)
        {
            _device = device;
            _pipeline = pipeline;
            _textureBindGroupLayout = textureBindGroupLayout;
            _sampler = sampler;
            _uniformBuffer = uniformBuffer;
        }

        public static DeferredShader Create(DevicePtr device, TextureFormat lightTextureFormat)
        {
            var uniformBuffer = device.CreateBuffer(BufferUsage.CopyDst | BufferUsage.Uniform, (ulong)Marshal.SizeOf<Uniforms>());

            var sampler = device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, AddressMode.ClampToEdge,
                magFilter: FilterMode.Nearest, minFilter: FilterMode.Nearest, mipmapFilter: MipmapFilterMode.Nearest,
                lodMinClamp: 0, lodMaxClamp: 1, compare: CompareFunction.Undefined);

            var textureBindGroupLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Fragment, BufferBindingType.Uniform, (ulong)Marshal.SizeOf<Uniforms>()),
                    Buffer(1, ShaderStage.Fragment, BufferBindingType.ReadOnlyStorage, 0),
                    Texture(2, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, false),
                    Texture(3, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, false),
                    Texture(4, ShaderStage.Fragment, TextureSampleType.Depth, TextureViewDimension.Dimension2D, false)
                }
            );

            var module = device.CreateShaderModuleWGSL(
                """
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
                    InverseViewProjection: mat4x4<f32>,
                    LightCount: u32,
                }

                struct LightData {
                  position : vec4<f32>,
                  color : vec3<f32>,
                  radius : f32,
                }
                struct LightsBuffer {
                  lights: array<LightData>,
                }
                
                @group(0) @binding(0)
                var<uniform> ub: Uniforms;
                @group(0) @binding(1)
                var<storage, read> lightsBuffer: LightsBuffer;
                @group(0) @binding(2)
                var uAlbedo: texture_2d<f32>;
                @group(0) @binding(3)
                var uNormal: texture_2d<f32>;
                @group(0) @binding(4)
                var uDepth: texture_depth_2d;

                @fragment
                fn fs_main(v: Varyings) -> @location(0) vec4<f32> {
                    let albedo = textureLoad(uAlbedo, vec2<i32>(floor(v.Position.xy)), 0).rgb;
                    let normal = textureLoad(uNormal, vec2<i32>(floor(v.Position.xy)), 0).xyz;
                    let depth = textureLoad(uDepth, vec2<i32>(floor(v.Position.xy)), 0);

                    let nrm = normal.xyz * 2.0 - vec3(1.0);
                    let _position = ub.InverseViewProjection * 
                        vec4(v.TexCoord*2.0-vec2(1.0), depth, 1.0);
                    let position = _position.xyz/_position.w;

                    var result = vec3(0.0);

                    for (var i = 0u; i < ub.LightCount; i++) {
                      let L = lightsBuffer.lights[i].position.xyz - position;
                      let distance = length(L);
                      if (distance > lightsBuffer.lights[i].radius) {
                        continue;
                      }
                      let lambert = max(dot(nrm, normalize(L)), 0.0);
                      result += vec3<f32>(
                        lambert * pow(1.0 - distance / lightsBuffer.lights[i].radius, 2.0) * lightsBuffer.lights[i].color * albedo
                      );
                    }

                    // some manual ambient
                    result += vec3(0.1);

                    if (depth == 1.0) {
                        return vec4(albedo, 1.0);
                    } else {
                        return vec4(result, 1.0);
                    }
                }
                """,
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
                        new Safe.ColorTargetState(lightTextureFormat, 
                        (
                            color: new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.One), 
                            alpha: new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.Zero)
                        ), ColorWriteMask.All),
                    }
                }
                );

            return new(device, pipeline, textureBindGroupLayout, sampler, uniformBuffer);
        }

        public void Apply(DevicePtr device, QueuePtr queue, TextureViewPtr inAlbedo, TextureViewPtr inNormal, TextureViewPtr inDepth, TextureViewPtr output, 
            Matrix4X4<float> inverseViewProjection, BufferPtr lightsBuffer, uint lightCount)
        {
            textureBindGroup?.Release();

            var uniforms = new Uniforms()
            {
                InverseViewProjection = inverseViewProjection,
                LightCount = lightCount
            };

            queue.WriteBuffer(_uniformBuffer, 0, new ReadOnlySpan<Uniforms>(uniforms));

            textureBindGroup = _device.CreateBindGroup(_textureBindGroupLayout, new BindGroupEntry[]
            {
                Buffer(0, _uniformBuffer, 0, (ulong)Marshal.SizeOf<Uniforms>()),
                Buffer(1, lightsBuffer, 0, (ulong)Marshal.SizeOf<LightData>() * lightCount),
                Texture(2, inAlbedo),
                Texture(3, inNormal),
                Texture(4, inDepth),
            });

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
