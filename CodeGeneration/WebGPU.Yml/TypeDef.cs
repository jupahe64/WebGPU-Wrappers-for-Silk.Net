using System.Diagnostics;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

using _Type = Scalars.Unions.Type;

/// <summary>
/// An alias of a primitive type
/// </summary>
public class TypeDef
{
    public string Name { get; init; }
    public string Doc { get; init; }
    public _Type Type { get; init; }

    internal TypeDef(IParser parser)
    {
        parser.Consume<MappingStart>();

        while (!parser.TryConsume<MappingEnd>(out _))
        {
            string key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "name":
                    Name = parser.Consume<Scalar>().Value;
                    break;
                case "doc":
                    Doc = parser.Consume<Scalar>().Value;
                    break;
                case "type":
                    Type = _Type.Parse(parser.ParseScalar());
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