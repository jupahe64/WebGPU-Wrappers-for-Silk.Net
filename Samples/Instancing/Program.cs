using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Safe = Silk.NET.WebGPU.Safe;

using Silk.NET.WebGPU.Safe;
using static Silk.NET.WebGPU.Safe.BindGroupLayoutEntries;
using static Silk.NET.WebGPU.Safe.BindGroupEntries;
using System.Runtime.CompilerServices;
using System;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.Linq;
using System.Threading;

namespace Instancing
{
    internal class Program
    {
        struct UniformMat4x3_RowMajor<T>
            where T : unmanaged, IFormattable, IEquatable<T>, IComparable<T>
        {
            public Vector4D<T> Row1;
            public Vector4D<T> Row2;
            public Vector4D<T> Row3;

            public static implicit operator UniformMat4x3_RowMajor<T>(Matrix4X4<T> m) => From(m);

            public static UniformMat4x3_RowMajor<T> From(Matrix4X4<T> m)
            {
                m = Matrix4X4.Transpose(m); //ColumnMajor -> RowMajor

                return new UniformMat4x3_RowMajor<T>
                {
                    Row1 = m.Row1,
                    Row2 = m.Row2,
                    Row3 = m.Row3
                };
            }

            public readonly Matrix4X4<T> ToMatrix4X4()
            {
                return Matrix4X4.Transpose( //RowMajor -> ColumnMajor
                    new Matrix4X4<T>(
                        Row1,
                        Row2,
                        Row3,
                        Vector4D<T>.UnitW
                        )
                );
            }
        }

        record struct BufferRange(BufferPtr Buffer, ulong Offset, ulong Size);

        private static IWindow? window;
        private static SurfacePtr surface;
        private static RenderPipelinePtr pipeline;
        private static SwapChainPtr? swapchain;
        private static TexturePtr? depthBuffer;
        private static TextureViewPtr? depthBufferView;
        private static DevicePtr device;

        private static BufferRange vertexBuffer;
        private static BufferRange sceneUniformBuffer;
        private static BindGroupPtr sceneBindGroup;
        private static BufferRange instanceBuffer;
        private static BindGroupPtr modelBindGroup;

        const uint InstanceCount = 1000;

        private static ParticleOnPath[] particles = Enumerable.Repeat(0, (int)InstanceCount).Select(
            _=> new ParticleOnPath
            {
                OffsetOnPath        = Random.Shared.NextSingle(),
                LateralOffset = Random.Shared.NextSingle() * 2 - 1,
            }).ToArray();

        struct ParticleOnPath
        {
            public float OffsetOnPath;
            public float LateralOffset;
        }

        struct Vertex
        {
            public Vector3D<float> Position;
            public Vector2D<float> TexCoord;
        }

        static void Main()
        {
            Console.WriteLine("Setting up Window");
            var options = WindowOptions.Default;
            options.API = GraphicsAPI.None;
            options.Size = new Vector2D<int>(1000, 700);
            window = Window.Create(options);

            window.Load += W_Load;
            window.Render += W_Render;
            window.Move += _ => window.DoRender();
            window.Resize += _ => window.DoRender();

            window.Run();
        }

        static BufferRange CreateBufferWithData<T>(BufferUsage usage, T[] data, string? label = null)
            where T : unmanaged
        {
            var bufferSize = (ulong)(Unsafe.SizeOf<T>() * data.LongLength);

            var buffer = device.CreateBuffer(usage,
                bufferSize,
                mappedAtCreation: true, label);

            var mappedBuffer = buffer.GetMappedRange<T>(0, (nuint)bufferSize);
            data.CopyTo(mappedBuffer);

            buffer.Unmap();
            return new(buffer, 0, bufferSize);
        }

