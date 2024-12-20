﻿namespace WebGPU.Yml.Scalars.Unions;

public readonly struct Type
{
    public bool IsPrimitiveType(out PrimitiveType primitiveType)
    {
        primitiveType = _primitiveType ?? (PrimitiveType)0xFF;
        return _primitiveType.HasValue;
    }
    
    public bool IsPrimitiveType(PrimitiveType primitiveType)
    {
        return _primitiveType == primitiveType;
    }
    
    public bool IsComplexType(out string complexType)
    {
        complexType = _complexType ?? string.Empty;
        return _complexType != null;
    }
    
    public bool IsComplexType(string complexType)
    {
        return _complexType == complexType;
    }
    
    internal bool IsValid => _primitiveType.HasValue || _complexType != null;

    internal Type(string value)
    {
        if (!Simple.TryParsePrimitiveType(value, out _primitiveType))
            _complexType = value;
    }
    
    private readonly PrimitiveType? _primitiveType;
    private readonly string? _complexType;
}