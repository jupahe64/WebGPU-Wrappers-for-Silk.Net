using System.Text;
using TemplatingLibrary;
using TemplatingLibrary.TemplateLoading;
using TemplatingLibrary.TemplateParameters;
using TemplatingLibrary.TemplateParameters.Builder;
using static TemplatingLibrary.FieldAccessor;

namespace WrapperCodeGenExperiments;

public static class CodeGenExperiments
{
    private static Dictionary<string,LoadedTemplate> s_conversionTemplates = default!;

    public static void GenerateCallbacks()
    {
        string directory = Silk.NET.WebGPU.Safe.TemplatesEntryPoint.GetPath();
        var template = TemplateLoader.Load(File.ReadAllText(Path.Combine(directory, "Callbacks.cs")), Range.All);
        template.DebugPrint();

        s_conversionTemplates = TemplateLoader.LoadAllDefined(
            File.ReadAllText(Path.Combine(directory, "Conversions.cs")), Range.All);
        foreach (var (name, _template) in s_conversionTemplates)
        {
            Console.WriteLine($"// ----- {name} -----");
            _template.DebugPrint();
        }

        var parameters = template.ParameterizationBuilder()
            .ForeachLoop(GlobalField("Callbacks"),
                b =>
                {
                    b.BeginIteration();
                    TemplatedCallbackBuilder(b, b.Info.VariableName, "CallbackA", []);
                    b.BeginIteration();
                    TemplatedCallbackBuilder(b, b.Info.VariableName, "CallbackB", [
                        ("TestStructA", "UnsafeTestStructA", "testStructA"),
                        ("TestStructB", "UnsafeTestStructB", "testStructB"),
                    ]);
                }).GetTemplate();

        var sb = new StringBuilder();
        parameters.Write(sb);
        Console.WriteLine();
        Console.WriteLine(sb.ToString());
    }
    
    private static void TemplatedCallbackBuilder<T>(AbstractRegionBuilder<T> b, string callbackVariableName,
        string callbackName, IReadOnlyList<(string safeType, string unsafeType, string name)> arguments)
        where T : AbstractRegionBuilder<T>
    {
        var safeParameterString = string.Join(", ", arguments.Select(argument => argument.safeType));
        var unsafeParameterTypeString = string.Join("", 
            arguments.Select(argument => $"{argument.unsafeType}, "));
        var unsafeParameterString = string.Join("", 
            arguments.Select(argument => $"{argument.unsafeType} {argument.name}, "));
        var callParameterString = string.Join(", ", 
            arguments.Select(argument => $"_{argument.name}"));
        
        b.ReplaceRegion(
                VariableField(callbackVariableName, "Name"), 
                _ => callbackName,
                b =>
                {
                    b.ReplaceRegion(
                        VariableField(callbackVariableName, 
                            "SafeParameterString"),
                        _ => safeParameterString);
                    
                    b.ReplaceRegion(
                        VariableField(callbackVariableName, 
                            "UnsafeParameterTypeString"),
                        _ => unsafeParameterTypeString);
                    
                    b.ReplaceRegion(
                        VariableField(callbackVariableName, 
                            "UnsafeParameterString"),
                        _ => unsafeParameterString);
                    
                    b.ForeachLoop(
                        VariableField(callbackVariableName,
                            "ConversionStrings"),
                        b =>
                        {
                            b.BeginIteration();
                            b.Insert(Identity(b.Info.VariableName),
                                TemplatedStructUnpackConversion(
                                    arguments.Select(x => (x.unsafeType, x.name)))
                                );
                        });
                    
                    b.ReplaceRegion(
                        VariableField(callbackVariableName, 
                            "CallParametersString"),
                        _ => callParameterString);
                });
    }

    private static ParameterizedTemplate TemplatedStructUnpackConversion(
        IEnumerable<(string unsafeType, string variableName)> arguments)
    {
        var b = new ParameterizedTemplateBuilder(s_conversionTemplates["UnpackStructs"]);
        b.ForeachLoop(GlobalField("PackableStructs"),
            b =>
            {
                foreach (var argument in arguments)
                {
                    b.BeginIteration();
                    b.ReplaceRegion(
                        VariableField(b.Info.VariableName,
                            "Name"),
                        _ => argument.unsafeType,
                        VariableField(b.Info.VariableName,
                            "VariableName"),
                        _ => argument.variableName);

                }
            });
        return b.GetTemplate();
    }
}