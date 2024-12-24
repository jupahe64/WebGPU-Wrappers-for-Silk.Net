namespace TemplatingLibrary;

internal record ReplaceRegion(string VariableName, List<int> Ranges);
internal record LoopRegion(string VariableName, string CollectionName);

internal class RegionMarker
{
    public static RegionMarker CreateBeginMarker(int textRangesBeginPtr, ReplaceRegion replaceRegion) 
        => new(textRangesBeginPtr, replaceRegion);

    public static RegionMarker CreateBeginMarker(int textRangesBeginPtr, LoopRegion loopRegion) 
        => new(textRangesBeginPtr, loopRegion);
    
    public static RegionMarker CreateEndMarker(int textRangesEndPtr) 
        => new(textRangesEndPtr, null);

    public bool IsBeginMarker(out int textRangesBeginPtr, out ReplaceRegion? replaceRegion)
        => IsBeginMarker<ReplaceRegion>(out textRangesBeginPtr, out replaceRegion);
    
    public bool IsBeginMarker(out int textRangesBeginPtr, out LoopRegion? loopRegion)
        => IsBeginMarker<LoopRegion>(out textRangesBeginPtr, out loopRegion);
    
    public bool IsEndMarker(out int textRangesEndPtr)
    {
        if (_region == null)
        {
            textRangesEndPtr = _rangePtr;
            return true;
        }

        textRangesEndPtr = -1;
        return _region == null;
    }

    private bool IsBeginMarker<T>(out int rangePtr, out T? result)
        where T : class
    {
        if (_region is T region)
        {
            rangePtr = _rangePtr;
            result = region;
            return true;
        }

        rangePtr = -1;
        result = null;
        return false;
    }
    
    private readonly object? _region;
    private readonly int _rangePtr;

    private RegionMarker(int rangePtr, object? region)
    {
        _region = region;
        _rangePtr = rangePtr;
    }
};