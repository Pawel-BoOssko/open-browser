using System;
using System.IO;

namespace BridgeBrowserAlpha0;

public static class PageTap
{
    private static string? _script;

    public static string Script => _script ??= LoadScript();

    private static string LoadScript()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PageTap.js");
        return File.ReadAllText(path);
    }
}
