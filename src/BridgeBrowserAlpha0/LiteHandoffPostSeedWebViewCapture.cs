using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal static class LiteHandoffPostSeedWebViewCapture
{
    // Evidence-only, no click, DOM writes, messages or ownership changes.
    internal static async Task<(LiteHandoffPostSeedEvidence.Sample First,
        LiteHandoffPostSeedEvidence.Sample Second)> CapturePairAsync(
        CoreWebView2 browser, Func<long> routeEpoch)
    {
        long epoch = routeEpoch();
        var a = await LiteHandoffUserBubbleProbe.CaptureAsync(browser);
        var first = ToSample(a, epoch);
        await Task.Delay(1100);
        var b = await LiteHandoffUserBubbleProbe.CaptureAsync(browser);
        var second = ToSample(b, routeEpoch());
        VerifyReadOnlyPair(first, second, DateTimeOffset.UtcNow);
        return (first, second);
    }

    private static LiteHandoffPostSeedEvidence.Sample ToSample(
        LiteHandoffUserBubbleProbe.Observation a, long epoch) =>
        new(a.Url, a.RendererPid, epoch, a.Utc, a.Visible,
            a.BubbleSha256, a.Source);    internal static void VerifyReadOnlyPair(
        LiteHandoffPostSeedEvidence.Sample first,
        LiteHandoffPostSeedEvidence.Sample second, DateTimeOffset now)
    {
        if (!LiteDevWakeState.IsChatUrl(first.Url) ||
            first.Url != second.Url ||
            first.RendererPid < 1 ||
            first.RendererPid != second.RendererPid ||
            first.NavigationEpoch < 0 ||
            first.NavigationEpoch != second.NavigationEpoch ||
            !first.UserBubbleVisible || !second.UserBubbleVisible ||
            first.Evidence != "webview_response_probe" ||
            second.Evidence != "webview_response_probe" ||
            first.UserBubbleSha256.Length != 64 ||
            !first.UserBubbleSha256.All(Uri.IsHexDigit) ||
            first.UserBubbleSha256 != second.UserBubbleSha256)
            throw new InvalidDataException("Unstable user message evidence");
        double gap = (second.Utc - first.Utc).TotalMilliseconds;
        if (gap < 750 || gap > 15000 ||
            second.Utc > now.AddSeconds(2) ||
            (now - second.Utc).TotalSeconds > 30)
            throw new InvalidDataException("Unstable evidence time range");
    }
}
