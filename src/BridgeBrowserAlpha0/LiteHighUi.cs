using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Checks ChatGPT's actual model-effort picker. Never equates a text instruction
/// to changing the selected effort. If verification fails, resume remains gated.
/// </summary>
internal sealed partial class ChatGptLiteForm
{
    private readonly Label _highIndicator = new()
    {
        AutoSize = true, Text = "Tryb: sprawdzanie",
        Padding = new Padding(8, 8, 0, 0),
        ForeColor = Color.DarkRed
    };
    private readonly System.Windows.Forms.Timer _highTimer = new() { Interval = 6000 };
    private bool _highProbePending;
    private string _lastEffort = "";
    private string _effortChangeFrom = "";
    private DateTimeOffset _lastHighSwitch = DateTimeOffset.MinValue;

    private void InitializeHighUi()
    {
        _highTimer.Tick += async (_, _) => await VerifyHighAsync();
    }

    private async Task InstallHighScriptAsync(CoreWebView2 core)
    {
        string script = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "LiteHighGuard.js"));
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        LiteEventLog.Write("high_guard_installed");
    }

    private async Task VerifyHighAsync()
    {
        var core = _browser.CoreWebView2;
        if (core is null || _highProbePending) return;
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var address) ||
            address.Host != "chatgpt.com") return;
        _highProbePending = true;
        try
        {
            var query = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_HIGH_GUARD__ ? JSON.stringify(window.__OPENBROWSER_HIGH_GUARD__.audit()) : null");
            if (await Task.WhenAny(query, Task.Delay(4000)) != query)
            {
                _highIndicator.Text = "Tryb: BRAK ODPOWIEDZI";
                LiteEventLog.Write("high_guard_probe_timeout");
                _resumeEnabled = false;
                _resumeToggle.Text = "Nadzór: WYŁ.";
                return;
            }
            string? serialized = JsonSerializer.Deserialize<string>(await query);
            if (string.IsNullOrEmpty(serialized)) return;
            using var parsed = JsonDocument.Parse(serialized);
            string mode = parsed.RootElement.GetProperty("mode").GetString() ?? "unknown";
            if (mode != _lastEffort)
            {
                LiteEventLog.Write("high_guard_mode_changed", new {
                    current = mode, previous = _lastEffort
                });
                _lastEffort = mode;
            }

            if (mode is "high" or "extra high")
            {
                _highIndicator.ForeColor = Color.DarkGreen;
                _highIndicator.Text = "Tryb: HIGH";
                if (_effortChangeFrom.Length > 0)
                {
                    LiteEventLog.Write("high_guard_switch_confirmed", new {
                        previous = _effortChangeFrom, actual = mode
                    });
                    QueueHighReview(_effortChangeFrom, core.Source);
                    _effortChangeFrom = "";
                }
                await TryHighReviewAsync(core);
                return;
            }

            _highIndicator.ForeColor = Color.DarkRed;
            _highIndicator.Text = "Tryb: " + mode.ToUpperInvariant() + " — sprawdzam";
            if (mode is not ("instant" or "medium")) return;
            if (DateTimeOffset.UtcNow - _lastHighSwitch < TimeSpan.FromSeconds(20))
                return;

            // Known low effort: request the real selector in the WebView2 UI.
            // We do NOT claim the mode changed until the next audit confirms it.
            _effortChangeFrom = mode;
            _lastHighSwitch = DateTimeOffset.UtcNow;
            var switchTask = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_HIGH_GUARD__ ? JSON.stringify(window.__OPENBROWSER_HIGH_GUARD__.switchHigh()) : null");
            if (await Task.WhenAny(switchTask, Task.Delay(4000)) != switchTask)
            {
                LiteEventLog.Write("high_guard_switch_timeout");
                return;
            }
            string? attempted = JsonSerializer.Deserialize<string>(await switchTask);
            LiteEventLog.Write("high_guard_attempt", new {
                previous = mode,
                resultType = attempted?.Contains("selection_requested", StringComparison.Ordinal) == true
                    ? "selection_requested" : "no_verified_selection"
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("high_guard_failed", new {
                errorType = ex.GetType().Name
            });
        }
        finally { _highProbePending = false; }
    }
}
