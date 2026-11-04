namespace BridgeBrowserAlpha0;

// Read-only, hash-only evidence from the same WebView2 worker after an
// irrevocably reserved first seed click. It does not issue any click.
internal static class LiteHandoffPostSeedEvidence
{
    internal sealed record Sample(
        string Url, int RendererPid, long NavigationEpoch,
        DateTimeOffset Utc, bool UserBubbleVisible,
        string UserBubbleSha256, string Evidence);

    internal static string Verify(
        LiteHandoffSuccession.State state, Sample first, Sample second,
        IReadOnlyCollection<string> registeredUrls, DateTimeOffset now)
    {
        if (state.Step is not (LiteHandoffSuccession.Phase.SeedReserved or
                               LiteHandoffSuccession.Phase.SeedUncertain) ||
            !state.SeedStartedOnBlankRoute ||
            state.NewComposerRendererPid < 1 ||
            state.NewComposerNavigationEpoch < 1 ||
            !IsSha(state.SeedTextSha256))
            throw new InvalidOperationException("No reserved blank-route seed");
        if (first.Evidence != "webview_response_probe" ||
            second.Evidence != "webview_response_probe" ||
            first.RendererPid != state.NewComposerRendererPid ||
            second.RendererPid != state.NewComposerRendererPid ||
            first.NavigationEpoch < state.NewComposerNavigationEpoch ||
            second.NavigationEpoch != first.NavigationEpoch ||
            !first.UserBubbleVisible || !second.UserBubbleVisible ||
            !IsSha(first.UserBubbleSha256) ||
            !string.Equals(first.UserBubbleSha256, state.SeedTextSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(second.UserBubbleSha256, state.SeedTextSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Seed bubble / worker evidence mismatch");
        double gapMs = (second.Utc - first.Utc).TotalMilliseconds;
        if (first.Utc < state.UpdatedUtc || gapMs < 750 || gapMs > 15000 ||
            second.Utc > now.AddSeconds(2) ||
            (now - second.Utc).TotalSeconds > 30)
            throw new InvalidDataException("Post-seed observations not stable");
        if (!LiteHandoffPersistedRoute.IsServerConversation(first.Url) ||
            !LiteHandoffPersistedRoute.IsServerConversation(second.Url))
            throw new InvalidDataException("Missing post-seed /c/ URL");
        string candidate = LiteDevWakeState.NormalizeUrl(first.Url);
        if (candidate != LiteDevWakeState.NormalizeUrl(second.Url) ||
            candidate == state.OldChatUrl ||
            registeredUrls.Any(u => LiteDevWakeState.IsChatUrl(u) &&
                LiteDevWakeState.NormalizeUrl(u) == candidate))
            throw new InvalidDataException("URL mismatch or already registered");
        return candidate;
    }

    private static bool IsSha(string s) =>
        s.Length == 64 && s.All(Uri.IsHexDigit);
}