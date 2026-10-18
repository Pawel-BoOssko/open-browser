using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Post-switch review for explicitly managed project chats only.
/// Never tells ChatGPT it was switched unless the picker says High.
/// </summary>
internal sealed partial class ChatGptLiteForm
{
    private readonly LiteResumePolicy _highReviewPolicy = new();
    private string _highReviewOrigin = "";
    private string _highReviewChat = "";
    private bool _highReviewPending;

    private void QueueHighReview(string previous, string url)
    {
        if (previous is not ("instant" or "medium")) return;
        string normalized = LiteChatNotes.NormalizeKey(url);
        if (_projects?.Projects.Any(p => p.Active &&
            p.Chats.Any(c => LiteChatNotes.NormalizeKey(c.Url) == normalized)) != true) return;
        _highReviewOrigin = previous;
        _highReviewChat = normalized;
        _highReviewPending = true;
        LiteEventLog.Write("high_review_queued", new { previous });
    }

    private async Task TryHighReviewAsync(CoreWebView2 core)
    {
        if (!_highReviewPending || _highReviewChat != LiteChatNotes.NormalizeKey(core.Source))
            return;
        var query = core.ExecuteScriptAsync(
            "window.__OPENBROWSER_RESUME_PROBE__ ? JSON.stringify(window.__OPENBROWSER_RESUME_PROBE__.observe()) : null");
        if (await Task.WhenAny(query, Task.Delay(5000)) != query) return;
        var json = JsonSerializer.Deserialize<string>(await query);
        if (string.IsNullOrWhiteSpace(json)) return;
        using var doc = JsonDocument.Parse(json);
        var state = doc.RootElement;
        if (state.GetProperty("reasoningMode").GetString() is not ("high" or "extra high"))
            return;
        string lastAnswer = state.GetProperty("text").GetString() ?? "";
        var observation = new LiteResumePolicy.Observation(
            _highReviewChat, state.GetProperty("role").GetString() ?? "",
            lastAnswer, state.GetProperty("generating").GetBoolean(),
            state.GetProperty("toolBusy").GetBoolean(),
            state.GetProperty("hasDraft").GetBoolean(),
            state.GetProperty("error").GetBoolean(),
            state.GetProperty("composerReady").GetBoolean(),
            DateTimeOffset.UtcNow);
        var assess = _highReviewPolicy.Check(observation);
        if (!assess.MayAskLuna) return;

        string fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes("HIGH-REVIEW:" + _highReviewOrigin + ":" +
                                   assess.ResponseHash)));
        if (_resumeLedger.WasReviewed(_highReviewChat, fingerprint))
        {
            _highReviewPending = false;
            return;
        }
        var receipt = _resumeLedger.Register(_highReviewChat, fingerprint, "continue");
        if (receipt is null) return;
        if (!_resumeLedger.MarkSendingUncertain(_highReviewChat, receipt.Id)) return;
        _highReviewPending = false;
        string effort = _highReviewOrigin == "instant" ? "Instant" : "Medium";
        string message = "W poprzednim etapie używano trybu " + effort +
            ". Interfejs potwierdza teraz tryb High. Przejrzyj jakość i " +
            "poprawność poprzednich wyników, a następnie kontynuuj tylko " +
            "rzeczywiście niezakończone zadania z aktualnego planu. " +
            "Nie powtarzaj wykonanych działań, nie obchodź blokad bezpieczeństwa, " +
            "zgłoś blokery i podejmij inne niezależne prace. " +
            "Jeżeli wszystko jest ukończone, nie wymyślaj nowych zadań.";
        string expression = "window.__OPENBROWSER_RESUME_SENDER__?.send(" +
            JsonSerializer.Serialize(message) + "," +
            JsonSerializer.Serialize(receipt.Id) + ")";
        var sent = core.ExecuteScriptAsync(expression);
        if (await Task.WhenAny(sent, Task.Delay(5000)) != sent)
        {
            LiteEventLog.Write("high_review_send_uncertain");
            return;
        }
        string response = JsonSerializer.Deserialize<string>(await sent) ?? "";
        if (response is not ("submission_requested" or "already_visible"))
        {
            LiteEventLog.Write("high_review_send_uncertain", new { reason = response });
            return;
        }
        await Task.Delay(2500);
        var ack = core.ExecuteScriptAsync(
            "window.__OPENBROWSER_RESUME_SENDER__?.hasAck(" +
            JsonSerializer.Serialize(receipt.Id) + ")");
        if (await Task.WhenAny(ack, Task.Delay(5000)) == ack &&
            await ack == "true")
        {
            _resumeLedger.MarkConfirmed(_highReviewChat, receipt.Id);
            LiteEventLog.Write("high_review_acknowledged");
        }
        else LiteEventLog.Write("high_review_ack_uncertain");
    }
}
