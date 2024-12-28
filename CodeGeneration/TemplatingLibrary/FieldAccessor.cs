using System.Text.RegularExpressions;

namespace TemplatingLibrary;

public readonly partial struct FieldAccessor
{
    [GeneratedRegex(@"[_a-zA-Z][_a-zA-Z0-9]*(?:\.[_a-zA-Z][_a-zA-Z0-9]*)*")]
    private static partial Regex FieldAccessorRegex();
    [GeneratedRegex("[_a-zA-Z][_a-zA-Z0-9]*")]
    private static partial Regex VariableNameRegex();
    
    public static FieldAccessor GlobalField(string fieldAccessor)
    {
        if (fieldAccessor == string.Empty)
            throw new ArgumentException("Field accessor cannot be empty");
        
        if (!FieldAccessorRegex().IsMatch(fieldAccessor))
            throw new ArgumentException($"Field accessor \"{fieldAccessor}\" is not valid");
        
        return new FieldAccessor(string.Empty, fieldAccessor);
    }
    
    public static FieldAccessor VariableField(string variableName, string fieldAccessor)
    {
        if (variableName == string.Empty)
            throw new ArgumentException("Variable name cannot be empty");
        
        if (!VariableNameRegex().IsMatch(variableName))
            throw new ArgumentException($"Variable name \"{variableName}\" is not valid");
        
        if (fieldAccessor == string.Empty)
            throw new ArgumentException("Field accessor cannot be empty");
        
        if (!FieldAccessorRegex().IsMatch(fieldAccessor))
            throw new ArgumentException($"Field accessor \"{fieldAccessor}\" is not valid");
        
        return new FieldAccessor(variableName, fieldAccessor);
    }

    public static FieldAccessor Identity(string variableName)
    {
        if (variableName == string.Empty)
            throw new ArgumentException("Variable name cannot be empty");
        
        if (!VariableNameRegex().IsMatch(variableName))
            throw new ArgumentException($"Variable name \"{variableName}\" is not valid");
        
        return new FieldAccessor(variableName, null);
    }
    
    internal bool IsEquivalent(ReadOnlySpan<char> fieldAccessorString)
    {
        var ptr = 0;
        if (fieldAccessorString.Length < 1 || 
            fieldAccessorString[0] != '$')
            return false;
        ptr++;
        
        int length = _variableName.Length;
        if (fieldAccessorString.Length < ptr + length || 
            !fieldAccessorString.Slice(ptr, length)
                .SequenceEqual(_variableName))
            return false;
        ptr += length;

        if (_accessorString == null)
            return fieldAccessorString.Length == ptr;
        
        if (fieldAccessorString.Length < ptr + 1 || 
            fieldAccessorString[ptr] != '.')
            return false;
        ptr++;

        
        length = _accessorString.Length;
            
        if (fieldAccessorString.Length < ptr + length || 
            !fieldAccessorString.Slice(ptr, length)
                .SequenceEqual(_accessorString))
            return false;
        ptr += length;

        return fieldAccessorString.Length == ptr;
    }
    
    private readonly string _variableName;
    private readonly string? _accessorString;

    private FieldAccessor(string variableName, string? accessorString)
    {
        _variableName = variableName;
        _accessorString = accessorString;
    }
}