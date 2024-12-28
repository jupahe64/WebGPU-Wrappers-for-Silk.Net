namespace TemplatingLibrary.TemplateLoading;

public class ParserError(int line, int column, string message) : Exception
{
    public int Line => line;
    public int Column => column;
    public override string Message => message;
    public override string ToString()
    {
        return $"Parser error: {line+1}:{column+1}: {message}";
    }
}

internal static class DirectivesParser
{
    public interface IParsedDirective
    {
        public int Line { get; }
        public int Column { get; }
    }
    public record DefineDirective(int Line, int Column, string Name)
        : IParsedDirective;
    public record ReplaceDirective(int Line, int Column, string Pattern, string VariableName, ReplaceFlags Flags)
        : IParsedDirective;
    public record ForeachDirective(int Line, int Column, string VariableName, string CollectionName)
        : IParsedDirective;
    
    public record InsertDirective(int Line, int Column, string VariableName)
        : IParsedDirective;
    
    public static List<IParsedDirective> Parse(string text, int lineNumber, int lineBegin, IReadOnlyList<Token> tokens)
    {
        List<IParsedDirective> parsedDirectives = [];
        var tokenIdx = 0;
        while (tokens[tokenIdx].Type != TokenType.EOL)
        {
            var directiveNameToken = ConsumeToken(TokenType.Identifier);
            int column = directiveNameToken.Column;
            var directiveName = GetValue(directiveNameToken);
            Token accessorToken;
            String accessor;
            switch (directiveName)
            {
                case "DEFINE":
                    ConsumeToken(TokenType.OpenParens);
                    var nameToken = ConsumeToken(TokenType.String);
                    ConsumeToken(TokenType.CloseParens);
                    
                    var name = GetValue(nameToken)[1..^1].ToString();
                    parsedDirectives.Add(new DefineDirective(lineNumber, column, name));
                    break;
                case "REPLACE":
                    ConsumeToken(TokenType.OpenParens);
                    var patternToken = ConsumeToken(TokenType.StringWithBackticks);
                    ConsumeToken(TokenType.Comma);
                    accessorToken = ConsumeToken(TokenType.FieldAccessor);

                    var flags = ReplaceFlags.None;
                    while (tokens[tokenIdx].Type != TokenType.CloseParens)
                    {
                        ConsumeToken(TokenType.Comma);
                        var flagToken = ConsumeToken(TokenType.Identifier);
                        var flagName = GetValue(flagToken);
                        flags |= GetValue(flagToken).ToString() switch
                        {
                            "REMOVE_IF_NULL" => ReplaceFlags.RemoveIfNull,
                            _ => throw new ParserError(lineNumber, flagToken.Begin-lineBegin,
                                $"Unknown replace flag {flagName}")
                        };
                    }
                    ConsumeToken(TokenType.CloseParens);
                    
                    var pattern = GetValue(patternToken)[1..^1].ToString();
                    accessor = GetValue(accessorToken).ToString();
                    parsedDirectives.Add(new ReplaceDirective(lineNumber, column, pattern, accessor, flags));
                    break;
                case "FOREACH":
                    ConsumeToken(TokenType.OpenParens);
                    var varAccessorToken = ConsumeToken(TokenType.FieldAccessor);
                    ConsumeToken(TokenType.Colon);
                    var collectionAccessorToken = ConsumeToken(TokenType.FieldAccessor);
                    ConsumeToken(TokenType.CloseParens);
                    
                    var varAccessor = GetValue(varAccessorToken).ToString();
                    var collectionAccessor = GetValue(collectionAccessorToken).ToString();
                    parsedDirectives.Add(new ForeachDirective(lineNumber, column, varAccessor, collectionAccessor));
                    break;
                case "INSERT":
                    ConsumeToken(TokenType.OpenParens);
                    accessorToken = ConsumeToken(TokenType.FieldAccessor);
                    ConsumeToken(TokenType.CloseParens);
                    
                    accessor = GetValue(accessorToken).ToString();
                    parsedDirectives.Add(new InsertDirective(lineNumber, column, accessor));
                    break;
                default:
                    throw new ParserError(lineNumber, column, $"Unknown directive {directiveName}");
            }
        }
        
        return parsedDirectives;

        ReadOnlySpan<char> GetValue(Token token)
        {
            return text.AsSpan(token.Begin..token.End);
        }

        Token ConsumeToken(TokenType expectedType)
        {
            var token = tokens[tokenIdx];
            if (token.Type == expectedType)
            {
                tokenIdx++;
                return token;
            }
                
            int column = token.Begin - lineBegin;
            string expected = DirectivesLexer.TokenErrDisplayString(expectedType);
            string actual = DirectivesLexer.TokenErrDisplayString(token.Type);
            throw new ParserError(lineNumber, column, $"expected {expected} got {actual}");
        }
    }
}