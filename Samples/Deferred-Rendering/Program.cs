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
using System.Numerics;
using System.Linq;
using TinyDialogsNet;

namespace DeferredRendering
{
    internal partial class Program
    {
        record struct BufferRange(BufferPtr Buffer, ulong Offset, ulong Size);

        private static IWindow? window;
        private static RenderPipelinePtr pipeline;
        private static SwapChainPtr? swapchain;
        private static DevicePtr device;

        private static BufferRange vertexBuffer;
        private static BufferRange indexBuffer;
        private static SamplerPtr linearSampler;
        private static BufferRange sceneUniformBuffer;
        private static readonly SceneLights lights = new();
        private static BufferRange lightsBuffer;
        private static BindGroupPtr sceneBindGroup;
        private static BufferRange modelUniformBuffer;
        private static BindGroupLayoutPtr modelBindGroupLayout;
        private static BindGroupPtr? modelBindGroup;
        private static ImFontPtr imguiDefaultFont;
        private static ImFontPtr quicksandFont;
        private static ImGuiController? imguiController;
        private static GBuffer? sceneGBuffer;
        private static SurfacePtr surface;

        private static (TexturePtr tex, TextureViewPtr view) modelTextureAlbedo;
        private static (TexturePtr tex, TextureViewPtr view) modelTextureNormal;
        private static (TexturePtr tex, TextureViewPtr view) modelTextureEmission;
        private static Vector3 albedoColor = Vector3.One;
        private static Vector3 emissionColor = Vector3.One;
        private static float normalMapStrength = 1.0f;

        private static readonly string[] supportedImageFormatPatterns =
            SixLabors.ImageSharp.Configuration.Default.ImageFormats.SelectMany(
            f => f.FileExtensions).Select(ext => $"*.{ext}").ToArray();

        struct Vertex
        {
            public Vector3 Position;
            public Vector2 TexCoord;
            public Vector2 TexCoord2;
            public Vector3 Normal;
            public Vector3 Tangent;
        }

        struct ModelUB
        {
            public Matrix4X4<float> Transform;
            public Vector3D<float> Tex0Color;
            private uint _padding0;
            public Vector3D<float> Tex1Color;
            public float NormalMapStrength;
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

        static BufferRange CreateBufferWithData<T>(BufferUsage usage, ReadOnlySpan<T> data, string? label = null)
            where T : unmanaged
        {
            var bufferSize = (ulong)(Unsafe.SizeOf<T>() * data.Length);

            var buffer = device.CreateBuffer(usage,
                bufferSize,
                mappedAtCreation: true, label);

            var mappedBuffer = buffer.GetMappedRange<T>(0, (nuint)bufferSize);
            data.CopyTo(mappedBuffer);

            buffer.Unmap();
            return new(buffer, 0, bufferSize);
        }

        static (BufferRange vertexBuffer, BufferRange indexBuffer) CreateModelBuffers(string path, string? label = null)
        {
            ModelHelper.LoadModel(path,
                out Span<uint> indices,
                out Span<Vector3> positions,
                out Span<Vector3> normals,
                out Span<Vector3> tangents,
                out Span<Vector2> texCoords,
                out Span<Vector2> texCoords2);

            var modelVertices = new Vertex[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                modelVertices[i].Position = positions[i];
                modelVertices[i].TexCoord = texCoords[i];
                modelVertices[i].TexCoord2 = texCoords2[i];
                modelVertices[i].Normal = normals[i];
                modelVertices[i].Tangent = tangents[i];
            }

            var vertexBuffer = CreateBufferWithData<Vertex>(BufferUsage.Vertex | BufferUsage.CopyDst, modelVertices,
                label == null ? null : $"{label} - VertexBuffer");

            var indexBuffer = CreateBufferWithData<uint>(BufferUsage.Index | BufferUsage.CopyDst, indices,
                label == null ? null : $"{label} - IndexBuffer");

            return (vertexBuffer, indexBuffer);
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
                backendType: BackendType.Vulkan,
                powerPreference: PowerPreference.HighPerformance
            );

            surface = window!.CreateWebGPUSurface(wgpu, instance);

            var features = adapter.EnumerateFeatures();
            var limits = adapter.GetLimits();
            var properties = adapter.GetProperties();

            window!.Title = $"Deferred-Rendering {properties.BackendType} {properties.Name}";

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

            sceneGBuffer = GBuffer.Create(device,
                TextureFormat.Rgba8Unorm, TextureFormat.Rgb10A2Unorm, TextureFormat.Rgba32float, TextureFormat.Depth24Plus,
                label: "SceneGBuffer");

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
                    Texture(2, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, multisampled: false),
                    Texture(3, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, multisampled: false),
                    Texture(4, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, multisampled: false)
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

            using (var fileStream = File.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempleStone_alb.png"), FileMode.Open))
            {
                modelTextureAlbedo = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, fileStream,
                    label: "TempleStone_alb.png");
            }

            using (var fileStream = File.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempleStone_nrm.png"), FileMode.Open))
            {
                modelTextureNormal = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, fileStream,
                    label: "TempleStone_nrm.png");
            }

