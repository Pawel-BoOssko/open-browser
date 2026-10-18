using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

/// <summary>One last consistency check before one potentially irreversible web send.</summary>
internal sealed partial class ChatGptLiteForm
{
    private static async Task<bool> RevalidateResumeBeforeSendAsync(
        CoreWebView2 core, string chatKey, string responseHash)
    {
        if (LiteChatNotes.NormalizeKey(core.Source) != chatKey)
            return false;
        var task = core.ExecuteScriptAsync(
            "window.__OPENBROWSER_RESUME_PROBE__ ? " +
            "JSON.stringify(window.__OPENBROWSER_RESUME_PROBE__.observe()) : null");
        if (await Task.WhenAny(task, Task.Delay(4500)) != task)
            return false;
        string? json = JsonSerializer.Deserialize<string>(await task);
        if (string.IsNullOrEmpty(json)) return false;
        using var doc = JsonDocument.Parse(json);
        var state = doc.RootElement;
        if (state.GetProperty("reasoningMode").GetString() != "high" ||
            state.GetProperty("role").GetString() != "assistant" ||
            state.GetProperty("generating").GetBoolean() ||
            state.GetProperty("toolBusy").GetBoolean() ||
            state.GetProperty("hasDraft").GetBoolean() ||
            state.GetProperty("error").GetBoolean() ||
            !state.GetProperty("composerReady").GetBoolean())
            return false;
        string lastText = state.GetProperty("text").GetString() ?? "";
        string newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lastText)));
        return responseHash == newHash &&
               LiteChatNotes.NormalizeKey(core.Source) == chatKey;
    }
}
