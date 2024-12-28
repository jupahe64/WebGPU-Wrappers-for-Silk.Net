using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TemplatingLibrary.TemplateLoading;

public class ValidationError(int line, int column, string message) : Exception
{
    public int Line => line;
    public int Column => column;
    public override string Message => message;
    public override string ToString()
    {
        return $"Validation error: {line+1}:{column+1}: {message}";
    }
}

public class TemplateLoader
{
    /// <summary>
    /// Parses, validates and loads a c# file/snippet
    /// (defined by <paramref name="templateText"/> and <paramref name="range"/>)
    /// that has annotated TEMPLATE regions
    /// </summary>
    /// <returns>A <see cref="LoadedTemplate"/> that's ready for templating</returns>
    /// <exception cref="LexerError">Tokenizing failed</exception>
    /// <exception cref="ParserError">Invalid syntax used</exception>
    /// <exception cref="ValidationError">Syntax is used incorrectly or file/snippet has inconsistencies</exception>
    public static LoadedTemplate Load(string templateText, Range range)
    {
        var templateLoader = new TemplateLoader(templateText, range, hasNamedTemplates: false);
        templateLoader.Load();
        Debug.Assert(templateLoader._loadedTemplates.Count == 1);
        Debug.Assert(templateLoader._loadedTemplates.Keys.Single() == string.Empty);
        return templateLoader._loadedTemplates.Values.Single();
    }
    
    /// <summary>
    /// Parses, validates and loads a c# file/snippet
    /// (defined by <paramref name="templateText"/> and <paramref name="range"/>)
    /// that has annotated TEMPLATE regions enclosed in TEMPLATE DEFINE regions
    /// </summary>
    /// <returns>A mapping from name to <see cref="LoadedTemplate"/> that's ready for templating</returns>
    /// <exception cref="LexerError">Tokenizing failed</exception>
    /// <exception cref="ParserError">Invalid syntax used</exception>
    /// <exception cref="ValidationError">Syntax is used incorrectly or file/snippet has inconsistencies</exception>
    public static Dictionary<string, LoadedTemplate> LoadAllDefined(string templateText, Range range)
    {
        var templateLoader = new TemplateLoader(templateText, range, hasNamedTemplates: true);
        templateLoader.Load();
        Debug.Assert(templateLoader._loadedTemplates.Keys.All(x=>x!=string.Empty));
        return templateLoader._loadedTemplates;
    }

    #region Input
    private readonly string _text;
    private readonly bool _hasNamedTemplates;
    private readonly int _begin;
    private readonly int _end;
    #endregion
    
    #region Output
    private List<TemplateTextRange> _ranges = [];
    private List<RegionMarker> _directiveRegionMarkers = [];
    private readonly Dictionary<string, LoadedTemplate> _loadedTemplates = [];
    #endregion
    
    #region Variables
    private readonly Stack<(Regex regex, List<(int rangeRef, int replacementIdx)> replaceRanges)> _replaceRegionStack = new ();
    private readonly Stack<IReadOnlyList<DirectivesParser.IParsedDirective>> _directivesPerRegionStack = [];
    private string? _currentTemplateName = null;
    private bool _isInTemplateRegion = false;
    private int _currentTemplateIndentation = 0;
    private int _directiveNestingLevel = 0;
    private int _replaceRangesCount = 0;
    private bool _isInInsertRegion = false;
    #endregion

    private TemplateLoader(string text, Range range, bool hasNamedTemplates)
    {
        _text = text;
        _hasNamedTemplates = hasNamedTemplates;
        _begin = range.Start.GetOffset(text.Length);
        _end = range.End.GetOffset(text.Length);
    }

