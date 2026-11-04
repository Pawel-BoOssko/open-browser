using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

// READ ONLY. Uses current WebView2 DOM, never types, clicks, promotes or
// mutates the seed ledger. Cross-process readback must not pin renderer PID
// from the original send process.
internal static class LiteSuccessorDurableReadback
{
    internal sealed record Sample(
        string Url, int BrowserPid, DateTimeOffset Utc, string UserSha256,
        bool ExactAssistantReply, string Evidence);

    internal static async Task<Sample> CaptureAsync(
        CoreWebView2 core, string receipt, long wakeNumber = 0)
    {
        string before = core.Source;
        // Strictly inspect assistant-role nodes. A user's seed prompt
        // contains the requested response text and must NEVER count as ACK.
        string script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "LiteSuccessorAssistantAnswer.js"));
        string raw = await core.ExecuteScriptAsync(
            "(" + script + ")(" +
            JsonSerializer.Serialize("SUCCESSOR READY " + receipt) + ")");        string json = JsonSerializer.Deserialize<string>(raw) ??
            throw new InvalidDataException("No readback response");
        using var value = JsonDocument.Parse(json);
        bool assistantReply = value.RootElement.GetProperty("exact").GetBoolean();
        // The current two-line OBL prompt never asks the model to repeat a
        // technical receipt. Accept an actual assistant answer to the exact
        // minimal numeric first wake; keep the legacy answer for old ledgers.
        if (!assistantReply && wakeNumber > 0)
        {
            try
            {
                var proof = await LiteSuccessorFirstWakeProof.CaptureAsync(
                    core, receipt, wakeNumber);
                assistantReply = proof.ExactWake && proof.ExactReceipt &&
                    proof.AssistantStarted;
            }
            catch (InvalidDataException) { /* No evidence is not success. */ }
        }
        string userSha = "";
        try
        {
            var bubble = await LiteHandoffUserBubbleProbe.CaptureAsync(core);
            userSha = bubble.BubbleSha256;
        }
        catch (InvalidDataException) { /* Readback can diagnose missing user bubble. */ }
        if (before != core.Source)
            throw new InvalidDataException("Route changed during readback");
        return new Sample(before, checked((int)core.BrowserProcessId),
            DateTimeOffset.UtcNow, userSha, assistantReply, "webview_response_probe");
    }

    internal static bool VerifyStableCanonical(
        Sample first, Sample second, string reservedSha, DateTimeOffset now)
    {
        if (first.Evidence != "webview_response_probe" ||
            second.Evidence != "webview_response_probe" ||
            !LiteHandoffPersistedRoute.IsServerConversation(first.Url) ||
            first.Url != second.Url ||
            first.BrowserPid < 1 || first.BrowserPid != second.BrowserPid ||
            !first.ExactAssistantReply || !second.ExactAssistantReply ||
            !string.Equals(first.UserSha256, reservedSha,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(second.UserSha256, reservedSha,
                StringComparison.OrdinalIgnoreCase))
            return false;
        double gap = (second.Utc-first.Utc).TotalMilliseconds;
        return gap >= 750 && gap <= 15000 &&
            second.Utc <= now.AddSeconds(2) &&
            (now-second.Utc).TotalSeconds <= 30;
    }
}