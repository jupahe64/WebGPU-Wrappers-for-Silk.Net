using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Safe.Utils;

internal struct PayloadSizeCalculator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddStruct<T>() where T : unmanaged
    {
        throw null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOptional<T>(bool isPresent) where T : unmanaged
    {
        throw null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOptional<T>(T? value) where T : unmanaged
    {
        throw null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddArray<T>(int count) where T : unmanaged
    {
        throw null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddString(string? str, NativeStringEncoding encoding)
    {
        throw null!;
    }

    public readonly void GetSize(out int size, out int stringPoolOffset)
    {
        throw null!;
    }
}