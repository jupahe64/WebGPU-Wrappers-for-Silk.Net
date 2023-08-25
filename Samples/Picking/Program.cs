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
using Picking.Framebuffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Silk.NET.WebGPU.Extensions.WGPU;
using Silk.NET.WebGPU.Safe.Extensions;

namespace Picking
{
    internal partial class Program
    {
        record struct BufferRange(BufferPtr Buffer, ulong Offset, ulong Size);

        private static IWindow? window;
        private static RenderPipelinePtr modelPipelineRendering;
        private static RenderPipelinePtr modelPipelinePicking;
        private static RenderPipelinePtr modelPipelineHighlight;
        private static SwapChainPtr? swapchain;
        private static DevicePtr device;
        private static Wgpu? wgpuExtension;

        private static (BufferRange vertexBuffer, BufferRange indexBuffer)[]? modelBuffers;
        private static SamplerPtr linearSampler;
        private static BufferRange sceneUniformBuffer;
        private static BufferRange instanceInfoBuffer;
        private static readonly SceneLights lights = new();
        private static BufferRange lightsBuffer;
        private static BindGroupPtr sceneBindGroup;
        private static BufferRange modelUniformBuffer;
        private static BindGroupLayoutPtr modelBindGroupLayout;
        private static BindGroupPtr? modelBindGroup;
        private static ImFontPtr imguiDefaultFont;
        private static ImFontPtr quicksandFont;
        private static IInputContext? input;
        private static ImGuiController? imguiController;
        private static GBuffer? sceneGBuffer;
        private static PickingHighlightBuffer? pickingHighlightBuffer;
        private static RenderTexture? finalSceneTexture;
        private static Depth_ObjID_PixelReader? mousePixelReader;
        private static SurfacePtr surface;

        private static Depth_ObjID_PixelReader.Result underCursor = new()
        {
            Depth = 1,
            ObjID = 0
        };

        private static (TexturePtr tex, TextureViewPtr view) modelTextureAlbedo;
        private static (TexturePtr tex, TextureViewPtr view) modelTextureNormal;
        private static (TexturePtr tex, TextureViewPtr view) modelTextureEmission;
        private static Vector3 albedoColor = Vector3.One;
        private static Vector3 emissionColor = Vector3.One;
        private static float normalMapStrength = 1.0f;
        private static int outlineThickness = 2;

        private static Vector2D<int> _last_framebufferSize = new(0, 0);
        private static (TexturePtr tex, TextureViewPtr view)? requestedAlbedoTexture = null;
        private static (TexturePtr tex, TextureViewPtr view)? requestedEmissionTexture = null;
        private static DeferredShader? deferredShader;
        private static Compositor? compositor;
        private static OutlineDrawer? outlineDrawer;

        private static float _yaw = 0;
        private static float _pitch = 0;
        private static Quaternion _camRot = Quaternion.Identity;
        private static Vector3 _camTarget = Vector3.Zero;

        private static bool _isSceneViewHovered = false;

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

        struct InstanceInfo
        {
            public uint ObjID;
            public Vector4D<float> HighlightColor;
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

