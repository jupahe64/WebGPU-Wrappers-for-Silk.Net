using System.Diagnostics;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

using _Object = WebGPU.Yml.Object;
using _Enum = WebGPU.Yml.Enum;

namespace WebGPU.Yml;

public class RootNode
{
    /// <summary>
    /// The license string to include at the top of the generated header
    /// </summary>
    public string Copyright { get; init; }
    /// <summary>
    /// The name/namespace of the specification
    /// </summary>
    public string Name { get; init; }
    /// <summary>
    /// The dedicated enum prefix for the implementation specific header to avoid collisions
    /// </summary>
    public string EnumPrefix { get; init; }
    public List<TypeDef> TypeDefs { get; init; }
    public List<Constant> Constants { get; init; }
    public List<_Enum> Enums { get; init; }
    public List<BitFlag> BitFlags { get; init; }
    public List<Function> FunctionTypes { get; init; }
    public List<Struct> Structs { get; init; }
    public List<Function> Functions { get; init; }
    public List<_Object> Objects { get; init; }
    internal RootNode(IParser parser)
    {
        parser.Consume<MappingStart>();

        while (!parser.TryConsume<MappingEnd>(out _))
        {
            string key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "copyright":
                    Copyright = parser.ParseScalar();
                    break;
                case "name":
                    Name = parser.ParseScalar();
                    break;
                case "enum_prefix":
                    EnumPrefix = parser.ParseScalar();
                    break;
                case "constants":
                    Constants = parser.ParseSequence(p=>new Constant(p));
                    break;
                case "typedefs":
                    TypeDefs = parser.ParseSequence(p=>new TypeDef(p));
                    break;
                case "enums":
                    Enums = parser.ParseSequence(p=>new _Enum(p));
                    break;
                case "bitflags":
                    BitFlags = parser.ParseSequence(p=>new BitFlag(p));
                    break;
                case "function_types":
                    FunctionTypes = parser.ParseSequence(p=>new Function(p));
                    break;
                case "structs":
                    Structs = parser.ParseSequence(p=>new Struct(p));
                    break;
                case "functions":
                    Functions = parser.ParseSequence(p=>new Function(p));
                    break;
                case "objects":
                    Objects = parser.ParseSequence(p=>new _Object(p));
                    break;
                default:
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        Debug.Assert(Copyright != null);
        Debug.Assert(Name != null);
        Debug.Assert(EnumPrefix != null);
        Debug.Assert(Constants != null);
        Debug.Assert(TypeDefs != null);
        Debug.Assert(Enums != null);
        Debug.Assert(BitFlags != null);
        Debug.Assert(FunctionTypes != null);
        Debug.Assert(Structs != null);
        Debug.Assert(Functions != null);
        Debug.Assert(Objects != null);
    }
}

public enum Test
{
    A
}