        static (TexturePtr texture, TextureViewPtr view) CreateTextureFromRGBAImage(
            TextureUsage usage, Stream data, string? label = null)
        {
            using var image = Image.Load<Rgba32>(data);

            var pixelBuffer = new Rgba32[image.Width * image.Height];
            image.CopyPixelDataTo(pixelBuffer);

            var texture = device.CreateTexture(
                usage | TextureUsage.CopyDst, 
                TextureDimension.Dimension2D,
                new Extent3D
                {
                    Width = (uint)image.Width,
                    Height = (uint)image.Height,
                    DepthOrArrayLayers = 1,
                },
                TextureFormat.Rgba8Unorm,
                mipLevelCount: 1,
                sampleCount: 1,
                viewFormats: new[]
                {
                        TextureFormat.Rgba8Unorm
                },
                label: label);

            device.GetQueue().WriteTexture<Rgba32>(
                new Safe.ImageCopyTexture
                {
                    Aspect = TextureAspect.All,
                    MipLevel = 0,
                    Origin = new Origin3D(0, 0, 0),
                    Texture = texture
                }, pixelBuffer,
                new TextureDataLayout 
                { 
                    Offset = 0, 
                    BytesPerRow = 4 * (uint)image.Width, 
                    RowsPerImage = (uint)image.Height 
                },
                new Extent3D((uint)image.Width, (uint)image.Height, 1));

            var texureView = texture.CreateView(TextureFormat.Rgba8Unorm, TextureViewDimension.Dimension2D, TextureAspect.All,
                baseMipLevel: 0, mipLevelCount: 1, baseArrayLayer: 0, arrayLayerCount: 1,
                label: $"{label} - View");

            return (texture, texureView);
        }