        static (BufferRange vertexBuffer, BufferRange indexBuffer)[] CreateModelBuffers(string path, string? label = null)
        {
            var models = ModelHelper.LoadModels(path);

            var buffers = new (BufferRange vertexBuffer, BufferRange indexBuffer)[models.Count];

            for (int iModel = 0; iModel < models.Count; iModel++)
            {
                var mdl = models[iModel];

                var modelVertices = new Vertex[mdl.Positions.Length];

                for (int iVertex = 0; iVertex < mdl.Positions.Length; iVertex++)
                {
                    modelVertices[iVertex].Position = mdl.Positions[iVertex];
                    modelVertices[iVertex].TexCoord = mdl.TexCoords[iVertex];
                    modelVertices[iVertex].TexCoord2 = mdl.TexCoords2[iVertex];
                    modelVertices[iVertex].Normal = mdl.Normals[iVertex];
                    modelVertices[iVertex].Tangent = mdl.Tangents[iVertex];
                }

                var vertexBuffer = CreateBufferWithData<Vertex>(BufferUsage.Vertex | BufferUsage.CopyDst, modelVertices,
                    label == null ? null : $"{label} - VertexBuffer");

                var indexBuffer = CreateBufferWithData<uint>(BufferUsage.Index | BufferUsage.CopyDst, mdl.Indices,
                    label == null ? null : $"{label} - IndexBuffer");

                buffers[iModel] = (vertexBuffer, indexBuffer);
            }

            return buffers;
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

            window!.Title = $"Picking {properties.BackendType} {properties.Name}";

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

            if (!device.TryGetExtension<Wgpu>(out wgpuExtension))
                throw new Exception("Wgpu (wgpu-rs) extension is not supported");

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

            input = window.CreateInput();

            imguiController = new ImGuiController(device,
                TextureFormat.Bgra8Unorm,
                window, input, SetupFonts);

            var io = ImGui.GetIO();

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;         // Enable Docking
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;       // Enable Multi-Viewport / Platform Windows

            Console.WriteLine("Creating scene view resources");

            sceneGBuffer = GBuffer.Create(device,
                TextureFormat.Rgba8Unorm, TextureFormat.Rgb10A2Unorm, TextureFormat.Rgba32float, TextureFormat.Depth24Plus,
                label: "SceneGBuffer");

            pickingHighlightBuffer = PickingHighlightBuffer.Create(device,
                TextureFormat.R32Uint, TextureFormat.Rgba8Unorm, TextureFormat.Rgba8Unorm, sceneGBuffer.DepthStencilFormat,
                label: "PickingHighlightBuffer");

            finalSceneTexture = RenderTexture.Create(device, TextureFormat.Rgba8Unorm, label: "FinalSceneTexture");

            mousePixelReader = Depth_ObjID_PixelReader.Create(device);

            var mousePickingBindGroupLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Compute, BufferBindingType.Storage, (ulong)Unsafe.SizeOf<Matrix4X4<float>>())
                },
                label: "MousePickingGroupLayout"
            );

