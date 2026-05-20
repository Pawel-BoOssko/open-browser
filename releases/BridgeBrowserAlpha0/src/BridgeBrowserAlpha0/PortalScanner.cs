using System.IO;

namespace BridgeBrowserAlpha0;

public static class PortalScanner
{
    private static string? _script;

    public static string Script => _script ??= LoadScript();

    private static string LoadScript()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PortalScanner.js");
        return File.ReadAllText(path);
    }
}
