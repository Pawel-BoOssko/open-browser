using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private bool _foregroundRecoveryInProgress;

    // A browser-process crash invalidates the existing WebView2 controller.
    // Recover only that view, never the worker, lead or wake schedule.
    internal static bool CanRecoverForegroundBrowser(
        CoreWebView2ProcessFailedKind kind, int attempt) =>
        kind == CoreWebView2ProcessFailedKind.BrowserProcessExited &&
        attempt is >= 1 and <= 3;

    private async Task RecoverForegroundWebViewAfterExitAsync(string? previousUrl)
    {
        if (IsDisposed || Disposing || _foregroundRecoveryInProgress)
            return;
        _foregroundRecoveryInProgress = true;
        try
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (IsDisposed || Disposing) return;
                await Task.Delay(attempt == 1 ? 1500 : 5000);
                if (IsDisposed || Disposing) return;

                LiteEventLog.Write("foreground_webview_recovery_attempt", new {
                    attempt, maxAttempts = 3, workerUnaffected = true
                });

                try
                {
                    var old = _browser;
                    var parent = old.Parent ??
                        throw new InvalidOperationException("foreground_webview_parent_missing");

                    // Previous browser-process-exited controller cannot be
                    // reinitialized via EnsureCoreWebView2Async. Replace it.
                    var replacement = new WebView2 { Dock = DockStyle.Fill };
                    parent.Controls.Remove(old);
                    _browser = replacement;
                    parent.Controls.Add(replacement);
                    old.Dispose();

                    _foregroundWakeRouteEpoch++;
                    _domProbeTask = null;
                    _domProbePending = false;
                    _pruneProbeTask = null;
                    _pruneProbePending = false;
                    _highProbePending = false;
                    InvalidateResponseProbeForRouteChange();
                    ResetCaptureForNewLocation();

                    bool started = await InitializeBrowserAsync(
                        recoveryUrl: previousUrl, recovering: true);
                    if (started)
                    {
                        LiteEventLog.Write("foreground_webview_recovered", new {
                            attempt, newController = true, workerUnaffected = true,
                            wakeScheduleChanged = false
                        });
                        return;
                    }
                    LiteEventLog.Write("foreground_webview_recovery_retry", new {
                        attempt, reason = "initialization_returned_false"
                    });
                }
                catch (Exception ex)
                {
                    LiteEventLog.Write("foreground_webview_recovery_retry", new {
                        attempt, errorType = ex.GetType().Name,
                        error = ex.Message, hresult = ex.HResult
                    });
                }
            }
            _status.Text = "WebView2: odzyskiwanie nieudane";
            LiteEventLog.Write("foreground_webview_recovery_exhausted", new {
                attempts = 3, workerUnaffected = true
            });
        }
        finally
        {
            _foregroundRecoveryInProgress = false;
        }
    }
}