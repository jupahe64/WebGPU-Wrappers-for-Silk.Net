﻿using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TemplatingLibrary.TemplateLoading;
using TemplatingLibrary.TemplateParameters;
using TemplatingLibrary.TemplateParameters.Builder;
using WebGPU.Yml;
using WebGPU.Yml.Scalars;
using static TemplatingLibrary.FieldAccessor;

using _Type = WebGPU.Yml.Scalars.Unions.Type;
using _Function = WebGPU.Yml.Function;

string directory = Silk.NET.WebGPU.Safe.TemplatesEntryPoint.GetPath();
var template = TemplateLoader.Load(File.ReadAllText(Path.Combine(directory, "Callbacks.cs")), Range.All);
template.DebugPrint();

var conversionTemplates = TemplateLoader.LoadAllDefined(
    File.ReadAllText(Path.Combine(directory, "Conversions.cs")), Range.All);
foreach (var (name, _template) in conversionTemplates)
{
    Console.WriteLine($"// ----- {name} -----");
    _template.DebugPrint();
}

var parameters = template.ParameterizationBuilder()
    .ForeachLoop(GlobalField("Callbacks"),
    b =>
    {
        b.BeginIteration();
        TemplatedCallbackBuilder(b, b.Info.VariableName, "CallbackA", []);
        b.BeginIteration();
        TemplatedCallbackBuilder(b, b.Info.VariableName, "CallbackB", [
            ("TestStructA", "UnsafeTestStructA", "testStructA"),
            ("TestStructB", "UnsafeTestStructB", "testStructB"),
        ]);
    }).GetTemplate();

var sb = new StringBuilder();
parameters.Write(sb);
Console.WriteLine();
Console.WriteLine(sb.ToString());
return;

var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res/webgpu.yml");
WebGPU.Yml.Document? document;
using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
{
    if (!WebGPU.Yml.Document.TryLoad(reader, out document))
        throw new InvalidOperationException("Failed to load YAML document");
}

var complexScalarTypeRegex = new Regex(@"^(?:([\w_]+)\.)?([\w_]+)$");
var complexArrayTypeRegex = new Regex(@"^array<(?:([\w_]+)\.)?([\w_]+)>$");

var root = document.RootNode;

Console.WriteLine("enums:");
foreach (var _enum in root.Enums)
{
    Console.WriteLine($"  {ToPascalCase(_enum.Name)}:");
    foreach (var entry in _enum.Entries)
    {
        Console.WriteLine($"    {ToPascalCase(entry.Name)}"); 
    }
}

Console.WriteLine("callback types:");
foreach (var functionType in root.FunctionTypes)
{
    var _args = (functionType.Args ?? []).ToArray();
    if (_args is [.. var withoutUserdata, { Name: "userdata" }])
    {
        Console.WriteLine($"  delegate void {ToPascalCase(functionType.Name)}" +
                          $"({GenerateParameterString(withoutUserdata)});");
    }
    else
    {
        throw new InvalidOperationException("Callback has no userdata");
    }
}

Console.WriteLine("structs:");
foreach (var _struct in root.Structs)
{
    foreach (string extendedStructName in _struct.Extends ?? [])
        Console.WriteLine($"  [Extends(\"{ToPascalCase(extendedStructName)}\")]");

    var suffix = _struct.FreeMembers == true ? " : IDisposable" : "";
    Console.WriteLine($$"""
                          [StructType(StructType.{{_struct.Type}})]
                          struct {{ToPascalCase(_struct.Name)}} {{suffix}}
                          {
                        """);
    foreach (var member in _struct.Members ?? [])
    {
        Console.WriteLine($"    {ResolveType(member.Type)} {ToPascalCase(member.Name)};");
    }
    Console.WriteLine("  }");
}

Console.WriteLine("functions:");
foreach (var function in root.Functions)
{
    Console.WriteLine("  "+GenerateFunctionSignatureString(function));
}

Console.WriteLine("methods:");
foreach (var _object in root.Objects)
{
    
    Console.WriteLine($"  {ToPascalCase(_object.Name)}:");
    foreach (var method in _object.Methods ?? [])
    {
        Console.WriteLine("    "+GenerateFunctionSignatureString(method));
    }
}

