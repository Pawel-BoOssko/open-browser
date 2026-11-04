using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

// Recovery after project lead role was saved but before successor wake metadata.
internal static class LitePromotedRotationRecoveryPolicy
{
    internal static bool CanFinalize(string leadUrl, string serverUrl,
        string phase, string receipt, DateTimeOffset startedUtc) =>
        phase == "verified" &&
        !string.IsNullOrWhiteSpace(receipt) &&
        startedUtc != default &&
        LiteHandoffPersistedRoute.IsServerConversation(leadUrl) &&
        string.Equals(leadUrl, serverUrl, StringComparison.Ordinal);
}

internal sealed partial class ChatGptLiteForm
{
    // Called before any attempt to deliver wake N*10 to the old lead.
    // Returns true whenever the old chat MUST NOT receive the due wake.
    private async Task<bool> RotateLeadBeforeWakeAsync(LiteChatWakeRegistry.Chat due)
    {
        // Explicit owner lead takeover at the 9->0 boundary: the existing
        // verified chat receives N+1. Never create a second successor.
        const string ownerFirstWakePrefix = "owner_manual_first_wake_";
        if (due.RotationPhase.StartsWith(ownerFirstWakePrefix, StringComparison.Ordinal))
        {
            if (long.TryParse(due.RotationPhase[ownerFirstWakePrefix.Length..], out var manuallyTransferredAt)
                && manuallyTransferredAt == due.WakeSequence)
                return false;
            var ownerSchedules = LiteChatWakeRegistry.Read();
            var ownerSchedule = ownerSchedules.SingleOrDefault(x => x.Url == due.Url);
            if (ownerSchedule is not null && ownerSchedule.RotationPhase == due.RotationPhase)
            {
                ownerSchedule.RotationPhase = "";
                LiteChatWakeRegistry.Write(ownerSchedules);
            }
            due.RotationPhase = "";
        }
        // Crash after lead promotion: finalize existing verified seed, not resend.
        if (LitePromotedRotationRecoveryPolicy.CanFinalize(
                due.Url, due.RotationServerUrl, due.RotationPhase,
                due.RotationReceipt, due.RotationStartedUtc) &&
            due.Enabled &&
            LiteProjectRegistry.OpenDefault().Projects.Any(p =>
                p.Chats.Any(c => c.Role == "lead" && c.Url == due.Url)))
        {
            CompletePromotedRotationWakeSchedule(due.Url);
            LiteEventLog.Write("rotation_promoted_state_recovered",
                new { newUrl = due.Url, once = true, newWake = due.WakeSequence + 1 });
            return true;
        }
        if (!LiteWakeRotation.BeforeNewLeadWake(due.WakeSequence) &&
            due.EmergencyPhase != "rotate") return false;
        var owner = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(
            p => p.Chats.Any(c => c.Role=="lead" && c.Url==due.Url));
        if (owner is null) return false;
        if (!due.Enabled) return true;
        try
        {
            if (due.RotationPhase=="verified" &&
                LiteHandoffPersistedRoute.IsServerConversation(due.RotationServerUrl))
            {
                PromoteRotation(due.Url,due.RotationServerUrl);
                return true;
            }
            await EnsureWakeWorkerAsync();
            var core=_chatWakeWorker.CoreWebView2
                ?? throw new InvalidOperationException("No wake worker for rotation");
            if (!LiteWakeRotation.RequiresOldLeadPreflightForSuccessor(owner.Key) &&
                (due.RotationPhase is "" or "preparing"))
                LiteEventLog.Write("rotation_old_lead_gate_bypassed", new {
                    project=owner.Key, nextWake=due.WakeSequence+1,
                    reason="separate_successor_composer", noOldLeadMutation=true
                });
            if ((due.RotationPhase is "" or "preparing") &&
                due.EmergencyPhase == "" &&
                LiteWakeRotation.RequiresOldLeadPreflightForSuccessor(owner.Key))
            {
                // Non-OBL projects retain the old-chat probe. For OBL,
                // create the separate successor without inspecting, stopping
                // or overwriting an uncertain old lead or its draft.
                if (!LiteDevWakeState.IsChatUrl(core.Source) ||
                    LiteDevWakeState.NormalizeUrl(core.Source) != due.Url)
                {
                    if (!await NavigateWorkerAsync(due.Url))
                    {
                        DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                            "rotation_navigation_failed");
                        return true;
                    }
                }
                var oldProbe = await CheckWorkerPreflightAsync(
                    core, due.Url, due.LastAnswerFingerprint);
                if (oldProbe.Reason is "conversation_length_exceeded" or
                    "chatgpt_app_problem" or "connection_interrupted")
                {
                    MarkWakeEmergency(due.Url, oldProbe.Reason, "rotation_preflight");
                    return true;
                }
                if (await HandleStalledLeadAsync(due, core, oldProbe.Reason))
                    return true;
                if (LiteWakeRotation.MayRotatePastUnavailableOldLead(
                    oldProbe.Reason, oldProbe.DraftPresent))
                    LiteEventLog.Write("rotation_old_lead_unavailable", new {
                        nextWake=due.WakeSequence+1, reason=oldProbe.Reason,
                        oldLeadSend=false, nextLeadRequired=true });
                if (oldProbe.Reason != "ready" &&
                    !LiteWakeRotation.MayRotatePastUnavailableOldLead(
                        oldProbe.Reason, oldProbe.DraftPresent) &&
                    due.StallPhase != "force")
                {
                    DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                        oldProbe.Reason);
                    return true;
                }
            }
            if (due.RotationPhase=="send_uncertain")
            {
                // A completed successor journal plus the saved real server
                // route is independent evidence; promote without a 20-second
                // navigation that could fail on a damaged renderer.
                bool savedRouteMatches = LiteSuccessorJournalProof.CanPromotePersistedRoute(
                    due.Url, due.RotationServerUrl,
                    LiteHandoffPersistedRoute.IsServerConversation(due.RotationServerUrl) &&
                    LiteSuccessorJournalProof.VerifyPersisted(
                        due.RotationReceipt, due.WakeSequence+1,
                        due.RotationStartedUtc));
                if (savedRouteMatches)
                {
                    MarkAndPromote(due.Url,
                        LiteDevWakeState.NormalizeUrl(due.RotationServerUrl),
                        due.RotationReceipt);
                    LiteWakeJournal.Write("cross_source_evidence",
                        due.WakeSequence+1, due.RotationReceipt, "chat_finished");
                    LiteEventLog.Write("rotation_successor_journal_promoted_without_navigation",
                        new {wakeSequence=due.WakeSequence+1,
                            source="saved_server_route_and_started_finished_journal",
                            noResend=true, noWebViewNavigation=true,
                            domReceiptClaimed=false});
                    return true;
                }                // A click may already have created a conversation. NEVER resend.
                if (LiteHandoffPersistedRoute.IsServerConversation(due.RotationServerUrl) &&
                    core.Source!=due.RotationServerUrl)
                {
                    var nav=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    void Loaded(object? _,CoreWebView2NavigationCompletedEventArgs e)=>nav.TrySetResult(e.IsSuccess);
                    core.NavigationCompleted+=Loaded;
                    try { core.Navigate(due.RotationServerUrl);
                        await nav.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
                    finally { core.NavigationCompleted-=Loaded; }
                    await Task.Delay(1400);
                }
                bool exactServerRoute =
                    LiteHandoffPersistedRoute.IsServerConversation(core.Source) &&
                    core.Source!=due.Url &&
                    LiteDevWakeState.NormalizeUrl(core.Source)==due.RotationServerUrl;
                // The successor has independently written both start and finish
                // under the reserved one-shot receipt. This proves reception
                // even if WebView2 lost the DOM after a watchdog restart.
                bool chatProof = exactServerRoute &&
                    LiteSuccessorJournalProof.VerifyPersisted(
                        due.RotationReceipt,due.WakeSequence+1,
                        due.RotationStartedUtc);
                bool domProof = exactServerRoute && !chatProof &&
                    await VerifySeedAsync(core,due.RotationReceipt,
                        due.RotationSeedSha256,due.WakeSequence+1,
                        allowPendingAssistant: string.Equals(owner.Key, "obl", StringComparison.OrdinalIgnoreCase));
                if (chatProof || domProof)
                {
                    MarkAndPromote(due.Url,
                        LiteDevWakeState.NormalizeUrl(core.Source),
                        due.RotationReceipt);
                    if (chatProof)
                    {
                        LiteWakeJournal.Write("cross_source_evidence",
                            due.WakeSequence+1,due.RotationReceipt,"chat_finished");
                        LiteEventLog.Write("rotation_successor_chat_evidence",new {
                            wakeSequence=due.WakeSequence+1,
                            source="chat_started_finished_and_reserved_server_url",
                            noResend=true,domReceiptClaimed=false
                        });
                    }
                    else if (domProof)
                    {
                        LiteWakeJournal.Write("receipt_confirmed", due.WakeSequence+1,
                            due.RotationReceipt, string.Equals(owner.Key, "obl", StringComparison.OrdinalIgnoreCase) ? "server_bubble_confirmed" : "visible_ack");
                        LiteEventLog.Write("rotation_successor_dom_evidence", new {
                            wakeSequence=due.WakeSequence+1,
                            source=string.Equals(owner.Key, "obl", StringComparison.OrdinalIgnoreCase) ? "stable_server_user_bubble_obl" : "stable_server_user_bubble_and_assistant_start",
                            noResend=true, domReceiptClaimed=true });
                    }
                }
                else
                {
                    var now = DateTimeOffset.UtcNow;
                    bool retryEligible = !(string.Equals(owner.Key, "obl", StringComparison.OrdinalIgnoreCase) && LiteHandoffPersistedRoute.IsServerConversation(due.RotationServerUrl)) && LiteRotationRetryPolicy.MayReprepare(
                        due.RotationPhase, due.RotationStartedUtc, now,
                        hasConfirmedProof: false,
                        isOblLead: string.Equals(owner.Key, "obl",
                            StringComparison.OrdinalIgnoreCase), savedServerUrl: due.RotationServerUrl);
                    LiteEventLog.Write("rotation_uncertain_needs_inspection",new {
                        priorWake=due.WakeSequence, receipt=due.RotationReceipt,
                        serverRouteMatched=exactServerRoute,
                        retryEligible, noOldLeadWake=true
                    });
                    if (retryEligible)
                        TryRearmUncertainRotation(due, owner.Key, now);
                }
                return true;
            }
            if(due.RotationPhase is not ("" or "preparing")) return true;
            var navigation = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void Completed(object? _,CoreWebView2NavigationCompletedEventArgs e)
                => navigation.TrySetResult(e.IsSuccess);
            core.NavigationCompleted += Completed;
            try
            {
                core.Navigate("https://chatgpt.com/");
                if(!await navigation.Task.WaitAsync(TimeSpan.FromSeconds(20)))
                    throw new InvalidDataException("New chat homepage navigation failed");
            }
            finally {core.NavigationCompleted -= Completed;}
            await Task.Delay(1300);
            // A prior stage may have inserted OUR numbered first wake but
            // refused to click because of rich-editor whitespace rendering.
            // Clear ONLY the draft bearing the saved exact receipt and N+1.
            if (due.RotationPhase == "preparing" &&
                due.RotationReceipt.Length == 32)
            {
                string recoveryScript = File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory, "LiteSuccessorSeedDirect.js"));
                await core.ExecuteScriptAsync(recoveryScript);
                string result = await core.ExecuteScriptAsync(
                    "window.__OBL_SUCCESSOR_SEED_V1__?.clearOwnUnsentDraft(" +
                    JsonSerializer.Serialize(due.RotationReceipt) + "," +
                    (due.WakeSequence+1).ToString(
                        System.Globalization.CultureInfo.InvariantCulture) + ")");
                string cleanup = JsonSerializer.Deserialize<string>(result) ?? "unknown";
                LiteEventLog.Write("rotation_owned_draft_recovery",new {
                    wakeSequence=due.WakeSequence+1, result=cleanup
                });
                if (cleanup.StartsWith("blocked_", StringComparison.Ordinal))
                    return true; // Never overwrite a possibly user-owned draft.
                await Task.Delay(350);
            }
            long epoch=_workerRouteEpoch;
            var first=await LiteSuccessorComposerProbe.Capture(core,epoch);
            await Task.Delay(1200);
            var second=await LiteSuccessorComposerProbe.Capture(core,epoch);
            LiteHandoffRouteEvidence.VerifyBlankComposerPair(
                first,second,DateTimeOffset.UtcNow);
            string receipt=Guid.NewGuid().ToString("N");
            long next=due.WakeSequence+1;
            // First message to the successor is the next REAL numbered wake.
            DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
            var firstWake = new LiteChatWakeRegistry.Chat {
                Url=due.Url, WakeSequence=next, Prompt=due.Prompt,
                Minutes=due.Minutes, FastMinutes=due.FastMinutes,
                Enabled=due.Enabled
            };
            string notes = _noteStore.Load(LiteChatNotes.NormalizeKey(due.Url));
            string handoffPath = owner.Key == "obl"
                ? @"D:\projects\open-browser\docs\OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md"
                : @"D:\OpenBrowserLiteHandoffs\" + owner.Key + @"\HANDOFF_CURRENT.md";
            // The engine, not an optional model action at wake ...9, owns
            // the fresh handoff. A failure is recorded and the successor
            // still has the canonical checkpoint/Beads recovery path.
            var snapshot = owner.Key == "obl"
                ? LiteRotationHandoffSnapshot.Save(due.Url, due.WakeSequence)
                : (Saved: false, Reason: "other_project", Sha256: "");
            LiteEventLog.Write("rotation_handoff_snapshot", new {
                project = owner.Key, oldWake = due.WakeSequence,
                nextWake = due.WakeSequence + 1,
                saved = snapshot.Saved, reason = snapshot.Reason,
                sha256 = snapshot.Sha256
            });
            string context = due.EmergencyReason.Length > 0
                ? "AWARYJNE PRZYWRÓCENIE PO BŁĘDZIE INTERFEJSU: " +
                  due.EmergencyReason + ". Poprzednie działania mogą być niezakończone " +
                  "lub ich wynik niewidoczny. Sprawdź rzeczywisty stan przed powtórzeniem. " +
                  (due.EmergencyHandoffPath.Length > 0
                    ? "Aktualny handoff: " + due.EmergencyHandoffPath + ". "
                    : "AKTUALNEGO HANDOFFU BRAK: odtwórz stan z checkpointu i zadań. ")
                : "PLANOWA ROTACJA LEADA. Odczytaj wyłącznie aktualny " +
                  "HANDOFF_CURRENT, jeżeli ma nowy format, właściwy projekt, " +
                  "stary czat i ostatni numer wybudzenia: " + handoffPath + ". ";
            if (owner.Key == "obl")
                context += snapshot.Saved
                    ? "Silnik zapisal atomowo HANDOFF_CURRENT w formacie OBL_HANDOFF_SNAPSHOT_V2 dla tego starego URL i numeru wake. Zweryfikuj naglowek, a nastepnie przeczytaj pelen checkpoint i Beads. "
                    : "Silnik nie potwierdzil aktualnego HANDOFF_CURRENT, przyczyna: " + snapshot.Reason + ". Nie bazuj na starym pliku: czytaj aktualny checkpoint i Beads. ";
            string seed = owner.Key == "obl"
                ? LiteChatWakeRegistry.NumberedMessage(firstWake, "", receipt,
                    startedUtc, forceOrdinary: true, registeredOblLead: true)
                : LiteChatWakeRegistry.NumberedMessage(firstWake, notes, receipt,
                    startedUtc, registeredOblLead: false) +
                  "\n\nNOWY CZAT: TO TWOJE PIERWSZE NORMALNE WYBUDZENIE " +
                  next + ". " + context +
                  "Odczytaj bieżący checkpoint projektu i kontynuuj pracę. " +
                  "W drugim wierszu odpowiedzi napisz: SUCCESSOR READY " + receipt;
            string sha=Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();
            var all=LiteChatWakeRegistry.Read();
            var old=all.Single(c=>c.Url==due.Url);
            if(!old.Enabled || old.WakeSequence!=due.WakeSequence)return true;
            old.RotationPhase="preparing";
            old.RotationStartedUtc=startedUtc;
            old.RotationReceipt=receipt;
            old.RotationSeedSha256=sha;
            LiteChatWakeRegistry.Write(all);
            string js=File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,"LiteSuccessorSeedDirect.js"));
            await core.ExecuteScriptAsync(js);
            string staged=JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    "window.__OBL_SUCCESSOR_SEED_V1__?.stage("+
                    JsonSerializer.Serialize(receipt)+","+
                    JsonSerializer.Serialize(seed)+")"))??"missing";
            if(staged!="staged")
            {
                // Metadata-only diagnostics: content hashes/lengths, never text.
                string diagnostic = await core.ExecuteScriptAsync(
                    "window.__OBL_SUCCESSOR_SEED_V1__?.diagnose()");
                LiteEventLog.Write("rotation_first_wake_stage_failed",new {
                    result=staged, diagnostics=diagnostic, wakeSequence=next
                });
                throw new InvalidOperationException("Successor first wake stage: "+staged);
            }
            await Task.Delay(500);
            if(core.Source!="https://chatgpt.com/" || epoch!=_workerRouteEpoch)
                throw new InvalidOperationException("New chat route changed before send");
            all=LiteChatWakeRegistry.Read();
            old=all.Single(c=>c.Url==due.Url);
            if(!old.Enabled || old.WakeSequence!=due.WakeSequence ||
                old.RotationReceipt!=receipt)return true;
            // A durable fence BEFORE any possible click. No duplicate seed
            // after restart, even when the outcome is unknown.
            old.RotationPhase="send_uncertain";
            LiteChatWakeRegistry.Write(all);
            LiteWakeJournal.Write("attempt_reserved", next, receipt);
            LiteEventLog.Write("rotation_first_wake_reserved", new {
                wakeSequence=next, receipt, project=owner.Key
            });
            string click=JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    "window.__OBL_SUCCESSOR_SEED_V1__?.clickOnce("+
                    JsonSerializer.Serialize(receipt)+")"))??"unknown";
            LiteEventLog.Write("rotation_seed_click",new {
                project=owner.Key,nextWake=next,result=click,oneShot=true
            });
            if(click is "click_invoked" or "click_uncertain")
                LiteWakeJournal.Write("submission_attempted",next,receipt,click);
            if(click=="click_invoked")
                LiteWakeJournal.Write("notes_submitted",next,receipt,"click_invoked");
            if(click is not ("click_invoked" or "click_uncertain"))return true;
            for(int i=0;i<75;i++)
            {
                await Task.Delay(800);
                if(LiteHandoffPersistedRoute.IsServerConversation(core.Source) &&
                    core.Source!=due.Url) break;
            }
            if(!LiteHandoffPersistedRoute.IsServerConversation(core.Source) ||
                core.Source==due.Url)return true;
            string newUrl=LiteDevWakeState.NormalizeUrl(core.Source);
            // Save the actual server route for post-crash readback. A failed
            // ACK can be re-read, but the original seed is NEVER re-sent.
            var observed=LiteChatWakeRegistry.Read();
            var pending=observed.Single(x=>x.Url==due.Url);
            if(pending.RotationPhase=="send_uncertain" &&
                pending.RotationReceipt==receipt)
            { pending.RotationServerUrl=newUrl; LiteChatWakeRegistry.Write(observed); }
            if(!await VerifySeedAsync(core,receipt,sha,next))return true;
            // Delivery proof belongs to the verified first user/assistant turn,
            // not to the subsequent lead promotion. A crash during promotion
            // must not erase the evidence that this numbered wake was delivered.
            LiteWakeJournal.Write("receipt_confirmed",next,receipt,"visible_ack");
            MarkAndPromote(due.Url,newUrl,receipt);
            LiteEventLog.Write("rotation_new_lead_ready",new {
                project=owner.Key,oldWake=due.WakeSequence,nextWake=next,
                newUrl,verified=true,handoffFileRequired=false,
                engineHandoffVerified=snapshot.Saved,engineHandoffSha256=snapshot.Sha256
            });
            // The first wake after promotion bypasses the regular scheduler's
            // visible_ack handler. Run its bounded archival maintenance once,
            // only after the successor's server UUID and receipt are verified.
            try
            {
                LiteEventLog.Write("rotation_native_maintenance_started", new {
                    project=owner.Key, wakeSequence=next, newUrl });
                await ArchiveOlderNativeChatBatchAsync(newUrl);
                await AuditNativeArchiveVisibilityAsync(newUrl);
            }
            catch (Exception maintenanceEx)
            {
                // Archival trouble must not reclassify a proven rotation as
                // failed or cause a repeat of the already delivered first wake.
                LiteEventLog.Write("rotation_native_maintenance_failed", new {
                    project=owner.Key, wakeSequence=next,
                    errorType=maintenanceEx.GetType().Name,
                    error=maintenanceEx.Message });
            }
        }
        catch(Exception ex)
        {
            LiteEventLog.Write("rotation_failed",new {
                nextWake=due.WakeSequence+1,errorType=ex.GetType().Name,
                error=ex.Message,noOldLeadWake=true
            });
            // Navigation/probe errors must not leave send_uncertain forever.
            if (due.RotationPhase=="send_uncertain")
            {
                try {
                    TryRearmUncertainRotation(due, owner.Key,
                        DateTimeOffset.UtcNow);
                }
                catch(Exception retryEx) {
                    LiteEventLog.Write("rotation_retry_rearm_failed",new {
                        wakeSequence=due.WakeSequence+1,
                        errorType=retryEx.GetType().Name,
                        error=retryEx.Message
                    });
                }
            }
        }
        return true;
    }

    private static bool TryRearmUncertainRotation(
        LiteChatWakeRegistry.Chat due, string projectKey, DateTimeOffset now)
    {
        bool isOblLead = string.Equals(projectKey, "obl",
            StringComparison.OrdinalIgnoreCase);
        if ((isOblLead && LiteHandoffPersistedRoute.IsServerConversation(due.RotationServerUrl)) || !LiteRotationRetryPolicy.MayReprepare(
            due.RotationPhase, due.RotationStartedUtc, now,
            hasConfirmedProof:false, isOblLead:isOblLead, savedServerUrl:due.RotationServerUrl)) return false;
        // Re-read durable project state before re-arming; only the sole
        // current lead with the exact outstanding receipt can move forward.
        var candidates=LiteChatWakeRegistry.Read();
        var retryCandidate=candidates.Single(x=>x.Url==due.Url);
        string previousReceipt=retryCandidate.RotationReceipt;
        bool hadServerRoute=LiteHandoffPersistedRoute.IsServerConversation(
            retryCandidate.RotationServerUrl);
        if (!LiteRotationRetryPolicy.TryRearm(
            retryCandidate, due, now, isOblLead:isOblLead)) return false;
        LiteChatWakeRegistry.Write(candidates);
        LiteWakeJournal.Write("rotation_retry_rearmed",
            due.WakeSequence+1,previousReceipt,
            isOblLead ? "no_receipt_proof_after_2min" :
                "no_receipt_proof_after_3min");
        LiteEventLog.Write("rotation_uncertain_retry_rearmed",new {
            wakeSequence=due.WakeSequence+1,
            previousReceipt,hadServerRoute,
            nextAttemptUsesNewReceipt=true,project=projectKey
        });
        return true;
    }

    private static async Task<bool> VerifySeedAsync(
        CoreWebView2 core,string receipt,string sha,long wakeNumber, bool allowPendingAssistant = false)
    {
        // A numbered wake requests real project work, not an assistant response
        // consisting of one exact ACK line. Prove actual first USER bubble
        // with this receipt+number and an assistant response in the same /c/.
        if(receipt.Length!=32 || sha.Length!=64 || wakeNumber<1)return false;
        LiteSuccessorFirstWakeProof.Sample? first=null; LiteSuccessorFirstWakeProof.Sample? last=null;
        for(int i=0;i<55;i++)
        {
            try
            {
                var candidate=await LiteSuccessorFirstWakeProof.CaptureAsync(
                    core,receipt,wakeNumber);
                last=candidate;
                if((allowPendingAssistant || candidate.AssistantStarted) && candidate.ExactReceipt &&
                    candidate.ExactWake && candidate.UserSha256.Length==64)
                { first=candidate;break; }
            }
            catch(InvalidDataException){}
            await Task.Delay(800);
        }
        if(first is null){ LiteEventLog.Write("rotation_first_wake_proof_metadata",new { wakeNumber, receiptMatch=last?.ExactReceipt, wakeMatch=last?.ExactWake, assistantStarted=last?.AssistantStarted, userShaPresent=last?.UserSha256.Length==64, stage="no_first_sample" }); return false; }
        await Task.Delay(1100);
        var second=await LiteSuccessorFirstWakeProof.CaptureAsync(
            core,receipt,wakeNumber);
        LiteEventLog.Write("rotation_first_wake_proof_metadata",new { wakeNumber, receiptMatch=second.ExactReceipt, wakeMatch=second.ExactWake, assistantStarted=second.AssistantStarted, userShaStable=first.UserSha256==second.UserSha256, stage="two_samples" });
        return LiteSuccessorFirstWakeProof.Verify(
            first,second,DateTimeOffset.UtcNow, requireAssistantStart: !allowPendingAssistant);
    }

    private void MarkAndPromote(
        string oldUrl,string newUrl,string receipt)
    {
        var schedules=LiteChatWakeRegistry.Read();
        var prior=schedules.Single(x=>x.Url==oldUrl);
        if(prior.RotationPhase!="send_uncertain" ||
            prior.RotationReceipt!=receipt)
            throw new InvalidDataException("Rotation receipt mismatch");
        prior.RotationPhase="verified";
        prior.RotationServerUrl=newUrl;
        LiteChatWakeRegistry.Write(schedules);
        PromoteRotation(oldUrl,newUrl);
    }

    internal static bool ShouldFollowPromotedLeadForeground(
        string currentUrl, string oldUrl, string newUrl)
    {
        if (!LiteHandoffPersistedRoute.IsServerConversation(newUrl) ||
            !LiteDevWakeState.IsChatUrl(oldUrl)) return false;
        var visible = LiteDevWakeState.IsChatUrl(currentUrl)
            ? LiteDevWakeState.NormalizeUrl(currentUrl)
            : (currentUrl ?? "").TrimEnd('/');
        return visible == LiteDevWakeState.NormalizeUrl(oldUrl) ||
            string.Equals(visible, "https://chatgpt.com", StringComparison.OrdinalIgnoreCase);
    }

    private static void CompletePromotedRotationWakeSchedule(string newUrl)
    {
        var schedules=LiteChatWakeRegistry.Read();
        var current=schedules.Single(x=>x.Url==newUrl);
        // The first message to the new chat was already numbered N+1.
        // Its timestamp becomes the new 15-minute clock phase.
        current.WakeSequence = checked(current.WakeSequence + 1);
        current.LastAttemptUtc = current.RotationStartedUtc;
        current.SlotAnchorUtc = current.RotationStartedUtc;
        current.DueUtc = current.RotationStartedUtc.AddMinutes(current.EffectiveMinutes);
        current.RotationStartedUtc = default;
        current.RotationPhase="";
        current.RotationReceipt="";
        current.RotationSeedSha256="";
        current.RotationServerUrl="";
        current.LastAnswerFingerprint="";
        current.HandoffPhase="";
        current.LastState="successor_first_wake_confirmed";
        current.EmergencyReason="";
        current.EmergencyPhase="";
        current.EmergencyMessageId="";
        current.EmergencySentUtc=default;
        current.EmergencyDeadlineUtc=default;
        current.EmergencyHandoffPath="";
        current.EmergencyOrdinal=0;
        current.StallFirstDueUtc=default;
        current.StallPhase="";
        current.StallResumeUtc=default;
        LiteChatWakeRegistry.Write(schedules);
    }

    private void PromoteRotation(string oldUrl,string newUrl)
    {
        var retired = LiteChatWakeRegistry.Read().SingleOrDefault(x => x.Url == oldUrl);
        bool emergencyCleanup = retired is not null &&
            (retired.EmergencyPhase == "rotate" || retired.EmergencyReason.Length > 0);
        var db=LiteProjectRegistry.OpenDefault();
        var project=db.Projects.Single(p=>p.Chats.Any(
            c=>c.Role=="lead" && c.Url==oldUrl));
        var next=project.Chats.FirstOrDefault(c=>c.Url==newUrl) ??
            db.AddChat(project.Id,"Nowy lead",newUrl);
        db.PromoteLead(project.Id,next.Id);
        // The URL-less project schedule retained N, so next send is N+1.
        CompletePromotedRotationWakeSchedule(newUrl);
        try
        {
            var foreground = _browser.CoreWebView2;
            var source = foreground?.Source ?? "";
            if (foreground is not null &&
                ShouldFollowPromotedLeadForeground(source, oldUrl, newUrl))
            {
                FlushNotes();
                if (!_notesDirty)
                {
                    foreground.Navigate(newUrl);
                    LiteEventLog.Write("rotation_foreground_follow_requested",
                        new { from = source, to = newUrl });
                }
                else LiteEventLog.Write("rotation_foreground_follow_skipped",
                    new { reason = "notes_unsaved" });
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("rotation_foreground_follow_failed",
                new { errorType = ex.GetType().Name, error = ex.Message });
        }
        if (emergencyCleanup) QueueRetiredLeadCleanup(oldUrl, newUrl);
    }
}
