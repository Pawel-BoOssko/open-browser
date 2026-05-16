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

    private static readonly string Separator = Environment.NewLine + "====================" + Environment.NewLine;

    public static void Write(string fileName, string? content)
    {
        try
        {
            EnsureDir();
            var path = Path.GetFullPath(Path.Combine(Dir, fileName));
            var text = content ?? "";
            if (File.Exists(path) && new FileInfo(path).Length > 0)
                text = Separator + text;
            File.AppendAllText(path, text, System.Text.Encoding.UTF8);
        }
        catch { }
    }
}
