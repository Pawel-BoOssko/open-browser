using System.Text.Json;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // Called from foreground diagnostics or old-chat preflight.
    // The same project-owned projects.json persists this incident over restarts.
    private void MarkWakeEmergency(string url, string error, string source)
    {
        try
        {
            if (!LiteWakeEmergencyTriggerPolicy.StartsAutomaticHandoff(error))
            {
                if (error is "chatgpt_app_problem" or "connection_interrupted")
                    LiteEventLog.Write("wake_ui_error_rotation_suppressed", new {
                        kind = error, source, rotationRequested = false
                    });
                return;
            }
            if (!LiteDevWakeState.IsChatUrl(url)) return;
            var currentUrl = LiteDevWakeState.NormalizeUrl(url);
            var entries = LiteChatWakeRegistry.Read();
            var due = entries.FirstOrDefault(x => x.Url == currentUrl && x.Enabled);
            if (due is null) return; // Explicit owner pause always wins.
            var project = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(
                p => p.Chats.Any(c => c.Role == "lead" && c.Url == currentUrl));
            if (project is null) return;
            if (due.EmergencyPhase.Length > 0) return;
            // Length exhaustion alone initiates the established HANDOFF flow.
            due.EmergencyReason = error;
            due.EmergencyPhase = "request_pending";
            due.EmergencyMessageId = "";
            due.EmergencySentUtc = default;
            due.EmergencyDeadlineUtc = default;
            due.EmergencyHandoffPath = "";
            due.DueUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            due.LastState = "emergency_" + due.EmergencyPhase;
            LiteChatWakeRegistry.Write(entries);
            LiteEventLog.Write("wake_emergency_detected",new {
                project=project.Key, wakeSequence=due.WakeSequence,
                kind=error, phase=due.EmergencyPhase, source
            });
            UpdateMultiWakeStatus();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("wake_emergency_detection_failed",new {
                errorType=ex.GetType().Name, kind=error
            });
        }
    }

    // Returns true if the regular scheduler must not send this tick.
    private async Task<bool> HandleEmergencyBeforeWakeAsync(
        LiteChatWakeRegistry.Chat due)
    {
        // The OBL engine owns a fresh local handoff/checkpoint snapshot.
        // Do not send a separate emergency message to an exhausted old chat,
        // or wait up to 30 minutes for its uncertain reply/file write.
        if (due.EmergencyPhase is "request_pending" or "waiting_handoff")
        {
            var owner = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(p =>
                p.Chats.Any(c => c.Role == "lead" && c.Url == due.Url));
            if (owner is not null && LiteWakeEmergency.SkipOldChatHandoffWait(
                    owner.Key, due.EmergencyPhase))
            {
                var entries = LiteChatWakeRegistry.Read();
                var active = entries.FirstOrDefault(c => c.Url == due.Url &&
                    c.Enabled && c.EmergencyPhase == due.EmergencyPhase);
                if (active is null) return true;
                string priorPhase = active.EmergencyPhase;
                active.EmergencyPhase = "rotate";
                active.EmergencyHandoffPath = "";
                active.DueUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                active.LastState = "emergency_direct_rotation";
                LiteChatWakeRegistry.Write(entries);
                due.EmergencyPhase = active.EmergencyPhase;
                due.EmergencyHandoffPath = "";
                LiteEventLog.Write("wake_emergency_direct_rotation", new {
                    project = owner.Key, wakeSequence = due.WakeSequence + 1,
                    previousPhase = priorPhase, waitSeconds = 0,
                    oldChatRequestMayHaveBeenAttempted =
                        priorPhase == "waiting_handoff",
                    engineSnapshotPlanned = true
                });
                return false;
            }
        }
        if (due.EmergencyPhase == "request_pending")
        {
            await RequestEmergencyHandoffAsync(due);
            return true;
        }
        if (due.EmergencyPhase != "waiting_handoff") return false;
        var project = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(
            p => p.Chats.Any(c => c.Role == "lead" && c.Url == due.Url));
        if (project is null) return true;
        string path = LiteWakeEmergency.CurrentHandoffPath(project.Key);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool available = LiteWakeEmergency.FreshHandoff(path,
            due.EmergencySentUtc, due.EmergencyMessageId, project.Key, due.Url);
        if (!available && LiteWakeEmergency.WaitForHandoffUntilDeadline(
            now, due.EmergencySentUtc, due.EmergencyDeadlineUtc))
        {
            var entries = LiteChatWakeRegistry.Read();
            var held = entries.FirstOrDefault(x => x.Url == due.Url && x.Enabled);
            if (held is not null && held.EmergencyPhase == "waiting_handoff")
            {
                held.DueUtc = now.AddSeconds(20);
                LiteChatWakeRegistry.Write(entries);
            }
            return true;
        }
        var latest = LiteChatWakeRegistry.Read();
        var entry = latest.FirstOrDefault(x => x.Url == due.Url && x.Enabled);
        if (entry is null) return true;
        entry.EmergencyPhase = "rotate";
        entry.EmergencyHandoffPath = available ? path : "";
        entry.DueUtc = now.AddSeconds(-1);
        entry.LastState = available ? "emergency_handoff_ready" :
            "emergency_handoff_timeout";
        LiteChatWakeRegistry.Write(latest);
        due.EmergencyPhase = entry.EmergencyPhase;
        due.EmergencyHandoffPath = entry.EmergencyHandoffPath;
        LiteEventLog.Write("wake_emergency_handoff_decided",new {
            project=project.Key, available, wakeSequence=due.WakeSequence,
            waitSeconds=due.EmergencySentUtc == default ? 0 :
                (int)(now-due.EmergencySentUtc).TotalSeconds
        });
        return false; // Next normal numbered wake immediately creates new lead.
    }

    private async Task RequestEmergencyHandoffAsync(
        LiteChatWakeRegistry.Chat due)
    {
        var project = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(
            p => p.Chats.Any(c => c.Role == "lead" && c.Url == due.Url));
        if (project is null) return;
        // Reserve one emergency request and its deadline BEFORE touching the
        // unreliable old-chat WebView2. A failed navigation must not leave
        // request_pending forever without a 30-minute escape path.
        var receipt = Guid.NewGuid().ToString("N");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        int ordinal = checked(due.EmergencyOrdinal + 1);
        string fullId = LiteWakeEmergency.MessageId(project.Key,
            due.WakeSequence, ordinal, started);
        string handoff = LiteWakeEmergency.CurrentHandoffPath(project.Key);
        string payload = "[OB-" + receipt + "] [OB-EMERGENCY-" +
            due.WakeSequence.ToString("D6") + "." + ordinal + "]\n" +
            "PEŁNY IDENTYFIKATOR WIADOMOŚCI AWARYJNEJ: " + fullId + "\n" +
            "PROJEKT: " + project.Key + "\n" +
            "CZAS WYSŁANIA UTC: " + started.ToString("O") + "\n" +
            "PILNE: Wykryto limit długości rozmowy ChatGPT. Nie rozpoczynaj " +
            "nowych zadań. Zapisz PEŁNY HANDOFF bezpośrednio na dysku SATANAMD: " +
            handoff + ". NADPISZ ten jeden bieżący plik. " +
            "Na początku pliku zapisz HANDOFF, projekt: " + project.Key +
            ", ostatnie zwykłe wybudzenie: " + due.WakeSequence +
            ", pełny identyfikator polecenia: " + fullId +
            ", czas zapisania pliku, host: SATANAMD, poprzedni URL czatu: " +
            due.Url + ". Dalej: aktualny stan, wykonane/niedokończone zadania, " +
            "pliki i testy, blokady, następny krok i źródła specyficzne " +
            "dla projektu. Dla OBL: Beads i aktualny checkpoint, LIVE/SOURCE. " +
            "Zweryfikuj zapis pliku. NIE CZEKAJ NA widoczną odpowiedź w ChatGPT. " +
            "Nie czytaj starych handoffów ani archiwum.";
        // Persist before any possible send click. After a crash NEVER resend
        // an uncertain emergency request; resume the same 30-minute deadline.
        var entries = LiteChatWakeRegistry.Read();
        var entry = entries.FirstOrDefault(x => x.Url == due.Url && x.Enabled);
        if (entry is null || entry.EmergencyPhase != "request_pending") return;
        entry.EmergencyPhase = "waiting_handoff";
        entry.EmergencyOrdinal = ordinal;
        entry.EmergencyMessageId = fullId;
        entry.EmergencySentUtc = started;
        entry.EmergencyDeadlineUtc = started.AddMinutes(30);
        entry.DueUtc = started.AddSeconds(20);
        entry.LastState = "emergency_handoff_requested_uncertain";
        LiteChatWakeRegistry.Write(entries);
        LiteEventLog.Write("wake_emergency_request_reserved",new {
            project=project.Key, fullId, wakeSequence=due.WakeSequence,
            deadlineUtc=entry.EmergencyDeadlineUtc
        });
        try
        {
            await EnsureWakeWorkerAsync();
            var core = _chatWakeWorker.CoreWebView2;
            if (core is null || !await NavigateWorkerAsync(due.Url))
            {
                // One-shot reservation remains durable. Do not repeat a
                // possibly delivered request. Rotate on its fixed deadline.
                LiteEventLog.Write("wake_emergency_worker_unreachable",new {
                    project=project.Key, fullId,
                    phase="waiting_handoff", deadlineUtc=entry.EmergencyDeadlineUtc,
                    noAutomaticResend=true
                });
                return;
            }
            var encoded = await ExecuteWorkerScriptAsync(core,
                "window.__OPENBROWSER_RESUME_SENDER__?.sendDirect(" +
                JsonSerializer.Serialize(payload) + "," +
                JsonSerializer.Serialize(receipt) + ",true," +
                JsonSerializer.Serialize(new Uri(due.Url).AbsolutePath) + ")");
            string state = JsonSerializer.Deserialize<string>(encoded) ?? "sender_missing";
            string phase = "not_submitted";
            // sendDirect prepares the draft; submitDirect does the actual click.
            if (state == "submission_requested")
            {
                for (int attempt = 0; attempt < 7; attempt++)
                {
                    await Task.Delay(attempt == 0 ? 750 : 400);
                    string result = await ExecuteWorkerScriptAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__?.submitDirect(" +
                        JsonSerializer.Serialize(receipt) + ")");
                    phase = JsonSerializer.Deserialize<string>(result) ?? "sender_missing";
                    if (phase is "click_invoked" or "form_submitted" or
                        "blocked_changed" or "blocked_navigation" or
                        "already_submitted") break;
                }
            }
            if (phase is "click_invoked" or "form_submitted")
                LiteWakeJournal.Write("submission_attempted",
                    due.WakeSequence, receipt, phase);
            LiteEventLog.Write("wake_emergency_request_submitted",new {
                project=project.Key, fullId, state, phase, mayHaveClicked=
                    phase is "click_invoked" or "form_submitted"
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("wake_emergency_request_unknown",new {
                project=project.Key, fullId, errorType=ex.GetType().Name,
                noAutomaticResend=true
            });
        }
    }
}
