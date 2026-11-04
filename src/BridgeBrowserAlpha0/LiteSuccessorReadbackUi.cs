using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private bool _successorReadbackAttempted;

    private async Task ProbeSeedAckedSuccessorReadbackOnceAsync()
    {
        if (_successorReadbackAttempted || _chatWakeBusy ||
            !_ownsMultiWakeMutex || IsDisposed) return;
        var due = LiteChatWakeRegistry.Read().Where(x=>x.Enabled).ToArray();
        if (due.Length != 1 ||
            due[0].DueUtc <= DateTimeOffset.UtcNow.AddMinutes(2)) return;
        string oldUrl = LiteDevWakeState.NormalizeUrl(due[0].Url);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(oldUrl))).ToLowerInvariant()[..16];
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser","ChatGPT-Lite","diagnostics","handoff");
        var selected = LiteHandoffGenerationBootstrap.SelectCurrent(root,hash,oldUrl);
        if (!File.Exists(selected.Ledger)) return;
        var state = LiteHandoffSuccession.Read(selected.Ledger);
        if (state.Step != LiteHandoffSuccession.Phase.SeedAcked ||
            !state.NewChatUrl.Contains("local-chatgpt%3A",
                StringComparison.OrdinalIgnoreCase)) return;
        _successorReadbackAttempted = true;
        _chatWakeBusy = true;
        CoreWebView2? core=null;
        string? previous=null;
        string phase="init";
        try
        {
            await EnsureWakeWorkerAsync();
            core=_chatWakeWorker.CoreWebView2 ??
                throw new InvalidOperationException("Missing wake WebView2");
            previous=core.Source;
            phase="navigate_provisional_readonly";
            var navigation=new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void Done(object? _,CoreWebView2NavigationCompletedEventArgs e)
                => navigation.TrySetResult(e.IsSuccess);
            core.NavigationCompleted+=Done;
            try
            {
                core.Navigate(state.NewChatUrl);
                if (!await navigation.Task.WaitAsync(TimeSpan.FromSeconds(20)))
                    throw new InvalidDataException("Provisional readback navigate failed");
            }
            finally { core.NavigationCompleted-=Done; }
            await Task.Delay(1800);
            phase="read_user_and_assistant";
            var a=await LiteSuccessorDurableReadback.CaptureAsync(core,
                state.SeedReceipt, state.OldWakeSequence + 1);
            await Task.Delay(1200);
            var b=await LiteSuccessorDurableReadback.CaptureAsync(core,
                state.SeedReceipt, state.OldWakeSequence + 1);
            bool verified=LiteSuccessorDurableReadback.VerifyStableCanonical(
                a,b,state.SeedTextSha256,DateTimeOffset.UtcNow);
            LiteEventLog.Write("successor_server_route_readback",new {
                evidence="two_webview_dom_reads", verified,
                canonicalUrl=LiteHandoffPersistedRoute.IsServerConversation(b.Url),
                sameUrl=a.Url==b.Url, sameSha=a.UserSha256==b.UserSha256,
                reservedShaMatch=string.Equals(b.UserSha256,state.SeedTextSha256,
                    StringComparison.OrdinalIgnoreCase),
                assistantReplyExact=b.ExactAssistantReply,
                route=b.Url, oldLeadUnchanged=true, noSend=true
            });
            // Never mutate or promote the ledger here. Confirmation of a
            // server ID requires a separate CAS and an independent checkpoint.
        }
        catch(Exception ex)
        {
            LiteEventLog.Write("successor_server_route_readback",new {
                verified=false,phase,errorType=ex.GetType().Name,
                oldLeadUnchanged=true,noSend=true
            });
        }
        finally
        {
            try
            {
                if(core is not null)
                    core.Navigate(LiteDevWakeState.IsChatUrl(previous)
                        ? previous! : "about:blank");
            }
            catch { /* No sender attached to readback worker. */ }
            _chatWakeBusy=false;
        }
    }
}