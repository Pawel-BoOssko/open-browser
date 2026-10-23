using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly System.Windows.Forms.Timer _responseTimer = new() { Interval = 20000 };
    private bool _responseProbePending;
    private Task<string>? _responseProbeTask;
    // Navigation invalidates old in-flight probes without letting their
    // finally blocks disturb a newer document's probe.
    private long _responseProbeGeneration;
    private string _lastResponseFailure = "";
    private DateTimeOffset? _responseFailureSinceUtc;
    private DateTimeOffset _lastFailureObservationUtc;
    private bool _lastGenerationSignal;
    private DateTimeOffset? _responseProbeDelayedSinceUtc;
    private DateTimeOffset _lastResponseProbeStalledEventUtc;

    // Keep one outstanding renderer request, but do not leave a stale
    // "Generowanie" label indefinitely if that request never completes.
    internal static bool ResponseProbeIsStalled(
        DateTimeOffset? delayedSinceUtc, DateTimeOffset nowUtc) =>
        delayedSinceUtc.HasValue &&
        nowUtc - delayedSinceUtc.Value >= TimeSpan.FromSeconds(35);

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
        if (core is null || _responseProbePending) return;
        if (_responseProbeTask is { IsCompleted: false })
        {
            var nowUtc = DateTimeOffset.UtcNow;
            if (ResponseProbeIsStalled(_responseProbeDelayedSinceUtc, nowUtc))
            {
                if (!_status.Text.StartsWith("ChatGPT:", StringComparison.Ordinal) &&
                    !_status.Text.StartsWith("Błąd procesu strony", StringComparison.Ordinal))
                    _status.Text = "Renderer: brak odpowiedzi na odczyt";
                Text = "Open Browser Lite | Renderer opóźniony | " +
                    LiteBuildInfo.Display();
                if (nowUtc - _lastResponseProbeStalledEventUtc > TimeSpan.FromMinutes(2))
                {
                    _lastResponseProbeStalledEventUtc = nowUtc;
                    LiteEventLog.Write("response_probe_still_pending", new {
                        durationSeconds = (int)(nowUtc -
                            _responseProbeDelayedSinceUtc!.Value).TotalSeconds,
                        anotherProbeStarted = false
                    });
                }
            }
            return;
        }
        if (_responseProbeDelayedSinceUtc is DateTimeOffset delayedSince)
        {
            bool succeeded = _responseProbeTask?.IsCompletedSuccessfully == true;
            LiteEventLog.Write("response_probe_late_completion", new {
                elapsedSeconds = (int)(DateTimeOffset.UtcNow - delayedSince).TotalSeconds,
                succeeded
            });
            _responseProbeDelayedSinceUtc = null;
            // A delayed probe's result may be stale. The next fresh probe
            // will supply the correct generation/error signal.
            UpdateTaskbarTitle(_lastGenerationSignal, _lastResponseFailure);
        }
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) ||
            uri.Host != "chatgpt.com") return;
        _responseProbePending = true;
        long probeGeneration = ++_responseProbeGeneration;
        long routeEpoch = _foregroundWakeRouteEpoch;
        string routeSource = core.Source;
        try
        {
            var query = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_RESPONSE_SIGNALS__ ? JSON.stringify(window.__OPENBROWSER_RESPONSE_SIGNALS__.read()) : null");
            _responseProbeTask = query;
            var winner = await Task.WhenAny(query, Task.Delay(8000));
            if (winner != query)
            {
                _responseProbeDelayedSinceUtc = DateTimeOffset.UtcNow;
                LiteEventLog.Write("response_probe_delayed", new { beyondMs = 8000 });
                // Never keep the Windows UI await hanging. An unfinished
                // ExecuteScriptAsync stays tracked, so no second call piles up.
                return;
            }
            var raw = await query;
            // SPA history can switch A->B->A while an old script is in
            // flight. Never apply an obsolete failure or generation signal
            // to the newly selected conversation.
            if (probeGeneration != _responseProbeGeneration ||
                routeEpoch != _foregroundWakeRouteEpoch ||
                !string.Equals(routeSource, core.Source, StringComparison.Ordinal))
            {
                LiteEventLog.Write("response_probe_route_changed",
                    new { ignoredStaleResult = true });
                return;
            }
            string? inner = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrEmpty(inner)) return;
            using var json = JsonDocument.Parse(inner);
            var root = json.RootElement;
            string failure = root.GetProperty("visibleError").ValueKind == JsonValueKind.String
                ? root.GetProperty("visibleError").GetString() ?? "" : "";
            bool generating = root.GetProperty("generatingSignal").GetBoolean();
            string errorSource = root.TryGetProperty("errorSource", out var source)
                ? source.GetString() ?? "unknown" : "unknown";
            int ignoredHiddenAlerts = root.TryGetProperty("ignoredHiddenAlerts",
                out var hidden) && hidden.ValueKind == JsonValueKind.Number
                    ? hidden.GetInt32() : 0;
            int activeTurns = root.GetProperty("turnCount").GetInt32();
            int activeAssistants = root.GetProperty("assistantCount").GetInt32();
            string domVariant = root.TryGetProperty("domVariant", out var variant)
                ? variant.GetString() ?? "unknown" : "unknown";
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
                    turnCount = activeTurns, assistantCount = activeAssistants,
                    domVariant, errorSource, ignoredHiddenAlerts
                });
                _lastResponseFailure = failure;
                _responseFailureSinceUtc = failure.Length > 0 ? nowUtc : null;
                _lastFailureObservationUtc = nowUtc;
                UpdateTaskbarTitle(generating, failure);
                if (failure.Length > 0)
                    _status.Text = "ChatGPT: " + failure + " (dostarczenie niepotwierdzone)";
                else if (_status.Text.StartsWith("ChatGPT:", StringComparison.Ordinal))
                    _status.Text = generating ? "Generowanie..." : "Gotowe";
            }
            // The UI may retain a hidden stale error or may truly have a
            // failed answer despite repeated successful history GETs. Record
            // bounded DOM evidence, not the private response text or URL.
            if (failure.Length > 0 &&
                DateTimeOffset.UtcNow - _lastFailureObservationUtc >
                    TimeSpan.FromMinutes(2))
            {
                _lastFailureObservationUtc = DateTimeOffset.UtcNow;
                LiteEventLog.Write("response_error_still_visible", new {
                    state = failure, errorSource, ignoredHiddenAlerts,
                    turnCount = activeTurns, assistantCount = activeAssistants,
                    domVariant, documentReady = root.GetProperty("documentReady").GetString(),
                    generating, hasComposer = root.GetProperty("hasComposer").GetBoolean()
                });
            }
            if (generating != _lastGenerationSignal)
            {
                LiteEventLog.Write("response_generation_transition", new {
                    generating, hasComposer = root.GetProperty("hasComposer").GetBoolean(),
                    assistantCount = root.GetProperty("assistantCount").GetInt32()
                });
                _lastGenerationSignal = generating;
                UpdateTaskbarTitle(generating, failure);
                if (failure.Length == 0 &&
                    !_status.Text.StartsWith("Błąd procesu strony", StringComparison.Ordinal))
                    _status.Text = generating ? "Generowanie..." : "Gotowe";
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("response_probe_failed", new { errorType = ex.GetType().Name });
        }
        finally
        {
            if (probeGeneration == _responseProbeGeneration)
            {
                _responseProbePending = false;
                if (_responseProbeTask is { IsCompleted: true })
                    _responseProbeTask = null;
            }
        }
    }
}
