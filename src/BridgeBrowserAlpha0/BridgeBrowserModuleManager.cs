using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

public sealed class BridgeBrowserModuleManager
{
    private readonly LogWriter _log;
    private CoreWebView2? _core;
    private string? _documentScriptId;
    private string? _loadedVersion;

    public BridgeBrowserModuleManager(LogWriter log)
    {
        _log = log;
    }

    public string? LoadedVersion => _loadedVersion;

    public async Task InitializeAsync(CoreWebView2 core)
    {
        _core = core;
        AppPaths.EnsureAll();
        EnsureCurrentModuleExists();
        await LoadCurrentConversationTrimmerAsync();
    }

    public async Task<string> PromoteLatestConversationTrimmerAsync()
    {
        var latest = Directory.GetDirectories(AppPaths.ConversationTrimmerVersions)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(d => d.LastWriteTimeUtc)
            .FirstOrDefault();

        if (latest == null)
            throw new InvalidOperationException("No conversation-trimmer versions found.");

        var sourceFile = Path.Combine(latest.FullName, "conversation-trimmer.js");
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException("Latest conversation-trimmer version has no conversation-trimmer.js", sourceFile);

        Directory.CreateDirectory(AppPaths.ConversationTrimmerCurrent);
        File.Copy(sourceFile, AppPaths.ConversationTrimmerCurrentFile, true);
        _log.WriteRun("modules", "module_promote", "ok", "Conversation trimmer promoted to current", new { versionFolder = latest.Name, sourceFile, currentFile = AppPaths.ConversationTrimmerCurrentFile });

        return await LoadCurrentConversationTrimmerAsync();
    }

    public async Task<string> LoadCurrentConversationTrimmerAsync()
    {
        if (_core == null) throw new InvalidOperationException("WebView2 core is not initialized.");
        EnsureCurrentModuleExists();

        var script = await File.ReadAllTextAsync(AppPaths.ConversationTrimmerCurrentFile);

        // WebView2 package 1.0.2957.106 used in this project does not expose
        // RemoveScriptToExecuteOnDocumentCreatedAsync. Hot swap for the current page
        // is performed by ExecuteScriptAsync. AddScriptToExecuteOnDocumentCreatedAsync
        // keeps the latest module available after future navigations. Older document
        // scripts may remain registered until process restart, but the module assignment
        // overwrites window.__BRIDGE_BROWSER_MODULES__.conversationTrimmer on load.
        _documentScriptId = await _core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        await _core.ExecuteScriptAsync(script);
        var statusJson = await GetConversationTrimmerStatusJsonAsync();
        _loadedVersion = ExtractVersion(statusJson);

        _log.WriteRun("modules", "module_load", "ok", "Conversation trimmer loaded", new { currentFile = AppPaths.ConversationTrimmerCurrentFile, version = _loadedVersion, status = JsonSerializer.Deserialize<object>(statusJson) });
        return _loadedVersion ?? "unknown";
    }

    public async Task<string> GetConversationTrimmerStatusJsonAsync()
    {
        if (_core == null) return "{\"ok\":false,\"reason\":\"core_not_initialized\"}";

        const string script = """
(() => {
  try {
    const mod = window.__BRIDGE_BROWSER_MODULES__ && window.__BRIDGE_BROWSER_MODULES__.conversationTrimmer;
    if (!mod) return JSON.stringify({ ok: false, reason: "conversation_trimmer_missing" });
    const status = mod.getStatus ? mod.getStatus() : { ok: false, reason: "getStatus_missing" };
    const monitor = window.__BRIDGE_BROWSER_TURNS_MONITOR__ && window.__BRIDGE_BROWSER_TURNS_MONITOR__.getStatus
      ? window.__BRIDGE_BROWSER_TURNS_MONITOR__.getStatus()
      : null;
    return JSON.stringify({ ok: true, status, monitor });
  } catch (error) {
    return JSON.stringify({ ok: false, reason: "status_error", error: String(error), stack: error && error.stack || null });
  }
})()
""";

        var raw = await _core.ExecuteScriptAsync(script);
        return JsonSerializer.Deserialize<string>(raw) ?? raw;
    }

