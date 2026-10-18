namespace BridgeBrowserAlpha0;

/// <summary>
/// Local evidence classifier. Never claims a server generated or delivered
/// content without an independently verifiable server acknowledgement.
/// </summary>
internal static class LiteDeliveryEvidence
{
    internal sealed record Observation(
        bool? RendererResponsive, bool? JavaScriptResponsive,
        bool? AnswerVisible, bool? SnapshotPersisted,
        int? BackendHttpStatus, string? VisibleError,
        bool? GenerationActive);

    internal sealed record Assessment(
        string Code, string Confidence, bool ContentLocallyRecoverable,
        bool ServerDeliveryProven, string NextDiagnostic);

    internal static Assessment Classify(Observation o)
    {
        bool saved = o.SnapshotPersisted == true;
        // Current renderer/JS failure takes precedence over an earlier local
        // snapshot: a stale saved fragment cannot prove the current response.
        if (o.RendererResponsive == false || o.JavaScriptResponsive == false)
            return new("renderer_or_js_unresponsive", "medium_local", saved, false,
                "Check WebView2 process lifecycle and memory pressure");
        if (saved && o.AnswerVisible == true)
            return new("visible_and_archived", "high_local", true, false,
                "Compare later chat render with encrypted snapshot digest");
        if (saved && o.AnswerVisible == false)
            return new("archived_but_not_visible", "high_local", true, false,
                "Inspect encrypted local archive and renderer state");
        if (o.BackendHttpStatus is >= 400 and <= 599)
            return new("backend_http_error_observed", "high_http", saved, false,
                "Correlate endpoint metadata and request timing");
        if (o.VisibleError is "response_load_failed" or "delivery_timeout" or
            "reasoning_interrupted")
            return new("ui_failure_reported", "high_ui", saved, false,
                "Correlate UI error with transport and renderer signals");
        if (o.AnswerVisible == true && o.SnapshotPersisted == false)
            return new("visible_not_archived", "medium_local", false, false,
                "Check capture timer and DPAPI save failures");
        if (o.GenerationActive == true)
            return new("generation_in_progress", "medium_ui", saved, false,
                "Do not infer completion or auto-resubmit");
        return new("insufficient_evidence", "unknown", saved, false,
            "Wait for additional independent observations");
    }
}
