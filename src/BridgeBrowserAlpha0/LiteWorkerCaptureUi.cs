using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // The background wake worker renders responses for chats that may never
    // be opened in the foreground. They need the same durable archive.
    private bool _workerArchivePending;
    private Task<string>? _workerArchiveTask;
    private long _workerArchiveTaskEpoch = -1;

    private async Task CaptureWorkerAnswersAsync()
    {
        var core = _chatWakeWorker.CoreWebView2;
        if (!_archiveEnabled || core is null || _chatWakeBusy || _workerArchivePending)
            return;
        long epoch = _workerRouteEpoch;
        if (_workerArchiveTask is { IsCompleted: false } &&
            _workerArchiveTaskEpoch == epoch)
            return;
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) ||
            uri.Host != "chatgpt.com" ||
            !LiteDevWakeState.IsChatUrl(core.Source))
            return;
        // One writer per visible conversation. The foreground timer already
        // archives this URL, and simultaneous writers could race on index.jsonl.
        if (Uri.TryCreate(_browser.CoreWebView2?.Source, UriKind.Absolute, out var front) &&
            string.Equals(front.GetLeftPart(UriPartial.Path),
                uri.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase))
            return;
        _workerArchivePending = true;
        try
        {
            var task = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_LITE_SNAPSHOT__ ? " +
                "JSON.stringify(window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive()) : null");
            _workerArchiveTask = task;
            _workerArchiveTaskEpoch = epoch;
            if (await Task.WhenAny(task, Task.Delay(6500)) != task)
            {
                LiteEventLog.Write("worker_archive_probe_timeout");
                _ = task.ContinueWith(t => { _ = t.Exception; },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
                return;
            }
            string raw = await task;
            if (!_archiveEnabled || IsDisposed ||
                !IsArchiveResultCurrent(epoch, _workerRouteEpoch, uri, core.Source))
                return;
            string? json = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrWhiteSpace(json)) return;
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return;
            var persisted = new List<string>();
            int saved = 0;
            string url = uri.GetLeftPart(UriPartial.Path);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (epoch != _workerRouteEpoch) return;
                if (!item.TryGetProperty("turnId", out var idField) ||
                    !item.TryGetProperty("text", out var textField)) continue;
                var id = idField.GetString();
                var content = textField.GetString();
                if (id is null || content is null) continue;
                bool partial = item.TryGetProperty("partial", out var phase) &&
                    phase.ValueKind == JsonValueKind.True;
                if (_markdownArchive.Save(url, id, content, partial)) saved++;
                if (!partial && _markdownArchive.IsPersisted(url, id, content))
                    persisted.Add(id);
            }
            if (persisted.Count > 0 && _archiveEnabled &&
                IsArchiveResultCurrent(epoch, _workerRouteEpoch, uri, core.Source))
            {
                string ids = JsonSerializer.Serialize(persisted.Distinct().ToArray());
                try
                {
                    await ExecuteWorkerScriptAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__?.ackSaved(" + ids + ")", 4500);
                }
                catch (TimeoutException)
                {
                    LiteEventLog.Write("worker_archive_ack_timeout");
                }
            }
            if (saved > 0)
                LiteEventLog.Write("worker_archive_saved", new { snapshots = saved });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("worker_archive_failed",
                new { errorType = ex.GetType().Name });
        }
        finally
        {
            _workerArchivePending = false;
            if (_workerArchiveTask is { IsCompleted: true })
                _workerArchiveTask = null;
        }
    }
}