string ToPascalCase(string str, bool useLowerCase = false)
{
    int underscoreCount = str.Count(c => c == '_');
    var sb = new StringBuilder(str.Length - underscoreCount);

    void AppendWord(int begin, int end)
    {
        if (useLowerCase)
        {
            sb.Append(str[begin]);
            useLowerCase = false;
        }
        else
            sb.Append(char.ToUpperInvariant(str[begin]));
        sb.Append(str, begin+1, end-1 - begin);
    }

    var start = 0;
    var i = 0;
    for (; i < str.Length; i++)
    {
        if (str[i] != '_') continue;
        AppendWord(start, i);
        start = i + 1;
    }
    AppendWord(start, i);
    
    return sb.ToString();
}

string ResolveType(_Type type)
{
    var isArray = false;
    var isStruct = false;
    var isCallbackFunc = false;
    var isObject = false;
    string typeName;
    if (type.IsComplexType(out string complexType))
    {
        Match match;
        if ((match = complexScalarTypeRegex.Match(complexType)).Success)
        {
            string category = match.Groups[1].Value;
            if (category is not ("enum" or "bitflag" or "struct" or "function_type" or "object"))
                throw new InvalidOperationException($"Unsupported complex type category {category}");
        
            isStruct = category == "struct";
            isCallbackFunc = category == "function_type";
            isObject = category == "object";
            typeName = ToPascalCase(match.Groups[2].Value);
            isArray = false;
        }
        else if ((match = complexArrayTypeRegex.Match(complexType)).Success)
        {
            string category = match.Groups[1].Value;
            if (category is not ("enum" or "bitflag" or "struct" or "object"))
                throw new InvalidOperationException($"Unsupported complex type category {category}");
        
            isStruct = category == "struct";
            isObject = category == "object";
            typeName = ToPascalCase(match.Groups[2].Value);
            isArray = true;
        }
        else
            throw new InvalidOperationException($"Unsupported complex type {complexType}");
    }
    else if (type.IsPrimitiveType(out var primitiveType))
    {
        if (primitiveType is >= PrimitiveType.ArraysStart and < PrimitiveType.ArraysEnd)
        {
            primitiveType -= PrimitiveType.ArraysStart - PrimitiveType.ScalarsStart;
            isArray = true;
        }
    
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        typeName = primitiveType switch
        {
            PrimitiveType.Void => "IntPtr",
            PrimitiveType.Bool => "bool",
            PrimitiveType.String => "string",
            PrimitiveType.Uint16 => "ushort",
            PrimitiveType.Uint32 => "uint",
            PrimitiveType.Uint64 => "ulong",
            PrimitiveType.Usize => "nuint",
            PrimitiveType.Int16 => "short",
            PrimitiveType.Int32 => "int",
            PrimitiveType.Float32 => "float",
            PrimitiveType.Float64 => "double",
            _ => throw new UnreachableException(),
        };
    }
    else
    {
        throw new UnreachableException();
    }
    
    if (isArray)
        typeName += "[]";

    return typeName;
}

string GenerateParameterString(IReadOnlyList<ParameterType> arguments)
{
    {
        var sb = new StringBuilder();

        var isFirst = true;
        foreach (var arg in arguments)
        {
            if (!isFirst)
                sb.Append(", ");

            sb.Append(ResolveType(arg.Type));
            sb.Append(' ');
            sb.Append(ToPascalCase(arg.Name, useLowerCase: true));
            isFirst = false;
        }
            
        return sb.ToString();
    }
}

