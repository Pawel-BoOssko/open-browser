using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly System.Windows.Forms.Timer _responseTimer = new() { Interval = 20000 };
    private bool _responseProbePending;
    private string _lastResponseFailure = "";
    private DateTimeOffset? _responseFailureSinceUtc;
    private bool _lastGenerationSignal;

    private async Task InstallResponseDiagnosticsAsync(CoreWebView2 core)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "LiteResponseSignal.js");
        await core.AddScriptToExecuteOnDocumentCreatedAsync(
            await File.ReadAllTextAsync(file));

        core.WebResourceResponseReceived += (_, e) =>
        {
            try
            {
                if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) ||
                    uri.Host != "chatgpt.com" ||
                    !uri.AbsolutePath.StartsWith("/backend-api/", StringComparison.OrdinalIgnoreCase))
                    return;
                int status = e.Response.StatusCode;
                bool conversationEndpoint = uri.AbsolutePath.Contains("conversation", StringComparison.OrdinalIgnoreCase);
                if (status < 400 && !conversationEndpoint) return;
                LiteEventLog.Write("web_backend_response", new {
                    endpoint = conversationEndpoint ? "conversation" : "other_backend",
                    status, successHttp = status is >= 200 and < 400
                });
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("response_metadata_error", new { errorType = ex.GetType().Name });
            }
        };
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
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri) ||
            uri.Host != "chatgpt.com") return;
        _responseProbePending = true;
        try
        {
            var query = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_RESPONSE_SIGNALS__ ? JSON.stringify(window.__OPENBROWSER_RESPONSE_SIGNALS__.read()) : null");
            var winner = await Task.WhenAny(query, Task.Delay(8000));
            if (winner != query)
            {
                LiteEventLog.Write("response_probe_delayed", new { beyondMs = 8000 });
            }
            // Keep this probe single-flight even when WebView's JS is hung.
            var raw = await query;
            string? inner = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrEmpty(inner)) return;
            using var json = JsonDocument.Parse(inner);
            var root = json.RootElement;
            string failure = root.GetProperty("visibleError").ValueKind == JsonValueKind.String
                ? root.GetProperty("visibleError").GetString() ?? "" : "";
            bool generating = root.GetProperty("generatingSignal").GetBoolean();
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
        finally { _responseProbePending = false; }
    }
}
