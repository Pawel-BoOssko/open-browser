using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly Button _hardPruneToggle = new()
    {
        Text = "DOM mocne: NIE", Width = 128
    };
    private readonly System.Windows.Forms.Timer _pruneReportTimer = new()
    {
        Interval = 60000
    };
    private bool _hardPruneEnabled = true;
    private bool _pruneProbePending;
    private int _lastReportedPruned;

    private void InitializeHardPruneUi()
    {
        _hardPruneToggle.Click += async (_, _) =>
        {
            _hardPruneEnabled = !_hardPruneEnabled;
            _hardPruneToggle.Text = _hardPruneEnabled
                ? "DOM mocne: TAK" : "DOM mocne: NIE";
            LiteEventLog.Write("hard_dom_mode_changed", new { enabled = _hardPruneEnabled });
            await ApplyHardPruneModeAsync();
        };
        _pruneReportTimer.Tick += async (_, _) => await PollHardPruneStatusAsync();
    }

    private async Task InstallHardPruneScriptAsync(CoreWebView2 core)
    {
        string script = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "LiteDomHardPrune.js"));
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        LiteEventLog.Write("hard_dom_script_installed", new { defaultEnabled = true,
            keepNewest = 24, threshold = 90 });
    }

    private async Task ApplyHardPruneModeAsync()
    {
        var core = _browser.CoreWebView2;
        if (core is null) return;
        try
        {
            string script = "window.__OPENBROWSER_HARD_PRUNE__?.enable(" +
                (_hardPruneEnabled ? "true" : "false") + ")";
            var task = core.ExecuteScriptAsync(script);
            if (await Task.WhenAny(task, Task.Delay(6000)) != task)
            {
                LiteEventLog.Write("hard_dom_toggle_delayed");
                return;
            }
            await task;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("hard_dom_toggle_failed",
                new { errorType = ex.GetType().Name });
        }
    }

    private async Task PollHardPruneStatusAsync()
    {
        var core = _browser.CoreWebView2;
        if (core is null || _pruneProbePending) return;
        _pruneProbePending = true;
        try
        {
            string script = "window.__OPENBROWSER_HARD_PRUNE__ ? " +
                "JSON.stringify(window.__OPENBROWSER_HARD_PRUNE__.status()) : null";
            string raw = await core.ExecuteScriptAsync(script);
            string? json = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrWhiteSpace(json)) return;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int count = root.GetProperty("removedTotal").GetInt32();
            if (count != _lastReportedPruned)
            {
                LiteEventLog.Write("hard_dom_pruned", new
                {
                    removedTotal = count,
                    lastRemoved = root.GetProperty("lastRemoved").GetInt32(),
                    skipReason = root.GetProperty("skipped").GetString(),
                    failures = root.GetProperty("failures").GetInt32()
                });
                _lastReportedPruned = count;
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("hard_dom_status_failed",
                new { errorType = ex.GetType().Name });
        }
        finally { _pruneProbePending = false; }
    }
}
