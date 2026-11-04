using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private bool _successorComposerProbeAttempted;

    // Uses ONLY the already owned, on-screen HWND-backed OBL worker.
    // Never sends, types, creates a scheduled chat or promotes an owner.
    private async Task ProbeSuccessorFromWorkerOnceAsync()
    {
        if (_chatWakeBusy ||
            !_ownsMultiWakeMutex || IsDisposed) return;
        var due = LiteChatWakeRegistry.Read().Where(x => x.Enabled).ToArray();
        if (due.Length != 1) return;
        string currentChat = due[0].Url;
        string hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(currentChat))).ToLowerInvariant()[..16];
        string ledgerRoot = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "diagnostics", "handoff");
        var active = LiteHandoffGenerationBootstrap.SelectCurrent(
            ledgerRoot, hash, currentChat);
        string proof = active.Proof;
        string ledger = active.Ledger;
        string trialMarker = Path.Combine(ledgerRoot,
            "successor-seed-trial." +
            Path.GetFileNameWithoutExtension(ledger) + ".request");
        // A fresh one-shot request must run even after a prior passive probe.
        // Atomic marker claim and ledger CAS prevent duplicate sends.
        bool trialRequested = File.Exists(trialMarker);
        if (_successorComposerProbeAttempted && !trialRequested) return;
        if (due[0].DueUtc < DateTimeOffset.UtcNow.AddMinutes(2)
            && !trialRequested) return;
        if (!File.Exists(proof) || !File.Exists(ledger)) return;
        if (LiteHandoffSuccession.Read(ledger).Step !=
            LiteHandoffSuccession.Phase.Prepared) return;
        _successorComposerProbeAttempted = true;
        _chatWakeBusy = true;
        CoreWebView2? worker = null;
        string? priorRoute = null;
        string phase = "worker_init";
        LiteHandoffRouteEvidence.Sample? verifiedA = null;
        LiteHandoffRouteEvidence.Sample? verifiedB = null;
        bool restoreRequested = false;
        LiteSuccessorComposerLease? heldLease = null;
        try
        {
            await EnsureWakeWorkerAsync();
            worker = _chatWakeWorker.CoreWebView2
                ?? throw new InvalidOperationException("No initialized worker");
            priorRoute = worker.Source;
            phase = "navigate_blank_composer";
            var navigated = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnCompleted(object? _, CoreWebView2NavigationCompletedEventArgs e)
                => navigated.TrySetResult(e.IsSuccess);
            worker.NavigationCompleted += OnCompleted;
            try
            {
                worker.Navigate("https://chatgpt.com/");
                if (!await navigated.Task.WaitAsync(TimeSpan.FromSeconds(15)))
                    throw new InvalidDataException("Homepage navigation failed");
            }
            finally { worker.NavigationCompleted -= OnCompleted; }
            await Task.Delay(1600);
            long epoch = _workerRouteEpoch;
            phase = "read_blank_composer";
            var first = await LiteSuccessorComposerProbe.Capture(worker, epoch);
            await Task.Delay(1300);
            var second = await LiteSuccessorComposerProbe.Capture(worker, epoch);
            if (epoch != _workerRouteEpoch)
                throw new InvalidDataException("Worker route changed");
            phase = "verify_blank_composer";
            LiteHandoffRouteEvidence.VerifyBlankComposerPair(
                first, second, DateTimeOffset.UtcNow);
            verifiedA = first; verifiedB = second;
            heldLease = LiteSuccessorComposerLease.FromVerifiedPair(
                first, second, DateTimeOffset.UtcNow);
            await Task.Delay(1200);
            var third = await LiteSuccessorComposerProbe.Capture(worker, epoch);
            heldLease.VerifyStillHeld(third, checked((int)worker.BrowserProcessId),
                _workerRouteEpoch, DateTimeOffset.UtcNow);
            LiteEventLog.Write("successor_composer_held_lease", new {
                heldVerified=true, noSend=true, noOwnerChange=true });
            if (await LiteSuccessorSeedTrial.RunIfRequestedAsync(
                worker, ledger, heldLease, first, second, third,
                () => _workerRouteEpoch))
            {
                verifiedA = null; verifiedB = null;
            }
            LiteEventLog.Write("successor_inprocess_composer_probe", new {
                verified=true, result="blank_ready", liveLeadUnaffected=true
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("successor_inprocess_composer_probe", new {
                verified=false, phase,
                errorType=ex.GetType().Name, hresult=ex.HResult.ToString("X8"),
                liveLeadUnaffected=true
            });
        }
        finally
        {
            heldLease?.Revoke();
            try
            {
                if (worker is not null) { worker.Navigate(LiteDevWakeState.IsChatUrl(priorRoute) ? priorRoute! : "about:blank"); restoreRequested = true; }
            }
            catch { /* Scheduler always re-verifies navigation at next wake. */ }
            // Record a historical observation only AFTER the worker has
            // navigated away. Step remains Prepared, so it cannot seed.
            if (restoreRequested && verifiedA is not null &&
                verifiedB is not null)
            {
                try
                {
                    var current = LiteHandoffSuccession.Read(ledger);
                    if (current.Step == LiteHandoffSuccession.Phase.Prepared &&
                        current.OldChatUrl ==
                            LiteDevWakeState.NormalizeUrl(currentChat))
                    {
                        var saved = LiteHandoffSuccession.Advance(
                            ledger, current.Revision, state =>
                                LiteHandoffSuccession.RecordNonActionableComposerProbe(
                                    state, verifiedA, verifiedB,
                                    DateTimeOffset.UtcNow));
                        LiteEventLog.Write("successor_composer_evidence", new {
                            ledgerRevision=saved.Revision,
                            durable=true, nonActionable=true,
                            leadChanged=false, seedSent=false
                        });
                    }
                }
                catch (Exception ex)
                {
                    LiteEventLog.Write("successor_composer_evidence", new {
                        durable=false, nonActionable=true,
                        errorType=ex.GetType().Name
                    });
                }
            }
            _chatWakeBusy = false;
        }
    }
}
