namespace BridgeBrowserAlpha0;

internal static class LiteDeliveryEvidenceSelfTest
{
    internal static int Run()
    {
        try
        {
            static LiteDeliveryEvidence.Observation O(
                bool? renderer = null, bool? js = null, bool? visible = null,
                bool? saved = null, int? http = null, string? error = null,
                bool? generating = null) =>
                new(renderer, js, visible, saved, http, error, generating);

            var saved = LiteDeliveryEvidence.Classify(O(visible: true, saved: true));
            Check(saved.Code == "visible_and_archived" &&
                saved.ContentLocallyRecoverable, "archived visible");
            Check(!saved.ServerDeliveryProven, "no server delivery inference");
            Check(LiteDeliveryEvidence.Classify(O(visible: false, saved: true)).Code ==
                "archived_but_not_visible", "missing render with local archive");
            Check(LiteDeliveryEvidence.Classify(O(renderer: false)).Code ==
                "renderer_or_js_unresponsive", "renderer failure");
            Check(LiteDeliveryEvidence.Classify(O(js: false)).Code ==
                "renderer_or_js_unresponsive", "JS failure");
            var savedThenHung = LiteDeliveryEvidence.Classify(O(
                renderer: false, visible: true, saved: true, http: 200));
            Check(savedThenHung.Code == "renderer_or_js_unresponsive" &&
                savedThenHung.ContentLocallyRecoverable &&
                !savedThenHung.ServerDeliveryProven,
                "previous snapshot must not hide current renderer failure");
            Check(LiteDeliveryEvidence.Classify(O(http: 502)).Code ==
                "backend_http_error_observed", "HTTP 502");
            var archivedButHttpFail = LiteDeliveryEvidence.Classify(O(
                visible: true, saved: true, http: 502));
            Check(archivedButHttpFail.Code == "backend_http_error_observed" &&
                archivedButHttpFail.ContentLocallyRecoverable &&
                !archivedButHttpFail.ServerDeliveryProven, "HTTP failure precedes saved snippet");
            var archivedButUiFail = LiteDeliveryEvidence.Classify(O(
                visible: true, saved: true, error: "delivery_timeout"));
            Check(archivedButUiFail.Code == "ui_failure_reported" &&
                archivedButUiFail.ContentLocallyRecoverable,
                "visible page error precedes saved snippet");
            Check(LiteDeliveryEvidence.Classify(O(http: 200)).Code ==
                "insufficient_evidence", "HTTP 200 not proof of answer");
            Check(LiteDeliveryEvidence.Classify(O(error: "delivery_timeout")).Code ==
                "ui_failure_reported", "visible delivery error");
            Check(LiteDeliveryEvidence.Classify(O(visible: true, saved: false)).Code ==
                "visible_not_archived", "missing local backup");
            Check(LiteDeliveryEvidence.Classify(O(visible: true)).Code ==
                "insufficient_evidence", "unknown backup is not known missing");
            Check(LiteDeliveryEvidence.Classify(O(generating: true)).Code ==
                "generation_in_progress", "no early completion");
            Check(LiteDeliveryEvidence.Classify(O()).Confidence == "unknown",
                "unknown remains unknown");
            LiteEventLog.Write("delivery_evidence_selftest", new { passed = true, checks = 12 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("delivery_evidence_selftest", new {
                passed = false, errorType = ex.GetType().Name
            });
            return 2;
        }
    }

    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException("Delivery evidence selftest: " + description);
    }
}
