using System.Diagnostics;
using WebGPU.Yml.Scalars.Unions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

public class BitFlagEntry
{
    /// <summary>
    /// Name of the bitflag entry
    /// </summary>
    public string Name { get; init; }
    public string Doc { get; init; }
    /// <summary>
    /// Optional property, a 64-bit unsigned integer
    /// </summary>
    public Value64? Value { get; init; }
    /// <summary>
    /// Optional property, an array listing the names of bitflag entries to be OR-ed
    /// </summary>
    public List<string>? ValueCombination { get; init; }
    internal BitFlagEntry(IParser parser)
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
                case "value":
                    Value = new Value64(parser.ParseScalar());
                    break;
                case "value_combination":
                    ValueCombination = parser.ParseSequence(ParserExtensions.ParseScalar);
                    break;
                default:
                    Debugger.Break();
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        Debug.Assert(Name != null);
        Debug.Assert(Doc != null);
    }
}