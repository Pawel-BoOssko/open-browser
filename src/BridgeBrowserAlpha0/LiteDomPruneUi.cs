using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly Button _hardPruneToggle = new()
    {
        Text = "DOM mocne: TAK", Width = 128
    };
    private readonly System.Windows.Forms.Timer _pruneReportTimer = new()
    {
        Interval = 60000
    };
    private bool _hardPruneEnabled = true;
    private bool _pruneProbePending;
    private Task<string>? _pruneProbeTask;
    private int _lastReportedPruned;

    private void InitializeHardPruneUi()
    {
        _hardPruneToggle.Click += async (_, _) =>
        {
            _hardPruneEnabled = !_hardPruneEnabled;
            _hardPruneToggle.Text = _hardPruneEnabled
                ? "DOM mocne: TAK" : "DOM mocne: NIE";
            // Do not schedule a minute-by-minute WinForms callback while
            // pruning is OFF. Arm only after explicit user opt-in.
            UpdateHardPruneReportTimer();
            LiteEventLog.Write("hard_dom_mode_changed", new { enabled = _hardPruneEnabled });
            await ApplyHardPruneModeAsync();
        };
        _pruneReportTimer.Tick += async (_, _) => await PollHardPruneStatusAsync();
        UpdateHardPruneReportTimer();
    }

    internal static bool ShouldPollHardPrune(bool ownerEnabled) => ownerEnabled;

    private void UpdateHardPruneReportTimer()
    {
        if (ShouldPollHardPrune(_hardPruneEnabled))
            _pruneReportTimer.Start();
        else
            _pruneReportTimer.Stop();
    }

    private async Task InstallHardPruneScriptAsync(CoreWebView2 core)
    {
        string script = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "LiteDomHardPrune.js"));
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        LiteEventLog.Write("hard_dom_script_installed", new { defaultEnabled = true,
            keepNewest = 10, threshold = 11 });
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
        // The hard-prune feature is OFF by default. Do not poll its JS every
        // minute when it cannot remove anything.
        if (core is null || !_hardPruneEnabled || _pruneProbePending ||
            _pruneProbeTask is { IsCompleted: false }) return;
        _pruneProbePending = true;
        try
        {
            string script = "window.__OPENBROWSER_HARD_PRUNE__ ? " +
                "JSON.stringify(window.__OPENBROWSER_HARD_PRUNE__.status()) : null";
            var task = core.ExecuteScriptAsync(script);
            _pruneProbeTask = task;
            if (await Task.WhenAny(task, Task.Delay(6000)) != task)
            {
                LiteEventLog.Write("hard_dom_probe_timeout");
                return;
            }
            string raw = await task;
            string? json = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrWhiteSpace(json)) return;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("enabled", out var enabled) &&
                enabled.ValueKind == JsonValueKind.False && _hardPruneEnabled)
            {
                _hardPruneEnabled = false;
                _hardPruneToggle.Text = "DOM mocne: wstrzymano";
                LiteEventLog.Write("hard_dom_auto_disabled", new {
                    reason = root.GetProperty("skipped").GetString()
                });
                UpdateHardPruneReportTimer();
            }
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
        finally
        {
            _pruneProbePending = false;
            if (_pruneProbeTask is { IsCompleted: true }) _pruneProbeTask = null;
        }
    }
}
