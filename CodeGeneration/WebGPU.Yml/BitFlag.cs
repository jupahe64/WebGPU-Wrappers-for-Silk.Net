using System.Diagnostics;
using WebGPU.Yml.Scalars;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

public class BitFlag
{
    /// <summary>
    /// Name of the bitflag
    /// </summary>
    public string Name { get; init; }
    public string Doc { get; init; }
    /// <summary>
    /// Optional property, an indicator that this bitflag is an extension of an already present bitflag
    /// </summary>
    public bool? Extended { get; init; }
    public List<BitFlagEntry>? Entries { get; init; } = new();

    internal BitFlag(IParser parser)
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
                case "extended":
                    Extended = Simple.ParseBool(parser.ParseScalar());
                    break;
                case "entries":
                    Entries = parser.ParseSequence(p=>new BitFlagEntry(p));
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