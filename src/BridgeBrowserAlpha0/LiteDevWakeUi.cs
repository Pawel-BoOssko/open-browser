using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly System.Windows.Forms.Timer _devWakeTimer = new() { Interval = 60_000 };
    private readonly Button _devWakeToggle = new()
    {
        Text = "Autopraca: WYŁ.", Width = 146
    };
    private readonly Button _devWakePin = new()
    {
        Text = "Przypnij ten czat", Width = 124
    };
    private readonly Label _devWakeStatus = new()
    {
        AutoSize = true, Text = "Co 25 minut (jeden czat)",
        Padding = new Padding(6, 8, 0, 0)
    };
    private LiteDevWakeState.Record _devWake = new();
    private bool _devWakePending;
    private string _devWakeLastSkipReason = "";
    private DateTimeOffset _devWakeLastSkipLoggedUtc;
    private DateTimeOffset _devWakeLastAckProbeUtc;

    private void InitializeDevWake()
    {
        try {
            _devWake = LiteDevWakeState.Read();
            if (LiteDevWakeState.RecoverLegacyDefinitelyNotSent(
                _devWake, DateTimeOffset.UtcNow))
                LiteEventLog.Write("dev_wake_legacy_busy_recovered", new {
                    reason = "v168_busy_pre_send", deferredMinutes = 2
                });
            if (LiteDevWakeState.RecoverUncertainAfterRestart(
                _devWake, DateTimeOffset.UtcNow))
                LiteEventLog.Write("dev_wake_ambiguous_send_rearmed", new {
                    retryMinutes = _devWake.IntervalMinutes
                });
            UpdateDevWakeStatus();
            LiteEventLog.Write("dev_wake_loaded", new {
                enabled = _devWake.Enabled,
                intervalMinutes = _devWake.IntervalMinutes,
                chatBound = LiteDevWakeState.IsChatUrl(_devWake.TargetUrl)
            });
        }
        catch (Exception ex) {
            _devWake = new LiteDevWakeState.Record();
            _devWakeStatus.Text = "Autopraca: błąd ustawień";
            LiteEventLog.Write("dev_wake_configuration_failed", new {
                errorType = ex.GetType().Name
            });
        }
        _devWakeToggle.Click += (_, _) => {
            if (_multiWakeEnabled) { ToggleCurrentChatWake(); return; }
            try {
                if (!LiteDevWakeState.IsChatUrl(_devWake.TargetUrl)) {
                    _devWakeStatus.Text = "Brak przypiętego czatu";
                    return;
                }
                _devWake.Enabled = !_devWake.Enabled;
                if (_devWake.Enabled) {
                    _devWake.NextDueUtc = DateTimeOffset.UtcNow
                        .AddMinutes(_devWake.IntervalMinutes);
                    _devWake.LastState = "armed";
                } else _devWake.LastState = "paused_by_user";
                LiteDevWakeState.Write(_devWake);
                LiteEventLog.Write("dev_wake_toggle", new {
                    enabled = _devWake.Enabled
                });
                UpdateDevWakeStatus();
            }
            catch (Exception ex) {
                _devWake.Enabled = false;
                _devWakeStatus.Text = "Autopraca: błąd zapisu";
                LiteEventLog.Write("dev_wake_toggle_failed", new {
                    errorType = ex.GetType().Name
                });
            }
        };
        _devWakePin.Click += (_, _) => {
            if (_multiWakeEnabled) ConfigureCurrentChatWake();
            else PinCurrentConversation();
        };
        _devWakeTimer.Tick += async (_, _) => await PollDevWakeAsync();
    }

    private void PinCurrentConversation()
    {
        try
        {
            string? url = _browser.CoreWebView2?.Source;
            if (!LiteDevWakeState.IsChatUrl(url))
            {
                _devWakeStatus.Text = "Otwórz rozmowę ChatGPT";
                return;
            }
            if (_devWakePending)
            {
                _devWakeStatus.Text = "Autopraca: trwa poprzednia próba";
                return;
            }
            string target = LiteDevWakeState.NormalizeUrl(url!);
            if (string.Equals(_devWake.TargetUrl, target, StringComparison.Ordinal))
            {
                _devWakeStatus.Text = "Ten czat jest już przypięty";
                return;
            }
            string suffix = target.Substring(target.Length - 12);
            if (MessageBox.Show(this,
                    "Przypiąć bieżącą rozmowę (" + suffix +
                    ") jako jedyny cel Autopracy?\n" +
                    "Poprzedni czat przestanie otrzymywać wybudzenia.",
                    "Zmień przypięty czat", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _devWake = LiteDevWakeState.ArmForChat(
                _devWake, target, DateTimeOffset.UtcNow);
            LiteDevWakeState.Write(_devWake);
            LiteEventLog.Write("dev_wake_chat_rebound", new {
                boundToOneChat = true,
                intervalMinutes = _devWake.IntervalMinutes
            });
            UpdateDevWakeStatus();
        }
        catch (Exception ex)
        {
            _devWakeStatus.Text = "Nie udało się przypiąć czatu";
            LiteEventLog.Write("dev_wake_chat_rebind_failed",
                new { errorType = ex.GetType().Name });
        }
    }

    private void UpdateDevWakeStatus()
    {
        _devWakeToggle.Text = _devWake.Enabled
            ? "Autopraca: WŁ. " + _devWake.IntervalMinutes + "m" : "Autopraca: WYŁ.";
        string pinned = LiteDevWakeState.IsChatUrl(_devWake.TargetUrl)
            ? _devWake.TargetUrl.Substring(_devWake.TargetUrl.Length - 12) : "brak";
        string schedule = _devWake.Enabled &&
            _devWake.LastState.StartsWith("uncertain_editor_", StringComparison.Ordinal)
            ? "Szkic niewysłany"
            : _devWake.Enabled
                ? "Budzenie " + _devWake.NextDueUtc.ToLocalTime().ToString("HH:mm")
                : (_devWake.LastState == "complete" ? "Plan ukończony" : "Wstrzymana");
        _devWakeStatus.Text = "Czat " + pinned + " | " + schedule;
    }

    private void ReportDevWakeSkip(string reason)
    {
        var now = DateTimeOffset.UtcNow;
        if (reason == _devWakeLastSkipReason &&
            now - _devWakeLastSkipLoggedUtc < TimeSpan.FromMinutes(10)) return;
        _devWakeLastSkipReason = reason;
        _devWakeLastSkipLoggedUtc = now;
        LiteEventLog.Write("dev_wake_skipped", new { reason });
    }

    private async Task PollDevWakeAsync()
    {
        var core = _browser.CoreWebView2;
        if (_multiWakeEnabled || _devWakePending || !_devWake.Enabled || core is null) return;
        _devWakePending = true;
        try {
            var now = DateTimeOffset.UtcNow;
            if (!LiteDevWakeState.IsChatUrl(core.Source) ||
                LiteDevWakeState.NormalizeUrl(core.Source) !=
                LiteDevWakeState.NormalizeUrl(_devWake.TargetUrl)) {
                if (now >= _devWake.NextDueUtc)
                    ReportDevWakeSkip("other_chat");
                return; // NEVER follow another chat
            }
            // Direct recovery path enabled by default. Older detailed delivery
            // validation remains available only for later diagnostics.
            if (Environment.GetEnvironmentVariable("OPENBROWSER_LEGACY_DIRECT_SEND") != "0") {
                if (now >= _devWake.NextDueUtc)
                    await SendDevWakeDirectAsync(core, now);
                return;
            }
            // Passive reconciliation only. An uncertain delivery is never resent.
            // Check the original user bubble even after the short ACK window.
            if (LiteDevWakeState.CanReconcileReceipt(_devWake, now) &&
                now - _devWakeLastAckProbeUtc >= TimeSpan.FromMinutes(2)) {
                _devWakeLastAckProbeUtc = now;
                string pendingReceipt = _devWake.LastReceiptId;
                string expectedPath = new Uri(_devWake.TargetUrl).AbsolutePath;
                string ackScript = "(window.location.pathname === " +
                    JsonSerializer.Serialize(expectedPath) +
                    " && window.__OPENBROWSER_RESUME_SENDER__?.hasAck(" +
                    JsonSerializer.Serialize(pendingReceipt) + ")) === true";
                var lateAck = core.ExecuteScriptAsync(ackScript);
                if (await Task.WhenAny(lateAck, Task.Delay(1800)) == lateAck &&
                    await lateAck == "true" &&
                    LiteDevWakeState.ConfirmVisibleReceipt(_devWake,
                        pendingReceipt, DateTimeOffset.UtcNow)) {
                    LiteEventLog.Write("dev_wake_ack", new {
                        confirmed = true, late = true
                    });
                    UpdateDevWakeStatus();
                }
            }
            if (now < _devWake.NextDueUtc) return;
            if (!string.IsNullOrEmpty(_lastResponseFailure)) {
                _devWakeStatus.Text = "Autopraca: błąd odpowiedzi — pauza";
                ReportDevWakeSkip("response_failure");
                return;
            }
            // read-only snapshot of the active tab, do not inspect other chats
            var probe = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_RESUME_PROBE__ ? " +
                "JSON.stringify(window.__OPENBROWSER_RESUME_PROBE__.observe()) : null");
            if (await Task.WhenAny(probe, Task.Delay(6000)) != probe) {
                _devWakeStatus.Text = "Autopraca: renderer nie odpowiada";
                LiteEventLog.Write("dev_wake_probe_timeout");
                return; // fail closed; no timer retry of uncertain message
            }
            var encoded = JsonSerializer.Deserialize<string>(await probe);
            if (string.IsNullOrEmpty(encoded)) {
                ReportDevWakeSkip("probe_empty");
                return;
            }
            using var parsed = JsonDocument.Parse(encoded);
            var observation = parsed.RootElement;
            string answer = observation.GetProperty("text").GetString() ?? "";
            if (LiteDevWakeState.ContainsCompletionMarker(answer)) {
                _devWake.Enabled = false;
                _devWake.LastState = "complete";
                LiteDevWakeState.Write(_devWake);
                LiteEventLog.Write("dev_wake_completed");
                UpdateDevWakeStatus();
                return;
            }
            string hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(answer)));
            if (_devWake.LastObservedHash != hash) {
                _devWake.LastObservedHash = hash;
                _devWake.FirstObservedUtc = now;
                LiteDevWakeState.Write(_devWake);
            }
            var eligible = LiteDevWakeState.Eligibility(_devWake, core.Source,
                now, answer.Length == 0 ? "" : hash,
                observation.GetProperty("reasoningMode").GetString() == "high",
                observation.GetProperty("role").GetString() == "assistant",
                observation.GetProperty("generating").GetBoolean(),
                observation.GetProperty("toolBusy").GetBoolean(),
                observation.GetProperty("hasDraft").GetBoolean(),
                observation.GetProperty("error").GetBoolean(),
                observation.GetProperty("composerReady").GetBoolean());
            if (eligible != "ready") {
                _devWakeStatus.Text = "Autopraca: " + eligible;
                // Rate-limited, content-free evidence for diagnosing
                // answer_missing, high_not_verified and false busy states.
                bool diagnosticDue = eligible != _devWakeLastSkipReason ||
                    now - _devWakeLastSkipLoggedUtc >= TimeSpan.FromMinutes(10);
                ReportDevWakeSkip(eligible);
                if (diagnosticDue)
                    LiteEventLog.Write("dev_wake_probe_diagnostic", new {
                        reason = eligible,
                        role = observation.GetProperty("role").GetString(),
                        reasoningMode = observation.GetProperty("reasoningMode").GetString(),
                        turnCount = observation.GetProperty("turnCount").GetInt32(),
                        assistantNodeCount = observation.GetProperty("assistantNodeCount").GetInt32(),
                        userNodeCount = observation.GetProperty("userNodeCount").GetInt32(),
                        lastAssistantChars = observation.GetProperty("lastAssistantChars").GetInt32(),
                        answerChars = answer.Length,
                        generating = observation.GetProperty("generating").GetBoolean(),
                        toolBusy = observation.GetProperty("toolBusy").GetBoolean(),
                        hasDraft = observation.GetProperty("hasDraft").GetBoolean(),
                        composerReady = observation.GetProperty("composerReady").GetBoolean(),
                        error = observation.GetProperty("error").GetBoolean()
                    });
                return;
            }
            string normalizedChat = LiteChatNotes.NormalizeKey(core.Source);
            if (!await RevalidateResumeBeforeSendAsync(core, normalizedChat, hash)) {
                LiteEventLog.Write("dev_wake_preflight_rejected");
                return;
            }
            // The eligibility gate rejects any previously attempted answer,
            // including a send with unknown delivery. Never retry that hash.
            // A new answer may be woken after the normal interval.
            string receipt = Guid.NewGuid().ToString("N");
            LiteDevWakeState.CommitAttempt(_devWake, now, hash, receipt);
            LiteEventLog.Write("dev_wake_send_attempt", new {
                interval = _devWake.IntervalMinutes, receiptStored = true
            });
            string javascript =
                "window.__OPENBROWSER_RESUME_SENDER__?.send(" +
                JsonSerializer.Serialize(LiteDevWakeState.WakeText) + "," +
                JsonSerializer.Serialize(receipt) + ")";
            var send = core.ExecuteScriptAsync(javascript);
            if (await Task.WhenAny(send, Task.Delay(6000)) != send) {
                LiteDevWakeState.DeferUncertain(_devWake,
                    DateTimeOffset.UtcNow, "renderer_timeout");
                LiteEventLog.Write("dev_wake_submission_uncertain", new {
                    reason = "renderer_timeout"
                });
                UpdateDevWakeStatus();
                return;
            }
            string returned = JsonSerializer.Deserialize<string>(await send) ?? "";
            if (returned == "busy" || returned.StartsWith("busy_", StringComparison.Ordinal)) {
                // Sender returned before touching the editor or scheduling a click:
                // this is a confirmed NOT-SENT, unlike a renderer timeout.
                LiteDevWakeState.DeferDefinitelyUnsentBusy(
                    _devWake, receipt, DateTimeOffset.UtcNow);
                LiteEventLog.Write("dev_wake_deferred_busy", new {
                    confirmedNotSent = true, retryMinutes = 2,
                    reason = returned
                });
                UpdateDevWakeStatus();
                return;
            }
            if (returned is not ("submission_requested" or "already_visible")) {
                LiteDevWakeState.DeferUncertain(_devWake,
                    DateTimeOffset.UtcNow, "send_" + returned);
                LiteEventLog.Write("dev_wake_submission_uncertain", new {
                    reason = returned
                });
                UpdateDevWakeStatus();
                return;
            }
            await Task.Delay(2500);
            var ack = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_RESUME_SENDER__?.hasAck(" +
                JsonSerializer.Serialize(receipt) + ")");
            if (await Task.WhenAny(ack, Task.Delay(5000)) == ack &&
                await ack == "true") {
                _devWake.LastConfirmedReceiptId = receipt;
                _devWake.LastState = "confirmed";
                LiteDevWakeState.Write(_devWake);
                LiteEventLog.Write("dev_wake_ack", new { confirmed = true });
                UpdateDevWakeStatus();
            }
            else {
                // The old sender reported 'submission_requested' merely when
                // text entered the editor, even if it never clicked a button.
                // Distinguish a draft from an actual unacknowledged click.
                string sendPhase = "phase_unknown";
                var phaseQuery = core.ExecuteScriptAsync(
                    "window.__OPENBROWSER_RESUME_SENDER__?.phase(" +
                    JsonSerializer.Serialize(receipt) + ")");
                if (await Task.WhenAny(phaseQuery, Task.Delay(2500)) == phaseQuery)
                    sendPhase = JsonSerializer.Deserialize<string>(await phaseQuery)
                        ?? "phase_unknown";
                LiteEventLog.Write("dev_wake_delivery_phase", new {
                    phase = sendPhase, ackConfirmed = false,
                    triedClick = sendPhase == "click_invoked",
                    deliveryStillUnknown = true
                });
                bool unsentDraft = sendPhase.StartsWith("draft_", StringComparison.Ordinal) ||
                    sendPhase.StartsWith("blocked_", StringComparison.Ordinal) ||
                    sendPhase is "preparing";
                LiteDevWakeState.DeferUncertain(_devWake,
                    DateTimeOffset.UtcNow,
                    unsentDraft ? "editor_" + sendPhase : "no_ack");
                if (unsentDraft)
                    LiteEventLog.Write("dev_wake_draft_unsent", new {
                        phase = sendPhase, mustNotRetrySameAnswer = true
                    });
                else
                    LiteEventLog.Write("dev_wake_submission_uncertain", new {
                        reason = "no_visible_ack", phase = sendPhase
                    });
                UpdateDevWakeStatus();
            }
        }
        catch (Exception ex) {
            _devWakeStatus.Text = "Autopraca: błąd — późniejsza próba";
            try {
                if (LiteDevWakeState.MayRetryUncertain(_devWake))
                    LiteDevWakeState.DeferUncertain(_devWake,
                        DateTimeOffset.UtcNow, "exception");
                else {
                    _devWake.NextDueUtc = DateTimeOffset.UtcNow
                        .AddMinutes(_devWake.IntervalMinutes);
                    _devWake.LastState = "probe_exception_retry_later";
                    LiteDevWakeState.Write(_devWake);
                }
            } catch { /* local state inaccessible: do not corrupt it */ }
            LiteEventLog.Write("dev_wake_failed", new {
                errorType = ex.GetType().Name
            });
        }
        finally { _devWakePending = false; }
    }
}
