using System.Diagnostics.CodeAnalysis;
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

    public static PrimitiveType ParsePrimitiveType(string value)
    {
        if (!TryParsePrimitiveType(value, out var result))
            throw new ArgumentException($"'{value}' is not a valid primitive type.");
        
        return result.Value;
    }
    public static bool TryParsePrimitiveType(string value, 
        [NotNullWhen(true)] out PrimitiveType? primitiveType)
    {
        primitiveType = value switch
        {
            "c_void" => PrimitiveType.Void,
            "bool" => PrimitiveType.Bool,
            "string" => PrimitiveType.String,
            "uint16" => PrimitiveType.Uint16,
            "uint32" => PrimitiveType.Uint32,
            "uint64" => PrimitiveType.Uint64,
            "usize" => PrimitiveType.Usize,
            "int16" => PrimitiveType.Int16,
            "int32" => PrimitiveType.Int32,
            "float32" => PrimitiveType.Float32,
            "float64" => PrimitiveType.Float64,
            "array<bool>" => PrimitiveType.ArrayOfBool,
            "array<string>" => PrimitiveType.ArrayOfString,
            "array<uint16>" => PrimitiveType.ArrayOfUint16,
            "array<uint32>" => PrimitiveType.ArrayOfUint32,
            "array<uint64>" => PrimitiveType.ArrayOfUint64,
            "array<usize>" => PrimitiveType.ArrayOfUsize,
            "array<int16>" => PrimitiveType.ArrayOfInt16,
            "array<int32>" => PrimitiveType.ArrayOfInt32,
            "array<float32>" => PrimitiveType.ArrayOfFloat32,
            "array<float64>" => PrimitiveType.ArrayOfFloat64,
            _ => null
        };
        
        return primitiveType.HasValue;
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

public enum PrimitiveType : byte
{
    InvalidValue,
    Void,
    ScalarsStart,
    Bool = ScalarsStart,
    String,
    Uint16,
    Uint32,
    Uint64,
    Usize,
    Int16,
    Int32,
    Float32,
    Float64,
    ScalarsEnd,
    ArraysStart,
    ArrayOfBool = ArraysStart,
    ArrayOfString,
    ArrayOfUint16,
    ArrayOfUint32,
    ArrayOfUint64,
    ArrayOfUsize,
    ArrayOfInt16,
    ArrayOfInt32,
    ArrayOfFloat32,
    ArrayOfFloat64,
    ArraysEnd,
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