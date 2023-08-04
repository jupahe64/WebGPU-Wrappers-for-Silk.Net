using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Maths;
using static Silk.NET.WebGPU.Safe.BindGroupEntries;
using static Silk.NET.WebGPU.Safe.BindGroupLayoutEntries;
using Silk.NET.WebGPU.Safe;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.Input.Extensions;
using MouseButton = Silk.NET.Input.MouseButton;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Extensions.ImGui
{
    record struct BufferRange(BufferPtr Buffer, ulong Offset, ulong Size);

    /// <summary>
    /// Can render draw lists produced by ImGui.
    /// Also provides functions for updating ImGui input.
    /// </summary>
    public class ImGuiController : IDisposable
    {
        private DevicePtr _device;
        private IView _view;
        private IInputContext _input;
        private readonly List<char> _pressedChars = new List<char>();
        private IKeyboard _keyboard;

        // Device objects
        private BufferRange _vertexBuffer;
        private BufferRange _indexBuffer;
        private BufferPtr _projMatrixBuffer;
        private TexturePtr? _fontTexture;
        private ShaderModulePtr _shaderModule;
        private BindGroupLayoutPtr _layout;
        private BindGroupLayoutPtr _textureLayout;
        private RenderPipelinePtr _pipeline;
        private BindGroupPtr _mainBindGroup;
        private BindGroupPtr? _fontTextureResourceSet;
        private IntPtr _fontAtlasID = (IntPtr)1;

        public IntPtr Context;

        private readonly Dictionary<TextureViewPtr, BindGroupPtr> _bindGroupsByView = new();
        private bool _frameBegun;

        private static ulong NextValidBufferSize(ulong size) => (size + 15) / 16 * 16;

        /// <summary>
        /// Constructs a new ImGuiController.
        /// </summary>
        public ImGuiController(DevicePtr gd, TextureFormat colorOutputFormat, IView view, IInputContext input) : this(gd, colorOutputFormat, view, input, null, null)
        {
        }

        /// <summary>
        /// Constructs a new ImGuiController with font configuration.
        /// </summary>
        public ImGuiController(DevicePtr gd, TextureFormat colorOutputFormat, IView view, IInputContext input, ImGuiFontConfig imGuiFontConfig) : this(gd, colorOutputFormat, view, input, imGuiFontConfig, null)
        {
        }

        /// <summary>
        /// Constructs a new ImGuiController with an onConfigureIO Action.
        /// </summary>
        public ImGuiController(DevicePtr gd, TextureFormat colorOutputFormat, IView view, IInputContext input, Action onConfigureIO) : this(gd, colorOutputFormat, view, input, null, onConfigureIO)
        {
        }

        /// <summary>
        /// Constructs a new ImGuiController with font configuration and onConfigure Action.
        /// </summary>
        public unsafe ImGuiController(DevicePtr gd, TextureFormat colorOutputFormat, IView view, IInputContext input, ImGuiFontConfig? imGuiFontConfig = null, Action? onConfigureIO = null)
        {
            _device = gd;
            _view = view;
            _input = input;

            Context = ImGuiNET.ImGui.CreateContext();
            ImGuiNET.ImGui.SetCurrentContext(Context);
            ImGuiNET.ImGui.StyleColorsDark();

            var io = ImGuiNET.ImGui.GetIO();
            if (imGuiFontConfig is not null)
            {
                var glyphRange = imGuiFontConfig.Value.GetGlyphRange?.Invoke(io) ?? IntPtr.Zero;

                var fontPathPtr = SilkMarshal.StringToPtr(imGuiFontConfig.Value.FontPath);

                ImGuiNative.ImFontAtlas_AddFontFromFileTTF(io.Fonts.NativePtr, (byte*)fontPathPtr, imGuiFontConfig.Value.FontSize, 
                    null, (ushort*)glyphRange);

                SilkMarshal.Free(fontPathPtr);
            }

            onConfigureIO?.Invoke();

            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

            CreateDeviceResources(gd, colorOutputFormat);
            SetKeyMappings();

            SetPerFrameImGuiData(1f / 60f);

            BeginFrame();
        }

        public void MakeCurrent()
        {
            ImGuiNET.ImGui.SetCurrentContext(Context);
        }

        private void BeginFrame()
        {
            ImGuiNET.ImGui.NewFrame();
            _frameBegun = true;
            _keyboard = _input.Keyboards[0];
            _keyboard.KeyChar += OnKeyChar;
        }

        private void OnKeyChar(IKeyboard arg1, char arg2)
        {
            _pressedChars.Add(arg2);
        }

        /// <summary>
        /// Renders the ImGui draw list data.
        /// This method requires a <see cref="GraphicsDevice"/> because it may create new DeviceBuffers if the size of vertex
        /// or index data has increased beyond the capacity of the existing buffers.
        /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
        /// </summary>
        public void Render(RenderPassEncoderPtr pass)
        {
            if (_frameBegun)
            {
                var oldCtx = ImGuiNET.ImGui.GetCurrentContext();

                if (oldCtx != Context)
                {
                    ImGuiNET.ImGui.SetCurrentContext(Context);
                }

                _frameBegun = false;
                ImGuiNET.ImGui.Render();
                RenderImDrawData(ImGuiNET.ImGui.GetDrawData(), pass);

                if (oldCtx != Context)
                {
                    ImGuiNET.ImGui.SetCurrentContext(oldCtx);
                }
            }
        }

        /// <summary>
        /// Updates ImGui input and IO configuration state.
        /// </summary>
        public void Update(float deltaSeconds)
        {
            var oldCtx = ImGuiNET.ImGui.GetCurrentContext();

            if (oldCtx != Context)
            {
                ImGuiNET.ImGui.SetCurrentContext(Context);
            }

            if (_frameBegun)
            {
                ImGuiNET.ImGui.Render();
            }

            SetPerFrameImGuiData(deltaSeconds);
            UpdateImGuiInput();

            _frameBegun = true;
            ImGuiNET.ImGui.NewFrame();

            if (oldCtx != Context)
            {
                ImGuiNET.ImGui.SetCurrentContext(oldCtx);
            }
        }

        /// <summary>
        /// Sets per-frame data based on the associated window.
        /// This is called by Update(float).
        /// </summary>
        private void SetPerFrameImGuiData(float deltaSeconds)
        {
            var io = ImGuiNET.ImGui.GetIO();
            io.DisplaySize = new Vector2(_view.Size.X, _view.Size.Y);

            if (_view.Size.X * _view.Size.Y > 0)
            {
                io.DisplayFramebufferScale = new Vector2(_view.FramebufferSize.X / (float)_view.Size.X,
                    _view.FramebufferSize.Y / (float)_view.Size.Y);
            }

            io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
        }

        private static Key[] keyEnumArr = (Key[])Enum.GetValues(typeof(Key));

        private void UpdateImGuiInput()
        {
            var io = ImGuiNET.ImGui.GetIO();

            var mouseState = _input.Mice[0].CaptureState();
            var keyboardState = _input.Keyboards[0];

            io.MouseDown[0] = mouseState.IsButtonPressed(MouseButton.Left);
            io.MouseDown[1] = mouseState.IsButtonPressed(MouseButton.Right);
            io.MouseDown[2] = mouseState.IsButtonPressed(MouseButton.Middle);

            var point = new Vector2D<int>((int)mouseState.Position.X, (int)mouseState.Position.Y);
            io.MousePos = new Vector2(point.X, point.Y);

            var wheel = mouseState.GetScrollWheels()[0];
            io.MouseWheel = wheel.Y;
            io.MouseWheelH = wheel.X;

            foreach (var key in keyEnumArr)
            {
                if (key == Key.Unknown)
                {
                    continue;
                }
                io.KeysDown[(int)key] = keyboardState.IsKeyPressed(key);
            }

            foreach (var c in _pressedChars)
            {
                io.AddInputCharacter(c);
            }

            _pressedChars.Clear();

            io.KeyCtrl = keyboardState.IsKeyPressed(Key.ControlLeft) || keyboardState.IsKeyPressed(Key.ControlRight);
            io.KeyAlt = keyboardState.IsKeyPressed(Key.AltLeft) || keyboardState.IsKeyPressed(Key.AltRight);
            io.KeyShift = keyboardState.IsKeyPressed(Key.ShiftLeft) || keyboardState.IsKeyPressed(Key.ShiftRight);
            io.KeySuper = keyboardState.IsKeyPressed(Key.SuperLeft) || keyboardState.IsKeyPressed(Key.SuperRight);
        }

        internal void PressChar(char keyChar)
        {
            _pressedChars.Add(keyChar);
        }

        private static void SetKeyMappings()
        {
            var io = ImGuiNET.ImGui.GetIO();
            io.KeyMap[(int)ImGuiKey.Tab] = (int)Key.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Key.Left;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Key.Right;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Key.Up;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Key.Down;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)Key.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)Key.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int)Key.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)Key.End;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)Key.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)Key.Backspace;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)Key.Enter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)Key.Escape;
            io.KeyMap[(int)ImGuiKey.A] = (int)Key.A;
            io.KeyMap[(int)ImGuiKey.C] = (int)Key.C;
            io.KeyMap[(int)ImGuiKey.V] = (int)Key.V;
            io.KeyMap[(int)ImGuiKey.X] = (int)Key.X;
            io.KeyMap[(int)ImGuiKey.Y] = (int)Key.Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int)Key.Z;
        }

        private void CreateDeviceResources(DevicePtr device, TextureFormat colorOutputFormat)
        {
            _device = device;
            _vertexBuffer = new(device.CreateBuffer(BufferUsage.Vertex | BufferUsage.CopyDst, 10000, label: "ImGui.NET Vertex Buffer"),
                                            Offset: 0, Size: 10000);
            _indexBuffer = new(device.CreateBuffer(BufferUsage.Index | BufferUsage.CopyDst, 2000, label: "ImGui.NET Index Buffer"),
                                        Offset: 0, Size: 2000);

            _projMatrixBuffer = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst,  64, label: "ImGui.NET Projection Buffer");

            Safe.VertexBufferLayout[] vertexLayouts = new Safe.VertexBufferLayout[]
            {
                new Safe.VertexBufferLayout
                {
                    ArrayStride = 5*sizeof(float),
                    StepMode = VertexStepMode.Vertex,
                    Attributes = new VertexAttribute[]
                    {
                        //in_position
                        new(VertexFormat.Float32x2, offset: 0, shaderLocation: 0),
                        //in_texCoord
                        new(VertexFormat.Float32x2, offset: 2*sizeof(float), shaderLocation: 1),
                        //in_color
                        new(VertexFormat.Unorm8x4, offset: 4*sizeof(float), shaderLocation: 2)
                    }
                }
            };

            _layout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Vertex, BufferBindingType.Uniform, 64),
                    Sampler(1, ShaderStage.Fragment, SamplerBindingType.Filtering)
                },
                label: "ImGui.NET Resource Layout"
            );

            _textureLayout = device.CreateBindGroupLayout(
                new BindGroupLayoutEntry[]
                {
                    Texture(2, ShaderStage.Fragment, TextureSampleType.Float, TextureViewDimension.Dimension2D, multisampled: false)
                },
                label: "ImGui.NET Texture Layout"
            );

            SamplerPtr pointSampler = _device.CreateSampler(
                AddressMode.Repeat, AddressMode.Repeat, AddressMode.Repeat,
                FilterMode.Nearest, FilterMode.Nearest, MipmapFilterMode.Nearest,
                0, 0, default, 1, 
                label: "ImGui.NET PointSampler");

            var pipelineLayout = _device.CreatePipelineLayout(
                new Safe.BindGroupLayoutPtr[]
                {
                    _layout,
                    _textureLayout
                }
            );

            var shaderCode = """
                                struct Uniforms {
                    u_Matrix: mat4x4<f32>
                };

                struct VertexInput {
                    @location(0) a_Pos: vec2<f32>,
                    @location(1) a_UV: vec2<f32>,
                    @location(2) a_Color: vec4<f32>
                };

                struct VertexOutput {
                    @location(0) v_UV: vec2<f32>,
                    @location(1) v_Color: vec4<f32>,
                    @builtin(position) v_Position: vec4<f32>
                };

                @group(0)
                @binding(0)
                var<uniform> uniforms: Uniforms;

                @vertex
                fn vs_main(in: VertexInput) -> VertexOutput {
                    var out: VertexOutput;
                    out.v_UV = in.a_UV;
                    out.v_Color = in.a_Color;
                    out.v_Position = uniforms.u_Matrix * vec4<f32>(in.a_Pos.xy, 0.0, 1.0);
                    return out;
                }

                struct FragmentOutput {
                    @location(0) o_Target: vec4<f32>
                };

                @group(0)
                @binding(1)
                var u_Sampler: sampler;
                @group(1)
                @binding(2)
                var u_Texture: texture_2d<f32>;

                @fragment
                fn fs_main(in: VertexOutput) -> FragmentOutput {
                    let color = in.v_Color;

                    return FragmentOutput(color * textureSample(u_Texture, u_Sampler, in.v_UV));
                }
                """u8;

            _shaderModule = device.CreateShaderModuleWGSL(shaderCode,
                new Safe.ShaderModuleCompilationHint[]{
                    new Safe.ShaderModuleCompilationHint("vs_main", pipelineLayout),
                    new Safe.ShaderModuleCompilationHint("fs_main", pipelineLayout),
                });

            _pipeline = _device.CreateRenderPipeline(
                pipelineLayout,
                vertex: new Safe.VertexState
                {
                    Module = _shaderModule,
                    EntryPoint = "vs_main",
                    Buffers = vertexLayouts,
                    Constants = Array.Empty<(string, double)>()
                },
                primitive: new Safe.PrimitiveState()
                {
                    Topology = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.None
                },
                depthStencil: null,
                multisample: new MultisampleState
                {
                    Count = 1,
                    Mask = uint.MaxValue,
                    AlphaToCoverageEnabled = false
                },
                fragment: new Safe.FragmentState
                {
                    Module = _shaderModule,
                    EntryPoint = "fs_main",
                    Targets = new Safe.ColorTargetState[]
                    {
                        new Safe.ColorTargetState(colorOutputFormat, (
                            new BlendComponent(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                            new BlendComponent(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.DstAlpha)),
                            ColorWriteMask.All)
                    },
                    Constants = Array.Empty<(string, double)>()
                }, 
                label: "ImGui.NET Pipeline"
            );

            _mainBindGroup = device.CreateBindGroup(_layout,
                new BindGroupEntry[]
                {
                    Buffer(0, _projMatrixBuffer, 0, 64),
                    Sampler(1, pointSampler)
                },
                label: "ImGui.NET Main Resource Set");

            RecreateFontDeviceTexture(device);
        }

        /// <summary>
        /// Creates the <see cref="BindGroup"/> necessary for using <paramref name="textureView"/> in <see cref="Render(RenderPassEncoderPtr)"/>.
        /// <para>If this method wasn't called the <see cref="BindGroup"/> will be created just in time</para>
        /// </summary>
        public unsafe IntPtr CreateTextureBindGroup(TextureViewPtr textureView)
        {
            if (!_bindGroupsByView.TryGetValue(textureView, out BindGroupPtr bindGroup))
            {
                bindGroup = CreateTextureBindGroupInternal(textureView);
            }

            return bindGroup.GetIntPtr();
        }

        private unsafe BindGroupPtr CreateTextureBindGroupInternal(TextureViewPtr textureView)
        {
            var entry = Texture(2, textureView);
            var bindGroup = _device.CreateBindGroup(_textureLayout,
                    new ReadOnlySpan<BindGroupEntry>(&entry, 1));

            _bindGroupsByView.Add(textureView, bindGroup);

            return bindGroup;
        }

        public void RemoveTextureBindGroup(TextureViewPtr textureView)
        {
            if (_bindGroupsByView.TryGetValue(textureView, out BindGroupPtr bindGroup))
            {
                _bindGroupsByView.Remove(textureView);
                bindGroup.Release();
            }
        }

        public void ClearCachedImageResources()
        {
            foreach (BindGroupPtr bindGroup in _bindGroupsByView.Values)
            {
                bindGroup.Release();
            }

            _bindGroupsByView.Clear();
        }

        /// <summary>
        /// Recreates the device texture used to render text.
        /// </summary>
        public unsafe void RecreateFontDeviceTexture() => RecreateFontDeviceTexture(_device);

        /// <summary>
        /// Recreates the device texture used to render text.
        /// </summary>
        public unsafe void RecreateFontDeviceTexture(DevicePtr device)
        {
            ImGuiIOPtr io = ImGuiNET.ImGui.GetIO();
            // Build
            io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);

            // Store our identifier
            io.Fonts.SetTexID(_fontAtlasID);

            _fontTexture?.Destroy();
            _fontTexture = device.CreateTexture(
                TextureUsage.TextureBinding | TextureUsage.CopyDst, TextureDimension.Dimension2D, new Extent3D(
                (uint)width,
                (uint)height, 
                depthOrArrayLayers: 1), TextureFormat.Rgba8Unorm,
                mipLevelCount: 1,
                sampleCount: 1,
                viewFormats: new TextureFormat[]
                {
                    TextureFormat.Rgba8Unorm
                },
                label: "ImGui.NET Font Texture");

            device.GetQueue().WriteTexture<byte>(
                new Safe.ImageCopyTexture{
                    Aspect = TextureAspect.All,
                    MipLevel = 0,
                    Origin = new(0, 0, 0),
                    Texture = _fontTexture.Value
                },

                data: new Span<byte>(pixels,
                (int)(bytesPerPixel * width * height)),
                dataLayout: new TextureDataLayout
                {
                    BytesPerRow = (uint)(bytesPerPixel * width),
                    RowsPerImage = (uint)height,
                    Offset = 0
                },
                writeSize: new Extent3D
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    DepthOrArrayLayers = 1
                });

            var fontTextureView = _fontTexture.Value.CreateView(TextureFormat.Rgba8Unorm, TextureViewDimension.Dimension2D, TextureAspect.All,
                baseMipLevel: 0, mipLevelCount: 1, baseArrayLayer: 0, arrayLayerCount: 1, label: "ImGui.NET Font Texture - View");

            _fontTextureResourceSet?.Release();
            _fontTextureResourceSet = device.CreateBindGroup(_textureLayout, 
                new BindGroupEntry[]
                {
                    Texture(2, fontTextureView)
                }, 
                label: "ImGui.NET Font Texture Resource Set");

            io.Fonts.ClearTexData();
        }

        private unsafe void RenderImDrawData(ImDrawDataPtr draw_data, RenderPassEncoderPtr pass)
        {
            uint vertexOffsetInVertices = 0;
            uint indexOffsetInElements = 0;

            if (draw_data.CmdListsCount == 0)
            {
                return;
            }

            uint totalVBSize = (uint)(draw_data.TotalVtxCount * sizeof(ImDrawVert));
            if (totalVBSize > _vertexBuffer.Size)
            {
                _vertexBuffer.Buffer.Destroy();
                ulong size = NextValidBufferSize((ulong)(totalVBSize * 1.5f));
                _vertexBuffer = new(_device.CreateBuffer(_vertexBuffer.Buffer.GetUsage(), size, label: "ImGui.NET Vertex Buffer"),
                    Offset: 0, size);
            }

            uint totalIBSize = (uint)(draw_data.TotalIdxCount * sizeof(ushort));
            if (totalIBSize > _indexBuffer.Size)
            {
                _indexBuffer.Buffer.Destroy();
                ulong size = NextValidBufferSize((ulong)(totalIBSize * 1.5f));
                _indexBuffer = new(_device.CreateBuffer(_indexBuffer.Buffer.GetUsage(), size, label: "ImGui.NET Index Buffer"),
                    Offset: 0, size);
            }

            var queue = _device.GetQueue();


            var vertexData = new ImDrawVert[draw_data.TotalVtxCount];
            var indexData = new ushort[draw_data.TotalIdxCount];

            for (int i = 0; i < draw_data.CmdListsCount; i++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdListsRange[i];

                new ReadOnlySpan<ImDrawVert>((void*)cmd_list.VtxBuffer.Data, cmd_list.VtxBuffer.Size)
                .CopyTo(new Span<ImDrawVert>(vertexData, (int)vertexOffsetInVertices, cmd_list.VtxBuffer.Size));

                new ReadOnlySpan<ushort>((void*)cmd_list.IdxBuffer.Data, cmd_list.IdxBuffer.Size)
                .CopyTo(new Span<ushort>(indexData, (int)indexOffsetInElements, cmd_list.IdxBuffer.Size));

                vertexOffsetInVertices += (uint)cmd_list.VtxBuffer.Size;
                indexOffsetInElements += (uint)cmd_list.IdxBuffer.Size;
            }


            queue.WriteBufferAligned<ImDrawVert>(
                _vertexBuffer.Buffer,
                _vertexBuffer.Offset,
                data: vertexData
            );

            queue.WriteBufferAligned<ushort>(
                _indexBuffer.Buffer,
                _indexBuffer.Offset,
                data: indexData
            );

            // Setup orthographic projection matrix into our constant buffer
            {
                var io = ImGuiNET.ImGui.GetIO();

                Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(
                    0f,
                    io.DisplaySize.X,
                    io.DisplaySize.Y,
                    0.0f,
                    -1.0f,
                    1.0f);

                queue.WriteBuffer(_projMatrixBuffer, 0, new ReadOnlySpan<Matrix4x4>(&mvp, 1));
            }

            pass.SetVertexBuffer(0, _vertexBuffer.Buffer, _vertexBuffer.Offset, _vertexBuffer.Size);
            pass.SetIndexBuffer(_indexBuffer.Buffer, IndexFormat.Uint16, _indexBuffer.Offset, _indexBuffer.Size);
            pass.SetPipeline(_pipeline);
            pass.SetBindGroup(0, _mainBindGroup, dynamicOffsets: null);

            draw_data.ScaleClipRects(ImGuiNET.ImGui.GetIO().DisplayFramebufferScale);

            // Render command lists
            int vtx_offset = 0;
            int idx_offset = 0;
            for (int n = 0; n < draw_data.CmdListsCount; n++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdListsRange[n];
                for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
                {
                    ImDrawCmdPtr pcmd = cmd_list.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        if (pcmd.TextureId != IntPtr.Zero)
                        {
                            if (pcmd.TextureId == _fontAtlasID)
                            {
                                pass.SetBindGroup(1, _fontTextureResourceSet!.Value, dynamicOffsets: null);
                            }
                            else
                            {
                                var textureView = new TextureViewPtr(_device.GetAPI(), (TextureView*)pcmd.TextureId);

                                if (!_bindGroupsByView.TryGetValue(textureView, out BindGroupPtr bindGroup))
                                    bindGroup = CreateTextureBindGroupInternal(textureView);

                                pass.SetBindGroup(1, bindGroup, dynamicOffsets: null);
                            }
                        }

                        var scissorRectWidth  = Math.Min((uint)_view.FramebufferSize.X, (uint)pcmd.ClipRect.Z) - (uint)pcmd.ClipRect.X;
                        var scissorRectHeight = Math.Min((uint)_view.FramebufferSize.Y, (uint)pcmd.ClipRect.W) - (uint)pcmd.ClipRect.Y;

                        if (scissorRectWidth * scissorRectHeight == 0)
                            continue;

                        pass.SetScissorRect(
                            Math.Max(0, (uint)pcmd.ClipRect.X),
                            Math.Max(0, (uint)pcmd.ClipRect.Y),
                            scissorRectWidth, scissorRectHeight
                            );

                        pass.DrawIndexed(pcmd.ElemCount, 1, pcmd.IdxOffset + (uint)idx_offset, (int)(pcmd.VtxOffset + vtx_offset), 0);
                    }
                }

                idx_offset += cmd_list.IdxBuffer.Size;
                vtx_offset += cmd_list.VtxBuffer.Size;
            }
        }

        /// <summary>
        /// Frees all graphics resources used by the renderer.
        /// </summary>
        public void Dispose()
        {
            _vertexBuffer.Buffer.Destroy();
            _indexBuffer.Buffer.Destroy();
            _projMatrixBuffer.Destroy();
            _fontTexture?.Destroy();
            _shaderModule.Release();
            _layout.Release();
            _textureLayout.Release();
            _pipeline.Release();
            _mainBindGroup.Release();
            _fontTextureResourceSet?.Release();

            foreach (BindGroupPtr bindGroup in _bindGroupsByView.Values)
            {
                bindGroup.Release();
            }
        }
    }
}