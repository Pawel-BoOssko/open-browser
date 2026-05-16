namespace BridgeBrowserAlpha0;

public static class PipelineRawDump
{
    private static readonly string Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "..", "..", "diagnostics", "pipeline-output");

    private static bool _ensured;

    private static void EnsureDir()
    {
        if (_ensured) return;
        try { Directory.CreateDirectory(Path.GetFullPath(Dir)); _ensured = true; } catch { }
    }

    public static void Write(string fileName, string? content)
    {
        try
        {
            EnsureDir();
            var path = Path.GetFullPath(Path.Combine(Dir, fileName));
            File.WriteAllText(path, content ?? "", System.Text.Encoding.UTF8);
        }
        catch { }
    }
}
