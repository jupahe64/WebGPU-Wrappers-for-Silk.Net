using System.Diagnostics;
using WebGPU.Yml.Scalars;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

using _Type = Scalars.Unions.Type;

public class ReturnType
{
    public string Doc { get; init; }
    /// <summary>
    /// Return type of the function
    /// </summary>
    public _Type Type { get; init; }
    /// <summary>
    /// Optional property, specifies if a method return type is a pointer
    /// </summary>
    public Pointer? Pointer { get; init; }

    internal ReturnType(IParser parser)
    {
        parser.Consume<MappingStart>();

        while (!parser.TryConsume<MappingEnd>(out _))
        {
            string key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "doc":
                    Doc = parser.ParseScalar();
                    break;
                case "type":
                    Type = new _Type(parser.ParseScalar());
                    break;
                case "pointer":
                    Pointer = Simple.ParsePointer(parser.ParseScalar());
                    break;
                default:
                    Debugger.Break();
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        Debug.Assert(Doc != null);
        Debug.Assert(Type.IsValid);
    }
}