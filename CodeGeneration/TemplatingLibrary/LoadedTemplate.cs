using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TemplatingLibrary.TemplateLoading;

namespace TemplatingLibrary;

public class LoadedTemplate
{
    internal IReadOnlyList<RegionMarker> RegionMarkers { get; }

    internal IReadOnlyList<TemplateTextRange> TextRanges { get; }
    internal string Text { get; }
    internal int ReplacementCount { get; }

    internal LoadedTemplate(string text, IReadOnlyList<RegionMarker> regionMarkers,
        IReadOnlyList<TemplateTextRange> textRanges, int replacementCount)
    {
        Text = text;
        RegionMarkers = regionMarkers;
        TextRanges = textRanges;
        ReplacementCount = replacementCount;
    }

    public void DebugPrint()
    {
        var replacementPtr = 0;
        var replacements = new string[ReplacementCount];

        foreach (var marker in RegionMarkers)
        {
            if (!marker.IsBeginMarker(out _, out _, out ReplaceRegion? region)) 
                continue;
            
            foreach ((int _, int idx) in region.Ranges)
            {
                replacements[idx] = region.VariableName;
            }
        }
        
        var sb = new StringBuilder();
        foreach (var textRange in TextRanges)
        {
            bool isReplaceRange = textRange.IsReplaceMatch(out _);
            
            if (textRange.Indentation.HasValue)
                sb.Append(' ', textRange.Indentation.Value);

            if (isReplaceRange)
            {
                sb.Append('[');
                sb.Append(replacements[replacementPtr++]);
                sb.Append(']');
            }
            else
                sb.Append(Text.AsSpan(textRange.Begin..textRange.End));
            
            if (textRange.NewLine)
                sb.Append(Environment.NewLine);
        }
        Console.WriteLine(sb.ToString());
    }
}