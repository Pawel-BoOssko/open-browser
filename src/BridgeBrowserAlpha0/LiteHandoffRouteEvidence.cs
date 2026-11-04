namespace BridgeBrowserAlpha0;

/// <summary>
/// Two independent WebView2 observations before a successor can be marked
/// NewChatObserved. This does not navigate, create a chat, send a seed or
/// alter schedules; the caller must provide real WebView2 measurements.
/// </summary>
internal static class LiteHandoffRouteEvidence
{
    internal sealed record Sample(
        string Url, int RendererPid, DateTimeOffset Utc,
        bool ComposerReady, bool ComposerEmpty,
        bool Generating, bool Error, string Evidence,
        bool ExplicitNewChatAction = false, long NavigationEpoch = 0);

    // A newly opened ChatGPT composer can stay at "/" UNTIL first send.
    // Requiring /c/<id> before the first seed creates a deadlock. Instead
    // establish fresh blank composer provenance, then reserve seed once.
    // No send or browser navigation is performed by this validator.
    internal static long VerifyBlankComposerPair(Sample first, Sample second,
        DateTimeOffset now)
    {
        if (!first.ExplicitNewChatAction || !second.ExplicitNewChatAction ||
            first.NavigationEpoch < 1 ||
            first.NavigationEpoch != second.NavigationEpoch)
            throw new InvalidDataException("No fresh new-chat navigation");
        if (first.Url != "https://chatgpt.com/" ||
            second.Url != "https://chatgpt.com/")
            throw new InvalidDataException("Not the new-chat composer route");
        if (first.RendererPid <= 0 ||
            first.RendererPid != second.RendererPid)
            throw new InvalidDataException("Renderer identity changed");
        if (first.Evidence != "webview_response_probe" ||
            second.Evidence != "webview_response_probe" ||
            !first.ComposerReady || !second.ComposerReady ||
            !first.ComposerEmpty || !second.ComposerEmpty ||
            first.Generating || second.Generating ||
            first.Error || second.Error)
            throw new InvalidDataException("Blank composer not verified");
        double gapMs = (second.Utc - first.Utc).TotalMilliseconds;
        if (gapMs < 750 || gapMs > 15000 ||
            second.Utc > now.AddSeconds(2) ||
            (now - second.Utc).TotalSeconds > 30)
            throw new InvalidDataException("Composer observations not stable");
        return first.NavigationEpoch;
    }
    internal static string VerifyPair(
        string previousUrl, Sample a, Sample b,
        IReadOnlyCollection<string> registeredChatUrls, DateTimeOffset now)
    {
        if (a.RendererPid <= 0 || a.RendererPid != b.RendererPid)
            throw new InvalidDataException("Renderer identity changed");
        if (a.Evidence != "webview_response_probe" ||
            b.Evidence != "webview_response_probe")
            throw new InvalidDataException("Not a WebView2 observation");
        if (!a.ComposerReady || !b.ComposerReady ||
            !a.ComposerEmpty || !b.ComposerEmpty ||
            a.Generating || b.Generating || a.Error || b.Error)
            throw new InvalidDataException("Composer / generation not idle");
        var gap = (b.Utc - a.Utc).TotalMilliseconds;
        if (gap < 750 || gap > 15000 || b.Utc > now.AddSeconds(2) ||
            (now-b.Utc).TotalSeconds > 30)
            throw new InvalidDataException("Route not independently stable");
        if (!LiteDevWakeState.IsChatUrl(a.Url) ||
            !LiteDevWakeState.IsChatUrl(b.Url) ||
            !string.Equals(LiteDevWakeState.NormalizeUrl(a.Url),
                LiteDevWakeState.NormalizeUrl(b.Url), StringComparison.Ordinal))
            throw new InvalidDataException("Route changed between probes");
        string candidate = LiteDevWakeState.NormalizeUrl(a.Url);
        if (candidate == LiteDevWakeState.NormalizeUrl(previousUrl) ||
            registeredChatUrls.Any(x => LiteDevWakeState.IsChatUrl(x) &&
                LiteDevWakeState.NormalizeUrl(x) == candidate))
            throw new InvalidDataException("Chat already owned/registered");
        return candidate;
    }
}