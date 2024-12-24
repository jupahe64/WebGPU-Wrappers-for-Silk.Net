using System.Text.RegularExpressions;

namespace TemplatingLibrary;

internal class TemplateTextRange(int begin, int end, int? indentation, bool newLine, Match? replaceMatch = null)
{
    public int Begin => begin;
    public int End => end;
    public int? Indentation => indentation;
    public bool NewLine => newLine;

    public bool IsReplaceMatch(out Match? match)
    {
        match = replaceMatch;
        return replaceMatch != null;
    }

    public override string ToString()
    {
        if (IsReplaceMatch(out _))
            return $"Replace match [{Begin}..{End}]";
        else
            return $"Text [{Begin}..{End}]";
    }
}