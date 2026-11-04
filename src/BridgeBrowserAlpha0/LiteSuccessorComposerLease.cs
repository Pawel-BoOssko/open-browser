namespace BridgeBrowserAlpha0;

// In-memory, single-worker lease: never serialized as permission to send.
// Losing WebView, a navigation epoch, PID or TTL irrevocably expires it.
internal sealed class LiteSuccessorComposerLease
{
    private readonly int _rendererPid;
    private readonly long _navigationEpoch;
    private readonly DateTimeOffset _deadline;
    private bool _revoked;
    private LiteSuccessorComposerLease(int pid, long epoch, DateTimeOffset until)
    {
        _rendererPid = pid;
        _navigationEpoch = epoch;
        _deadline = until;
    }
    internal static LiteSuccessorComposerLease FromVerifiedPair(
        LiteHandoffRouteEvidence.Sample first,
        LiteHandoffRouteEvidence.Sample second, DateTimeOffset now)
    {
        long epoch = LiteHandoffRouteEvidence.VerifyBlankComposerPair(first, second, now);
        return new LiteSuccessorComposerLease(second.RendererPid, epoch,
            now.AddSeconds(8));
    }
    internal void VerifyStillHeld(LiteHandoffRouteEvidence.Sample sample,
        int currentRendererPid, long currentNavigationEpoch, DateTimeOffset now)
    {
        if (_revoked || now >= _deadline || now < sample.Utc ||
            sample.Utc < now.AddSeconds(-3) ||
            sample.RendererPid != _rendererPid ||
            currentRendererPid != _rendererPid ||
            sample.NavigationEpoch != _navigationEpoch ||
            currentNavigationEpoch != _navigationEpoch ||
            sample.Url != "https://chatgpt.com/" ||
            sample.Evidence != "webview_response_probe" ||
            !sample.ExplicitNewChatAction ||
            !sample.ComposerReady || !sample.ComposerEmpty ||
            sample.Generating || sample.Error)
            throw new InvalidOperationException("Successor worker lease lost");
    }
    internal void Revoke() => _revoked = true;
}