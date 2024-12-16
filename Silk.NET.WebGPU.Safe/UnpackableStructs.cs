using System;
using Silk.NET.Core.Attributes;
using Silk.NET.Core.Native;
using System.Diagnostics;
using WGPU = Silk.NET.WebGPU;

namespace Silk.NET.WebGPU.Safe
{
    internal static unsafe class UnpackHelper
    {
        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
        internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
        {
            public string ParameterName { get; } = parameterName;
        }
        
        public static string UnpackUtf8String(void* ptr,
            [CallerArgumentExpression("ptr")] string callerParamName= "")
        {
            var str = SilkMarshal.PtrToString((nint)ptr, NativeStringEncoding.UTF8);
            if (str == null)
                throw new NullReferenceException($"Expected non-null string for {callerParamName} got null.");
            
            return str;
        }
        
        public static string? UnpackUtf8StringNullable(void* ptr)
        {
            var str = SilkMarshal.PtrToString((nint)ptr, NativeStringEncoding.UTF8);
            return str;
        }

        public static T[] UnpackArray<T>(nuint count, T* ptr) where T : unmanaged
        {
            var array = new T[count];

            for (var i = 0; i < array.Length; i++)
                array[i] = ptr[i];
            
            return array;
        }
    }
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
                    Message = UnpackHelper.UnpackUtf8StringNullable(native->Messages[i].Message),
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

    public unsafe struct AdapterInfo
    {
        public string Vendor;
        public string Architecture;
        public string Device;
        public string Description;

        internal static AdapterInfo UnpackFrom(WGPU.AdapterInfo* native)
        {
            return new AdapterInfo()
            {
                Vendor = UnpackHelper.UnpackUtf8String(native->Vendor),
                Architecture = UnpackHelper.UnpackUtf8String(native->Architecture),
                Device = UnpackHelper.UnpackUtf8String(native->Device),
                Description = UnpackHelper.UnpackUtf8String(native->Description),
            };
        }
    }

    public unsafe struct AdapterProperties
    {
        public uint VendorID;

        public string VendorName;

        public string Architecture;

        public uint DeviceID;

        public string Name;

        public string DriverDescription;

        public AdapterType AdapterType;

        public BackendType BackendType;

        internal static AdapterProperties UnpackFrom(WGPU.AdapterProperties* native)
        {
            Debug.Assert(native->NextInChain == null);

            var properties = new AdapterProperties
            {
                VendorID = native->VendorID,
                VendorName = UnpackHelper.UnpackUtf8String(native->VendorName),
                Architecture = UnpackHelper.UnpackUtf8String(native->Architecture),
                DeviceID = native->DeviceID,
                Name = UnpackHelper.UnpackUtf8String(native->Name),
                DriverDescription = UnpackHelper.UnpackUtf8String(native->DriverDescription),

                AdapterType = native->AdapterType,
                BackendType = native->BackendType
            };

            return properties;
        }
    }

    public unsafe struct SurfaceCapabilities : IDisposable
    {
        private WebGPU _wgpu;
        private WGPU.SurfaceCapabilities* _ptr;
        public TextureFormat[] Formats;

        public PresentMode[] PresentModes;

        public CompositeAlphaMode[] AlphaModes;

        internal static SurfaceCapabilities UnpackFrom(WebGPU wgpu, WGPU.SurfaceCapabilities* native)
        {
            return new SurfaceCapabilities
            {
                _wgpu = wgpu,
                _ptr = native,
                Formats = UnpackHelper.UnpackArray(
                    native->FormatCount,
                    native->Formats  
                ),
                PresentModes = UnpackHelper.UnpackArray(
                    native->PresentModeCount,
                    native->PresentModes  
                ),
                AlphaModes = UnpackHelper.UnpackArray(
                    native->AlphaModeCount,
                    native->AlphaModes  
                )
            };
        }
        
        public void Dispose()
        {
            _wgpu.SurfaceCapabilitiesFreeMembers(*_ptr);
        }
    }
}
