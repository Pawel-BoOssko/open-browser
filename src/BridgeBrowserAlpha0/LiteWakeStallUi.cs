using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private static bool IsStopPresentScriptState(string state) =>
        state is "clicked" or "not_available";

    // A lead ending on an unanswered user turn is stalled even if the
    // renderer no longer reports generating. Do not apply this to other chats.
    internal static bool IsStalledLeadReason(string reason, bool isProjectLead) =>
        reason == "generating" ||
        (isProjectLead && reason == "assistant_not_complete");

    internal static bool HasStallPassedSecondSlot(
        DateTimeOffset firstDue, DateTimeOffset now, int minutes) =>
        firstDue != default && minutes > 0 &&
        now >= firstDue.AddMinutes(minutes);

    internal static int StallRecoveryMinutes(bool isOblLead, int configuredMinutes) =>
        isOblLead ? 2 : configuredMinutes;

    // STOP only if the actual active chat has a visible button.
    private async Task<string> StopGeneratingForOverdueAsync(CoreWebView2 core)
    {
        const string js = """
            (() => {
              const candidates=[...document.querySelectorAll(
                '[data-testid="stop-button"],button[aria-label="Stop generating"],' +
                'button[aria-label="Przerwij generowanie"],button[aria-label="Zatrzymaj"]')];
              const btn=candidates.find(e => !e.disabled && !e.hidden &&
                (!e.getClientRects || e.getClientRects().length>0));
              if(!btn) return "not_available";
              btn.click();
              return "clicked";
            })()
            """;
        try
        {
            string result = await ExecuteWorkerScriptAsync(core, js);
            string state = JsonSerializer.Deserialize<string>(result) ?? "unknown";
            LiteEventLog.Write("wake_overdue_stop",new {state});
            return state;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("wake_overdue_stop_failed", new {
                errorType=ex.GetType().Name
            });
            return "unknown";
        }
    }

    // Runs with a verified worker pointed to the current old lead chat.
    // True means defer this wake; false means proceed (possibly forced).
    private async Task<bool> HandleStalledLeadAsync(
        LiteChatWakeRegistry.Chat due, CoreWebView2 core, string reason)
    {

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool isOblLead = LiteProjectRegistry.OpenDefault().Projects.Any(p =>
            p.Key == "obl" && p.Chats.Any(c =>
                c.Role == "lead" && c.Url == due.Url));
        // P0 obl-5w3 wake358: NIEAKTUALNE for OBL: waiting for a generating
        // lead and clicking STOP. Preserve the old turn; rotate instead.
        if (isOblLead && reason == "generating")
        {
            var entries = LiteChatWakeRegistry.Read();
            var active = entries.FirstOrDefault(c => c.Url == due.Url && c.Enabled);
            if (active is not null && active.EmergencyPhase.Length == 0)
            {
                active.EmergencyPhase = "rotate";
                active.EmergencyReason = "busy_generating_separate_lead";
                active.EmergencyHandoffPath = "";
                active.StallFirstDueUtc = default;
                active.StallPhase = "";
                active.DueUtc = now.AddSeconds(-1);
                active.LastState = "recovery_rotating_busy_generating";
                LiteChatWakeRegistry.Write(entries);
                LiteWakeJournal.Write("deferred", active.WakeSequence + 1,
                    result: "busy_generating_rotate");
                LiteEventLog.Write("multi_wake_busy_editor_rotate", new {
                    wakeSequence = active.WakeSequence + 1, stopClicked = false,
                    oldChatPreserved = true });
                UpdateMultiWakeStatus();
            }
            return true;
        }
        if (due.StallPhase == "force") return false;
        // P0 obl-5w3 2026-11-02: NIEAKTUALNE for OBL: a user turn
        // without an assistant reply alone required a two-minute stall.
        // Attempt the numbered wake immediately. The direct sender checks
        // the exact chat URL, live Stop button and untouched editor draft.
        if (isOblLead && reason == "assistant_not_complete") return false;
        bool projectLeadPendingAnswer = reason == "assistant_not_complete" &&
            LiteProjectRegistry.OpenDefault().Projects.Any(p =>
                p.Chats.Any(c => c.Role == "lead" && c.Url == due.Url));
        if (!IsStalledLeadReason(reason, projectLeadPendingAnswer))
        {
            // Recovered before the second missed slot: normal operation.
            if (due.StallFirstDueUtc != default)
            {
                var entries = LiteChatWakeRegistry.Read();
                var row = entries.FirstOrDefault(x => x.Url == due.Url);
                if (row is not null)
                {
                    row.StallFirstDueUtc = default;
                    LiteChatWakeRegistry.Write(entries);
                }
            }
            return false;
        }
        if (due.StallFirstDueUtc == default)
        {
            var entries = LiteChatWakeRegistry.Read();
            var row = entries.FirstOrDefault(x => x.Url == due.Url && x.Enabled);
            if (row is not null)
            {
                row.StallFirstDueUtc = due.DueUtc;
                row.DueUtc = now.AddMinutes(2);
                row.LastState = reason == "assistant_not_complete"
                    ? "waiting_assistant_not_complete"
                    : "waiting_generating";
                LiteChatWakeRegistry.Write(entries);
            }
            return true;
        }
        // OBL: one bounded two-minute wait. Other projects retain their
        // configured supervision window. Never defer OBL for two full slots.
        int stallWindowMinutes = StallRecoveryMinutes(isOblLead, due.EffectiveMinutes);
        if (!HasStallPassedSecondSlot(due.StallFirstDueUtc, now, stallWindowMinutes))
        {
            // A pending assistant turn uses the short stall-recovery cadence,
            // not the regular 15-minute assistant_not_complete deferral.
            DeferWakeWithoutSend(due.Url, now,
                reason == "assistant_not_complete" ? "stall_pending_answer" : "generating", 2);
            return true;
        }
        // Bounded stall window expired: one STOP if available, then
        // immediately continue the numbered wake. No diagnostic message
        // and no additional waiting phase.
        string stopResult = await StopGeneratingForOverdueAsync(core);
        var currentEntries = LiteChatWakeRegistry.Read();
        var current = currentEntries.FirstOrDefault(x => x.Url == due.Url && x.Enabled);
        if (current is null) return true;
        current.StallPhase = "force";
        current.DueUtc = now.AddSeconds(-1);
        LiteChatWakeRegistry.Write(currentEntries);
        due.StallPhase = "force";
        LiteEventLog.Write("wake_stall_force_continuation", new {
            sequence=due.WakeSequence+1, stopResult,
            waitedSlots=2, diagnosticSent=false });
        return false;
    }
}
