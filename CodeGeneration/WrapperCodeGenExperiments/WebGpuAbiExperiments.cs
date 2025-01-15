using System.Diagnostics;
using System.Text;
using WebGPU.Yml;
using WebGPU.Yml.Scalars;

using _Type = WebGPU.Yml.Scalars.Unions.Type;
using _Function = WebGPU.Yml.Function;
using KIND = WebGPU.Yml.Scalars.Unions.Type.ComplexTypeKind;
using PRIMITIVE = WebGPU.Yml.Scalars.Unions.Type.PrimitiveType;

namespace WrapperCodeGenExperiments;

public static class WebGpuAbiExperiments
{
    public static void WriteAbiInfo()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res/webgpu.yml");
        Document? document;
        using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            if (!Document.TryLoad(reader, out document))
                throw new InvalidOperationException("Failed to load YAML document");
        }

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
    }

    private static string GenerateFunctionSignatureString(_Function method)
    {
        string parameterString = GenerateParameterString(method.Args ?? []);
        
        if (method.ReturnsAsync != null && method.ReturnsAsync.Count != 0)
        {
            switch (method.ReturnsAsync)
            {
                //get or create resource/object (can fail with status and message)
                case [
                        {
                            Name: "status", 
                            Type.ScalarOfComplexType: (KIND.Enum, var statusTypeEnumName)
                        }, 
                        {
                            Name: var objectParamName, 
                            Type.ScalarOfComplexType: (KIND.Object, var objectTypeObjectName), 
                            Pointer: null
                        },
                        {
                            Name: "message", 
                            Type.ScalarOfPrimitiveType: PRIMITIVE.String
                        }
                    ]:
                
                    return $"async {ToPascalCase(method.Name)}" +
                           $"({parameterString})" +
                           $" -> {ToPascalCase(objectTypeObjectName)}" +
                           $" | GPUError<{ToPascalCase(statusTypeEnumName)}>";
            
                //get info
                case [
                    {Type.ScalarOfComplexType: (kind: KIND.Struct, var structTypeName)}
                ]:
                
                    return $"async {ToPascalCase(method.Name)}" +
                           $"({parameterString})" +
                           $" -> {ToPascalCase(structTypeName)}";
            
                //get info (can fail with status and message)
                case [
                        {
                            Name: "status", 
                            Type.ScalarOfComplexType: (KIND.Enum, var statusTypeEnumName)
                        },
                        {
                            Type.ScalarOfComplexType: (kind: KIND.Struct, var structTypeName),
                            Pointer: Pointer.Immutable
                        }
                    ]:
                
                    return $"async {ToPascalCase(method.Name)}" +
                           $"({parameterString})" +
                           $" -> {ToPascalCase(structTypeName)}" +
                           $" | GPUError<{ToPascalCase(statusTypeEnumName)}>";
            
                //awaitable action/event (can fail with status and message)
                case [
                        {Name: "status", Type.ScalarOfComplexType: (KIND.Enum, var statusTypeEnumName)}
                    ]:
                
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

    private static string GenerateParameterString(IReadOnlyList<ParameterType> arguments)
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

    private static string ResolveType(_Type type)
    {
        bool isArray = type.IsArrayOf;
        string typeName;
        if (type.IsComplexType(out var kind, out string complexTypeName))
        {
            typeName = ToPascalCase(complexTypeName);
            
            if (isArray && kind is not (KIND.Enum or KIND.Bitflag or KIND.Struct or KIND.Object))
                throw new InvalidOperationException($"Unsupported complex type category {kind} for arrays");
            
            if (!isArray && kind is not (KIND.Enum or KIND.Bitflag or KIND.Struct or KIND.FunctionType or KIND.Object))
                throw new InvalidOperationException($"Unsupported complex type category {kind} for scalars");
        }
        else if (type.IsPrimitiveType(out var primitiveType))
        {
            // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
            typeName = primitiveType switch
            {
                PRIMITIVE.Void => "IntPtr",
                PRIMITIVE.Bool => "bool",
                PRIMITIVE.String => "string",
                PRIMITIVE.Uint16 => "ushort",
                PRIMITIVE.Uint32 => "uint",
                PRIMITIVE.Uint64 => "ulong",
                PRIMITIVE.Usize => "nuint",
                PRIMITIVE.Int16 => "short",
                PRIMITIVE.Int32 => "int",
                PRIMITIVE.Float32 => "float",
                PRIMITIVE.Float64 => "double",
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

    private static string ToPascalCase(string str, bool useLowerCase = false)
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
}