string GenerateFunctionSignatureString(_Function method)
{
    string parameterString = GenerateParameterString(method.Args ?? []);
        
    if (method.ReturnsAsync != null && method.ReturnsAsync.Count != 0)
    {
        switch (method.ReturnsAsync)
        {
            //get or create resource/object (can fail with status and message)
            case [
                {Name: "status", Type: var statusType}, 
                {Name: var objectParamName, Type: var objectType, Pointer: null}, 
                {Name: "message", Type: var messageType}
                ] when statusType.IsComplexType(out string statusTypeName) &&
                       statusTypeName.Split('.') is ["enum", var statusTypeEnumName] &&
                       objectType.IsComplexType(out string objectTypeName) &&
                       objectTypeName.Split('.') is ["object", var objectTypeObjectName] &&
                       messageType.IsPrimitiveType(PrimitiveType.String):
                
                return $"async {ToPascalCase(method.Name)}" +
                       $"({parameterString})" +
                       $" -> {ToPascalCase(objectTypeObjectName)}" +
                       $" | GPUError<{ToPascalCase(statusTypeEnumName)}>";
            
            //get info
            case [
                {Name: var structTypeName, Type: var structType}
            ] when structType.IsComplexType($"struct.{structTypeName}"):
                
                return $"async {ToPascalCase(method.Name)}" +
                       $"({parameterString})" +
                       $" -> {ToPascalCase(structTypeName)}";
            
            //get info (can fail with status and message)
            case [
                {Name: "status", Type: var statusType},
                {Name: var structTypeName, Type: var structType, Pointer: Pointer.Immutable}
            ] when statusType.IsComplexType(out string statusTypeName) &&
                   statusTypeName.Split('.') is ["enum", var statusTypeEnumName] &&
                   structType.IsComplexType($"struct.{structTypeName}"):
                
                return $"async {ToPascalCase(method.Name)}" +
                       $"({parameterString})" +
                       $" -> {ToPascalCase(structTypeName)}" +
                       $" | GPUError<{ToPascalCase(statusTypeEnumName)}>";
            
            //awaitable action/event (can fail with status and message)
            case [
                {Name: "status", Type: var statusType}
            ] when statusType.IsComplexType(out string statusTypeName) &&
                   statusTypeName.Split('.') is ["enum", var statusTypeEnumName]:
                
                return $"async {ToPascalCase(method.Name)}" +
                       $"({parameterString})" +
                       $" -> Void | GPUError<{ToPascalCase(statusTypeEnumName)}>";
            
            default:
                throw new InvalidOperationException($"Method {method.Name} " +
                                                    $"has an unsupported async return convention.");
        }
    }
    else
    {
        if (method.Returns != null)
        {
            return $"{ToPascalCase(method.Name)}" +
                   $"({parameterString})" +
                   $" -> {ResolveType(method.Returns.Type)}";
        }
        else
        {
            return $"{ToPascalCase(method.Name)}" +
                   $"({parameterString})";
        }
    }
}

void TemplatedCallbackBuilder<T>(AbstractRegionBuilder<T> b, string callbackVariableName,
    string callbackName, IReadOnlyList<(string safeType, string unsafeType, string name)> arguments)
    where T : AbstractRegionBuilder<T>
{
    var safeParameterString = string.Join(", ", arguments.Select(argument => argument.safeType));
    var unsafeParameterTypeString = string.Join("", 
        arguments.Select(argument => $"{argument.unsafeType}, "));
    var unsafeParameterString = string.Join("", 
        arguments.Select(argument => $"{argument.unsafeType} {argument.name}, "));
    var callParameterString = string.Join(", ", 
        arguments.Select(argument => $"_{argument.name}"));
    
    b.ReplaceRegion(
            VariableField(callbackVariableName, "Name"), 
            _ => callbackName,
            b =>
            {
                b.ReplaceRegion(
                    VariableField(callbackVariableName, 
                        "SafeParameterString"),
                    _ => safeParameterString);
                
                b.ReplaceRegion(
                    VariableField(callbackVariableName, 
                        "UnsafeParameterTypeString"),
                    _ => unsafeParameterTypeString);
                
                b.ReplaceRegion(
                    VariableField(callbackVariableName, 
                        "UnsafeParameterString"),
                    _ => unsafeParameterString);
                
                b.ForeachLoop(
                    VariableField(callbackVariableName,
                        "ConversionStrings"),
                    b =>
                    {
                        b.BeginIteration();
                        b.Insert(Identity(b.Info.VariableName),
                            TemplatedStructUnpackConversion(
                                arguments.Select(x => (x.unsafeType, x.name)))
                            );
                    });
                
                b.ReplaceRegion(
                    VariableField(callbackVariableName, 
                        "CallParametersString"),
                    _ => callParameterString);
            });
}

ParameterizedTemplate TemplatedStructUnpackConversion(
    IEnumerable<(string unsafeType, string variableName)> arguments)
{
    var b = new ParameterizedTemplateBuilder(conversionTemplates["UnpackStructs"]);
    b.ForeachLoop(GlobalField("PackableStructs"),
        b =>
        {
            foreach (var argument in arguments)
            {
                b.BeginIteration();
                b.ReplaceRegion(
                    VariableField(b.Info.VariableName,
                        "Name"),
                    _ => argument.unsafeType,
                    VariableField(b.Info.VariableName,
                        "VariableName"),
                    _ => argument.variableName);

            }
        });
    return b.GetTemplate();
}
