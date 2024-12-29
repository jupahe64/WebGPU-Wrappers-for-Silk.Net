using System.Diagnostics;

namespace TemplatingLibrary.TemplateParameters.Builder;

internal class ParameterizedTemplateBuilderContext
{
    private const string MissingReplacementString = "<MISSING REPLACEMENT>";
    
    public LoadedTemplate Template { get; }
    
    public ParameterizedTemplateBuilderContext(LoadedTemplate template)
    {
        Template = template;
        _replacementSlots = new string[template.ReplacementCount];
    }

    public void EnterForeachLoop(FieldAccessor collectionAccessor, out string variableName)
    {
        var marker = Template.RegionMarkers[_regionMarkerIdx];
        if (!marker.IsBeginMarker(out int rangePtr, out int replacementPtr, out ForeachLoopRegion? foreachMarker))
            throw new InvalidOperationException("No Foreach Loop to begin");
            
        if (!collectionAccessor.IsEquivalent(foreachMarker.CollectionName))
            throw new ArgumentException("Collection name mismatch");
        
        variableName = foreachMarker.VariableName[1..]; //strip $
            
        _regionStack.Push((_regionMarkerIdx, marker));
        Advance(rangePtr, replacementPtr);
    }

    public void ExitForeachLoop()
    {
        Debug.Assert(_regionStack.Peek().beginMarker
            .IsBeginMarker(out _, out _, out ForeachLoopRegion? _));
        
        if (!Template.RegionMarkers[_regionMarkerIdx]
                .IsEndMarker(out int rangePtr, out int replacementPtr))
            throw new InvalidOperationException("End of foreach loop region not reached yet");

        _regionStack.Pop();
        
        Advance(rangePtr, replacementPtr);
    }

    public void NewLoopIteration()
    {
        if (!Template.RegionMarkers[_regionMarkerIdx].IsEndMarker(out _, out _))
            throw new InvalidOperationException("End of foreach loop region not reached yet");

        (int beginMarkerIdx, var beginMarker) = _regionStack.Peek();
        Debug.Assert(beginMarker.IsBeginMarker(out int rangesBeginPtr, out int replacementBeginPtr,
            out ForeachLoopRegion? _));
        
        _instructions.Add((_writtenRangesCount, 
            new ParameterizedTemplate.SetRangePointer(rangesBeginPtr)));
        
        _rangePtr = rangesBeginPtr;
        _replacementPtr = replacementBeginPtr;
        
        _regionMarkerIdx = beginMarkerIdx + 1;
    }

    public void SkipBody()
    {
        Debug.Assert(_regionStack.Peek().beginMarker
            .IsBeginMarker(out int rangesBeginPtr, out int replacementsBeginPtr, out ForeachLoopRegion? _));
        
        //must be called right at the beginning of foreach loop
        Debug.Assert(_rangePtr == rangesBeginPtr);
        Debug.Assert(_replacementPtr == replacementsBeginPtr);

        var nestingLevel = 1;
        do
        {
            if (Template.RegionMarkers[_regionMarkerIdx].IsEndMarker(out _, out _))
                nestingLevel--;
            else
                nestingLevel++;

            _regionMarkerIdx++;
        } while(nestingLevel > 0);

        _regionMarkerIdx--;
        Debug.Assert(Template.RegionMarkers[_regionMarkerIdx].IsEndMarker(
            out int rangesEndPtr, out int replacementsEndPtr));
        
        _instructions.Add((_writtenRangesCount, new ParameterizedTemplate.SetRangePointer(rangesEndPtr)));
        _rangePtr = rangesEndPtr;
        _replacementPtr = replacementsEndPtr;
    }

    public void EnterReplaceRegion(FieldAccessor variableAccessor, 
        out IReadOnlyList<(int rangeRef, int replacementSlot)> replaceRanges)
    {
        var marker = Template.RegionMarkers[_regionMarkerIdx];
        if (!marker.IsBeginMarker(out int rangePtr, out int replacementPtr, out ReplaceRegion? replaceRegion))
            throw new InvalidOperationException("No Replace region to enter");
        
        if (!variableAccessor.IsEquivalent(replaceRegion.VariableName))
            throw new ArgumentException("Variable name mismatch");
        
        replaceRanges = replaceRegion.Ranges;
        _regionStack.Push((_regionMarkerIdx, marker));
        Advance(rangePtr, replacementPtr);
    }
    
