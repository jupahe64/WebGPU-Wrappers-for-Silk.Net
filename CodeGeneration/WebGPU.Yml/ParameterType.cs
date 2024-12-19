using System.Diagnostics;
using WebGPU.Yml.Scalars;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

using _Type = Scalars.Unions.Type;

public class ParameterType
{
    /// <summary>
    /// Parameter Name
    /// </summary>
    public string Name { get; init; }
    public string Doc { get; init; }
    /// <summary>
    /// Parameter type
    /// </summary>
    public _Type Type { get; init; }
    /// <summary>
    /// Optional property, specifies if a parameter type is a pointer
    /// </summary>
    public Pointer? Pointer { get; init; }
    /// <summary>
    /// Optional property, to indicate if a parameter is optional
    /// </summary>
    public bool? Optional { get; init; }
    /// <summary>
    /// Optional property, specifying the external namespace where this type is defined
    /// </summary>
    public string? Namespace { get; init; }

    internal ParameterType(IParser parser)
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
                    Type = new _Type(parser.ParseScalar());
                    break;
                case "pointer":
                    Pointer = Simple.ParsePointer(parser.ParseScalar());
                    break;
                case "optional":
                    Optional = Simple.ParseBool(parser.ParseScalar());
                    break;
                case "namespace":
                    Namespace = parser.ParseScalar();
                    break;
                default:
                    Debugger.Break();
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        Debug.Assert(Name != null);
        Debug.Assert(Doc != null);
        Debug.Assert(Type.IsValid);
    }
}