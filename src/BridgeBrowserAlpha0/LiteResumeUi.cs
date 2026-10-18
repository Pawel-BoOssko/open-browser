using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Opt-in experimental supervisor for the owner's "testowy" conversation.
/// Until a configured Luna API model and a test chat are available, observe only.
/// </summary>
internal sealed partial class ChatGptLiteForm
{
    private readonly LiteResumePolicy _resumePolicy = new();
    private readonly LiteResumeLedger _resumeLedger = new();
    private readonly LiteLunaAdvisor _luna = new();
    private readonly System.Windows.Forms.Timer _resumeTimer = new() { Interval = 20000 };
    private readonly Button _resumeToggle = new()
    {
        Text = "Nadzór: WYŁ.", Width = 130
    };
    private readonly Label _resumeStatus = new()
    {
        Text = "Luna: oczekuje", AutoSize = true,
        Padding = new Padding(8, 8, 0, 0)
    };
    private bool _resumeEnabled;
    private bool _resumePolling;
    private DateTimeOffset _lastLunaAttemptUtc = DateTimeOffset.MinValue;

    private void InitializeResumeUi()
    {
        _resumeToggle.Click += (_, _) =>
        {
            if (!_resumeEnabled && !LiteLunaAdvisor.HasCredentials)
            {
                _resumeStatus.Text = "Luna: brak API model/klucz";
                MessageBox.Show(this,
                    "Nadzorca wymaga OPENBROWSER_LUNA_MODEL oraz OPENAI_API_KEY " +
                    "w ustawieniach bieżącego użytkownika Windows. " +
                    "Nie uruchamiam nieznanego modelu ani płatnych żądań.",
                    "Open Browser — konfiguracja Luny");
                return;
            }
            if (!_resumeEnabled && _browser.CoreWebView2 is { } activeCore)
            {
                try
                {
                    string chatKey = LiteChatNotes.NormalizeKey(activeCore.Source);
                if (_resumeLedger.IsTransportQuarantined(chatKey))
                {
                    if (!string.IsNullOrEmpty(_lastResponseFailure))
                    {
                        _resumeStatus.Text = "Nadzór: aktywny błąd ChatGPT";
                        return;
                    }
                    var approved = MessageBox.Show(this,
                        "W tym czacie wystąpił błąd dostarczenia. Samo zniknięcie " +
                        "komunikatu nie oznacza, że odpowiedź została dostarczona.\n\n" +
                        "Czy sprawdziłeś historię rozmowy i świadomie zezwalasz " +
                        "na wznowienie nadzoru?",
                        "Open Browser — ręczne uzgodnienie dostarczenia",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    if (approved != DialogResult.Yes) return;
                    if (_resumeLedger.ClearTransportQuarantineAfterManualReview(chatKey))
                        LiteEventLog.Write("resume_transport_manual_clear", new {
                            userConfirmed = true
                        });
                }
                }
                catch (Exception ex)
                {
                    LiteEventLog.Write("resume_transport_ledger_unavailable", new {
                        errorType = ex.GetType().Name
                    });
                    _resumeStatus.Text = "Nadzór zablokowany: błąd rejestru";
                    return;
                }
            }
            _resumeEnabled = !_resumeEnabled;
            _resumeToggle.Text = _resumeEnabled ? "Nadzór: TEST" : "Nadzór: WYŁ.";
            _resumeStatus.Text = _resumeEnabled ? "Oczekiwanie na testowy czat" :
                "Nadzór wyłączony";
            LiteEventLog.Write("resume_toggle", new { enabled = _resumeEnabled });
        };
        _resumeTimer.Tick += async (_, _) => await PollResumeAsync();
    }

    private async Task InstallResumeScriptsAsync(CoreWebView2 core)
    {
        foreach (string filename in new[] { "LiteResumeProbe.js", "LiteResumeSender.js" })
        {
            string script = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, filename));
            await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }
        LiteEventLog.Write("resume_probe_installed");
    }

    private bool IsApprovedTestChat(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var target) ||
            target.Host != "chatgpt.com" ||
            !target.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase))
            return false;
        string path = target.GetLeftPart(UriPartial.Path);
        string title = _browser.CoreWebView2?.DocumentTitle?.Trim() ?? "";
        if (title.Equals("testowy", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("testowy - ChatGPT", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("testowy | ChatGPT", StringComparison.OrdinalIgnoreCase))
            return true;
        return _projects?.Projects.Any(project => project.Chats.Any(chat =>
            chat.Url == path && string.Equals(chat.Title.Trim(), "testowy",
                StringComparison.OrdinalIgnoreCase))) == true;
    }

    private async Task PollResumeAsync()
    {
        var core = _browser.CoreWebView2;
        if (core is null || _resumePolling || !_resumeEnabled ||
            !string.IsNullOrEmpty(_lastResponseFailure) ||
            !IsApprovedTestChat(core.Source)) return;
        _resumePolling = true;
        try
        {
            string observedChat = LiteChatNotes.NormalizeKey(core.Source);
            if (_resumeLedger.IsTransportQuarantined(observedChat))
            {
                _resumeEnabled = false;
                _resumeToggle.Text = "Nadzór: WYŁ.";
                _resumeStatus.Text = "Nadzór: ręczna kontrola dostarczenia wymagana";
                return;
            }
            var read = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_RESUME_PROBE__ ? JSON.stringify(window.__OPENBROWSER_RESUME_PROBE__.observe()) : null");
            if (await Task.WhenAny(read, Task.Delay(6500)) != read)
            {
                _resumeEnabled = false;
                _resumeToggle.Text = "Nadzór: WYŁ.";
                _resumeStatus.Text = "Nadzór wyłączony: renderer nie odpowiada";
                LiteEventLog.Write("resume_probe_timeout");
                return;
            }
            string? json = JsonSerializer.Deserialize<string>(await read);
            if (string.IsNullOrWhiteSpace(json)) return;
            using var parsed = JsonDocument.Parse(json);
            var node = parsed.RootElement;
            string effort = node.GetProperty("reasoningMode").GetString() ?? "unknown";
            if (effort != "high")
            {
                _resumeStatus.Text = "Nadzór: wymagany High (jest " + effort + ")";
                LiteEventLog.Write("resume_high_effort_not_verified", new { detected = effort });
                return;
            }
            string text = node.GetProperty("text").GetString() ?? "";
            string chat = LiteChatNotes.NormalizeKey(core.Source);
            var observation = new LiteResumePolicy.Observation(
                chat, node.GetProperty("role").GetString() ?? "", text,
                node.GetProperty("generating").GetBoolean(),
                node.GetProperty("toolBusy").GetBoolean(),
                node.GetProperty("hasDraft").GetBoolean(),
                node.GetProperty("error").GetBoolean(),
                node.GetProperty("composerReady").GetBoolean(), DateTimeOffset.UtcNow);
            var decision = _resumePolicy.Check(observation);
            _resumeStatus.Text = "Nadzór: " + decision.Code;
            if (!decision.MayAskLuna || _resumeLedger.WasReviewed(chat, decision.ResponseHash) ||
                DateTimeOffset.UtcNow - _lastLunaAttemptUtc < TimeSpan.FromMinutes(10))
                return;

            _lastLunaAttemptUtc = DateTimeOffset.UtcNow;
            LiteEventLog.Write("resume_candidate", new { stable = true });
            // Only trusted, explicitly scoped test-chat data may reach Luna.
            var advice = await _luna.ProposeAsync(text,
                "Testowy czat pisze książkę przygodową. " +
                "Wznawiaj wyłącznie rozdziały, które według odpowiedzi pozostają. " +
                "Jeśli książka jest skończona, odpowiedz stop.", CancellationToken.None);
            if (advice is null)
            {
                _resumeStatus.Text = "Luna niedostępna / odmówiła";
                return;
            }
            var receipt = _resumeLedger.Register(chat, decision.ResponseHash,
                advice.Decision);
            if (receipt is null)
            {
                _resumeStatus.Text = "Nadzór: limit / już oceniono";
                return;
            }
            _resumeStatus.Text = "Luna: " + advice.Decision;
            LiteEventLog.Write("resume_luna_decision", new {
                decision = advice.Decision, confidence = advice.Confidence
            });
            if (advice.Decision != "continue") return;
            if (!IsApprovedTestChat(core.Source) ||
                _resumeLedger.IsTransportQuarantined(chat) ||
                !string.IsNullOrEmpty(_lastResponseFailure) ||
                !await RevalidateResumeBeforeSendAsync(core, chat, decision.ResponseHash))
            {
                LiteEventLog.Write("resume_preflight_rejected", new {
                    reason = "chat_or_last_answer_changed_or_not_idle_high"
                });
                _resumeStatus.Text = "Wznowienie wstrzymane: stan rozmowy się zmienił";
                return;
            }

            // Commit uncertain BEFORE attempting to touch the DOM. No retries,
            // including after a renderer crash or whole-PC restart.
            if (!_resumeLedger.MarkSendingUncertain(chat, receipt.Id)) return;
            string cmd = "window.__OPENBROWSER_RESUME_SENDER__?.send(" +
                JsonSerializer.Serialize(advice.Message) + "," +
                JsonSerializer.Serialize(receipt.Id) + ")";
            var call = core.ExecuteScriptAsync(cmd);
            if (await Task.WhenAny(call, Task.Delay(6000)) != call)
            {
                LiteEventLog.Write("resume_submit_uncertain",
                    new { reason = "renderer_timeout" });
                _resumeStatus.Text = "Nadzór: wysłanie niepewne — STOP";
                return;
            }
            string returned = JsonSerializer.Deserialize<string>(await call) ?? "";
            if (returned is not ("submission_requested" or "already_visible"))
            {
                LiteEventLog.Write("resume_submit_uncertain", new { reason = returned });
                _resumeStatus.Text = "Nadzór: brak potwierdzenia — STOP";
                return;
            }
            var ackScript = "window.__OPENBROWSER_RESUME_SENDER__?.hasAck(" +
                            JsonSerializer.Serialize(receipt.Id) + ")";
            await Task.Delay(2500);
            var ack = core.ExecuteScriptAsync(ackScript);
            if (await Task.WhenAny(ack, Task.Delay(5000)) == ack &&
                await ack == "true")
            {
                _resumeLedger.MarkConfirmed(chat, receipt.Id);
                LiteEventLog.Write("resume_delivery_ack", new { confirmed = true });
                _resumeStatus.Text = "Wznowiono testowy czat";
            }
            else
            {
                _resumeStatus.Text = "Wysyłka niepewna — nie ponawiam";
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("resume_cycle_failed", new {
                errorType = ex.GetType().Name
            });
            _resumeStatus.Text = "Nadzór: błąd (bez ponowienia)";
        }
        finally { _resumePolling = false; }
    }
}
