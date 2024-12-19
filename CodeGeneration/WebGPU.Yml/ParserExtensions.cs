using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace WebGPU.Yml;

internal static class ParserExtensions
{
    public static List<T> ParseSequence<T>(this IParser parser, Func<IParser, T> mapper)
    {
        List<T> result = new();
        parser.Consume<SequenceStart>();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            result.Add(mapper(parser));
        }
        return result;
    }

    public static string ParseScalar(this IParser parser)
    {
        return parser.Consume<Scalar>().Value;
    }
}