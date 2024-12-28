using System.Text.RegularExpressions;

namespace TemplatingLibrary.TemplateLoading;

public class LexerError(int line, int column, string message) : Exception
{
    public int Line => line;
    public int Column => column;
    public override string Message => message;
    public override string ToString()
    {
        return $"Lexer error: {line+1}:{column+1}: {message}";
    }
}

//code based on
//https://github.com/dotnet/roslyn-sdk/blob/main/samples/CSharp/SourceGenerators/SourceGeneratorSamples/MathsGenerator.cs

public enum TokenType
{
    None,
    Identifier,
    FieldAccessor,
    OpenParens,
    CloseParens,
    String,
    StringWithBackticks,
    Spaces,
    Comma,
    Colon,
    EOL
}

public struct Token
{
    public TokenType Type;
    public int Begin;
    public int End;
    public int Line;
    public int Column;
}

public static class DirectivesLexer
{
    public static string TokenErrDisplayString(TokenType tokenType) => tokenType switch
    {
        TokenType.Identifier => "Identifier",
        TokenType.FieldAccessor => "FieldAccessor",
        TokenType.OpenParens => "(",
        TokenType.CloseParens => ")",
        TokenType.String => "Quote delimited String",
        TokenType.StringWithBackticks => "Backtick delimited String",
        TokenType.Spaces => "Spaces",
        TokenType.Comma => ",",
        TokenType.Colon => ":",
        TokenType.EOL => "<EOL>",
        TokenType.None => throw new ArgumentException($"Bad token type {tokenType}"),
        _ => throw new ArgumentException($"Unknown token type {tokenType}")
    };
    
    static (TokenType, string)[] tokenStrings = {
        (TokenType.Spaces,              @"\s+"),
        (TokenType.Identifier,          "[_a-zA-Z][_a-zA-Z0-9]*"),
        (TokenType.FieldAccessor,       @"\$(?:[_a-zA-Z][_a-zA-Z0-9]*)?(?:\.[_a-zA-Z][_a-zA-Z0-9]*)*"),
        (TokenType.String,              "\"[^\"]*\""),
        (TokenType.StringWithBackticks, "`[^`]*`"),
        (TokenType.OpenParens,          @"\("),
        (TokenType.CloseParens,         @"\)"),
        (TokenType.Comma,               ","),
        (TokenType.Colon,               ":")
    };

    private static readonly IEnumerable<(TokenType, Regex)> s_tokenExpressions =
        tokenStrings
            .Select(
                t => (t.Item1, new Regex($"^{t.Item2}", 
                    RegexOptions.Compiled | RegexOptions.Singleline)))
            .ToList();

    public static IReadOnlyList<Token> Tokenize(string source, int begin, int end, int line, int column)
    {
        List<Token> tokens = [];

        while (begin < end)
        {
            var matchIndex = 0;
            var matchLength = 0;
            var tokenType = TokenType.None;

            foreach (var (type, rule) in s_tokenExpressions)
            {
                var match = rule.Match(source, begin, end-begin);
                if (!match.Success) continue;
                
                matchIndex = match.Index;
                matchLength = match.Length;
                tokenType = type;
                break;
            }

            if (matchLength == 0)
            {
                throw new LexerError(line, column, $"Unrecognized symbol '{source[begin]}'");
            }

            if (tokenType != TokenType.Spaces)
                tokens.Add(new Token
                {
                    Type = tokenType,
                    Begin = matchIndex,
                    End = matchIndex + matchLength,
                    Line = line,
                    Column = column
                });

            column += matchLength;
            begin += matchLength;
        }

        tokens.Add(new Token
        {
            Type = TokenType.EOL,
            Line = line,
            Column = column
        });
        return tokens;
    }
}