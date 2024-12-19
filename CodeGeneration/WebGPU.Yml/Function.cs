using System.Diagnostics;
using WebGPU.Yml.Scalars;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

public class Function
{
    /// <summary>
    /// Name of the function
    /// </summary>
    public string Name { get; init; }
    public string Doc { get; init; }
    /// <summary>
    /// Optional property, return type of the function
    /// </summary>
    public ReturnType? Returns { get; init; }
    /// <summary>
    /// Optional property, list of async callback arguments
    /// </summary>
    public IReadOnlyList<ParameterType>? ReturnsAsync;
    /// <summary>
    /// Optional property, list of function arguments
    /// </summary>
    public IReadOnlyList<ParameterType>? Args;

    internal Function(IParser parser)
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
                case "returns":
                    Returns = new ReturnType(parser);
                    break;
                case "returns_async":
                    ReturnsAsync = parser.ParseSequence(p=>new ParameterType(p));
                    break;
                case "args":
                    Args = parser.ParseSequence(p=>new ParameterType(p));
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