            using (var fileStream = File.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempleStone_emm.png"), FileMode.Open))
            {
                modelTextureEmission = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, fileStream,
                    label: "TempleStone_emm.png");
            }

            linearSampler = device.CreateSampler(AddressMode.Repeat, AddressMode.Repeat, AddressMode.ClampToEdge,
                minFilter: FilterMode.Linear, magFilter: FilterMode.Linear, mipmapFilter: MipmapFilterMode.Linear,
                lodMinClamp: 0, lodMaxClamp: 1, compare: CompareFunction.Undefined, label: "LinearSampler");


            sceneUniformBuffer = new(
                device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, (ulong)Unsafe.SizeOf<Matrix4X4<float>>(),
                label: "SceneUniformBuffer"),
                0, (ulong)Unsafe.SizeOf<Matrix4X4<float>>());

            lightsBuffer = CreateBufferWithData(BufferUsage.Storage | BufferUsage.CopyDst,
                lights.LightData, label: "LightsBuffer");

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

            RecreateModelBindgroup();

            Console.WriteLine("Compiling Shader");

            var shaderModule = device.CreateShaderModuleWGSL(File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shader.wgsl")),
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
                                new(VertexFormat.Float32x2, shaderLocation: 2, offset:
                                    (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.TexCoord2))),
                                new(VertexFormat.Float32x3, shaderLocation: 3, offset:
                                    (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Normal))),
                                new(VertexFormat.Float32x3, shaderLocation: 4, offset:
                                    (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Tangent))),
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
                    Format = sceneGBuffer.DepthStencilFormat,
                    DepthCompare = CompareFunction.LessEqual,
                    DepthWriteEnabled = true,
                    StencilFront = new StencilFaceState { Compare = CompareFunction.Always },
                    StencilBack = new StencilFaceState { Compare = CompareFunction.Always },
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
                            sceneGBuffer.AlbedoChannelFormat,
                            blendState: null,
                            ColorWriteMask.All
                        ),
                        new(
                            sceneGBuffer.NormalChannelFormat,
                            blendState: null,
                            ColorWriteMask.All
                        ),
                        new(
                            sceneGBuffer.LightChannelFormat,
                            blendState: null,
                            ColorWriteMask.All
                        )
                    }
                }
            );

            Console.WriteLine("Creating Vertex and Index-Buffer");

            (vertexBuffer, indexBuffer) = CreateModelBuffers(Path.Combine(AppContext.BaseDirectory, "TempleModel.glb"),
                "TempleModel.glb");


            Console.WriteLine("Create Deferred Shading Shader+Pipeline");

            deferredShader = DeferredShader.Create(device, sceneGBuffer.LightChannelFormat);

            Console.WriteLine("Done");
        }

        private static void RecreateModelBindgroup()
        {
            modelBindGroup?.Release();
            modelBindGroup = device.CreateBindGroup(
                            bindGroupLayout: modelBindGroupLayout,
                            new BindGroupEntry[]
                            {
                    Buffer(0, modelUniformBuffer.Buffer, modelUniformBuffer.Offset, modelUniformBuffer.Size),
                    Sampler(1, linearSampler),
                    Texture(2, modelTextureAlbedo.view),
                    Texture(3, modelTextureNormal.view),
                    Texture(4, modelTextureEmission.view),
                            },
                            label: "ModelBindGroup"
                        );
        }

        private static void W_Update(double deltaTime)
        {
            imguiController?.Update((float)deltaTime);
            lights.Animate((float)deltaTime);
        }

        private static void RenderScene(CommandEncoderPtr cmd, QueuePtr queue, TextureViewPtr albedoTarget,
            TextureViewPtr normalTarget, TextureViewPtr emissionTarget, 
            TextureViewPtr depthStencilTarget, float aspectRatio, out Matrix4X4<float> ViewProjectionMatrix)
        {
            float time = (float)window!.Time;

            ViewProjectionMatrix = 
                Matrix4X4.CreateRotationY(time * 0.3f) *
                Matrix4X4.CreateRotationX(0.5f) *
                Matrix4X4.CreateTranslation(0f, 0f, -15) *
                Matrix4X4.CreatePerspectiveFieldOfView((float)((2 * Math.PI) / 5),
                    aspectRatio, 0.01f, 100f);

            queue.WriteBuffer(sceneUniformBuffer.Buffer, sceneUniformBuffer.Offset,
                new ReadOnlySpan<Matrix4X4<float>>(ViewProjectionMatrix));

            queue.WriteBuffer(modelUniformBuffer.Buffer, modelUniformBuffer.Offset,
                new ReadOnlySpan<ModelUB>(new ModelUB
                {
                    Transform =
                    Matrix4X4<float>.Identity,

                    Tex0Color = SystemNumericsExtensions.ToGeneric(albedoColor),
                    Tex1Color = SystemNumericsExtensions.ToGeneric(emissionColor),
                    NormalMapStrength = normalMapStrength
                }));

            queue.WriteBuffer(lightsBuffer.Buffer, lightsBuffer.Offset, lights.LightData);

            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(albedoTarget, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0, 1)),
                new(normalTarget, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0, 1)),
                new(emissionTarget, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0.1, 1))
            }, null,
            new Safe.RenderPassDepthStencilAttachment(depthStencilTarget, LoadOp.Clear, StoreOp.Store, 1f, false,
                /*stencil*/ LoadOp.Clear, StoreOp.Discard, 0, true),
            null);

            pass.SetPipeline(pipeline);

            pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

            pass.SetIndexBuffer(indexBuffer.Buffer, IndexFormat.Uint32, indexBuffer.Offset, indexBuffer.Size);

            pass.SetBindGroup(0, sceneBindGroup, ReadOnlySpan<uint>.Empty);
            pass.SetBindGroup(1, modelBindGroup!.Value, ReadOnlySpan<uint>.Empty);

            pass.DrawIndexed((uint)(indexBuffer.Size / (uint)sizeof(uint)), 1, 0, 0, 0);

            pass.End();
        }

        private static Vector2D<int> _last_framebufferSize = new(0,0);
        private static (TexturePtr tex, TextureViewPtr view)? requestedAlbedoTexture = null;
        private static (TexturePtr tex, TextureViewPtr view)? requestedEmissionTexture = null;
        private static DeferredShader? deferredShader;

        private static void W_Render(double deltaTime)
        {
            if (device==default)
                return;

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

            if(requestedAlbedoTexture != null)
            {
                modelTextureAlbedo.tex.Destroy();
                modelTextureAlbedo.view.Release();

                modelTextureAlbedo = requestedAlbedoTexture.Value;
                requestedAlbedoTexture = null;

                RecreateModelBindgroup();
            }
            if (requestedEmissionTexture != null)
            {
                modelTextureEmission.tex.Destroy();
                modelTextureEmission.view.Release();

                modelTextureEmission = requestedEmissionTexture.Value;
                requestedEmissionTexture = null;

                RecreateModelBindgroup();
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

                uint prevWidth = sceneGBuffer!.Width;
                uint prevHeight = sceneGBuffer!.Height;

                sceneGBuffer!.EnsureSize(width, height);
                if (sceneGBuffer!.TryGetViews(out var albedo, out var normal, out var emission, out var depthStencil, out var depth))
                {
                    RenderScene(cmd, queue, albedo, normal, emission, depthStencil, (float)width / height, out Matrix4X4<float> ViewProjection);

                    if (!Matrix4X4.Invert(ViewProjection, out Matrix4X4<float> InvViewProjection))
                        throw new InvalidOperationException("ViewProjection Matrix could not be inverted");

                    deferredShader!.Apply(cmd, queue, albedo, normal, depth, emission, InvViewProjection, 
                        lightsBuffer.Buffer, lights.LightCount);

                    if (prevWidth!=width && prevHeight!=height)
                        imguiController!.RemoveTextureBindGroup(emission);

                    ImGui.Image(emission.GetIntPtr(),
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
                ImGui.Text("Albedo");
                ImGui.NextColumn();
                ImGui.SetCursorPosY(y);
                var imageButtonSize = ImGui.GetContentRegionAvail().X;
                if (ImGui.ImageButton("ChooseTextureAlbedo", modelTextureAlbedo.view.GetIntPtr(), new(imageButtonSize)))
                {
                    var files = Dialogs.OpenFileDialog(title: "Choose Albedo Texture", filterName: "Image files",
                        filterPatterns: supportedImageFormatPatterns);

                    if (files?.Count() == 1)
                    {
                        using var stream = new FileStream(files.First(), FileMode.Open);

                        requestedAlbedoTexture = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, stream);
                    }
                }
                ImGui.NextColumn();

                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

                ImGui.Text("Albedo tint");
                ImGui.NextColumn();
                ImGui.ColorEdit3("##AlbedoTint", ref albedoColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);
                ImGui.NextColumn();

                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

                y = ImGui.GetCursorPosY();
                ImGui.SetCursorPosY(y + ImGui.GetStyle().FramePadding.Y);
                ImGui.Text("Emission");
                ImGui.NextColumn();
                ImGui.SetCursorPosY(y);
                imageButtonSize = ImGui.GetContentRegionAvail().X;
                if (ImGui.ImageButton("ChooseTextureEmission", modelTextureEmission.view.GetIntPtr(), new(imageButtonSize)))
                {
                    var files = Dialogs.OpenFileDialog(title: "Choose Emission Texture", filterName: "Image files",
                        filterPatterns: supportedImageFormatPatterns);

                    if (files?.Count() == 1)
                    {
                        using var stream = new FileStream(files.First(), FileMode.Open);

                        requestedEmissionTexture = CreateTextureFromRGBAImage(TextureUsage.TextureBinding, stream);
                    }
                }
                ImGui.NextColumn();

                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

                ImGui.Text("Emission tint");
                ImGui.NextColumn();
                ImGui.ColorEdit3("##EmissionTint", ref emissionColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);
                ImGui.NextColumn();

                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

                ImGui.Text("Normal-Map strength");
                ImGui.NextColumn();
                ImGui.SliderFloat("##NormalMapStrength", ref normalMapStrength, 0, 1);
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