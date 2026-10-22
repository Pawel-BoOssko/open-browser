using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly System.Windows.Forms.Timer _responseTimer = new() { Interval = 20000 };
    private bool _responseProbePending;
    private Task<string>? _responseProbeTask;
    private string _lastResponseFailure = "";
    private DateTimeOffset? _responseFailureSinceUtc;
    private bool _lastGenerationSignal;

    private async Task InstallResponseDiagnosticsAsync(CoreWebView2 core)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "LiteResponseSignal.js");
        await core.AddScriptToExecuteOnDocumentCreatedAsync(
            await File.ReadAllTextAsync(file));

        core.WebResourceResponseReceived += (_, e) =>
            LogBackendResponseMetadata(e, "foreground");
        _responseTimer.Tick += async (_, _) => await PollResponseDiagnosticsAsync();
        LiteEventLog.Write("response_signals_installed");
    }

    private void UpdateTaskbarTitle(bool generating, string failure)
    {
        string state = failure.Length != 0 ? "Błąd: " + failure :
            generating ? "Generowanie" : "Gotowe";
        Text = "Open Browser Lite | " + state + " | " + LiteBuildInfo.Display();
    }

    private async Task PollResponseDiagnosticsAsync()
    {
        var core = _browser.CoreWebView2;
        if (core is null || _responseProbePending ||
            _responseProbeTask is { IsCompleted: false }) return;
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) ||
            uri.Host != "chatgpt.com") return;
        _responseProbePending = true;
        try
        {
            var query = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_RESPONSE_SIGNALS__ ? JSON.stringify(window.__OPENBROWSER_RESPONSE_SIGNALS__.read()) : null");
            _responseProbeTask = query;
            var winner = await Task.WhenAny(query, Task.Delay(8000));
            if (winner != query)
            {
                LiteEventLog.Write("response_probe_delayed", new { beyondMs = 8000 });
                // Never keep the Windows UI await hanging. An unfinished
                // ExecuteScriptAsync stays tracked, so no second call piles up.
                return;
            }
            var raw = await query;
            string? inner = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrEmpty(inner)) return;
            using var json = JsonDocument.Parse(inner);
            var root = json.RootElement;
            string failure = root.GetProperty("visibleError").ValueKind == JsonValueKind.String
                ? root.GetProperty("visibleError").GetString() ?? "" : "";
            bool generating = root.GetProperty("generatingSignal").GetBoolean();
            // Quarantine per chat on EVERY observation, not just a failure
            // transition. The same error can be visible in two different chats.
            if ((failure is "delivery_timeout" or "response_load_failed") &&
                Uri.TryCreate(core.Source, UriKind.Absolute, out var currentUri) &&
                currentUri.Host == "chatgpt.com" &&
                currentUri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase))
            {
                string chatKey = LiteChatNotes.NormalizeKey(core.Source);
                if (_resumeLedger.QuarantineTransport(chatKey, failure))
                    LiteEventLog.Write("resume_transport_quarantined", new {
                        errorKind = failure, requiresManualReview = true
                    });
                _resumeEnabled = false;
                _resumeToggle.Text = "Nadzór: WYŁ.";
                _resumeStatus.Text = "Nadzór: blokada po błędzie dostarczenia";
            }
            if (failure != _lastResponseFailure)
            {
                var assessment = LiteDeliveryEvidence.Classify(new(
                    RendererResponsive: true, JavaScriptResponsive: true,
                    AnswerVisible: null, SnapshotPersisted: null,
                    BackendHttpStatus: null,
                    VisibleError: failure.Length == 0 ? null : failure,
                    GenerationActive: generating));
                var nowUtc = DateTimeOffset.UtcNow;
                double? displayedSeconds = failure.Length == 0 && _responseFailureSinceUtc.HasValue
                    ? Math.Round((nowUtc - _responseFailureSinceUtc.Value).TotalSeconds)
                    : null;
                LiteEventLog.Write("response_error_transition", new
                {
                    state = failure.Length == 0 ? "cleared" : failure,
                    generating,
                    evidence = assessment.Code,
                    confidence = assessment.Confidence,
                    serverDeliveryProven = assessment.ServerDeliveryProven,
                    displayDurationSeconds = displayedSeconds,
                    phase = "visible_page_error_only",
                    turnCount = root.GetProperty("turnCount").GetInt32()
                });
                _lastResponseFailure = failure;
                _responseFailureSinceUtc = failure.Length > 0 ? nowUtc : null;
                UpdateTaskbarTitle(generating, failure);
                if (failure.Length > 0)
                    _status.Text = "ChatGPT: " + failure + " (dostarczenie niepotwierdzone)";
                else if (_status.Text.StartsWith("ChatGPT:", StringComparison.Ordinal))
                    _status.Text = "Gotowe";
            }
            if (generating != _lastGenerationSignal)
            {
                LiteEventLog.Write("response_generation_transition", new {
                    generating, hasComposer = root.GetProperty("hasComposer").GetBoolean(),
                    assistantCount = root.GetProperty("assistantCount").GetInt32()
                });
                _lastGenerationSignal = generating;
                UpdateTaskbarTitle(generating, failure);
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("response_probe_failed", new { errorType = ex.GetType().Name });
        }
        finally
        {
            _responseProbePending = false;
            if (_responseProbeTask is { IsCompleted: true })
                _responseProbeTask = null;
        }
    }
}
