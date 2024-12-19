using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace WebGPU.Yml;

public class Document
{
    public RootNode RootNode { get; init; }
    private Document(IParser parser)
    {
        parser.Consume<DocumentStart>();

        while (!parser.TryConsume<DocumentEnd>(out _))
        {
            Debug.Assert(RootNode == null);
            RootNode = new RootNode(parser);
        }
    }

    public static bool TryLoad(TextReader reader, [NotNullWhen(true)] out Document? document)
    {
        var parser = new Parser(reader);
    
        parser.Consume<StreamStart>();

        document = null;
        while (!parser.TryConsume<StreamEnd>(out _))
        {
            Debug.Assert(document == null);
            document = new Document(parser);
        }
        
        return document != null;
    }
}