    private void Load()
    {
        var lines = TemplateLexer.TokenizeLines(_text, _begin, _end);

        Dictionary<int, List<DirectivesParser.IParsedDirective>> parsedDirectivesPerLine = [];
        for (var lineNumber = 0; lineNumber < lines.Count; lineNumber++)
        {
            var line = lines[lineNumber];
            if (!line.IsBeginRegionWithDirectives(out var directives))
                continue;
                
            var list = DirectivesParser.Parse(_text, lineNumber, line.Begin, directives.Value.tokens);
            
            if (list.Count > 0)
                parsedDirectivesPerLine[lineNumber] = list;
        }
        
        if (!_hasNamedTemplates)
            BeginTemplate(indentation: 0, name: null);
        
        for (var lineNumber = 0; lineNumber < lines.Count; lineNumber++)
        {
            bool isLastLine = lineNumber == lines.Count - 1;
            var line = lines[lineNumber];
            
            if (line.IsEmptyLine())
            {
                if (!isLastLine && _isInTemplateRegion)
                {
                    _ranges.Add(new TemplateTextRange(line.Begin, line.End, 
                        indentation: null, newLine: true));
                }
            }
            else if (line.IsRegularLine())
            {
                HandleRegularLine(lineNumber, line, isLastLine);
            }
            else if (line.IsBeginRegion())
            {
                _directivesPerRegionStack.Push([]);
                HandleRegularLine(lineNumber, line, isLastLine);
            }
            else if (line.IsBeginRegionWithDirectives(out _))
            {
                Debug.Assert(line.Indentation != -1);
                if (parsedDirectivesPerLine.TryGetValue(lineNumber, out var parsedDirectives))
                {
                    _directivesPerRegionStack.Push(parsedDirectives);
                    foreach (var directive in parsedDirectives)
                    {
                        EnterDirectiveRegion(line.Indentation, directive);
                        _directiveNestingLevel++;
                    }
                }
                else
                    _directivesPerRegionStack.Push([]);
            }
            else if (line.IsEndRegion())
            {
                var directives = _directivesPerRegionStack.Pop();
                
                if (directives.Count == 0)
                    HandleRegularLine(lineNumber, line, isLastLine);
                
                foreach (var directive in directives)
                {
                    _directiveNestingLevel--;
                    ExitDirectiveRegion(directive);
                }
            }
            else
            {
                Debug.Fail("Invalid tokenized line");
            }
        }

        if (_hasNamedTemplates) 
            return;
        
        Debug.Assert(_currentTemplateName == null);
        EndTemplate();
    }

    private void HandleRegularLine(int lineNumber, TokenizedLine line, bool isLastLine)
    {
        if (!_isInTemplateRegion || _isInInsertRegion)
            return;
        
        Debug.Assert(line.Indentation != -1);
        int inTemplateIndentation = line.Indentation - _currentTemplateIndentation;
        
        if (inTemplateIndentation < 0)
            throw new ValidationError(lineNumber, line.Indentation, 
                "Line indentation is smaller than the" +
                " defined template regions indentation");
        
        var isFirstRange = true;
        int previousMatchEnd = line.Begin + line.Indentation;
        (Match match, List<(int rangeRef, int replacementIdx)> replaceRanges)? earliestMatch;
        do
        {
            earliestMatch = null;
            var earliestIndex = int.MaxValue;
            foreach (var (regex, rangesRef) in _replaceRegionStack)
            {
                var match = regex.Match(_text, previousMatchEnd, line.End - previousMatchEnd);
                if (!(match.Success && match.Index < earliestIndex))
                    continue;
                
                earliestIndex = match.Index;
                earliestMatch = (match, replaceRanges: rangesRef);
            }

            if (earliestMatch == null)
                continue;
            
            int matchBegin = earliestMatch.Value.match.Index;
            int matchEnd = matchBegin + earliestMatch.Value.match.Length;

            if (previousMatchEnd < matchBegin)
            {
                _ranges.Add(new TemplateTextRange(previousMatchEnd, matchBegin, 
                    indentation: isFirstRange ? inTemplateIndentation : null, 
                    newLine: false));
                Debug.Assert(!_text.AsSpan(_ranges[^1].Begin.._ranges[^1].End).Contains('\n'));
                
                isFirstRange = false;
            }

            var replaceMatchRange = new TemplateTextRange(
                matchBegin, matchEnd,
                indentation: isFirstRange ? inTemplateIndentation : null,
                newLine: !isLastLine && matchEnd == line.End,
                replaceMatch: earliestMatch.Value.match);
            
            earliestMatch.Value.replaceRanges.Add((_ranges.Count, _replaceRangesCount++));
            _ranges.Add(replaceMatchRange);
            Debug.Assert(!_text.AsSpan(_ranges[^1].Begin.._ranges[^1].End).Contains('\n'));
            isFirstRange = false;
            
            previousMatchEnd = matchEnd;
            
        } while (earliestMatch != null);

        if (previousMatchEnd >= line.End) 
            return;
        
        _ranges.Add(new TemplateTextRange(previousMatchEnd, line.End, 
            indentation: isFirstRange ? inTemplateIndentation : null, newLine: !isLastLine));
            
        Debug.Assert(!_text.AsSpan(_ranges[^1].Begin.._ranges[^1].End).Contains('\n'));
    }

