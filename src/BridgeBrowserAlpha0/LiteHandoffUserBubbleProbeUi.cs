namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private bool _handoffReadOnlyBubbleCaptured;
    private int _handoffBubbleReadAttempts;

    private async Task CaptureExistingUserBubbleHashOnceAsync()
    {
        if (_handoffReadOnlyBubbleCaptured || !_ownsMultiWakeMutex ||
            _handoffBubbleReadAttempts >= 5 || IsDisposed ||
            _browser.CoreWebView2 is null || _chatWakeBusy) return;
        _handoffBubbleReadAttempts++;
        try
        {
            var result = await LiteHandoffUserBubbleProbe.CaptureAsync(
                _browser.CoreWebView2);
            _handoffReadOnlyBubbleCaptured = true;
            LiteEventLog.Write("handoff_user_bubble_readonly_proof", new {
                verified=true, sha256=result.BubbleSha256,
                source=result.Source, rendererPid=result.RendererPid,
                noTyping=true, noSend=true, noOwnerChange=true
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("handoff_user_bubble_readonly_proof", new {
                verified=false, errorType=ex.GetType().Name,
                noTyping=true, noSend=true
            });
        }
    }
}