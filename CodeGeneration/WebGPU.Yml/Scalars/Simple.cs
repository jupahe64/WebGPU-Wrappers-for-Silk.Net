using System.Globalization;

namespace WebGPU.Yml.Scalars;

internal static class Simple
{
    public static bool ParseBool(string value)
    {
        return value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new ArgumentException($"'{value}' is not a valid bool.")
        };
    }
    
    public static ushort ParseValue16(string value)
    {
        return value.StartsWith("0x") ? 
            ushort.Parse(value.AsSpan()[2..], NumberStyles.AllowHexSpecifier) : 
            ushort.Parse(value);
    }

    public static Pointer ParsePointer(string value)
    {
        return value switch
        {
            "immutable" => Pointer.Immutable,
            "mutable" => Pointer.Mutable,
            _ => throw new ArgumentException($"'{value}' is not a valid pointer type."),
        };
    }
    
    public static StructType ParseStructType(string value)
    {
        return value switch
        {
            "base_in" => StructType.BaseIn,
            "base_out" => StructType.BaseOut,
            "extension_in" => StructType.ExtensionIn,
            "extension_out" => StructType.ExtensionOut,
            "standalone" => StructType.Standalone,
            _ => throw new ArgumentException($"'{value}' is not a valid struct type."),
        };
    }
}

public enum Pointer : byte
{
    InvalidValue,
    Immutable,
    Mutable,
}

public enum StructType : byte
{
    InvalidValue,
    BaseIn,
    BaseOut,
    ExtensionIn,
    ExtensionOut,
    Standalone,
}