    public void ExitReplaceRegion()
    {
        Debug.Assert(_regionStack.Peek().beginMarker
            .IsBeginMarker(out _, out _, out ReplaceRegion? _));
        
        if (!Template.RegionMarkers[_regionMarkerIdx]
            .IsEndMarker(out int rangePtr, out int replacementPtr))
            throw new InvalidOperationException("End of replace region not reached yet");

        _regionStack.Pop();

        Advance(rangePtr, replacementPtr);
    }
    
    public void EnterInsertRegion(FieldAccessor variableAccessor)
    {
        var marker = Template.RegionMarkers[_regionMarkerIdx];
        if (!marker.IsBeginMarker(out int rangePtr, out int replacementPtr, out InsertRegion? replaceRegion))
            throw new InvalidOperationException("No Replace region to enter");
        
        if (!variableAccessor.IsEquivalent(replaceRegion.VariableName))
            throw new ArgumentException("Variable name mismatch");
        
        _regionStack.Push((_regionMarkerIdx, marker));
        Advance(rangePtr, replacementPtr);
    }
    
    public void ExitInsertRegion()
    {
        Debug.Assert(Template.RegionMarkers[_regionMarkerIdx]
            .IsEndMarker(out int rangePtr, out int replacementPtr));

        Debug.Assert(_regionStack.Pop().beginMarker
            .IsBeginMarker(out _, out _, out InsertRegion? _));
        Advance(rangePtr, replacementPtr);
    }

    public void SetReplacement(int replacementSlot, string replacement)
    {
        Debug.Assert(_replacementPtr <= replacementSlot && replacementSlot < _replacementSlots.Length);
        _replacementSlots[replacementSlot] = replacement;
    }

    public void Insert(ParameterizedTemplate template)
    {
        Debug.Assert(_regionStack.Peek().beginMarker.IsBeginMarker(out int rangePtr, out int replacementPtr, 
            out InsertRegion? insertRegion));
        Debug.Assert(rangePtr == _rangePtr);
        Debug.Assert(replacementPtr == _replacementPtr);
        _instructions.Add((_writtenRangesCount, 
            new ParameterizedTemplate.InsertTemplate(template, insertRegion.Indentation)));
    }
    
    public void Insert(string str)
    {
        Debug.Assert(_regionStack.Peek().beginMarker.IsBeginMarker(out int rangePtr, 
            out _, out InsertRegion? insertRegion));
        Debug.Assert(rangePtr == _rangePtr);
        _instructions.Add((_writtenRangesCount, 
            new ParameterizedTemplate.InsertString(str, insertRegion.Indentation)));
    }

    public ParameterizedTemplate CreateTemplate()
    {
        return new ParameterizedTemplate(Template, _replacements, _instructions, _writtenRangesCount);
    }

    private void Advance(int newRangePointer, int newReplacementPtr)
    {
        Debug.Assert(newRangePointer >= _rangePtr);
        Debug.Assert(newReplacementPtr >= _replacementPtr);
        
        _writtenRangesCount += newRangePointer - _rangePtr;
        
        Debug.Assert(!_replacementSlots.AsSpan(_replacementPtr..newReplacementPtr)
            .Contains(MissingReplacementString));
        
        _replacements.AddRange(_replacementSlots.AsSpan(_replacementPtr..newReplacementPtr));
        
        _rangePtr = newRangePointer;
        _replacementPtr = newReplacementPtr;
        _regionMarkerIdx++;
    }
    
    #region Output
    private readonly List<string> _replacements = [];
    private readonly List<(int writtenRangesCount, ParameterizedTemplate.IInstruction instruction)> _instructions = [];
    private int _writtenRangesCount = 0;
    private int _rangePtr = 0;
    private int _replacementPtr = 0;
    #endregion
    
    #region Variables

    private string[] _replacementSlots;
    private int _regionMarkerIdx = 0;
    private readonly Stack<(int beginMarkerIdx, RegionMarker beginMarker)> _regionStack = new();
    private readonly Stack<(int writtenReplacementsCount, int regionStackPtr)> _foreachStack = new();
    #endregion
}