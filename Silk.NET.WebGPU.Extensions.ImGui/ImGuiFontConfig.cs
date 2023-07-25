using ImGuiNET;
using System;

namespace Silk.NET.WebGPU.Extensions.ImGui
{
    // copied and modified from https://github.com/veldrid/veldrid/blob/master/src/Veldrid.ImGui/ImGuiRenderer.cs
    // and https://github.com/dotnet/Silk.NET/tree/main/src/OpenGL/Extensions/Silk.NET.OpenGL.Extensions.ImGui


    public readonly struct ImGuiFontConfig
    {
        public ImGuiFontConfig(string fontPath, int fontSize, Func<ImGuiIOPtr, IntPtr>? getGlyphRange = null)
        {
            if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
            FontPath = fontPath ?? throw new ArgumentNullException(nameof(fontPath));
            FontSize = fontSize;
            GetGlyphRange = getGlyphRange;
        }

        public string FontPath { get; }
        public int FontSize { get; }
        public Func<ImGuiIOPtr, IntPtr>? GetGlyphRange { get; }
    }
}