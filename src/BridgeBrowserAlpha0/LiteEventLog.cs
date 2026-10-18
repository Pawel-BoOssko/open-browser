using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Minimal persistent diagnostics for the isolated ChatGPT Lite window.
/// No prompts, answers, cookies, tokens, or complete URLs are recorded.
/// </summary>
internal static class LiteEventLog
{
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "diagnostics");

    public static void Write(string eventName, object? details = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath,
                    "lite-events-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl");
                if (File.Exists(path) && new FileInfo(path).Length > 10 * 1024 * 1024)
                    return;

                string json = JsonSerializer.Serialize(new
                {
                    utc = DateTimeOffset.UtcNow.ToString("O"),
                    pid = Environment.ProcessId,
                    eventName,
                    details
                });
                File.AppendAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Telemetry may not break the browser or prevent recovery.
        }
    }

    public static void PruneOldFiles()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
                return;
            foreach (string path in Directory.GetFiles(DirectoryPath, "lite-events-*.jsonl"))
            {
                if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-14))
                    File.Delete(path);
            }
        }
        catch
        {
            // Diagnostic retention is best-effort.
        }
    }

    public static string HostOnly(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? uri.Host
            : "unknown";
    }
}
