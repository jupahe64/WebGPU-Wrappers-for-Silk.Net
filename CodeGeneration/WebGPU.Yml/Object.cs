using System.Diagnostics;
using WebGPU.Yml.Scalars;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

public class Object
{
    /// <summary>
    /// Name of the object
    /// </summary>
    public string Name { get; init; }
    public string Doc { get; init; }
    /// <summary>
    /// Optional property, an indicator that this object is an extension of an already present object
    /// </summary>
    public bool? Extended { get; init; }
    /// <summary>
    /// Optional property, specifying the external namespace where this object is defined
    /// </summary>
    public string? Namespace { get; init; }
    public List<Function>? Methods { get; init; }
    
    internal Object(IParser parser)
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
                case "namespace":
                    Namespace = parser.ParseScalar();
                    break;
                case "methods":
                    Methods = parser.ParseSequence(p=>new Function(p));
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