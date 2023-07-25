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
using Silk.NET.WebGPU.Extensions.ImGui;
using ImGuiNET;
using Silk.NET.Input;
using NativeFileDialogExtendedSharp;
using System.Numerics;
using static Silk.NET.Maths.SystemNumericsExtensions;

namespace ImGuiDemo
{
    internal partial class Program
    {
        record struct BufferRange(BufferPtr Buffer, ulong Offset, ulong Size);

        private static IWindow? window;
        private static RenderPipelinePtr pipeline;
        private static SwapChainPtr? swapchain;
        private static DevicePtr device;

        private static BufferRange vertexBuffer;
        private static SamplerPtr linearSampler;
        private static BufferRange sceneUniformBuffer;
        private static BindGroupPtr sceneBindGroup;
        private static BufferRange modelUniformBuffer;
        private static BindGroupLayoutPtr modelBindGroupLayout;
        private static BindGroupPtr modelBindGroup;
        private static ImFontPtr imguiDefaultFont;
        private static ImFontPtr quicksandFont;
        private static ImGuiController? imguiController;
        private static Framebuffer? sceneViewport;
        private static SurfacePtr surface;

        private static (TexturePtr tex, TextureViewPtr view) cubeTexture;
        private static Vector4 cubeColor = Vector4.One;

        struct Vertex
        {
            public Vector3D<float> Position;
            public Vector2D<float> TexCoord;
        }

        struct ModelUB
        {
            public Matrix4X4<float> Transform;
            public Vector4D<float> Color;
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
            window.Move += _ =>
            {
                window.DoUpdate();
                window.DoRender();
            };
            window.Resize += _ =>
            {
                window.DoUpdate();
                window.DoRender();
            };
            window.Update += W_Update;

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

            var textureView = texture.CreateView(TextureFormat.Rgba8Unorm, TextureViewDimension.Dimension2D, TextureAspect.All,
                baseMipLevel: 0, mipLevelCount: 1, baseArrayLayer: 0, arrayLayerCount: 1,
                label: $"{label} - View");

            return (texture, textureView);
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

            window!.Title = $"Imgui {properties.BackendType} {properties.Name}";

            device = await adapter.RequestDevice(requiredLimits: limits, requiredFeatures: features,
                deviceLostCallback: (r, m) =>
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

            unsafe static void SetupFonts()
            {
                imguiDefaultFont = ImGui.GetIO().Fonts.AddFontDefault();

                var fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "Quicksand-SemiBold.ttf");

                var nativeConfig = ImGuiNative.ImFontConfig_ImFontConfig();
                //Add a higher horizontal/vertical sample rate for global scaling.
                nativeConfig->OversampleH = 8;
                nativeConfig->OversampleV = 8;
                nativeConfig->RasterizerMultiply = 1f;
                nativeConfig->GlyphOffset = new Vector2(0);

                quicksandFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontPath, 18, nativeConfig);
            }

            imguiController = new ImGuiController(device,
                TextureFormat.Bgra8Unorm,
                window, window.CreateInput(), SetupFonts);

            var io = ImGui.GetIO();

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;         // Enable Docking
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;       // Enable Multi-Viewport / Platform Windows

            sceneViewport = Framebuffer.Create(device, TextureFormat.Rgba8Unorm, label: "SceneFB");

