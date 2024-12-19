using System.Globalization;

namespace WebGPU.Yml.Scalars.Unions;

public readonly struct Value64
{
    public bool IsOrdinary(out ulong value)
    {
        value = _value;
        return _specialValue == SpecialValue.None;
    }
    
    public bool IsSpecial(out SpecialValue value)
    {
        value = _specialValue;
        return _specialValue != SpecialValue.None;
    }

    public bool IsValid => _specialValue != SpecialValue.InvalidValue;
    
    internal Value64(string value)
    {
        (_value, _specialValue) = value switch
        {
            "usize_max" => (0ul, SpecialValue.UsizeMax),
            "uint32_max" => (0ul, SpecialValue.Uint32Max),
            "uint64_max" => (0ul, SpecialValue.Uint64Max),
            _ => (ParseUInt64(value), SpecialValue.None)
        };
    }

    private static ulong ParseUInt64(string value)
    {
        return value.StartsWith("0x") ? 
            ulong.Parse(value.AsSpan()[2..], NumberStyles.AllowHexSpecifier) : 
            ulong.Parse(value);
    }
    
    private readonly ulong _value = 0;
    private readonly SpecialValue _specialValue;

    public enum SpecialValue : byte
    {
        InvalidValue,
        None,
        UsizeMax,
        Uint32Max,
        Uint64Max,
    }
}