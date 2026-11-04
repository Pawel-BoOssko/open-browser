namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private bool _handoffPairReadOnlyVerified;
    private int _handoffPairReadAttempts;

    private async Task ProbeExistingUserBubblePairOnceAsync()
    {
        if (_handoffPairReadOnlyVerified || _handoffPairReadAttempts >= 4 ||
            !_ownsMultiWakeMutex || IsDisposed || _chatWakeBusy ||
            _browser.CoreWebView2 is null) return;
        _handoffPairReadAttempts++;
        try
        {
            var core = _browser.CoreWebView2;
            var pair = await LiteHandoffPostSeedWebViewCapture.CapturePairAsync(
                core, () => _foregroundWakeRouteEpoch);
            _handoffPairReadOnlyVerified = true;
            LiteEventLog.Write("handoff_user_bubble_pair_readonly_proof", new {
                verified=true, sha256=pair.First.UserBubbleSha256,
                rendererPid=pair.First.RendererPid,
                navigationEpoch=pair.First.NavigationEpoch,
                sampleGapMs=(int)(pair.Second.Utc-pair.First.Utc).TotalMilliseconds,
                noTyping=true, noSend=true, noOwnerChange=true
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("handoff_user_bubble_pair_readonly_proof", new {
                verified=false, errorType=ex.GetType().Name,
                noTyping=true, noSend=true
            });
        }
    }
}