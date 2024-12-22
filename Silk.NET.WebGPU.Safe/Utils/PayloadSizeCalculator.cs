using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Safe.Utils;

internal unsafe struct PayloadSizeCalculator
{
    private int _payloadSize;
    private int _stringPoolSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddStruct<T>() where T : unmanaged
    {
        _payloadSize += sizeof(T);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOptional<T>(bool isPresent) where T : unmanaged
    {
        _payloadSize += isPresent ? sizeof(T) : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOptional<T>(T? value) where T : unmanaged
    {
        _payloadSize += value.HasValue ? sizeof(T) : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddArray<T>(int count) where T : unmanaged
    {
        _payloadSize += sizeof(T) * count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddString(string? str, NativeStringEncoding encoding)
    {
        _stringPoolSize += SilkMarshal.GetMaxSizeOf(str, encoding);
    }

    public readonly void GetSize(out int size, out int stringPoolOffset)
    {
        size = _payloadSize + _stringPoolSize;
        stringPoolOffset = _payloadSize;
    }
}