using System.Diagnostics.CodeAnalysis;

namespace TemplatingLibrary;

internal record ReplaceRegion(string VariableName, List<(int rangeRef, int replacementIdx)> Ranges);
internal record ForeachLoopRegion(string VariableName, string CollectionName);
internal record InsertRegion(int Indentation, string VariableName);

internal class RegionMarker
{
    public static RegionMarker CreateBeginMarker(int textRangesBeginPtr, int replacementPtr, 
        ReplaceRegion replaceRegion) 
        => new(textRangesBeginPtr, replacementPtr, replaceRegion);

    public static RegionMarker CreateBeginMarker(int textRangesBeginPtr, int replacementPtr, 
        ForeachLoopRegion foreachLoopRegion) 
        => new(textRangesBeginPtr, replacementPtr, foreachLoopRegion);
    
    public static RegionMarker CreateBeginMarker(int textRangesBeginPtr, int replacementPtr, 
        InsertRegion insertRegion) 
        => new(textRangesBeginPtr, replacementPtr, insertRegion);
    
    public static RegionMarker CreateEndMarker(int textRangesEndPtr, int replacementPtr) 
        => new(textRangesEndPtr, replacementPtr, null);

    public bool IsBeginMarker(out int textRangesBeginPtr, out int replacementsBeginPtr,
        [NotNullWhen(true)] out ReplaceRegion? replaceRegion)
        => _IsBeginMarker(out textRangesBeginPtr, out replacementsBeginPtr, out replaceRegion);
    
    public bool IsBeginMarker(out int textRangesBeginPtr, out int replacementsBeginPtr,
        [NotNullWhen(true)] out ForeachLoopRegion? loopRegion)
        => _IsBeginMarker(out textRangesBeginPtr, out replacementsBeginPtr, out loopRegion);
    
    public bool IsBeginMarker(out int textRangesBeginPtr, out int replacementsBeginPtr,
        [NotNullWhen(true)] out InsertRegion? insertRegion)
        => _IsBeginMarker(out textRangesBeginPtr, out replacementsBeginPtr, out insertRegion);
    
    public bool IsEndMarker(out int textRangesEndPtr, out int replacementsEndPtr)
    {
        if (_region == null)
        {
            textRangesEndPtr = _rangePtr;
            replacementsEndPtr = _replacementPtr;
            return true;
        }

        textRangesEndPtr = -1;
        replacementsEndPtr = -1;
        return _region == null;
    }

    private bool _IsBeginMarker<T>(out int rangePtr, out int replacementPtr, 
        [NotNullWhen(true)] out T? result)
        where T : class
    {
        if (_region is T region)
        {
            rangePtr = _rangePtr;
            replacementPtr = _replacementPtr;
            result = region;
            return true;
        }

        rangePtr = -1;
        replacementPtr = -1;
        result = null;
        return false;
    }
    
    private readonly object? _region;
    private readonly int _rangePtr;
    private readonly int _replacementPtr;

    private RegionMarker(int rangePtr, int replacementPtr, object? region)
    {
        _region = region;
        _rangePtr = rangePtr;
        _replacementPtr = replacementPtr;
    }
};