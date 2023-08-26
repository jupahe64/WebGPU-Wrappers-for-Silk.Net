using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Safe = Silk.NET.WebGPU.Safe;

using Silk.NET.WebGPU.Safe;
using static Silk.NET.WebGPU.Safe.BindGroupLayoutEntries;

namespace HelloTriangle
{
    internal class Program
    {
        record struct BufferRange(BufferPtr Buffer, ulong Offset, ulong Size);

        private static IWindow window;
        private static RenderPipelinePtr pipeline;
        private static SwapChainPtr swapchain;
        private static DevicePtr device;

        private static BufferRange vertexBuffer;

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
            var bufferSize = (ulong)(Marshal.SizeOf<T>() * data.LongLength);

            var buffer = device.CreateBuffer(usage,
                bufferSize,
                mappedAtCreation: true, label);

            var mappedBuffer = buffer.GetMappedRange<T>(0, (nuint)bufferSize);
            data.CopyTo(mappedBuffer);

            buffer.Unmap();
            return new(buffer, 0, bufferSize);
        }

        private async static void W_Load()
        {
            Console.WriteLine("Setting up WebGPU");
            var wgpu = WebGPU.GetApi();

            var instance = wgpu.CreateInstance();

            var adapter = await instance.RequestAdapter(
                backendType: default,
                powerPreference: PowerPreference.Undefined
            );

            SurfacePtr surface = window.CreateWebGPUSurface(wgpu, instance);

            var features = adapter.EnumerateFeatures();
            var limits = adapter.GetLimits();
            var properties = adapter.GetProperties();

            window!.Title = $"Hello-Triangle {properties.BackendType} {properties.Name}";

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

            var pipelineLayout = device.CreatePipelineLayout(
                Array.Empty<BindGroupLayoutPtr>(),
                label: "EmptyPipelineLayout"
            );

            device.SetUncapturedErrorCallback((type, m) =>
            {
                var message = m?
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
                Debugger.Break();
            });

            device.PushErrorScope(ErrorFilter.Validation);

            Console.WriteLine("Compiling Shader");

            var shaderModule = device.CreateShaderModuleWGSL(File.ReadAllBytes("shader.wgsl"),
                new Safe.ShaderModuleCompilationHint[]
                {
                    new ("vs_main", pipelineLayout),
                    new ("fs_main", pipelineLayout),
                });

            //var result = await shaderModule.GetCompilationInfo();
            //foreach (var message in result.Messages)
            //{
            //    if (message.Type == CompilationMessageType.Error)
            //        Debugger.Break();
            //    else
            //        Console.WriteLine($"{message.Type}: {message.Message}");
            //}

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
                    CullMode = CullMode.None,
                    UnclippedDepth = true
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

            window.FramebufferResize += size =>
            {
                if (size.X * size.Y == 0)
                    return;

                swapchain = device.CreateSwapChain(surface,
                TextureUsage.RenderAttachment,
                TextureFormat.Bgra8Unorm,
                (uint)size.X,
                (uint)size.Y,
                PresentMode.Immediate);
            };

            Console.WriteLine("Creating Vertexbuffer");

            var triangleVertices = new Vertex[]
            {
                new Vertex { Position = new(-1, -1, 2), TexCoord = new(0, 1) },
                new Vertex { Position = new(1, -1, 2),  TexCoord = new(1, 1) },
                new Vertex { Position = new(0, 1, 0),   TexCoord = new(0.5f, 0) },
            };

            vertexBuffer = CreateBufferWithData(BufferUsage.Vertex | BufferUsage.CopyDst, triangleVertices,
                "TriangleVertexBuffer");

            var error = await device.PopErrorScope();

            if (error is not null)
            {
                string message = error.Message;
                Debugger.Break();
            }

            Console.WriteLine("Done");
        }

        private static void W_Render(double deltaTime)
        {
            var swapchainView = swapchain.GetCurrentTextureView();

            var cmd = device.CreateCommandEncoder();

            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(swapchainView, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(.1, .1, .1, 1))
            }, Span<Safe.RenderPassTimestampWrite>.Empty, null,
            null);

            pass.SetPipeline(pipeline);

            pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

            pass.Draw(3, 1, 0, 0);

            pass.End();

            var queue = device.GetQueue();

            queue.Submit(new[] { cmd.Finish() });

            swapchain.Present();
        }
    }
}