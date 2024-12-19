using System.Diagnostics;
using WebGPU.Yml.Scalars.Unions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

/// <summary>
/// An alias of a primitive type
/// </summary>
public class Constant
{
    /// <summary>
    /// Name of the constant variable/define
    /// </summary>
    public string Name { get; init; }
    /// <summary>
    /// An enum of predefined max constants or a 64-bit unsigned
    /// </summary>
    public Value64 Value { get; init; }
    public string Doc { get; init; }

    internal Constant(IParser parser)
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
                case "value":
                    Value = new Value64(parser.ParseScalar());
                    break;
                case "doc":
                    Doc = parser.ParseScalar();
                    break;
                default:
                    Debugger.Break();
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        Debug.Assert(Name != null);
        Debug.Assert(Value.IsValid);
        Debug.Assert(Doc != null);
    }
}