using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WebGPU.Yml.Scalars.Unions;

public readonly partial struct Type
{
    [GeneratedRegex(
        @"^(array<)?(typedef\.|enum\.|bitflag\.|struct\.|function_type\.|object\.)([a-zA-Z0-9]([a-zA-Z0-9_]*[a-zA-Z0-9])?)(>)?$"
        )]
    private static partial Regex ComplexTypeRegex();
    
    public enum PrimitiveType : byte
    {
        InvalidValue,
        Void,
        Bool,
        String,
        Uint16,
        Uint32,
        Uint64,
        Usize,
        Int16,
        Int32,
        Float32,
        Float64,
    }
    
    public enum ComplexTypeKind
    {
        InvalidValue,
        TypeDef,
        Enum,
        Bitflag,
        Struct,
        FunctionType,
        Object
    }

    public static Type OfPrimitiveType(PrimitiveType primitiveType, bool isArrayOf = false) 
        => new(primitiveType, isArrayOf);

    public static Type OfComplexType(ComplexTypeKind kind, string name, bool isArrayOf = false) 
        => new(kind, name, isArrayOf);

    public override string ToString()
    {
        if (!IsValid)
            return "InvalidType";
        
        string str;
        bool isComplex;
        if (IsComplexType(out var kind, out string name))
        {
            str = $"(kind: {kind}, typeName: {name})";
            isComplex = true;
        }
        else
        {
            Debug.Assert(IsPrimitiveType(out var primitiveType));
            str = primitiveType.ToString();
            isComplex = false;
        }

        if (_isArray)
            return isComplex ? $"ArrayOf{str}" : $"ArrayOf({str})";
        
        return str;
    }
    
    public bool IsArrayOf => _isArray;
    /// <summary>
    /// Only meant to be used in pattern matching
    /// </summary>
    public PrimitiveType? ArrayOfPrimitiveType => IsArrayOf && IsPrimitiveType(out var value) ? value : null;
    /// <summary>
    /// Only meant to be used in pattern matching
    /// </summary>
    public PrimitiveType? ScalarOfPrimitiveType => !IsArrayOf && IsPrimitiveType(out var value) ? value : null;

    public bool IsPrimitiveType(out PrimitiveType primitiveType)
    {
        primitiveType = _primitiveType;
        return _primitiveType != PrimitiveType.InvalidValue;
    }
    
    public bool IsPrimitiveType(PrimitiveType primitiveType)
    {
        return _primitiveType == primitiveType;
    }
    
    /// <summary>
    /// Only meant to be used in pattern matching
    /// </summary>
    public (ComplexTypeKind kind, string name)? ArrayOfComplexType => 
        IsArrayOf && IsComplexType(out var kind, out string name) ? (kind, name) : null;
    
    /// <summary>
    /// Only meant to be used in pattern matching
    /// </summary>
    public (ComplexTypeKind kind, string name)? ScalarOfComplexType => 
        !IsArrayOf && IsComplexType(out var kind, out string name) ? (kind, name) : null;
    
    public bool IsComplexType(out ComplexTypeKind kind, out string name)
    {
        kind = _complexTypeKind;
        name = _complexTypeName ?? string.Empty;
        return _complexTypeKind != ComplexTypeKind.InvalidValue;
    }
    
    public bool IsComplexType(ComplexTypeKind kind, string name)
    {
        return _complexTypeKind == kind && _complexTypeName == name;
    }
    
    internal bool IsValid => _primitiveType != PrimitiveType.InvalidValue || 
                             _complexTypeKind != ComplexTypeKind.InvalidValue;

    private Type(PrimitiveType primitiveType, bool isArray = false)
    {
        _primitiveType = primitiveType;
        _isArray = isArray;
    }
    private Type(ComplexTypeKind complexTypeKind, string complexTypeName, bool isArray = false)
    {
        _complexTypeKind = complexTypeKind;
        _complexTypeName = complexTypeName;
        _isArray = isArray;
    }
    internal static Type Parse(string value)
    {
        if (TryParsePrimitiveType(value, out var primitiveType, out var isArray))
            return new Type(primitiveType, isArray);
        
        var match = ComplexTypeRegex().Match(value);
        if (!match.Success)
            throw new ArgumentException($"\"{value}\" doesn't match the pattern for complex types.");

        var regexGroupArrayOpening = match.Groups[1];
        var regexGroupKind = match.Groups[2];
        var regexGroupName = match.Groups[3]; //contains Group 4
        var regexGroupArrayClosing = match.Groups[5];
        
        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (regexGroupArrayOpening.Success && !regexGroupArrayClosing.Success)
            throw new ArgumentException("expected closing > for array type");
        if (!regexGroupArrayOpening.Success && regexGroupArrayClosing.Success)
            throw new ArgumentException("unexpected closing > for non array type");
        
        isArray = regexGroupArrayOpening.Success;
        
        var complexTypeKind = regexGroupKind.ValueSpan switch
        {
            "typedef." => ComplexTypeKind.TypeDef,
            "enum." => ComplexTypeKind.Enum,
            "bitflag." => ComplexTypeKind.Bitflag,
            "struct." => ComplexTypeKind.Struct,
            "function_type." => ComplexTypeKind.FunctionType,
            "object." => ComplexTypeKind.Object,
            _ => ComplexTypeKind.InvalidValue
        };
        string complexTypeName = regexGroupName.Value;
        
        Debug.Assert(complexTypeKind != ComplexTypeKind.InvalidValue);
        return new Type(complexTypeKind, complexTypeName, isArray);
    }
    
    private readonly bool _isArray;
    private readonly PrimitiveType _primitiveType;
    private readonly string? _complexTypeName;
    private readonly ComplexTypeKind _complexTypeKind;
    
    private static bool TryParsePrimitiveType(string value, 
        out PrimitiveType primitiveType, 
        out bool isArray)
    {
        var res = value switch
        {
            "c_void" => (PrimitiveType.Void,            isArray: false),
            "bool" => (PrimitiveType.Bool,              isArray: false),
            "string" => (PrimitiveType.String,          isArray: false),
            "uint16" => (PrimitiveType.Uint16,          isArray: false),
            "uint32" => (PrimitiveType.Uint32,          isArray: false),
            "uint64" => (PrimitiveType.Uint64,          isArray: false),
            "usize" => (PrimitiveType.Usize,            isArray: false),
            "int16" => (PrimitiveType.Int16,            isArray: false),
            "int32" => (PrimitiveType.Int32,            isArray: false),
            "float32" => (PrimitiveType.Float32,        isArray: false),
            "float64" => (PrimitiveType.Float64,        isArray: false),
            
            "array<bool>" => (PrimitiveType.Bool,       isArray: true),
            "array<string>" => (PrimitiveType.String,   isArray: true),
            "array<uint16>" => (PrimitiveType.Uint16,   isArray: true),
            "array<uint32>" => (PrimitiveType.Uint32,   isArray: true),
            "array<uint64>" => (PrimitiveType.Uint64,   isArray: true),
            "array<usize>" => (PrimitiveType.Usize,     isArray: true),
            "array<int16>" => (PrimitiveType.Int16,     isArray: true),
            "array<int32>" => (PrimitiveType.Int32,     isArray: true),
            "array<float32>" => (PrimitiveType.Float32, isArray: true),
            "array<float64>" => (PrimitiveType.Float64, isArray: true),
            _ => default
        };
        (primitiveType, isArray) = res;
        
        return primitiveType != default;
    }
}