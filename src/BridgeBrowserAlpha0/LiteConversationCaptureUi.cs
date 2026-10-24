using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly LiteSecureArchive _archive = new();
    private readonly LiteResponseMarkdownArchive _markdownArchive = new();
    // Completed-answer snapshots only. Avoid a full DOM read every 12 seconds.
    private readonly System.Windows.Forms.Timer _captureTimer = new() { Interval = 30000 };
    private readonly Button _archiveToggle = new()
    {
        Text = "Kopie: włączone", Width = 125
    };
    private bool _archiveEnabled = true;
    private bool _capturePending;
    private Task<string>? _captureTask;
    private long _captureEpoch;

    private void ResetCaptureForNewLocation()
    {
        // A hung script belongs to the old document and must not block
        // snapshots for a newly selected chat. Late completions cannot write.
        _captureEpoch++;
        _captureTask = null;
        _capturePending = false;
    }

    internal static bool IsArchiveResultCurrent(long startedEpoch,
        long currentEpoch, Uri startedUri, string? currentSource)
    {
        if (startedEpoch != currentEpoch ||
            !Uri.TryCreate(currentSource, UriKind.Absolute, out var liveUri))
            return false;
        return string.Equals(liveUri.GetLeftPart(UriPartial.Path),
            startedUri.GetLeftPart(UriPartial.Path),
            StringComparison.OrdinalIgnoreCase);
    }

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
        if (!_archiveEnabled || _capturePending ||
            _captureTask is { IsCompleted: false } || core is null) return;
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) ||
            uri.Host != "chatgpt.com" ||
            !(uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
              uri.AbsolutePath.StartsWith("/g/", StringComparison.OrdinalIgnoreCase))) return;

        _capturePending = true;
        long epoch = _captureEpoch;
        try
        {
            var call = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_LITE_SNAPSHOT__ ? JSON.stringify(window.__OPENBROWSER_LITE_SNAPSHOT__.collect()) : null");
            _captureTask = call;
            if (await Task.WhenAny(call, Task.Delay(7000)) != call)
            {
                LiteEventLog.Write("archive_probe_timeout");
                // Remain single-flight for this document, but never await
                // indefinitely. Observe eventual exceptions off the UI thread.
                _ = call.ContinueWith(t => { _ = t.Exception; },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
                return;
            }
            string result = await call;
            if (!_archiveEnabled || IsDisposed ||
                !IsArchiveResultCurrent(epoch, _captureEpoch, uri, core.Source))
                return;
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
                bool partial = item.TryGetProperty("partial", out var phase) &&
                    phase.ValueKind == JsonValueKind.True;
                string chatUrl = uri.GetLeftPart(UriPartial.Path);
                if (_markdownArchive.Save(chatUrl, turnId, text, partial))
                    snapshots++;
                if (!partial && _archive.Save(chatUrl, turnId, text))
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
            // An old page's finished probe must not clear a new page's
            // pending flag or overwrite the current in-flight task.
            if (epoch == _captureEpoch)
            {
                _capturePending = false;
                if (_captureTask is { IsCompleted: true })
                    _captureTask = null;
            }
        }
    }
}
