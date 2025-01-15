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
                bool isUnsafe = false;
                var parameterString = GenerateParameterString(withoutUserdata, ref isUnsafe);
                if (isUnsafe)
                    Console.WriteLine($"""
                                        //unsafe (needs to be wrapped or hidden from the user)
                                        delegate void _{ToPascalCase(functionType.Name)}({parameterString});
                                      """);
                else
                    Console.WriteLine($"  delegate void {ToPascalCase(functionType.Name)}({parameterString});");
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
                var isUnsafe = false;
                string typeStr;
                if (member.Pointer == Pointer.Immutable)
                {
                    if (member.Type.IsArrayOf)
                        typeStr = ResolveType(member.Type, ref isUnsafe, member.Pointer);
                    else if (member.Type is { ScalarOfComplexType: (KIND.Struct, _) })
                        typeStr = "ref " + ResolveType(member.Type, ref isUnsafe);
                    else if (member.Type.IsPrimitiveType(out _))
                        typeStr = "ref " + ResolveType(member.Type, ref isUnsafe);
                    else
                        throw new InvalidOperationException($"Can't have pointer member to {member.Type} as member");
                }
                else
                {
                    typeStr = ResolveType(member.Type, ref isUnsafe, member.Pointer);
                }
                
                if (isUnsafe)
                    Console.WriteLine($"""
                                           //unsafe (needs to be wrapped or hidden from the user)
                                           {typeStr} _{ToPascalCase(member.Name)};
                                       """);
                else
                    Console.WriteLine($"    {typeStr} {ToPascalCase(member.Name)};");
            }
            Console.WriteLine("  }");
        }

        Console.WriteLine("functions:");
        foreach (var function in root.Functions)
        {
            var isUnsafe = false;
            var signatureStr = GenerateFunctionSignatureString(function, ref isUnsafe);
            if (isUnsafe)
                Console.WriteLine("    //unsafe (needs to be wrapped or hidden from the user)");
            Console.WriteLine("  "+signatureStr);
        }

        Console.WriteLine("methods:");
        foreach (var _object in root.Objects)
        {
            
            Console.WriteLine($"  {ToPascalCase(_object.Name)}:");
            foreach (var method in _object.Methods ?? [])
            {
                var isUnsafe = false;
                var signatureStr = GenerateFunctionSignatureString(method, ref isUnsafe);
                if (isUnsafe)
                    Console.WriteLine("    //unsafe (needs to be wrapped or hidden from the user)");
                Console.WriteLine("    "+signatureStr);
            }
        }
    }

    private static string GenerateFunctionSignatureString(_Function method, ref bool isUnsafe)
    {
        string parameterString = GenerateParameterString(method.Args ?? [], ref isUnsafe);
        
        if (method.ReturnsAsync != null && method.ReturnsAsync.Count != 0)
        {
            switch (method.ReturnsAsync)
            {
                //get or create resource/object (can fail with status and message)
                case [
                        {
                            Name: "status", 
                            Type.ScalarOfComplexType: (KIND.Enum, var statusTypeEnumName),
                            Pointer: null
                        }, 
                        {
                            Name: var objectParamName, 
                            Type.ScalarOfComplexType: (KIND.Object, var objectTypeObjectName), 
                            Pointer: null
                        },
                        {
                            Name: "message", 
                            Type.ScalarOfPrimitiveType: PRIMITIVE.String,
                            Pointer: null
                        }
                    ]:
                
                    return $"async {(isUnsafe?"_":"")}{ToPascalCase(method.Name)}" +
                           $"({parameterString})" +
                           $" -> {ToPascalCase(objectTypeObjectName)}" +
                           $" | GPUError<{ToPascalCase(statusTypeEnumName)}>";
            
                //get info
                case [
                    {Type.ScalarOfComplexType: (kind: KIND.Struct, var structTypeName), Pointer: null}
                ]:
                
                    return $"async {(isUnsafe?"_":"")}{ToPascalCase(method.Name)}" +
                           $"({parameterString})" +
                           $" -> {ToPascalCase(structTypeName)}";
            
                //get info (can fail with status and message)
                case [
                        {
                            Name: "status", 
                            Type.ScalarOfComplexType: (KIND.Enum, var statusTypeEnumName),
                            Pointer: null
                        },
                        {
                            Type.ScalarOfComplexType: (kind: KIND.Struct, var structTypeName),
                            Pointer: Pointer.Immutable
                        }
                    ]:
                
                    return $"async {(isUnsafe?"_":"")}{ToPascalCase(method.Name)}" +
                           $"({parameterString})" +
                           $" -> {ToPascalCase(structTypeName)}" +
                           $" | GPUError<{ToPascalCase(statusTypeEnumName)}>";
            
                //awaitable action/event (can fail with status and message)
                case [
                    {
                        Name: "status", 
                        Type.ScalarOfComplexType: (KIND.Enum, var statusTypeEnumName),
                        Pointer: null
                    }
                    ]:
                
                    return $"async {(isUnsafe?"_":"")}{ToPascalCase(method.Name)}" +
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
                if (method.Returns.Pointer == Pointer.Immutable)
                {
                    switch (method.Returns.Type)
                    {
                        case {ScalarOfPrimitiveType: PRIMITIVE.Void}:
                        case {IsArrayOf: true}:
                            break;
                        default:
                            throw new InvalidOperationException($"Can't have pointer to {method.Returns.Type}");
                    }
                }

                string returnTypeStr = ResolveType(method.Returns.Type, ref isUnsafe, method.Returns.Pointer);
                return $"{(isUnsafe?"_":"")}{ToPascalCase(method.Name)}" +
                       $"({parameterString})" +
                       $" -> {returnTypeStr}";
            }
            else
            {
                return $"{(isUnsafe?"_":"")}{ToPascalCase(method.Name)}" +
                       $"({parameterString})";
            }
        }
    }

    private static string GenerateParameterString(IReadOnlyList<ParameterType> arguments, ref bool isUnsafe)
    {
        var sb = new StringBuilder();

        var isFirst = true;
        foreach (var arg in arguments)
        {
            if (!isFirst)
                sb.Append(", ");

            if (arg.Pointer == Pointer.Immutable)
            {
                switch (arg.Type)
                {
                    case {ScalarOfComplexType: (KIND.Struct, _)}:
                        sb.Append("in ");
                        sb.Append(ResolveType(arg.Type, ref isUnsafe));
                        break;
                    case {ScalarOfPrimitiveType: PRIMITIVE.Void}:
                    case {IsArrayOf: true}:
                        sb.Append(ResolveType(arg.Type, ref isUnsafe, arg.Pointer));
                        break;
                    default:
                        throw new InvalidOperationException($"Can't have pointer to {arg.Type}");
                }
            }
            else if (arg.Pointer == Pointer.Mutable)
            {
                switch (arg.Type)
                {
                    case {ScalarOfComplexType: (KIND.Enum or KIND.Struct, _)}:
                        sb.Append("out ");
                        sb.Append(ResolveType(arg.Type, ref isUnsafe));
                        break;
                    case {ScalarOfPrimitiveType: PRIMITIVE.Void}:
                        string typeStr = ResolveType(arg.Type, ref isUnsafe, arg.Pointer);
                        Debug.Assert(typeStr == "void*");
                        sb.Append(typeStr);
                        break;
                    default:
                        throw new InvalidOperationException($"Can't have mutable pointer to {arg.Type}");
                }
            }
            else
            {
                sb.Append(ResolveType(arg.Type, ref isUnsafe, arg.Pointer));
            }

            sb.Append(' ');
            sb.Append(ToPascalCase(arg.Name, useLowerCase: true));
            isFirst = false;
        }
        
        return sb.ToString();
    }

    private static string ResolveType(_Type type, ref bool isUnsafe, Pointer? pointer = null)
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
                PRIMITIVE.Void => "void*",
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

        // ReSharper disable once InvertIf
        if (isArray)
        {
            if (pointer == null)
                throw new InvalidOperationException($"Expected array type {type} to be used via pointer");
            
            typeName += "[]";
        }
        else
        {
            if (pointer != null && !type.IsPrimitiveType(PRIMITIVE.Void))
                throw new InvalidOperationException($"Expected scalar type {type} to not be used via pointer");
            
            if (pointer != null)
                isUnsafe = true;
        }

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