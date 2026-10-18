using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly LiteSecureArchive _archive = new();
    private readonly System.Windows.Forms.Timer _captureTimer = new() { Interval = 12000 };
    private readonly Button _archiveToggle = new()
    {
        Text = "Kopie: włączone", Width = 125
    };
    private bool _archiveEnabled = true;
    private bool _capturePending;

    private void InitializeCapture()
    {
        _archiveToggle.Click += (_, _) =>
        {
            _archiveEnabled = !_archiveEnabled;
            _archiveToggle.Text = _archiveEnabled ? "Kopie: włączone" : "Kopie: wyłączone";
            LiteEventLog.Write("archive_mode_changed", new { enabled = _archiveEnabled });
        };
        _captureTimer.Tick += async (_, _) => await CaptureVisibleAnswersAsync();
    }

    private async Task InstallCaptureScriptAsync(CoreWebView2 core)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "LiteConversationSnapshot.js");
        string script = await File.ReadAllTextAsync(path);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        LiteEventLog.Write("archive_observer_installed");
    }

    private async Task CaptureVisibleAnswersAsync()
    {
        var core = _browser.CoreWebView2;
        if (!_archiveEnabled || _capturePending || core is null) return;
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) ||
            uri.Host != "chatgpt.com" ||
            !(uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
              uri.AbsolutePath.StartsWith("/g/", StringComparison.OrdinalIgnoreCase))) return;

        _capturePending = true;
        try
        {
            var call = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_LITE_SNAPSHOT__ ? JSON.stringify(window.__OPENBROWSER_LITE_SNAPSHOT__.collect()) : null");
            if (await Task.WhenAny(call, Task.Delay(7000)) != call)
                LiteEventLog.Write("archive_probe_timeout");

            // Allow at most one pending script to avoid piling up hung renderer calls.
            string result = await call;
            if (!_archiveEnabled || IsDisposed) return;
            string? json = JsonSerializer.Deserialize<string>(result);
            if (string.IsNullOrWhiteSpace(json)) return;
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return;
            int snapshots = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("turnId", out var idElement) ||
                    !item.TryGetProperty("text", out var textElement)) continue;
                string? turnId = idElement.GetString();
                string? text = textElement.GetString();
                if (turnId is null || text is null) continue;
                if (_archive.Save(uri.GetLeftPart(UriPartial.Path), turnId, text))
                    snapshots++;
            }
            if (snapshots > 0)
                _archiveToggle.Text = "Kopie: zapisano";
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("archive_probe_failed", new { errorType = ex.GetType().Name });
        }
        finally
        {
            _capturePending = false;
        }
    }
}
