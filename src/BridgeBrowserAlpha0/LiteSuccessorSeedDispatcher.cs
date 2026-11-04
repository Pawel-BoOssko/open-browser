using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal static class LiteSuccessorSeedDispatcher
{
    // The caller holds a fresh, one-shot CAS permit and a worker-wide lock.
    // No automatic retry is allowed, including on "blocked" or exceptions.
    internal static async Task<string> SendOneAttemptAsync(
        CoreWebView2 core, LiteHandoffOneShotReservation.Permit permit,
        LiteHandoffRouteEvidence.Sample current,
        Func<long> workerEpoch, string receipt, string seed)
    {
        if (!permit.ConsumeOnce(current, checked((int)core.BrowserProcessId),
            workerEpoch(), DateTimeOffset.UtcNow))
            throw new InvalidOperationException("One-shot permit already spent");
        string script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "LiteSuccessorSeedDirect.js"));
        await core.ExecuteScriptAsync(script);
        string stageJs = "window.__OBL_SUCCESSOR_SEED_V1__?.stage(" +
            JsonSerializer.Serialize(receipt) + "," +
            JsonSerializer.Serialize(seed) + ")";
        string result = JsonSerializer.Deserialize<string>(
            await core.ExecuteScriptAsync(stageJs)) ?? "bridge_missing";
        if (result != "staged") return result;
        await Task.Delay(600);
        if (current.NavigationEpoch != workerEpoch() ||
            core.Source != "https://chatgpt.com/")
            return "blocked_worker_route";
        // No other caller can spend the same permit, even after a crash.
        LiteEventLog.Write("successor_seed_click_reserved", new {
            noRetry=true, expectedReceipt=true, onlyOneAttempt=true
        });
        string clickJs = "window.__OBL_SUCCESSOR_SEED_V1__?.clickOnce(" +
            JsonSerializer.Serialize(receipt) + ")";
        return JsonSerializer.Deserialize<string>(
            await core.ExecuteScriptAsync(clickJs)) ?? "click_result_unknown";
    }
}