            var sceneBindGroupLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Vertex, BufferBindingType.Uniform, sizeof(float) * 16)
                },
                label: "SceneBindGroupLayout"
            );

            modelBindGroupLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Vertex | ShaderStage.Fragment, BufferBindingType.Uniform, (ulong)Unsafe.SizeOf<ModelUB>()),
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

            using (var fileStream = File.Open("CubeTexture.png", FileMode.Open))
            {
                cubeTexture = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, fileStream,
                    label: "CubeTexture.png");
            }

            linearSampler = device.CreateSampler(AddressMode.Repeat, AddressMode.Repeat, AddressMode.ClampToEdge,
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
                device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, (ulong)Unsafe.SizeOf<ModelUB>(),
                label: "ModelUniformBuffer"),
                0, (ulong)Unsafe.SizeOf<ModelUB>());

            modelBindGroup = device.CreateBindGroup(
                bindGroupLayout: modelBindGroupLayout,
                new BindGroupEntry[]
                {
                    Buffer(0, modelUniformBuffer.Buffer, modelUniformBuffer.Offset, modelUniformBuffer.Size),
                    Sampler(1, linearSampler),
                    Texture(2, cubeTexture.view)
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
                            TextureFormat.Rgba8Unorm,
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

            foreach (var rot in new[] {
                Quaternion<float>.CreateFromYawPitchRoll(0, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(MathF.PI*0.5f, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(MathF.PI*1.0f, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(MathF.PI*1.5f, 0, 0),
                Quaternion<float>.CreateFromYawPitchRoll(0, MathF.PI/2, 0),
                Quaternion<float>.CreateFromYawPitchRoll(0, -MathF.PI/2, 0),
            })
            {
                var a = new Vertex { Position = Vector3D.Transform(new(-1, 1, 1), rot), TexCoord = new(0, 0) };
                var b = new Vertex { Position = Vector3D.Transform(new(1, 1, 1), rot), TexCoord = new(1, 0) };
                var c = new Vertex { Position = Vector3D.Transform(new(-1, -1, 1), rot), TexCoord = new(0, 1) };
                var d = new Vertex { Position = Vector3D.Transform(new(1, -1, 1), rot), TexCoord = new(1, 1) };

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

        private static void W_Update(double deltaTime)
        {
            imguiController!.Update((float)deltaTime);
        }

        private static void RenderScene(CommandEncoderPtr cmd, QueuePtr queue, TextureViewPtr colorTarget0, float aspectRatio)
        {
            float time = (float)window!.Time;

            queue.WriteBuffer(sceneUniformBuffer.Buffer, sceneUniformBuffer.Offset,
            new ReadOnlySpan<Matrix4X4<float>>(
                Matrix4X4.CreateTranslation(0f, 0f, -10) *
                Matrix4X4.CreatePerspectiveFieldOfView((float)((2 * Math.PI) / 5),
                    aspectRatio, 0.01f, 100f)
            ));

            queue.WriteBuffer(modelUniformBuffer.Buffer, modelUniformBuffer.Offset,


            new ReadOnlySpan<ModelUB>(new ModelUB
            {
                Transform =
                Matrix4X4.CreateFromYawPitchRoll(time*2, time, 0) *
                Matrix4X4.CreateTranslation(0f, MathF.Sin(time*2)*0.5f+0.5f, 0f),

                Color = ToGeneric(cubeColor)
            }));

            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(colorTarget0, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(.06, .1, .1, 1))
            }, Span<Safe.RenderPassTimestampWrite>.Empty, null,
            null);

            pass.SetPipeline(pipeline);

            pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

            pass.SetBindGroup(0, sceneBindGroup, ReadOnlySpan<uint>.Empty);
            pass.SetBindGroup(1, modelBindGroup, ReadOnlySpan<uint>.Empty);

            pass.Draw(6 * 6, 1, 0, 0);

            pass.End();
        }

        private static Vector2D<int> _last_framebufferSize = new(0,0);
        private static (TexturePtr tex, TextureViewPtr view)? requestedCubeTexture = null;

        private static void W_Render(double deltaTime)
        {
            var framebufferSize = window!.FramebufferSize;

            if (framebufferSize.X * framebufferSize.Y == 0)
                return;

            if (_last_framebufferSize != framebufferSize)
            {
                _last_framebufferSize = framebufferSize;

                swapchain?.Release();
                swapchain = device.CreateSwapChain(surface,
                TextureUsage.RenderAttachment,
                TextureFormat.Bgra8Unorm,
                (uint)framebufferSize.X,
                (uint)framebufferSize.Y,
                PresentMode.Immediate);
            }

            var swapchainView = swapchain!.Value.GetCurrentTextureView();

            var cmd = device.CreateCommandEncoder();

            var queue = device.GetQueue();

            if(requestedCubeTexture != null)
            {
                cubeTexture.tex.Destroy();
                cubeTexture.view.Release();

                cubeTexture = requestedCubeTexture.Value;
                requestedCubeTexture = null;

                modelBindGroup.Release();
                modelBindGroup = device.CreateBindGroup(
                    bindGroupLayout: modelBindGroupLayout,
                    new BindGroupEntry[]
                    {
                                Buffer(0, modelUniformBuffer.Buffer, modelUniformBuffer.Offset, modelUniformBuffer.Size),
                                Sampler(1, linearSampler),
                                Texture(2, cubeTexture.view)
                    },
                    label: "ModelBindGroup"
                );
            }


            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4);
            ImGui.PushFont(quicksandFont);

            ImGui.DockSpaceOverViewport(ImGui.GetMainViewport());

            ImGui.ShowDemoWindow();

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            if (ImGui.Begin("Scene"))
            {
                var size = ImGui.GetContentRegionAvail();
                uint width = (uint)size.X, height = (uint)Math.Max(0, size.Y);

                uint prevWidth = sceneViewport!.Width;
                uint prevHeight = sceneViewport!.Height;

                sceneViewport!.EnsureSize(width, height);
                if (sceneViewport!.TryGetViews(out var color0, out _, out _))
                {
                    RenderScene(cmd, queue, color0, (float)width / height);

                    if (prevWidth!=width && prevHeight!=height)
                        imguiController!.RemoveTextureBindGroup(color0);

                    ImGui.Image(color0.GetIntPtr(),
                        new Vector2(width, height));
                }
                ImGui.End();
            }
            ImGui.PopStyleVar();

            if (ImGui.Begin("Controls"))
            {
                ImGui.Columns(2, null, false);
                float y = ImGui.GetCursorPosY();
                ImGui.SetCursorPosY(y + ImGui.GetStyle().FramePadding.Y);
                ImGui.Text("Texture");
                ImGui.NextColumn();
                ImGui.SetCursorPosY(y);
                var imageButtonSize = ImGui.GetContentRegionAvail().X;
                if (ImGui.ImageButton("ChooseTexture", cubeTexture.view.GetIntPtr(), new(imageButtonSize)))
                {
                    var result = Nfd.FileOpen(new NfdFilter[]
                    {
                        new NfdFilter { Description="Image files", Specification="png,jpg,jpeg,bmp" }
                    });

                    if (result.Status == NfdStatus.Ok)
                    {
                        using var stream = new FileStream(result.Path, FileMode.Open);

                        requestedCubeTexture = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, stream);
                    }
                }
                ImGui.NextColumn();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.Text("Tint");
                ImGui.NextColumn();
                ImGui.ColorEdit4("##Tint", ref cubeColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);
                ImGui.Spacing();
                ImGui.Separator();

                ImGui.End();
            }

            ImGui.PopFont();
            ImGui.PopStyleVar();

            if (ImGui.GetFrameCount() == 2) //seems to be the magic number for loading dock layouts
                ImGui.LoadIniSettingsFromDisk("imgui.ini");



            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(swapchainView, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(.1, .1, .1, 1))
            }, Span<Safe.RenderPassTimestampWrite>.Empty, null,
            null);

            imguiController!.Render(pass);

            pass.End();

            queue.Submit(new[] { cmd.Finish() });

            swapchain.Value.Present();
        }
    }
}