using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // Initial recovery phase: prioritize the pre-Lite insertion + Send click,
    // rather than requiring renderer/probe/receipt validation to succeed.
    private async Task SendDevWakeDirectAsync(CoreWebView2 core, DateTimeOffset now)
    {
        string receipt = Guid.NewGuid().ToString("N");
        string attemptHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(receipt)));
        LiteDevWakeState.CommitAttempt(_devWake, now, attemptHash, receipt);
        string js = "window.__OPENBROWSER_RESUME_SENDER__?.sendDirect(" +
            JsonSerializer.Serialize(LiteDevWakeState.WakeText) + "," +
            JsonSerializer.Serialize(receipt) + ")";
        var result = await core.ExecuteScriptAsync(js);
        string status = JsonSerializer.Deserialize<string>(result) ?? "sender_missing";
        LiteEventLog.Write("dev_wake_direct_send", new {
            status, intervalMinutes = _devWake.IntervalMinutes
        });
        _devWake.LastState = "direct_" + status;
        if (status != "submission_requested")
            _devWake.NextDueUtc = now.AddMinutes(2);
        LiteDevWakeState.Write(_devWake);
        UpdateDevWakeStatus();
    }
}
