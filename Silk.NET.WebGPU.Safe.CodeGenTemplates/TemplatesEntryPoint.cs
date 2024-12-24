using System.IO;
using System.Runtime.CompilerServices;

namespace Silk.NET.WebGPU.Safe;

#if DEBUG
public class TemplatesEntryPoint
{
    public static string GetPath()
    {
        return s_getPath();
    }

    private static string s_getPath([CallerFilePath] string file = "")
    {
        return Path.GetDirectoryName(file)!;
    }
}
#endif