using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // Explicit read-only forensic check of a spent one-shot seed.
    // It never changes the ledger, retries a seed or transfers lead ownership.
    private async Task ProbeUncertainSuccessorReadbackOnceAsync()
    {
        if (_chatWakeBusy || !_ownsMultiWakeMutex || IsDisposed) return;
        var chats = LiteChatWakeRegistry.Read().Where(x => x.Enabled).ToArray();
        if (chats.Length != 1) return;
        string current = LiteDevWakeState.NormalizeUrl(chats[0].Url);
        string key = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(current))).ToLowerInvariant()[..16];
        string root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "diagnostics", "handoff");
        var selected = LiteHandoffGenerationBootstrap.SelectCurrent(root, key, current);
        string marker = Path.Combine(root, "successor-uncertain-readback." +
            Path.GetFileNameWithoutExtension(selected.Ledger) + ".request");
        if (!File.Exists(marker) || !File.Exists(selected.Ledger)) return;
        // Claim one-shot before any worker navigation. No retries on errors.
        string claimed = marker + ".claimed." + Guid.NewGuid().ToString("N");
        File.Move(marker, claimed, overwrite: false);
        string target = "";
        LiteHandoffSuccession.State state;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(claimed));
            target = doc.RootElement.GetProperty("url").GetString() ?? "";
            string epoch = doc.RootElement.GetProperty("ownerEpoch").GetString() ?? "";
            state = LiteHandoffSuccession.Read(selected.Ledger);
            if (!doc.RootElement.TryGetProperty("recoverAck", out var requestRecovery) ||
                requestRecovery.ValueKind != JsonValueKind.True)
                throw new InvalidDataException("Readback recovery not explicitly requested");
            if (state.Step != LiteHandoffSuccession.Phase.SeedUncertain ||
                state.OwnerEpoch != epoch || state.OldChatUrl != current ||
                !LiteHandoffPersistedRoute.IsServerConversation(target) ||
                LiteDevWakeState.NormalizeUrl(target) == current ||
                state.SeedReceipt.Length != 32 || state.SeedTextSha256.Length != 64)
                throw new InvalidDataException("Uncertain readback request invalid");
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("successor_uncertain_readback", new {
                verified=false, stage="request_validation", errorType=ex.GetType().Name,
                noSend=true, noLeadChange=true });
            return;
        }
        _chatWakeBusy = true;
        CoreWebView2? core = null;
        string? previous = null;
        string stage = "worker_init";
        try
        {
            await EnsureWakeWorkerAsync();
            core = _chatWakeWorker.CoreWebView2 ??
                throw new InvalidDataException("No worker WebView2");
            previous = core.Source;
            stage = "navigate_canonical_readonly";
            var reached = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void Completed(object? _, CoreWebView2NavigationCompletedEventArgs e)
                => reached.TrySetResult(e.IsSuccess);
            core.NavigationCompleted += Completed;
            try
            {
                core.Navigate(target);
                if (!await reached.Task.WaitAsync(TimeSpan.FromSeconds(20)))
                    throw new InvalidDataException("Successor route readback failed");
            }
            finally { core.NavigationCompleted -= Completed; }
            await Task.Delay(1900);
            stage = "pair_capture";
            var a = await LiteSuccessorDurableReadback.CaptureAsync(
                core, state.SeedReceipt, state.OldWakeSequence + 1);
            await Task.Delay(1400);
            var b = await LiteSuccessorDurableReadback.CaptureAsync(
                core, state.SeedReceipt, state.OldWakeSequence + 1);
            bool verified = LiteSuccessorDurableReadback.VerifyStableCanonical(
                a, b, state.SeedTextSha256, DateTimeOffset.UtcNow);
            // Structural diagnosis only. No message contents or identifiers logged.
            string shapeScript = "(() => { const m=document.querySelector('main'); " +
                "if(!m) return {main:false}; " +
                "const a=[...m.querySelectorAll('[data-message-author-role=assistant]')]; " +
                "const s=[...m.querySelectorAll('[data-chatgpt-selection-message-id]')]; " +
                "return {main:true, assistantRoleNodes:a.length, selectionNodes:s.length, " +
                "assistantNonempty:a.filter(e=>(e.innerText||'').trim().length>0).length, " +
                "selectionNonempty:s.filter(e=>(e.innerText||'').trim().length>0).length, " +
                "generating:!!document.querySelector('button[data-testid=stop-button]')}; " +
                "})()";
            string shapeResult = await core.ExecuteScriptAsync(shapeScript);
            LiteEventLog.Write("successor_uncertain_dom_shape", new {
                raw=shapeResult, noMessageContents=true, noSend=true,
                noLeadChange=true });
            LiteEventLog.Write("successor_uncertain_readback", new {
                verified, canonical=LiteHandoffPersistedRoute.IsServerConversation(b.Url),
                sameUrl=a.Url==b.Url, urlMatchesRequest=target==b.Url,
                assistantExactA=a.ExactAssistantReply,
                assistantExactB=b.ExactAssistantReply,
                userShaMatchesA=string.Equals(a.UserSha256,state.SeedTextSha256,
                    StringComparison.OrdinalIgnoreCase),
                userShaMatchesB=string.Equals(b.UserSha256,state.SeedTextSha256,
                    StringComparison.OrdinalIgnoreCase),
                noSend=true, noLeadChange=true, ledgerUnchanged=true,
                stage="pair_capture" });
            if (verified && target == a.Url && target == b.Url)
            {
                stage = "recovery_cas";
                string[] registered = chats.Select(x =>
                    LiteDevWakeState.NormalizeUrl(x.Url)).ToArray();
                var latest = LiteHandoffSuccession.Read(selected.Ledger);
                if (latest.Revision != state.Revision ||
                    latest.Step != LiteHandoffSuccession.Phase.SeedUncertain)
                    throw new InvalidOperationException("Uncertain ledger changed");
                var recovered = LiteHandoffSuccession.Advance(
                    selected.Ledger, state.Revision,
                    prior => LiteHandoffSuccession.RecoverUncertainServerAck(
                        prior, state.SeedReceipt, target, a, b,
                        registered, DateTimeOffset.UtcNow));
                LiteEventLog.Write("successor_uncertain_ack_recovered", new {
                    verified = true,
                    phase = recovered.Step.ToString(),
                    revision = recovered.Revision,
                    canonicalUrl = recovered.NewChatUrl,
                    noSend = true,
                    noLeadChange = true,
                    checkpointPending = true
                });
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("successor_uncertain_readback", new {
                verified=false, stage, errorType=ex.GetType().Name,
                noSend=true, noLeadChange=true });
        }
        finally
        {
            try
            {
                if (core is not null)
                    core.Navigate(LiteDevWakeState.IsChatUrl(previous)
                        ? previous! : "about:blank");
            }
            catch { /* No send or lead transfer may follow an error. */ }
            _chatWakeBusy = false;
        }
    }
}