using System.Diagnostics;
using WebGPU.Yml.Scalars;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

public class Struct
{
    /// <summary>
    /// Name of the structure
    /// </summary>
    public string Name { get; init; }
    public string Doc { get; init; }
    /// <summary>
    /// Type of the structure
    /// </summary>
    public StructType Type { get; init; }
    /// <summary>
    /// Optional property, list of names of the structs that this extension structure extends
    /// </summary>
    public List<string>? Extends { get; init; }
    /// <summary>
    /// Optional property, to indicate if a free members function be emitted for the struct
    /// </summary>
    public bool? FreeMembers { get; init; }
    /// <summary>
    /// Optional property, list of struct members
    /// </summary>
    public List<ParameterType>? Members { get; init; }

    internal Struct(IParser parser)
    {
        parser.Consume<MappingStart>();

        while (!parser.TryConsume<MappingEnd>(out _))
        {
            string key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "name":
                    Name = parser.ParseScalar();
                    break;
                case "doc":
                    Doc = parser.ParseScalar();
                    break;
                case "type":
                    Type = Simple.ParseStructType(parser.ParseScalar());
                    break;
                case "extends":
                    Extends = parser.ParseSequence(ParserExtensions.ParseScalar);
                    break;
                case "free_members":
                    FreeMembers = Simple.ParseBool(parser.ParseScalar());
                    break;
                case "members":
                    Members = parser.ParseSequence(p=>new ParameterType(p));
                    break;
                default:
                    Debugger.Break();
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        Debug.Assert(Name != null);
        Debug.Assert(Doc != null);
        Debug.Assert(Type != StructType.InvalidValue);
    }
}