            var sceneBindGroupLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Vertex, BufferBindingType.Uniform, (ulong)Unsafe.SizeOf<Matrix4X4<float>>())
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

            var pickingPipelineLayout = device.CreatePipelineLayout(
                new[]
                {
                    sceneBindGroupLayout,
                    modelBindGroupLayout
                },
                label: "PipelineLayout"
            );

            Console.WriteLine("Creating Vertex and Index-Buffers");

            modelBuffers = CreateModelBuffers(Path.Combine(AppContext.BaseDirectory, "TempleModel.glb"),
                "TempleModel.glb");

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

            instanceInfoBuffer = new(
                device.CreateBuffer(BufferUsage.Vertex | BufferUsage.CopyDst, (ulong)(Unsafe.SizeOf<InstanceInfo>() * modelBuffers.Length),
                label: "PickingHighlightInstanceBuffer"),
                0, (ulong)(Unsafe.SizeOf<InstanceInfo>() * modelBuffers.Length));

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

            var vertexDataLayout = new Safe.VertexBufferLayout(
                arrayStride: (uint)Marshal.SizeOf<Vertex>(), 
                stepMode: VertexStepMode.Vertex,
                attributes: new VertexAttribute[]
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
                });

            var instanceDataLayout = new Safe.VertexBufferLayout(
                arrayStride: (uint)Marshal.SizeOf<InstanceInfo>(), 
                VertexStepMode.Instance,
                new VertexAttribute[]
                {
                    new(VertexFormat.Uint32, shaderLocation: 5, offset:
                        (uint)Marshal.OffsetOf<InstanceInfo>(nameof(InstanceInfo.ObjID))),
                    new(VertexFormat.Float32x4, shaderLocation: 6, offset:
                        (uint)Marshal.OffsetOf<InstanceInfo>(nameof(InstanceInfo.HighlightColor))),
                });

            var primitiveState = new Safe.PrimitiveState
            {
                Topology = PrimitiveTopology.TriangleList,
                FrontFace = FrontFace.Ccw,
                CullMode = CullMode.Back
            };

            var multisampleState = new MultisampleState
            {
                Count = 1,
                AlphaToCoverageEnabled = false,
                Mask = uint.MaxValue
            };

            modelPipelineRendering = device.CreateRenderPipeline(
                label: "ModelPipeline:Rendering",
                layout: pipelineLayout,
                vertex: new Safe.VertexState
                {
                    Module = shaderModule,
                    EntryPoint = "vs_main",
                    Constants = Array.Empty<(string key, double value)>(),
                    Buffers = new Safe.VertexBufferLayout[]
                    {
                        vertexDataLayout
                    }
                },
                primitive: primitiveState,
                depthStencil: new DepthStencilState
                {
                    Format = sceneGBuffer.DepthStencilFormat,
                    DepthCompare = CompareFunction.LessEqual,
                    DepthWriteEnabled = true,
                    StencilFront = new StencilFaceState { Compare = CompareFunction.Always },
                    StencilBack = new StencilFaceState { Compare = CompareFunction.Always },
                },
                multisample: multisampleState,
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

            modelPipelinePicking = device.CreateRenderPipeline(
                label: "ModelPipeline:Picking",
                layout: pickingPipelineLayout,
                vertex: new Safe.VertexState
                {
                    Module = shaderModule,
                    EntryPoint = "vs_main_PH",
                    Constants = Array.Empty<(string key, double value)>(),
                    Buffers = new Safe.VertexBufferLayout[]
                    {
                        vertexDataLayout,
                        instanceDataLayout
                    }
                },
                primitive: primitiveState,
                depthStencil: new DepthStencilState
                {
                    Format = pickingHighlightBuffer.DepthStencilFormat,
                    DepthCompare = CompareFunction.LessEqual,
                    DepthWriteEnabled = true,
                    StencilFront = new StencilFaceState { Compare = CompareFunction.Always },
                    StencilBack = new StencilFaceState { Compare = CompareFunction.Always },
                },
                multisample: multisampleState,
                fragment: new Safe.FragmentState
                {
                    Module = shaderModule,
                    EntryPoint = "fs_main_PH",
                    Constants = Array.Empty<(string key, double value)>(),
                    Targets = new Safe.ColorTargetState[]
                    {
                        new(
                            pickingHighlightBuffer.ObjIdChannelFormat,
                            blendState: null,
                            ColorWriteMask.All
                        ),
                        new(
                            pickingHighlightBuffer.HighlightChannelFormat,
                            blendState: null,
                            ColorWriteMask.None
                        )
                    }
                }
            );

            modelPipelineHighlight = device.CreateRenderPipeline(
                label: "ModelPipeline:Highlight",
                layout: pickingPipelineLayout,
                vertex: new Safe.VertexState
                {
                    Module = shaderModule,
                    EntryPoint = "vs_main_PH",
                    Constants = Array.Empty<(string key, double value)>(),
                    Buffers = new Safe.VertexBufferLayout[]
                    {
                        vertexDataLayout,
                        instanceDataLayout
                    }
                },
                primitive: primitiveState,
                depthStencil: new DepthStencilState
                {
                    Format = pickingHighlightBuffer.DepthStencilFormat,
                    DepthCompare = CompareFunction.LessEqual,
                    DepthWriteEnabled = true,
                    StencilFront = new StencilFaceState { Compare = CompareFunction.Always },
                    StencilBack = new StencilFaceState { Compare = CompareFunction.Always },
                },
                multisample: multisampleState,
                fragment: new Safe.FragmentState
                {
                    Module = shaderModule,
                    EntryPoint = "fs_main_PH",
                    Constants = Array.Empty<(string key, double value)>(),
                    Targets = new Safe.ColorTargetState[]
                    {
                        new(
                            pickingHighlightBuffer.ObjIdChannelFormat,
                            blendState: null,
                            ColorWriteMask.All
                        ),
                        new(
                            pickingHighlightBuffer.HighlightChannelFormat,
                            blendState: null,
                            ColorWriteMask.All
                        )
                    }
                }
            );


            Console.WriteLine("Create Deferred Shading Shader+Pipeline");

            deferredShader = DeferredShader.Create(device, sceneGBuffer.LightChannelFormat);

            Console.WriteLine("Create Compositor Shader(s)+Pipeline(s)");

            compositor = Compositor.Create(device, finalSceneTexture.ColorFormat);

            Console.WriteLine("Create Outliner Shader(s)+Pipeline(s)");

            outlineDrawer = OutlineDrawer.Create(device, pickingHighlightBuffer.OutlineChannelFormat);

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

            if(_isSceneViewHovered && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                _camTarget += Vector3.Transform(new Vector3(
                    -ImGui.GetIO().MouseDelta.X * 0.02f,
                    ImGui.GetIO().MouseDelta.Y * 0.02f,
                0), _camRot);
            }

            if (_isSceneViewHovered && ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            {
                _yaw -= ImGui.GetIO().MouseDelta.X * 0.005f;
                _pitch -= ImGui.GetIO().MouseDelta.Y * 0.005f;
                _camRot =
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, _yaw) *
                    Quaternion.CreateFromAxisAngle(Vector3.UnitX, _pitch);

            }
        }

        private static void RenderScene(QueuePtr queue, (TextureViewPtr albedo,
            TextureViewPtr normal, TextureViewPtr emission, TextureViewPtr depth) gbuffer, 
            (TextureViewPtr id, TextureViewPtr highlightColor, TextureViewPtr depth) highlightID, 
            float aspectRatio, Vector2 mousePosition, out Matrix4X4<float> ViewProjectionMatrix)
        {
            ViewProjectionMatrix = 
                Matrix4X4.CreateTranslation(-_camTarget.ToGeneric()) *
                Matrix4X4.CreateFromQuaternion(Quaternion.Inverse(_camRot).ToGeneric()) *
                Matrix4X4.CreateTranslation(0f, 0f, -15) *
                Matrix4X4.CreatePerspectiveFieldOfView((float)((2 * Math.PI) / 5),
                    aspectRatio, 0.01f, 100f);

            queue.WriteBuffer(sceneUniformBuffer.Buffer, sceneUniformBuffer.Offset,
                new ReadOnlySpan<Matrix4X4<float>>(ViewProjectionMatrix));

            InstanceInfo[] instanceInfos = new InstanceInfo[modelBuffers!.Length];

            for (uint i = 0; i < instanceInfos.Length; i++)
            {
                instanceInfos[i] = new InstanceInfo
                {
                    ObjID = i+1,
                    HighlightColor = new Vector4D<float>(0f)

                };
            }

            instanceInfos[2].HighlightColor = new Vector4D<float>(1, 1, 0.5f, 0.5f);
            instanceInfos[1].HighlightColor = new Vector4D<float>(0.5f, 1, 1, 0.5f);

            if (underCursor.ObjID > 0)
            {
                instanceInfos[underCursor.ObjID - 1].HighlightColor =
                   new Vector4D<float>(1f, 1f, 1f, 0.5f);
            }

            queue.WriteBuffer<InstanceInfo>(
                instanceInfoBuffer.Buffer, instanceInfoBuffer.Offset,
                instanceInfos);

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

            var cmd = device.CreateCommandEncoder();

            //picking pass
            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(highlightID.id, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0, 0)),
                new(highlightID.highlightColor, resolveTarget: null,
                LoadOp.Load, StoreOp.Store, new Color(0, 0, 0, 0))
            }, null,
            new Safe.RenderPassDepthStencilAttachment(highlightID.depth, LoadOp.Clear, StoreOp.Store, 1f, false,
                /*stencil*/ LoadOp.Clear, StoreOp.Discard, 0, true),
            null);

            pass.SetPipeline(modelPipelinePicking);

            for (uint i = 0; i < modelBuffers.Length; i++)
            {
                if (instanceInfos[i].ObjID == 0)
                    continue;

                var (vertexBuffer, indexBuffer) = modelBuffers[i];

                pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

                pass.SetVertexBuffer(1, instanceInfoBuffer.Buffer,
                instanceInfoBuffer.Offset, instanceInfoBuffer.Size);

                pass.SetIndexBuffer(indexBuffer.Buffer, IndexFormat.Uint32, indexBuffer.Offset, indexBuffer.Size);

                pass.SetBindGroup(0, sceneBindGroup, null);
                pass.SetBindGroup(1, modelBindGroup!.Value, null);

                pass.DrawIndexed((uint)(indexBuffer.Size / sizeof(uint)), 1, 0, 0, i);
            }

            pass.End();

            queue.Submit(new ReadOnlySpan<CommandBufferPtr>(cmd.Finish()));


            mousePixelReader!.ReadPixels(device, queue,
                    mousePosition.ToGeneric().As<uint>(), highlightID.depth, highlightID.id);

            cmd = device.CreateCommandEncoder();

            //gbuffer pass
            pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(gbuffer.albedo, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0, 1)),
                new(gbuffer.normal, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0, 1)),
                new(gbuffer.emission, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0.1, 1))
            }, null,
            new Safe.RenderPassDepthStencilAttachment(gbuffer.depth, LoadOp.Clear, StoreOp.Store, 1f, false,
                /*stencil*/ LoadOp.Clear, StoreOp.Discard, 0, true),
            null);

            pass.SetPipeline(modelPipelineRendering);

            for (int i = 0; i < modelBuffers.Length; i++)
            {
                var (vertexBuffer, indexBuffer) = modelBuffers[i];

                pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

                pass.SetIndexBuffer(indexBuffer.Buffer, IndexFormat.Uint32, indexBuffer.Offset, indexBuffer.Size);

                pass.SetBindGroup(0, sceneBindGroup, null);
                pass.SetBindGroup(1, modelBindGroup!.Value, null);

                pass.DrawIndexed((uint)(indexBuffer.Size / sizeof(uint)), 1, 0, 0, 0);
            }

            pass.End();

            //highlight pass
            pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(highlightID.id, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0, 0)),
                new(highlightID.highlightColor, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(0, 0, 0, 0))
            }, null,
            new Safe.RenderPassDepthStencilAttachment(highlightID.depth, LoadOp.Clear, StoreOp.Store, 1f, false,
                /*stencil*/ LoadOp.Clear, StoreOp.Discard, 0, true),
            null);

            pass.SetPipeline(modelPipelineHighlight);

            for (uint i = 0; i < modelBuffers.Length; i++)
            {
                if (instanceInfos[i].HighlightColor.W == 0)
                    continue;

                var (vertexBuffer, indexBuffer) = modelBuffers[i];

                pass.SetVertexBuffer(0, vertexBuffer.Buffer,
                vertexBuffer.Offset, vertexBuffer.Size);

                pass.SetVertexBuffer(1, instanceInfoBuffer.Buffer,
                instanceInfoBuffer.Offset, instanceInfoBuffer.Size);

                pass.SetIndexBuffer(indexBuffer.Buffer, IndexFormat.Uint32, indexBuffer.Offset, indexBuffer.Size);

                pass.SetBindGroup(0, sceneBindGroup, null);
                pass.SetBindGroup(1, modelBindGroup!.Value, null);

                pass.DrawIndexed((uint)(indexBuffer.Size / sizeof(uint)), 1, 0, 0, i);
            }

            pass.End();

            queue.Submit(new ReadOnlySpan<CommandBufferPtr>(cmd.Finish()));
        }

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
                pickingHighlightBuffer!.EnsureSize(width, height);
                finalSceneTexture!.EnsureSize(width, height);
                if (
                    sceneGBuffer!.TryGetTargets(out var albedo, out var normal, out var emission, out var depth) &&
                    pickingHighlightBuffer!.TryGetTargets(out var objID, out var highlightColor, out var outline, out var highlightDepth) &&
                    finalSceneTexture!.TryGetTargets(out var colorOutput)
                    )
                {
                    var cursorPos = ImGui.GetCursorPos();

                    var mousePosition = ImGui.GetMousePos() - (cursorPos + ImGui.GetWindowPos()) * ImGui.GetWindowDpiScale();

                    RenderScene(queue, 
                        (albedo.View, normal.View, emission.View, depth.View),
                        (objID.View, highlightColor.View, highlightDepth.View), 
                        (float)width / height, mousePosition, out Matrix4X4<float> ViewProjection);

                    if (!Matrix4X4.Invert(ViewProjection, out Matrix4X4<float> InvViewProjection))
                        throw new InvalidOperationException("ViewProjection Matrix could not be inverted");

                    deferredShader!.Apply(device, queue, albedo.View, normal.View, depth.DepthOnlyView, emission.View, InvViewProjection, 
                        lightsBuffer.Buffer, lights.LightCount);

                    outlineDrawer!.Apply(device, queue, highlightColor.View, highlightDepth.DepthOnlyView, objID.View, depth.DepthOnlyView, 
                        new(width, height), outline.View, outlineThickness);

                    compositor!.Apply(device, queue, emission.View, highlightColor.View, outline.View,
                        depth.DepthOnlyView, highlightDepth.DepthOnlyView, colorOutput.View);

                    if (prevWidth!=width && prevHeight != height)
                    {
                        imguiController!.RemoveTextureBindGroup(colorOutput.View);
                    }


                    ImGui.Image(colorOutput.View.GetIntPtr(),
                        new Vector2(width, height));

                    _isSceneViewHovered = ImGui.IsItemHovered();

                    ImGui.SetCursorPos(cursorPos + new Vector2(10));
                    ImGui.TextColored(new Vector4(1, 1, 1, 0.5f),
                        "Drag with left mouse button to pan\n" +
                        "Drag with right mouse button to rotate");
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
                    var result = Nfd.FileOpen(new NfdFilter[]
                    {
                        new NfdFilter { Description="Image files", Specification="png,jpg,jpeg,bmp" }
                    });

                    if (result.Status == NfdStatus.Ok)
                    {
                        using var stream = new FileStream(result.Path, FileMode.Open);

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
                    var result = Nfd.FileOpen(new NfdFilter[]
                    {
                        new NfdFilter { Description="Image files", Specification="png,jpg,jpeg,bmp" }
                    });

                    if (result.Status == NfdStatus.Ok)
                    {
                        using var stream = new FileStream(result.Path, FileMode.Open);

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
                ImGui.NextColumn();

                ImGui.Separator(); ImGui.Spacing(); ImGui.Spacing();

                ImGui.Columns();

                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 30);

                ImGui.Text("Outline thickness");
                ImGui.SliderInt("##OutlineThickness", ref outlineThickness, 2, 12);

                ImGui.End();
            }

            ImGui.PopFont();
            ImGui.PopStyleVar();

            if (ImGui.GetFrameCount() == 2) //seems to be the magic number for loading dock layouts
                ImGui.LoadIniSettingsFromDisk("imgui.ini");


            var cmd = device.CreateCommandEncoder();
            var pass = cmd.BeginRenderPass(new Safe.RenderPassColorAttachment[]
            {
                new(swapchainView, resolveTarget: null,
                LoadOp.Clear, StoreOp.Store, new Color(.1, .1, .1, 1))
            }, Span<Safe.RenderPassTimestampWrite>.Empty, null,
            null);

            imguiController!.Render(pass);

            pass.End();

            queue.Submit(new ReadOnlySpan<CommandBufferPtr>(cmd.Finish()));

            var resultBufferMapTask = mousePixelReader!.RequestResultBufferMapping();

            device.GetExtension(wgpuExtension!)
                  .Poll(true, null);

            underCursor = mousePixelReader.ReadResultBuffer(resultBufferMapTask);

            swapchain.Value.Present();
        }
    }
}