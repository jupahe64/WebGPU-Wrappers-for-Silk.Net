using System.Diagnostics;
using System.Text;

namespace TemplatingLibrary.TemplateParameters;

public class ParameterizedTemplate
{
    internal interface IInstruction;

    internal record InsertTemplate(ParameterizedTemplate Template, int Indent) : IInstruction;
    internal record InsertString(string String, int Indent) : IInstruction;

    internal record SetRangePointer(int Value) : IInstruction;
    
    private readonly LoadedTemplate _template;
    private readonly IReadOnlyList<string> _replacements;
    private readonly IReadOnlyList<(int writtenRangesCount, IInstruction instruction)> _instructions;
    private readonly int _rangesToWrite;

    internal ParameterizedTemplate(LoadedTemplate template,
        IReadOnlyList<string> replacements,
        IReadOnlyList<(int writtenRangesCount, IInstruction instruction)> instructions, 
        int rangesToWrite)
    {
        _template = template;
        _replacements = replacements;
        _instructions = instructions;
        _rangesToWrite = rangesToWrite;
    }

    public void Write(StringBuilder sb, int indentation = 0)
    {
        var rangePtr = 0;
        var writtenRangesCount = 0;
        var writtenReplacedRangesCount = 0;
        var instructionIdx = 0;
        while (writtenRangesCount < _rangesToWrite)
        {
            // handle all instruction that are meant to be executed at this point
            while (instructionIdx < _instructions.Count &&
                _instructions[instructionIdx].writtenRangesCount == writtenRangesCount)
            {
                switch (_instructions[instructionIdx].instruction)
                {
                    case InsertString(var stringToInsert, var relativeIndent):
                        sb.Append(' ', indentation + relativeIndent);
                        sb.Append(stringToInsert);
                        break;
                    case InsertTemplate(var templateToInsert, var relativeIndent):
                        templateToInsert.Write(sb, indentation + relativeIndent);
                        break;
                    case SetRangePointer(var value):
                        rangePtr = value;
                        break;
                    default:
                        Debug.Fail($"Unexpected instruction type {_instructions[instructionIdx].instruction}");
                        break;
                }
                instructionIdx++;
            }
            
            var textRange = _template.TextRanges[rangePtr];

            if (textRange.IsReplaceMatch(out _))
            {
                if (textRange.Indentation.HasValue)
                    sb.Append(' ', indentation + textRange.Indentation.Value);
            
                sb.Append(_replacements[writtenReplacedRangesCount++]);
            
                if (textRange.NewLine)
                    sb.Append(Environment.NewLine);
            }
            else
            {
                Debug.Assert(!textRange.IsReplaceMatch(out _));
                
                if (textRange.Indentation.HasValue)
                    sb.Append(' ', indentation + textRange.Indentation.Value);
            
                sb.Append(_template.Text.AsSpan(textRange.Begin..textRange.End));
            
                if (textRange.NewLine)
                    sb.Append(Environment.NewLine);
            }
            
            writtenRangesCount++;
            rangePtr++;
        }
    }
}