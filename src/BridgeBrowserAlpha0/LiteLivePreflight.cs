using System.Text.Json;

namespace BridgeBrowserAlpha0;

// Local, atomic state from a successful WebView2 probe. Never an idle claim
// unless it has a fresh timestamp and the expected foreground route.
internal static class LiteLivePreflight
{
    internal static void Store(string source, bool generating, bool draft, int turns, string error)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            uri.Host != "chatgpt.com" || !uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase))
            return;
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "diagnostics");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "live-preflight.json");
        string temp = path + "." + Environment.ProcessId + ".tmp";
        var record = new
        {
            schema = 1,
            utc = DateTimeOffset.UtcNow.ToString("o"),
            pid = Environment.ProcessId,
            source = uri.GetLeftPart(UriPartial.Path),
            generating,
            draft,
            turns,
            error,
            toolBusy = "unknown",
            evidence = "webview_response_probe"
        };
        File.WriteAllText(temp, JsonSerializer.Serialize(record));
        File.Move(temp, path, true);
    }
}