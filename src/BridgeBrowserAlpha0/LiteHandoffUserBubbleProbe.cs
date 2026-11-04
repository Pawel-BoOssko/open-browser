using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal static class LiteHandoffUserBubbleProbe
{
    internal sealed record Observation(
        string Url, int RendererPid, string BubbleSha256,
        bool Visible, string Source, DateTimeOffset Utc);

    // Synchronous DOM read. Raw content exists only transiently in RAM;
    // we return, record and persist SHA256 exclusively.
    private const string Script = """
        (() => {
          const nodes=[...document.querySelectorAll(
            'main [data-user-message-bubble],main [data-message-author-role="user"]'
          )];
          const target=nodes.reverse().find(e =>
            (e.innerText||'').trim().length>0);
          if (!target) return JSON.stringify({found:false,text:""});
          return JSON.stringify({found:true,text:(target.innerText||'').trim()});
        })()
        """;

    internal static async Task<Observation> CaptureAsync(
        CoreWebView2 webview)
    {
        string before = webview.Source;
        if (!LiteDevWakeState.IsChatUrl(before))
            throw new InvalidDataException("Not a chat page");
        string raw = await webview.ExecuteScriptAsync(Script);
        string json = JsonSerializer.Deserialize<string>(raw)
            ?? throw new InvalidDataException("JavaScript returned null");
        using var result = JsonDocument.Parse(json);
        if (!result.RootElement.GetProperty("found").GetBoolean())
            throw new InvalidDataException("Missing user bubble");
        string text = result.RootElement.GetProperty("text").GetString() ?? "";
        if (text.Length == 0)
            throw new InvalidDataException("Empty user bubble");
        string sha = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        string after = webview.Source;
        if (!string.Equals(before, after, StringComparison.Ordinal))
            throw new InvalidDataException("Route changed during probe");
        return new Observation(after, checked((int)webview.BrowserProcessId),
            sha, true, "webview_response_probe", DateTimeOffset.UtcNow);
    }
}