using Microsoft.VisualBasic;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace WgpuWrappersSilk.Net
{
    internal class Program
    {
        private static IWindow? window;

        static unsafe void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1000, 700);
            window = Window.Create(options);

            window.Load += W_Load;

            window.Run();
        }

        private async static void W_Load()
        {
            var wgpu = WebGPU.GetApi();

            var instance = InstancePtr.Create(wgpu, default);

            var adapter = await instance.RequestAdapter(default);

            SurfacePtr surface;

            if (window?.Native?.Win32 is not null)
            {
                var hwnd = window.Native.Win32.Value.Hwnd;
                surface = instance.CreateSurfaceFromWindowsHWND(hwnd, label: "Test");
            }

            var features = adapter.EnumerateFeatures();
            var limits = adapter.GetLimits();

            var device = await adapter.RequestDevice(default);

            //device.CreateComputePipeline(new ProgrammableStage(default, "main",
            //    new (string key, double value)[]
            //    {
            //        ("PushConstant0", 0.5),
            //        ("PushConstant1", 1.5),
            //        ("PushConstant2", 2.5),
            //    }), default, "Hello");

            device.CreateRenderPipeline(
                label: "DefaultPipeline",
                layout: default, 
                vertex: new VertexState
                {
                    ShaderModule = default, 
                    EntryPoint = "main",
                    Constants = new (string key, double value)[]
                    {
                        ("PushConstant0", 0.5),
                        ("PushConstant1", 1.5),
                        ("PushConstant2", 2.5),
                    },
                    Buffers = new VertexBufferLayout[]
                    {
                        new(
                            8, VertexStepMode.Instance,
                            new VertexAttribute[]
                            {
                                new(VertexFormat.Float32x3, 0, 0),
                            }
                        ),
                        new(
                            8, VertexStepMode.Vertex,
                            new VertexAttribute[]
                            {
                                new(VertexFormat.Float32x3, 0, 1),
                                new(VertexFormat.Float32x2, 12, 2),
                            }
                        )
                    }
                },
                primitive: new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Uint16,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.Back
                },
                depthStencil: new DepthStencilState
                {
                    DepthCompare = CompareFunction.LessEqual,
                    DepthWriteEnabled = true,
                    Format = TextureFormat.Depth24PlusStencil8,
                },
                multisample: new MultisampleState
                {
                    Count = 1
                },
                fragment: new FragmentState
                {
                    ShaderModule = default,
                    EntryPoint = "main",
                    Constants = new (string key, double value)[]
                    {
                        ("PushConstant3", 3.5),
                        ("PushConstant4", 4.5),
                    },
                    ColorTargets = new ColorTargetState[]
                    {
                        new(
                            TextureFormat.Rgba8Uint, 
                            (
                                color: new(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                                alpha: new(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha)
                            ),
                            ColorWriteMask.All
                        ),
                        new(
                            TextureFormat.R32Uint,
                            null,
                            ColorWriteMask.All
                        ),
                        new(
                            TextureFormat.Rgba8Uint,
                            (
                                color: new(BlendOperation.Max, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                                alpha: new(BlendOperation.Add, BlendFactor.Zero, BlendFactor.One)
                            ),
                            ColorWriteMask.All
                        )
                    }
                }
            );

            Debugger.Break();
        }
    }
}