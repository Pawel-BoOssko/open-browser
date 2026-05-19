using System.Diagnostics;

namespace BridgeBrowserAlpha0;

public static class AppConstants
{
    public const string AppTitle = "Open Browser";

    public static string? ConversationId { get; set; }
    public static string? LastAssistantResponseText { get; set; }
    public static string? LastAssistantMessageId { get; set; }

    private static string? _downloadsPath;
    public static string DownloadsPath
    {
        get
        {
            if (_downloadsPath != null) return _downloadsPath;
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "config", "local", "downloads-path.json");
                if (!File.Exists(configPath))
                {
                    configPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..", "..", "config", "local", "downloads-path.json"));
                }
                if (File.Exists(configPath))
                {
                    using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
                    if (json.RootElement.TryGetProperty("path", out var prop))
                        _downloadsPath = Path.GetFullPath(prop.GetString() ?? "");
                }
            }
            catch { }
            _downloadsPath ??= @"D:\downloads-open-browser";
            return _downloadsPath;
        }
    }

    public static readonly string BuildNumber = GetGitCount() ?? "?";
    public static readonly string CommitHash = GetGitHash() ?? "unknown";
    public static readonly string BuildStamp =
        DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

    public static readonly string BuildInfo =
        $"Build {BuildNumber}  {CommitHash}";

    public static readonly string AppTitleWithBuild =
        $"{AppTitle}  {BuildInfo}  {BuildStamp}";

    private static string? GetGitCount()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-list --count HEAD")
            {
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            p.WaitForExit(3000);
            return p.ExitCode == 0 ? p.StandardOutput.ReadToEnd().Trim() : null;
        }
        catch { return null; }
    }

    private static string? GetGitHash()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            p.WaitForExit(3000);
            return p.ExitCode == 0 ? p.StandardOutput.ReadToEnd().Trim() : null;
        }
        catch { return null; }
    }
}
