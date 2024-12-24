using System.Text;
using TemplatingLibrary.TemplateLoading;

namespace TemplatingLibrary;

public class LoadedTemplate
{
    internal LoadedTemplate(string text, IReadOnlyList<RegionMarker> regionMarkers, 
        IReadOnlyList<TemplateTextRange> textRanges)
    {
        _text = text;
        _regionMarkers = regionMarkers;
        _textRanges = textRanges;
    }
    
    private readonly string _text;
    private readonly IReadOnlyList<RegionMarker> _regionMarkers;
    private readonly IReadOnlyList<TemplateTextRange> _textRanges;

    public void DebugPrint()
    {
        var sb = new StringBuilder();
        foreach (var textRange in _textRanges)
        {
            bool isReplaceRange = textRange.IsReplaceMatch(out _);
            
            if (textRange.Indentation.HasValue)
                sb.Append(' ', textRange.Indentation.Value);
            
            if (isReplaceRange)
                sb.Append('[');
            sb.Append(_text.AsSpan(textRange.Begin..textRange.End));
            if (isReplaceRange)
                sb.Append(']');
            
            if (textRange.NewLine)
                sb.Append(Environment.NewLine);
        }
        foreach (var regionMarker in _regionMarkers)
        {
            
            //if (regionMarker.IsBeginMarker(out int begin))
        }
        Console.WriteLine(sb.ToString());
    }
}