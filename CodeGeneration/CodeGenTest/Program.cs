using System.Diagnostics;
using System.Text;

var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res/webgpu.yml");
WebGPU.Yml.Document? document;
using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
{
    if (!WebGPU.Yml.Document.TryLoad(reader, out document))
        throw new InvalidOperationException("Failed to load YAML document");
}

var root = document.RootNode;
Console.WriteLine($"name: {root.Name}");
Console.WriteLine("enums:");
foreach (var _enum in root.Enums)
{
    Console.WriteLine($"\t{ToPascalCase(_enum.Name)}");
}
Console.WriteLine("structs:");
foreach (var _struct in root.Structs)
{
    Console.WriteLine($"\t{ToPascalCase(_struct.Name)}");
}

Console.WriteLine("objects:");
foreach (var _object in root.Objects)
{
    Console.WriteLine($"\t{ToPascalCase(_object.Name)}");
}

Console.WriteLine("functions:");
foreach (var _function in root.Functions)
{
    Console.WriteLine($"\t{ToPascalCase(_function.Name)}");
}

string ToPascalCase(string str)
{
    int underscoreCount = 0;
    foreach (char c in str)
    {
        if (c == '_')
           underscoreCount++; 
    }
    var sb = new StringBuilder(str.Length - underscoreCount);

    int start = 0;
    int i = 0;
    for (; i < str.Length; i++)
    {
        if (str[i] == '_')
        {
            sb.Append(char.ToUpperInvariant(str[start]));
            sb.Append(str, start+1, i-1 - start);
            start = i + 1;
        }
    }
    sb.Append(char.ToUpperInvariant(str[start]));
    sb.Append(str, start+1, i-1 - start);
    
    return sb.ToString();
}
