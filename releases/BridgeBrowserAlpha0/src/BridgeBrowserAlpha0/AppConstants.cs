using System.Diagnostics;

namespace BridgeBrowserAlpha0;

public static class AppConstants
{
    public const string AppTitle = "Open Browser";

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
