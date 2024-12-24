using System;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Safe.Utils;

/// <summary>
/// This is just a dummy class do NOT use at runtime
/// </summary>
internal unsafe struct PayloadWriter
{
    public PayloadWriter(int totalSize, byte* payloadPtr, byte* stringPoolPtr) : this()
    {
        throw null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddStruct<T>() where T : unmanaged
    {
        throw null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddOptional<T>(in T? value) where T : unmanaged
    {
        throw null!;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddArray<T>(int count) where T : unmanaged
    {
        throw null!;
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddArray<T>(T[] array) where T : unmanaged
    {
        throw null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte* AddString(string? str, NativeStringEncoding encoding)
    {
        throw null!;
    }
}