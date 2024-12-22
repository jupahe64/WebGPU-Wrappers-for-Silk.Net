using System;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;

namespace Silk.NET.WebGPU.Safe.Utils;

internal unsafe struct PayloadWriter
{
    private byte* _payloadPtr;
    private byte* _stringPoolPtr;
    private byte* _payloadEnd;

    public PayloadWriter(int totalSize, byte* payloadPtr, byte* stringPoolPtr) : this()
    {
        _payloadPtr = payloadPtr;
        _stringPoolPtr = stringPoolPtr;
        _payloadEnd = payloadPtr + totalSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddStruct<T>() where T : unmanaged
    {
        var ptr = (T*)_payloadPtr;
        _payloadPtr += sizeof(T);
        return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddOptional<T>(in T? value) where T : unmanaged
    {
        if (value is null)
            return null;

        var ptr = (T*)_payloadPtr;
        *ptr = value.Value;
        _payloadPtr += sizeof(T);
        return ptr;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddArray<T>(int count) where T : unmanaged
    {
        var ptr = (T*)_payloadPtr;
        _payloadPtr += sizeof(T) * count;
        return ptr;
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* AddArray<T>(T[] array) where T : unmanaged
    {
        var ptr = (T*)_payloadPtr;
        _payloadPtr += sizeof(T) * array.Length;
            
        for (var i = 0; i < array.Length; i++)
            ptr[i] = array[i];
            
        return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte* AddString(string? str, NativeStringEncoding encoding)
    {
        var ptr = _stringPoolPtr;

        SilkMarshal.StringIntoSpan(str, 
            new Span<byte>(_stringPoolPtr, (int)(_payloadEnd - _stringPoolPtr)));
        _stringPoolPtr += SilkMarshal.GetMaxSizeOf(str, encoding);

        return ptr;
    }
}