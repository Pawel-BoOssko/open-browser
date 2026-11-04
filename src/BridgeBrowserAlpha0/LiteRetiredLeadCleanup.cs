using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

// One-way sidecar. Failure, timeout and disposal never affect promotion/wake.
internal static class LiteRetiredLeadCleanupPolicy
{
    internal static bool Eligible(string oldUrl, string newUrl, string currentLeadUrl) =>
        LiteHandoffPersistedRoute.IsServerConversation(oldUrl) &&
        LiteHandoffPersistedRoute.IsServerConversation(newUrl) &&
        !string.Equals(oldUrl, newUrl, StringComparison.Ordinal) &&
        string.Equals(currentLeadUrl, newUrl, StringComparison.Ordinal);
}

internal sealed partial class ChatGptLiteForm
{
    private void QueueRetiredLeadCleanup(string oldUrl, string newUrl)
    {
        try
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => { _ = RunRetiredLeadCleanupAsync(oldUrl, newUrl); }));
            LiteEventLog.Write("retired_lead_cleanup_queued", new { oldUrl, newUrl });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("retired_lead_cleanup_queue_failed",
                new { errorType = ex.GetType().Name, error = ex.Message });
        }
    }

    private bool IsRetiredLeadCleanupSafe(string oldUrl, string newUrl)
    {
        var projects = LiteProjectRegistry.OpenDefault().Projects;
        return projects.Any(p =>
            p.Chats.Any(c => c.Role == "lead" && c.Url == newUrl) &&
            p.Chats.Any(c => c.Url == oldUrl && c.Role != "lead") &&
            LiteRetiredLeadCleanupPolicy.Eligible(oldUrl, newUrl, newUrl));
    }

    private async Task RunRetiredLeadCleanupAsync(string oldUrl, string newUrl)
    {
        WebView2? sidecar = null;
        try
        {
            if (IsDisposed || !IsRetiredLeadCleanupSafe(oldUrl, newUrl))
            {
                LiteEventLog.Write("retired_lead_cleanup_skipped",
                    new { oldUrl, newUrl, reason = "lead_changed_or_url_invalid" });
                return;
            }
            // Independent of foreground and wake-worker routes. Never reuse
            // either WebView2: the successor may already be working there.
            sidecar = new WebView2 {
                Size = new Size(1100, 740), Location = new Point(0, 42),
                Visible = true, TabStop = false
            };
            Controls.Add(sidecar);
            sidecar.SendToBack();
            string profile = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "OpenBrowser", "ChatGPT-Lite", "Profile");
            var env = await CoreWebView2Environment.CreateAsync(null, profile)
                .WaitAsync(TimeSpan.FromSeconds(12));
            await sidecar.EnsureCoreWebView2Async(env)
                .WaitAsync(TimeSpan.FromSeconds(12));
            var core = sidecar.CoreWebView2
                ?? throw new InvalidOperationException("Cleanup WebView2 not initialized");
            core.Settings.AreDevToolsEnabled = false;
            async Task NavigateAndWaitAsync(Action navigate, string stage)
            {
                var complete = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                void OnCompleted(object? _, CoreWebView2NavigationCompletedEventArgs e)
                    => complete.TrySetResult(e.IsSuccess);
                core.NavigationCompleted += OnCompleted;
                try {
                    navigate();
                    if (!await complete.Task.WaitAsync(TimeSpan.FromSeconds(15)))
                        throw new InvalidDataException(stage + "_navigation_failed");
                }
                finally { core.NavigationCompleted -= OnCompleted; }
            }
            await NavigateAndWaitAsync(() => core.Navigate(oldUrl), "open_old");
            if (!IsRetiredLeadCleanupSafe(oldUrl, newUrl) ||
                LiteDevWakeState.NormalizeUrl(core.Source) != oldUrl)
                throw new InvalidDataException("old_route_not_verified_after_open");
            await NavigateAndWaitAsync(() => core.Reload(), "refresh_old");
            LiteEventLog.Write("retired_lead_cleanup_refreshed", new { oldUrl, newUrl });
            string scriptTemplate = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "LiteRetiredLeadCleanup.js"));
            bool clicked = false;
            for (int i = 1; i <= 4; i++)
            {
                if (IsDisposed || !IsRetiredLeadCleanupSafe(oldUrl, newUrl) ||
                    LiteDevWakeState.NormalizeUrl(core.Source) != oldUrl)
                {
                    LiteEventLog.Write("retired_lead_cleanup_skipped",
                        new { oldUrl, newUrl, reason = "route_or_lead_changed", attempt = i });
                    return;
                }
                string script = scriptTemplate
                    .Replace("__OLD_UUID__", oldUrl["https://chatgpt.com/c/".Length..])
                    .Replace("__CLICK_ALLOWED__", clicked ? "no" : "yes");
                var raw = await core.ExecuteScriptAsync(script)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                string result = JsonSerializer.Deserialize<string>(raw) ?? "no_script_result";
                LiteEventLog.Write("retired_lead_cleanup_probe",
                    new { oldUrl, newUrl, attempt = i, result });
                if (result == "wrong_route") return;
                if (result == "stop_click_invoked") clicked = true;
                else if (clicked && result == "stop_unavailable")
                {
                    LiteEventLog.Write("retired_lead_cleanup_stopped",
                        new { oldUrl, newUrl, confirmed = true, attempts = i });
                    return;
                }
                await Task.Delay(900);
            }
            LiteEventLog.Write("retired_lead_cleanup_finished",
                new { oldUrl, newUrl, clickInvoked = clicked, confirmedStopped = false,
                    reason = clicked ? "stop_still_visible_or_unverified" : "stop_unavailable" });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("retired_lead_cleanup_failed",
                new { oldUrl, newUrl, errorType = ex.GetType().Name, error = ex.Message });
        }
        finally
        {
            if (sidecar is not null)
            {
                try { Controls.Remove(sidecar); sidecar.Dispose(); }
                catch (Exception ex) {
                    LiteEventLog.Write("retired_lead_cleanup_dispose_failed",
                        new { errorType = ex.GetType().Name, error = ex.Message });
                }
            }
        }
    }
}
