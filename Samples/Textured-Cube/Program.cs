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

namespace TexturedCube
{
    internal class Program
    {
        record struct BufferRange(BufferPtr Buffer, ulong Offset, ulong Size);

        private static IWindow? window;
        private static RenderPipelinePtr pipeline;
        private static SwapChainPtr swapchain;
        private static DevicePtr device;

        private static BufferRange vertexBuffer;
        private static BufferRange sceneUniformBuffer;
        private static BindGroupPtr sceneBindGroup;
        private static BufferRange modelUniformBuffer;
        private static BindGroupPtr modelBindGroup;

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

            SurfacePtr surface = window!.CreateWebGPUSurface(wgpu, instance);

            var features = adapter.EnumerateFeatures();
            var limits = adapter.GetLimits();
            var properties = adapter.GetProperties();

            window!.Title = $"Textured-Cube {properties.BackendType} {properties.Name}";

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
                    Buffer(0, ShaderStage.Vertex, BufferBindingType.Uniform, sizeof(float) * 16),
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

            modelUniformBuffer = new(
                device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, (ulong)Unsafe.SizeOf<Matrix4X4<float>>(), 
                label: "ModelUniformBuffer"),
                0, (ulong)Unsafe.SizeOf<Matrix4X4<float>>());

            modelBindGroup = device.CreateBindGroup(
                bindGroupLayout: modelBindGroupLayout,
                new BindGroupEntry[]
                {
                    Buffer(0, modelUniformBuffer.Buffer, modelUniformBuffer.Offset, modelUniformBuffer.Size),
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
                        )
                    }
                },
                primitive: new Safe.PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.Back
                },
                depthStencil: null,
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

            Console.WriteLine("Creating Swapchain");

            swapchain = device.CreateSwapChain(surface,
                TextureUsage.RenderAttachment,
                TextureFormat.Bgra8Unorm,
                (uint)window!.FramebufferSize.X,
                (uint)window!.FramebufferSize.Y,
                PresentMode.Immediate);

            window.FramebufferResize += _ =>
            {
                swapchain.Release();
                swapchain = device.CreateSwapChain(surface,
                TextureUsage.RenderAttachment,
                TextureFormat.Bgra8Unorm,
                (uint)window!.FramebufferSize.X,
                (uint)window!.FramebufferSize.Y,
                PresentMode.Immediate);
            };

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

        private static void W_Render(double deltaTime)
        {
            var swapchainView = swapchain.GetCurrentTextureView();

            var cmd = device.CreateCommandEncoder();

            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(swapchainView, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(.06, .1, .1, 1))
            }, Span<Safe.RenderPassTimestampWrite>.Empty, null,
            null);

            pass.SetPipeline(pipeline);

            pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

            pass.SetBindGroup(0, sceneBindGroup, ReadOnlySpan<uint>.Empty);
            pass.SetBindGroup(1, modelBindGroup, ReadOnlySpan<uint>.Empty);

            pass.Draw(6*6, 1, 0, 0);

            pass.End();

            var queue = device.GetQueue();

            float time = (float)window!.Time;

            queue.WriteBuffer<Matrix4X4<float>>(sceneUniformBuffer.Buffer, sceneUniformBuffer.Offset,
            new Matrix4X4<float>[]
            {
                Matrix4X4.CreateTranslation(0f, 0f, -10) *
                Matrix4X4.CreatePerspectiveFieldOfView((float)((2 * Math.PI) / 5),  
                    (float)window.Size.X/window.Size.Y, 0.01f, 100f)
            });

            queue.WriteBuffer<Matrix4X4<float>>(modelUniformBuffer.Buffer, modelUniformBuffer.Offset,
            new Matrix4X4<float>[]
            {
                Matrix4X4.CreateFromYawPitchRoll(time*2, time, 0) *
                Matrix4X4.CreateTranslation(0f, MathF.Sin(time*2)*0.5f+0.5f, 0f)
            });

            queue.Submit(new[] { cmd.Finish() });

            swapchain.Present();
        }
    }
}