        private async static void W_Load()
        {
            Console.WriteLine("Setting up WebGPU");
            var wgpu = WebGPU.GetApi();

            var instance = wgpu.CreateInstance();

            var adapter = await instance.RequestAdapter(
                backendType: BackendType.Undefined,
                powerPreference: PowerPreference.Undefined
            );

            surface = window!.CreateWebGPUSurface(wgpu, instance);

            var features = adapter.EnumerateFeatures();
            var limits = adapter.GetLimits();
            var properties = adapter.GetProperties();

            window!.Title = $"Instancing {properties.BackendType} {properties.Name}";

            device = await adapter.RequestDevice(requiredLimits: limits, requiredFeatures: features, 
                deviceLostCallback:(r, m) =>
                {
                    var message = m?
                    .Replace("\\r\\n", "\n")
                    .Replace("\\n", "\n")
                    .Replace("\\t", "\t");
                        Debugger.Break();
                }, 
                defaultQueueLabel: "DefaultQueue", label: "MainDevice");

            device.SetUncapturedErrorCallback((type, m) =>
            {
                var message = m?
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
                Debugger.Break();
            });

            var sceneBindGroupLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Vertex, BufferBindingType.Uniform, sizeof(float) * 16)
                },
                label: "SceneBindGroupLayout"
            );

            var modelBindGroupLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    // no data for slot 0,
                    Sampler(1, ShaderStage.Fragment, SamplerBindingType.Filtering),
                    Texture(2, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, multisampled: false)
                },
                label: "ModelBindGroupLayout"
            );

            var pipelineLayout = device.CreatePipelineLayout(
                new[]
                {
                    sceneBindGroupLayout,
                    modelBindGroupLayout
                },
                label: "PipelineLayout"
            );

            Console.WriteLine("Creating uniform resources");

            TextureViewPtr cubeTextureView;

            using (var fileStream = File.Open("CubeTexture.png", FileMode.Open))
            {
                (_, cubeTextureView) = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, fileStream,
                    label: "CubeTexture.png");
            }

            var linearSampler = device.CreateSampler(AddressMode.Repeat, AddressMode.Repeat, AddressMode.ClampToEdge,
                minFilter: FilterMode.Linear, magFilter: FilterMode.Linear, mipmapFilter: MipmapFilterMode.Linear,
                lodMinClamp: 0, lodMaxClamp: 1, compare: CompareFunction.Undefined, label: "LinearSampler");
            

            sceneUniformBuffer = new(
                device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, (ulong)Unsafe.SizeOf<Matrix4X4<float>>(),
                label: "SceneUniformBuffer"),
                0, (ulong)Unsafe.SizeOf<Matrix4X4<float>>());

            sceneBindGroup = device.CreateBindGroup(
                bindGroupLayout: sceneBindGroupLayout,
                new BindGroupEntry[]
                {
                    Buffer(0, sceneUniformBuffer.Buffer, sceneUniformBuffer.Offset, sceneUniformBuffer.Size)
                },
                label: "SceneBindGroup"
            );

            {
                var size = (ulong)Unsafe.SizeOf<UniformMat4x3_RowMajor<float>>() * InstanceCount;
                instanceBuffer = new(
                    device.CreateBuffer(BufferUsage.Vertex | BufferUsage.CopyDst, size,
                    label: "InstanceBuffer"),
                    0, size);
            }

            modelBindGroup = device.CreateBindGroup(
                bindGroupLayout: modelBindGroupLayout,
                new BindGroupEntry[]
                {
                    // no data for slot 0,
                    Sampler(1, linearSampler),
                    Texture(2, cubeTextureView)
                },
                label: "ModelBindGroup"
            );

            Console.WriteLine("Compiling Shader");

            var shaderModule = device.CreateShaderModuleWGSL(File.ReadAllBytes("shader.wgsl"),
                new Safe.ShaderModuleCompilationHint[]
                {
                    new ("vs_main", pipelineLayout),
                    new ("fs_main", pipelineLayout),
                });

            Console.WriteLine("Creating Pipeline");

            pipeline = device.CreateRenderPipeline(
                label: "DefaultPipeline",
                layout: pipelineLayout,
                vertex: new Safe.VertexState
                {
                    Module = shaderModule,
                    EntryPoint = "vs_main",
                    Constants = Array.Empty<(string key, double value)>(),
                    Buffers = new Safe.VertexBufferLayout[]
                    {
                        new(
                            (uint)Marshal.SizeOf<Vertex>(), VertexStepMode.Vertex,
                            new VertexAttribute[]
                            {
                                new(VertexFormat.Float32x3, shaderLocation: 0, offset:
                                    (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Position))),
                                new(VertexFormat.Float32x2, shaderLocation: 1, offset:
                                    (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.TexCoord))),
                            }
                        ),
                        new(
                            3*4*sizeof(float), VertexStepMode.Instance,
                            new VertexAttribute[]
                            {
                                new(VertexFormat.Float32x4, shaderLocation: 2, 
                                offset: 0),
                                new(VertexFormat.Float32x4, shaderLocation: 3, 
                                offset: 4*sizeof(float)),
                                new(VertexFormat.Float32x4, shaderLocation: 4,
                                offset: 8*sizeof(float)),
                            }
                        )
                    }
                },
                primitive: new Safe.PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.Back
                },
                depthStencil: new DepthStencilState
                {
                    Format = TextureFormat.Depth16Unorm,
                    DepthCompare = CompareFunction.LessEqual,
                    DepthWriteEnabled = true,
                    StencilFront = new StencilFaceState { Compare = CompareFunction.Always },
                    StencilBack = new StencilFaceState { Compare=CompareFunction.Always },
                },
                multisample: new MultisampleState
                {
                    Count = 1,
                    AlphaToCoverageEnabled = false,
                    Mask = uint.MaxValue
                },
                fragment: new Safe.FragmentState
                {
                    Module = shaderModule,
                    EntryPoint = "fs_main",
                    Constants = Array.Empty<(string key, double value)>(),
                    Targets = new Safe.ColorTargetState[]
                    {
                        new(
                            TextureFormat.Bgra8Unorm,
                            blendState: null,
                            ColorWriteMask.All
                        )
                    }
                }
            );

            Console.WriteLine("Creating Vertexbuffer");



            var cubeVertices = new Vertex[6 * 6];
            Quaternion<float>.CreateFromYawPitchRoll(0, 0, 0);

            int index = 0;

            foreach(var rot in new[] {
                Quaternion<float>.CreateFromYawPitchRoll(0, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(MathF.PI*0.5f, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(MathF.PI*1.0f, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(MathF.PI*1.5f, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(0, MathF.PI/2, 0),
                Quaternion<float>.CreateFromYawPitchRoll(0, -MathF.PI/2, 0),
            })
            {
                var a = new Vertex { Position = Vector3D.Transform(new(-1,  1, 1), rot), TexCoord = new(0, 0) };
                var b = new Vertex { Position = Vector3D.Transform(new( 1,  1, 1), rot), TexCoord = new(1, 0) };
                var c = new Vertex { Position = Vector3D.Transform(new(-1, -1, 1), rot), TexCoord = new(0, 1) };
                var d = new Vertex { Position = Vector3D.Transform(new( 1, -1, 1), rot), TexCoord = new(1, 1) };

                cubeVertices[index++] = b;
                cubeVertices[index++] = a;
                cubeVertices[index++] = c;

                cubeVertices[index++] = b;
                cubeVertices[index++] = c;
                cubeVertices[index++] = d;
            };

            vertexBuffer = CreateBufferWithData(BufferUsage.Vertex | BufferUsage.CopyDst, cubeVertices,
                "TriangleVertexBuffer");

            Console.WriteLine("Done");
        }

        private static Vector2D<int> _last_framebufferSize = new(0, 0);

        private static void W_Render(double deltaTime)
        {
            var framebufferSize = window!.FramebufferSize;
            if (_last_framebufferSize != framebufferSize)
            {
                _last_framebufferSize = framebufferSize;

                swapchain?.Release();
                swapchain = device.CreateSwapChain(surface,
                TextureUsage.RenderAttachment,
                TextureFormat.Bgra8Unorm,
                (uint)window!.FramebufferSize.X,
                (uint)window!.FramebufferSize.Y,
                PresentMode.Immediate);

                depthBuffer = device.CreateTexture(TextureUsage.RenderAttachment, TextureDimension.Dimension2D, new Extent3D
                {
                    Width = (uint)window!.FramebufferSize.X,
                    Height = (uint)window!.FramebufferSize.Y,
                    DepthOrArrayLayers = 1
                }, TextureFormat.Depth16Unorm, mipLevelCount: 1, sampleCount: 1, viewFormats: new TextureFormat[]
                {
                TextureFormat.Depth16Unorm
                }, label: "DepthBuffer");

                depthBufferView?.Release();
                depthBufferView = depthBuffer.Value.CreateView(TextureFormat.Depth16Unorm, TextureViewDimension.Dimension2D, 
                    TextureAspect.All, baseMipLevel: 0, mipLevelCount: 1, baseArrayLayer: 0, arrayLayerCount: 1, 
                    label: "DepthBuffer - View");
            }

            var swapchainView = swapchain!.Value.GetCurrentTextureView();

            var cmd = device.CreateCommandEncoder();

            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(swapchainView, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(.6, .9, 1, 1))
            }, Span<Safe.RenderPassTimestampWrite>.Empty, 
            new Safe.RenderPassDepthStencilAttachment(depthBufferView!.Value, LoadOp.Clear, StoreOp.Discard, 1f, false,
                /*stencil*/ LoadOp.Clear, StoreOp.Discard, 0, true),
            null);

            pass.SetPipeline(pipeline);

            pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

            pass.SetVertexBuffer(1, instanceBuffer.Buffer,
                instanceBuffer.Offset, instanceBuffer.Size);

            pass.SetBindGroup(0, sceneBindGroup, ReadOnlySpan<uint>.Empty);
            pass.SetBindGroup(1, modelBindGroup, ReadOnlySpan<uint>.Empty);

            pass.Draw(6*6, InstanceCount, 0, 0);

            pass.End();

            var queue = device.GetQueue();

            float time = (float)window!.Time;

            float yaw = -MathF.PI / 4;
            Vector2D<float> cameraOffset;

            {
                float durationA = 10;
                float durationB = 10;

                float t = time % (durationA + durationB);

                if (t < durationA)
                {
                    cameraOffset = Vector2D.Lerp<float>(
                        new(-20f, 0f),
                        new(20f, 0f),
                        t / durationA
                    );
                }
                else
                {
                    cameraOffset = Vector2D.Lerp<float>(
                        new(20f, 0f),
                        new(-20f, 20f),
                        (t - durationA) / durationB
                    );

                    yaw = MathF.PI / 8;
                }

            }
            

            queue.WriteBuffer<Matrix4X4<float>>(sceneUniformBuffer.Buffer, sceneUniformBuffer.Offset,
            new Matrix4X4<float>[]
            {
                Matrix4X4.CreateTranslation(-10f, 15f, 0f) *
                Matrix4X4.CreateRotationY(-MathF.PI/2-yaw) *
                Matrix4X4.CreateRotationX(MathF.PI / 4) *
                Matrix4X4.CreateTranslation(-cameraOffset.X, -cameraOffset.Y, -30) *
                Matrix4X4.CreatePerspectiveFieldOfView((float)((2 * Math.PI) / 5),  
                    (float)window.Size.X/window.Size.Y, 0.01f, 100f)
            });

            var instanceData = new UniformMat4x3_RowMajor<float>[InstanceCount];

            for (int i = 0; i < InstanceCount; i++)
            {
                float minX = -100;
                float maxX = 60;
                float speed = 40;

                var t = (particles[i].OffsetOnPath + time * speed/(maxX-minX)) % 1f;

                float x = (1-t) * minX + t * maxX;
                float y = -0.05f * MathF.Pow(MathF.Max(0, x), 2);
                float z = particles[i].LateralOffset * 10;

                instanceData[i] =
                    Matrix4X4.CreateScale(0.75f+0.25f*MathF.Sin(i)) *
                    Matrix4X4.CreateFromYawPitchRoll(time+i, time+i, 0) *
                    Matrix4X4.CreateTranslation(x, y, z);
            }

            queue.WriteBuffer<UniformMat4x3_RowMajor<float>>(instanceBuffer.Buffer, instanceBuffer.Offset,
                instanceData);

            queue.Submit(new[] { cmd.Finish() });

            swapchain.Value.Present();
        }
    }
}