    private void EnterDirectiveRegion(int indentation, DirectivesParser.IParsedDirective directive)
    {
        bool expectsDefine = _hasNamedTemplates && _directiveNestingLevel == 0;
        if (directive is DirectivesParser.DefineDirective defineDirective)
        {
            if (!expectsDefine)
                throw new ValidationError(defineDirective.Line, defineDirective.Column,
                    "Can't define sub template");
                            
            BeginTemplate(indentation, defineDirective.Name);
            return;
        }
        
        if (expectsDefine)
            throw new ValidationError(directive.Line, directive.Column, "Expected define directive");
        
        if (_isInInsertRegion)
            throw new ValidationError(directive.Line, directive.Column, "Can't have any regions inside an" +
                                                                        " insert region");
                        
        switch (directive)
        {
            case DirectivesParser.ForeachDirective d:
                _directiveRegionMarkers.Add(
                    RegionMarker.CreateBeginMarker(_ranges.Count, _replaceRangesCount, 
                        new ForeachLoopRegion(d.VariableName, d.CollectionName))
                );
                break;
            case DirectivesParser.ReplaceDirective d:
                List<(int rangeRef, int replacementIdx)> ranges = [];
                _directiveRegionMarkers.Add(
                    RegionMarker.CreateBeginMarker(_ranges.Count, _replaceRangesCount, 
                        new ReplaceRegion(d.VariableName, ranges))
                );
                var regex = new Regex(d.Pattern, RegexOptions.Compiled);
                _replaceRegionStack.Push((regex, rangesRef: ranges));
                break;
            case DirectivesParser.InsertDirective d:
                _directiveRegionMarkers.Add(
                    RegionMarker.CreateBeginMarker(_ranges.Count, _replaceRangesCount, 
                        new InsertRegion(indentation, d.VariableName))
                );
                _isInInsertRegion = true;
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void ExitDirectiveRegion(DirectivesParser.IParsedDirective directive)
    {
        if (directive is DirectivesParser.ReplaceDirective)
            _replaceRegionStack.Pop();
        
        _directiveRegionMarkers.Add(RegionMarker.CreateEndMarker(_ranges.Count, _replaceRangesCount));

        switch (directive)
        {
            case DirectivesParser.DefineDirective:
                EndTemplate();
                break;
            case DirectivesParser.InsertDirective:
                _isInInsertRegion = false;
                break;
        }
    }
    
    private void BeginTemplate(int indentation, string? name)
    {
        Debug.Assert(!_isInTemplateRegion);
        Debug.Assert(_directiveNestingLevel == 0);

        _isInTemplateRegion = true;
        _currentTemplateName = name;
        _currentTemplateIndentation = indentation;
    }

    private void EndTemplate()
    {
        Debug.Assert(_isInTemplateRegion);
        Debug.Assert(_directiveNestingLevel == 0);
        Debug.Assert(_directiveNestingLevel == 0);
        
        _loadedTemplates[_currentTemplateName ?? string.Empty] =
            new LoadedTemplate(_text, _directiveRegionMarkers, _ranges, _replaceRangesCount);

        _isInTemplateRegion = false;
        _currentTemplateName = null;
        _directiveRegionMarkers = [];
        _ranges = [];
        _replaceRangesCount = 0;
    }
}