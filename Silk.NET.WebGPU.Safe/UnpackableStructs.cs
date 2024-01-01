using Silk.NET.Core.Attributes;
using Silk.NET.Core.Native;
using System.Diagnostics;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    public unsafe struct CompilationInfo
    {
        public CompilationMessage[] Messages;

        internal static CompilationInfo UnpackFrom(WGPU.CompilationInfo* native)
        {
            Debug.Assert(native->NextInChain == null);

            var messages = new CompilationMessage[native->MessageCount];

            for (uint i = 0; i < native->MessageCount; i++)
            {
                messages[i] = new CompilationMessage
                {
                    Message = SilkMarshal.PtrToString((nint)native->Messages[i].Message, NativeStringEncoding.UTF8),
                    Type = native->Messages[i].Type,
                    LineNum = native->Messages[i].LineNum,
                    LinePos = native->Messages[i].LinePos,
                    Offset = native->Messages[i].Offset,
                    Length = native->Messages[i].Length,
                    Utf16LinePos = native->Messages[i].Utf16LinePos,
                    Utf16Offset = native->Messages[i].Utf16Offset,
                    Utf16Length = native->Messages[i].Utf16Length,
                };
            }
                

            return new CompilationInfo
            {
                Messages = messages
            };
        }
    }

    public record struct CompilationMessage(string? Message, CompilationMessageType Type, 
        ulong LineNum, ulong LinePos, ulong Offset, ulong Length,
        ulong Utf16LinePos, ulong Utf16Offset, ulong Utf16Length);

    public unsafe struct AdapterProperties
    {
        public uint VendorID;

        public string? VendorName;

        public string? Architecture;

        public uint DeviceID;

        public string? Name;

        public string? DriverDescription;

        public AdapterType AdapterType;

        public BackendType BackendType;

        internal static AdapterProperties UnpackFrom(WGPU.AdapterProperties* native)
        {
            Debug.Assert(native->NextInChain == null);

            var properties = new AdapterProperties
            {
                VendorID = native->VendorID,
                VendorName = SilkMarshal.PtrToString(
                (nint)native->VendorName, NativeStringEncoding.UTF8),

                Architecture = SilkMarshal.PtrToString(
                (nint)native->Architecture, NativeStringEncoding.UTF8),

                DeviceID = native->DeviceID,
                Name = SilkMarshal.PtrToString(
                (nint)native->Name, NativeStringEncoding.UTF8),

                DriverDescription = SilkMarshal.PtrToString(
                (nint)native->DriverDescription, NativeStringEncoding.UTF8),

                AdapterType = native->AdapterType,
                BackendType = native->BackendType
            };

            return properties;
        }
    }

    public unsafe partial struct SurfaceCapabilities
    {
        public unsafe TextureFormat[] Formats;

        public unsafe PresentMode[] PresentModes;

        public CompositeAlphaMode[] AlphaModes;

        internal static SurfaceCapabilities UnpackFrom(WGPU.SurfaceCapabilities* native)
        {
            var formats = new TextureFormat[native->FormatCount];
            var presentModes = new PresentMode[native->PresentModeCount];
            var alphaModes = new CompositeAlphaMode[native->AlphaModeCount];

            for (int i = 0; i < formats.Length; i++)
                formats[i] = native->Formats[i];

            for (int i = 0; i < presentModes.Length; i++)
                presentModes[i] = native->PresentModes[i];

            for (int i = 0; i < alphaModes.Length; i++)
                alphaModes[i] = native->AlphaModes[i];

            return new SurfaceCapabilities
            {
                Formats = formats,
                PresentModes = presentModes,
                AlphaModes = alphaModes
            };
        }
    }
}
