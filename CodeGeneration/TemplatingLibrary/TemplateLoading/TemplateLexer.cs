using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TemplatingLibrary.TemplateLoading;

internal readonly struct TokenizedLine
{
    public static TokenizedLine BeginRegion(int begin, int end, int indentation)
    {
        return new TokenizedLine(begin, -1, end, indentation, [], 
            Kind.BeginRegion);
    }
    public static TokenizedLine BeginRegionWithDirectives(int begin, int end, int indentation, 
        int directivesBegin, IReadOnlyList<Token> tokens)
    {
        return new TokenizedLine(begin, directivesBegin, end, indentation, tokens, 
            Kind.BeginRegionWithDirectives);
    }
    public static TokenizedLine EndRegion(int begin, int end, int indentation)
    {
        return new TokenizedLine(begin, -1, end, indentation, [], 
            Kind.EndRegion);
    }
    public static TokenizedLine RegularLine(int begin, int end, int indentation)
    {
        return new TokenizedLine(begin, -1, end, indentation, [], 
            Kind.RegularLine);
    }
    public static TokenizedLine EmptyLine(int begin, int end)
    {
        return new TokenizedLine(begin, -1, end, -1, [], 
            Kind.EmptyLine);
    }

    public bool IsBeginRegion() => _kind == Kind.BeginRegion;
    public bool IsEndRegion() => _kind == Kind.EndRegion;
    public bool IsRegularLine() => _kind == Kind.RegularLine;
    public bool IsEmptyLine() => _kind == Kind.EmptyLine;

    public bool IsBeginRegionWithDirectives(
        [NotNullWhen(true)] out (int begin, IReadOnlyList<Token> tokens)? directives)
    {
        if (_kind == Kind.BeginRegionWithDirectives)
        {
            directives = (_directivesBegin, _directiveTokens);
            return true;
        }

        directives = null;
        return false;
    }

    public int Begin { get; }

    public int End { get; }
    public int Indentation { get; }

    private readonly Kind _kind;
    private readonly int _directivesBegin;
    private readonly IReadOnlyList<Token> _directiveTokens;

    private enum Kind
    {
        Invalid,
        BeginRegion,
        BeginRegionWithDirectives,
        EndRegion,
        RegularLine,
        EmptyLine,
    };

    private TokenizedLine(int begin, int directivesBegin, int end, int indentation, 
        IReadOnlyList<Token> directiveTokens, Kind kind)
    {
        Begin = begin;
        _directivesBegin = directivesBegin;
        End = end;
        Indentation = indentation;
        _directiveTokens = directiveTokens;
        _kind = kind;
    }

    public override string ToString()
    {
        if (IsEmptyLine())
            return "Empty Line";
        if (IsRegularLine())
            return "Regular Line";
        if (IsBeginRegion())
            return "Begin Region";
        if (IsBeginRegionWithDirectives(out var directives))
            return $"Begin Region ({string.Join(' ',directives.Value.tokens.Select(x=>x.Type))})";
        if (IsEndRegion())
            return "End Region";
        
        return "Invalid Line";
    }
}

internal static partial class TemplateLexer
{
    [GeneratedRegex(@"^\s*#region( TEMPLATE )?(.*)$")]
    private static partial Regex RegionStartMarkerLineRegex();
    [GeneratedRegex(@"^\s*#endregion\s*$")]
    private static partial Regex RegionEndMarkerLineRegex();
    
    public static List<TokenizedLine> TokenizeLines(string text, int textBegin, int textEnd)
    {
        List<TokenizedLine> lines = [];
        int lineBegin = textBegin;
        var encounteredNonWhitespace = false;
        var indentation = 0;
        for (int i = textBegin; i < textEnd; i++)
        {
            switch (text[i])
            {
                case '\t':
                    int line = lines.Count;
                    int column = i - lineBegin;
                    throw new LexerError(line, column, "Template text cannot contain tab characters");
                case ' ' when !encounteredNonWhitespace:
                    indentation++;
                    break;
                case '\r' or '\n':
                    break; //ignore (it gets handled later)
                default:
                    encounteredNonWhitespace = true;
                    break;
            }

            if (text[i] != '\n')
                continue;

            int previousLineEnd = i;
            if (textBegin <= i-1 && text[i-1] == '\r')
                previousLineEnd--;
            
            HandleLine(previousLineEnd, !encounteredNonWhitespace);
            lineBegin = i+1;
            indentation = 0;
            encounteredNonWhitespace = false;
        }
        if (lineBegin < textEnd-1)
            HandleLine(textEnd, !encounteredNonWhitespace, true);
        
        return lines;

        void HandleLine(int lineEnd, bool isEmptyLine, bool isLastLine = false)
        {
            int lineLength = lineEnd - lineBegin;
            if (isEmptyLine)
            {
                lines.Add(TokenizedLine.EmptyLine(lineBegin, lineEnd));
                return;
            }
            string lineString = text.Substring(lineBegin, lineLength);
            Match match;
            if ((match = RegionStartMarkerLineRegex().Match(text, lineBegin, lineLength)).Success)
            {
                if (match.Groups[1].Success) //includes TEMPLATE keyword
                {
                    int directivesBegin = match.Groups[1].Index + match.Groups[1].Length;
                    int line = lines.Count;
                    int column = directivesBegin - lineBegin;
                    lines.Add(TokenizedLine.BeginRegionWithDirectives(lineBegin, lineEnd, indentation, directivesBegin, 
                        DirectivesLexer.Tokenize(text, directivesBegin, lineEnd, line, column)));
                }
                else
                {
                    lines.Add(TokenizedLine.BeginRegion(lineBegin, lineEnd, indentation));
                }
            }
            else if (RegionEndMarkerLineRegex().Match(text, lineBegin, lineLength).Success)
            {
                lines.Add(TokenizedLine.EndRegion(lineBegin, lineEnd, indentation));
            }
            else
            {
                lines.Add(TokenizedLine.RegularLine(lineBegin, lineEnd, indentation));
            }
        }
    }
}