    public async Task<TrimmerCallResult> TrimConversationResponseTextAsync(string responseText, object meta)
    {
        if (_core == null) return TrimmerCallResult.Failed("core_not_initialized", responseText);

        var responseLiteral = JsonSerializer.Serialize(responseText);
        var metaLiteral = JsonSerializer.Serialize(meta);
        var script = $$"""
(() => {
  try {
    const mod = window.__BRIDGE_BROWSER_MODULES__ && window.__BRIDGE_BROWSER_MODULES__.conversationTrimmer;
    if (!mod || !mod.trimConversationResponseText) {
      return JSON.stringify({ ok: false, changed: false, reason: "conversation_trimmer_missing", responseText: {{responseLiteral}} });
    }
    const result = mod.trimConversationResponseText({{responseLiteral}}, {{metaLiteral}});
    return JSON.stringify(result);
  } catch (error) {
    return JSON.stringify({ ok: false, changed: false, reason: "trim_call_error", error: String(error), stack: error && error.stack || null, responseText: {{responseLiteral}} });
  }
})()
""";

        try
        {
            var raw = await _core.ExecuteScriptAsync(script);
            var json = JsonSerializer.Deserialize<string>(raw) ?? raw;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
            var changed = root.TryGetProperty("changed", out var chEl) && chEl.ValueKind == JsonValueKind.True;
            var reason = root.TryGetProperty("result", out var resultEl) && resultEl.TryGetProperty("reason", out var resultReason)
                ? resultReason.GetString() ?? "unknown"
                : root.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() ?? "unknown" : "unknown";
            var output = root.TryGetProperty("responseText", out var rt) && rt.ValueKind == JsonValueKind.String
                ? rt.GetString() ?? responseText
                : responseText;
            object? resultData = root.TryGetProperty("result", out var res)
                ? JsonSerializer.Deserialize<object>(res.GetRawText())
                : JsonSerializer.Deserialize<object>(root.GetRawText());

            return new TrimmerCallResult(ok, changed, reason, output, resultData);
        }
        catch (Exception ex)
        {
            return new TrimmerCallResult(false, false, "trim_execute_failed", responseText, new { ex.Message, ex.StackTrace });
        }
    }

    private static string? ExtractVersion(string statusJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(statusJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var status) && status.TryGetProperty("version", out var v)) return v.GetString();
            if (root.TryGetProperty("status", out var status2) && status2.TryGetProperty("status", out var nested) && nested.TryGetProperty("version", out var nv)) return nv.GetString();
        }
        catch { }
        return null;
    }

    private void EnsureCurrentModuleExists()
    {
        Directory.CreateDirectory(AppPaths.ConversationTrimmerCurrent);
        if (File.Exists(AppPaths.ConversationTrimmerCurrentFile)) return;

        var latest = Directory.GetDirectories(AppPaths.ConversationTrimmerVersions)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(d => d.LastWriteTimeUtc)
            .FirstOrDefault();

        if (latest == null)
        {
            _log.WriteRun("modules", "module_current_missing", "error", "No conversation-trimmer version available", new { AppPaths.ConversationTrimmerVersions });
            return;
        }

        var sourceFile = Path.Combine(latest.FullName, "conversation-trimmer.js");
        if (File.Exists(sourceFile))
            File.Copy(sourceFile, AppPaths.ConversationTrimmerCurrentFile, true);
    }
}

public sealed record TrimmerCallResult(bool Ok, bool Changed, string Reason, string ResponseText, object? ResultData)
{
    public static TrimmerCallResult Failed(string reason, string responseText) => new(false, false, reason, responseText, new { reason });
}
