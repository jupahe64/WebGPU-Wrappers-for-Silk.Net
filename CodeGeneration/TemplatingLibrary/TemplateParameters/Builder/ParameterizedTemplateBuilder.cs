using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TemplatingLibrary.TemplateParameters.Builder;

public class ForeachLoopBuilder : AbstractRegionBuilder<ForeachLoopBuilder>
{
    public ForeachLoopInfo Info { get; }
    internal bool HasIterated => _currentIteration >= 0;
    
    public ForeachLoopBuilder BeginIteration()
    {
        _currentIteration++;
        if (_currentIteration > 0)
            Context.NewLoopIteration();
        
        return this;
    }

    internal ForeachLoopBuilder(ParameterizedTemplateBuilderContext ctx,
        ForeachLoopInfo info) : base(ctx)
    {
        Info = info;
    }
    
    protected override void ThrowOnBadUsage()
    {
        base.ThrowOnBadUsage();
        
        if (_currentIteration == -1)
            throw new InvalidOperationException($"There is no current iteration. " + 
                                                $" Call {nameof(BeginIteration)} first.");
    }

    private int _currentIteration = -1;
}
public record struct ForeachLoopInfo(string VariableName);
    
public class ReplaceRegionBuilder : AbstractRegionBuilder<ReplaceRegionBuilder>
{
    public ReplaceRegionInfo Info { get; }

    internal ReplaceRegionBuilder(ParameterizedTemplateBuilderContext ctx, 
        ReplaceRegionInfo info) : base(ctx)
    {
        Info = info;
    }
}
public record struct ReplaceRegionInfo;

public class ParameterizedTemplateBuilder(LoadedTemplate template) 
    : AbstractRegionBuilder<ParameterizedTemplateBuilder>(new ParameterizedTemplateBuilderContext(template))
{
    public ParameterizedTemplate GetTemplate()
    {
        _Finalize();
        return Context.CreateTemplate();
    }
}

public delegate string ReplaceMatchAction(Match match);

public abstract class AbstractRegionBuilder<T> where T : AbstractRegionBuilder<T>
{
    
    public T ForeachLoop(FieldAccessor collectionAccessor, 
        Action<ForeachLoopBuilder>? body = null)
    {
        ThrowOnBadUsage();
        Context.EnterForeachLoop(collectionAccessor, out string variableName);

        var hasIterated = false;
        if (body != null)
        {
            var info = new ForeachLoopInfo(variableName);
            var builder = new ForeachLoopBuilder(Context, info);
        
            _lock = builder;
            body(builder);
            _lock = null;
        
            builder._Finalize();
            hasIterated = builder.HasIterated;
        }
        
        if (!hasIterated)
            Context.SkipBody();
        
        Context.ExitForeachLoop();

        return (T)this;
    }

    public T ReplaceRegion(FieldAccessor variableAccessor, 
        ReplaceMatchAction replacer,
        Action<ReplaceRegionBuilder>? body = null)
    {
        ThrowOnBadUsage();
        Context.EnterReplaceRegion(variableAccessor, out var replaceRanges);
        
        foreach ((int rangeIdx, int replacementSlot) in replaceRanges)
        {
            Debug.Assert(Context.Template.TextRanges[rangeIdx]
                .IsReplaceMatch(out var match));
            string replacement = replacer(match!);
            Context.SetReplacement(replacementSlot, replacement);
        }

        if (body != null)
        {
            var info = new ReplaceRegionInfo();
            var builder = new ReplaceRegionBuilder(Context, info);
            _lock = builder;

            body(builder);

            builder._Finalize();
        }

        Context.ExitReplaceRegion();
        _lock = null;
        
        return (T)this;
    }

    public T Insert(FieldAccessor fieldAccessor, ParameterizedTemplate template)
    {
        ThrowOnBadUsage();
        Context.EnterInsertRegion(fieldAccessor);
        Context.Insert(template);
        Context.ExitInsertRegion();
        
        return (T)this;
    }

    public T Insert(FieldAccessor fieldAccessor, string str)
    {
        ThrowOnBadUsage();
        Context.EnterInsertRegion(fieldAccessor);
        Context.Insert(str);
        Context.ExitInsertRegion();
        
        return (T)this;
    }

    public T NoInsert(FieldAccessor fieldAccessor)
    {
        ThrowOnBadUsage();
        Context.EnterInsertRegion(fieldAccessor);
        Context.ExitInsertRegion();
        
        return (T)this;
    }
    
    protected void _Finalize()
    {
        _isFinalized = true;
    }
    internal ParameterizedTemplateBuilderContext Context { get; }

    protected virtual void ThrowOnBadUsage()
    {
        if (_isFinalized)
            throw new InvalidOperationException($"{GetType().Name} is already finalized");
        
        if (_lock != null)
            throw new InvalidOperationException($"{GetType().Name} is locked by a nested {_lock.GetType().Name}");
    }
    
    private bool _isFinalized = false;
    private object? _lock = null;

    internal AbstractRegionBuilder(ParameterizedTemplateBuilderContext ctx)
    {
        Context = ctx;
    }
}

public static class ParameterizedTemplateBuilderExtensions
{
    public static ParameterizedTemplateBuilder ParameterizationBuilder(this LoadedTemplate template)
    {
        return new ParameterizedTemplateBuilder(template);
    }
    
    public static T ReplaceRegion<T>(
        this AbstractRegionBuilder<T> self,
        FieldAccessor variableAccessor1,
        ReplaceMatchAction replacer1,
        FieldAccessor variableAccessor2,
        ReplaceMatchAction replacer2,
        Action<ReplaceRegionBuilder>? builder = null)
        where T : AbstractRegionBuilder<T>
    {
        return self.ReplaceRegion(variableAccessor1, replacer1, 
            b =>
        {
            b.ReplaceRegion(variableAccessor2, replacer2, builder);
        });
    }
}