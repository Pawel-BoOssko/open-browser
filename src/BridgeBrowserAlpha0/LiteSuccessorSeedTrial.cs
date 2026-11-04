using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

// Explicit one-shot on-host QA trial only. NEVER runs without a consumed
// diagnostic request marker, never enables/promotes another scheduled lead.
internal static class LiteSuccessorSeedTrial
{
    internal static async Task<bool> RunIfRequestedAsync(
        CoreWebView2 core, string ledgerPath,
        LiteSuccessorComposerLease heldLease,
        LiteHandoffRouteEvidence.Sample first,
        LiteHandoffRouteEvidence.Sample second,
        LiteHandoffRouteEvidence.Sample current,
        Func<long> routeEpoch)
    {
        string marker = Path.Combine(Path.GetDirectoryName(ledgerPath)!,
            "successor-seed-trial." + Path.GetFileNameWithoutExtension(ledgerPath) + ".request");
        if (!File.Exists(marker)) return false;
        string expectedOwnerEpoch = File.ReadAllText(marker).Trim();
        // Consume the external request BEFORE any state change or UI action.
        File.Move(marker, marker + ".claimed", overwrite:false);
        var state = LiteHandoffSuccession.Read(ledgerPath);
        if (expectedOwnerEpoch != state.OwnerEpoch)
            throw new InvalidOperationException("Trial epoch mismatch");
        if (state.Step != LiteHandoffSuccession.Phase.Prepared)
            throw new InvalidOperationException("Successor ledger not Prepared");
        string receipt = Guid.NewGuid().ToString("N");
        string seed = "OBL isolated successor connectivity test. " +
            "This is NOT a lead transfer. Do not access files, tools or " +
            "schedule tasks; do not continue any project work. " +
            "Reply with exactly: SUCCESSOR READY " + receipt;
        string sha = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();
        var permit = LiteHandoffOneShotReservation.Arm(
            ledgerPath, state.Revision, heldLease, first, second, current,
            checked((int)core.BrowserProcessId), routeEpoch(), receipt, sha,
            DateTimeOffset.UtcNow);
        LiteEventLog.Write("successor_seed_trial_reserved", new {
            receipt, oneShot=true, oldLeadUnchanged=true
        });
        string result = "not_attempted";
        try
        {
            result = await LiteSuccessorSeedDispatcher.SendOneAttemptAsync(
                core, permit, current, routeEpoch, receipt, seed);
            LiteEventLog.Write("successor_seed_trial_dispatch", new {
                phase=result, noRetry=true, oldLeadUnchanged=true
            });            if (result is "click_invoked" or "click_uncertain")
            {
                bool newRoute = false;
                for (int i = 0; i < 75; i++)
                {
                    await Task.Delay(800);
                    if (LiteHandoffPersistedRoute.IsServerConversation(core.Source) &&
                        LiteDevWakeState.NormalizeUrl(core.Source) != state.OldChatUrl)
                    { newRoute = true; break; }
                }
                if (newRoute)
                {
                    LiteSuccessorDurableReadback.Sample? readA = null;
                    for (int i = 0; i < 60; i++)
                    {
                        await Task.Delay(750);
                        if (!LiteHandoffPersistedRoute.IsServerConversation(core.Source))
                            break;
                        try
                        {
                            var candidate = await LiteSuccessorDurableReadback.CaptureAsync(
                                core, receipt);
                            if (candidate.ExactAssistantReply &&
                                string.Equals(candidate.UserSha256, sha,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                readA = candidate;
                                break;
                            }
                        }
                        catch (InvalidDataException) { /* DOM still hydrating. */ }
                    }
                    if (readA is not null)
                    {
                        await Task.Delay(1300);
                        var readB = await LiteSuccessorDurableReadback.CaptureAsync(
                            core, receipt);
                        var pair = await LiteHandoffPostSeedWebViewCapture.CapturePairAsync(
                            core, routeEpoch);
                        var currentState = LiteHandoffSuccession.Read(ledgerPath);
                        var ack = LiteHandoffSuccession.Advance(
                            ledgerPath, currentState.Revision,
                            s => LiteHandoffSuccession.ConfirmPersistedAssistantAck(
                                s, receipt, pair.First, pair.Second,
                                readA, readB, new[] { state.OldChatUrl },
                                DateTimeOffset.UtcNow));
                        LiteEventLog.Write("successor_seed_trial_ack", new {
                            verified=true, persistentServerUrl=true,
                            exactAssistantReply=true, phase=ack.Step.ToString(),
                            successorUrl=ack.NewChatUrl, oldLeadUnchanged=true
                        });
                        return true;
                    }
                    LiteEventLog.Write("successor_seed_trial_no_server_answer", new {
                        noRetry=true, serverRouteObserved=true,
                        assistantExactNotConfirmed=true, oldLeadUnchanged=true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("successor_seed_trial_error", new {
                errorType=ex.GetType().Name, phase=result,
                noRetry=true, oldLeadUnchanged=true
            });
        }
        // Unconfirmed means forever uncertain. NEVER resend after restart.
        var latest = LiteHandoffSuccession.Read(ledgerPath);
        if (latest.Step == LiteHandoffSuccession.Phase.SeedReserved)
            LiteHandoffSuccession.Advance(ledgerPath, latest.Revision,
                s => LiteHandoffSuccession.MarkUncertain(
                    s, DateTimeOffset.UtcNow));
        LiteEventLog.Write("successor_seed_trial_final", new {
            result="uncertain_or_unsent", noRetry=true,
            oldLeadUnchanged=true
        });
        return true;
    }
}
