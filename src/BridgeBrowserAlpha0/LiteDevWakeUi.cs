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
    private readonly Label _devWakeStatus = new()
    {
        AutoSize = true, Text = "Co 25 minut (jeden czat)",
        Padding = new Padding(6, 8, 0, 0)
    };
    private LiteDevWakeState.Record _devWake = new();
    private bool _devWakePending;

    private void InitializeDevWake()
    {
        try {
            _devWake = LiteDevWakeState.Read();
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
        _devWakeTimer.Tick += async (_, _) => await PollDevWakeAsync();
    }

    private void UpdateDevWakeStatus()
    {
        _devWakeToggle.Text = _devWake.Enabled
            ? "Autopraca: WŁ. " + _devWake.IntervalMinutes + "m" : "Autopraca: WYŁ.";
        _devWakeStatus.Text = _devWake.Enabled
            ? "Budzenie: " + _devWake.NextDueUtc.ToLocalTime().ToString("HH:mm")
            : (_devWake.LastState == "complete" ? "Plan ukończony" : "Autopraca wstrzymana");
    }

    private async Task PollDevWakeAsync()
    {
        var core = _browser.CoreWebView2;
        if (_devWakePending || !_devWake.Enabled || core is null) return;
        _devWakePending = true;
        try {
            var now = DateTimeOffset.UtcNow;
            if (!LiteDevWakeState.IsChatUrl(core.Source) ||
                LiteDevWakeState.NormalizeUrl(core.Source) !=
                LiteDevWakeState.NormalizeUrl(_devWake.TargetUrl))
                return; // NEVER follow another chat
            if (now < _devWake.NextDueUtc) return;
            if (!string.IsNullOrEmpty(_lastResponseFailure)) {
                _devWakeStatus.Text = "Autopraca: błąd odpowiedzi — pauza";
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
            if (string.IsNullOrEmpty(encoded)) return;
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
                return;
            }
            string normalizedChat = LiteChatNotes.NormalizeKey(core.Source);
            if (!await RevalidateResumeBeforeSendAsync(core, normalizedChat, hash)) {
                LiteEventLog.Write("dev_wake_preflight_rejected");
                return;
            }
            // Persistent receipt first. If UI/client crashes, NEVER repeat for
            // this assistant answer on the next run.
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
                _devWake.LastState = "uncertain_renderer_timeout";
                _devWake.Enabled = false;
                LiteDevWakeState.Write(_devWake);
                LiteEventLog.Write("dev_wake_submission_uncertain", new {
                    reason = "renderer_timeout"
                });
                UpdateDevWakeStatus();
                return;
            }
            string returned = JsonSerializer.Deserialize<string>(await send) ?? "";
            if (returned is not ("submission_requested" or "already_visible")) {
                _devWake.LastState = "uncertain_send_" + returned;
                _devWake.Enabled = false;
                LiteDevWakeState.Write(_devWake);
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
                _devWake.Enabled = false;
                _devWake.LastState = "uncertain_no_ack";
                LiteDevWakeState.Write(_devWake);
                LiteEventLog.Write("dev_wake_submission_uncertain", new {
                    reason = "no_visible_ack"
                });
                UpdateDevWakeStatus();
            }
        }
        catch (Exception ex) {
            // Current attempt is stored before sending. Never blindly retry.
            _devWakeStatus.Text = "Autopraca: wstrzymana po wyjątku";
            _devWake.Enabled = false;
            try {
                _devWake.LastState = "exception";
                LiteDevWakeState.Write(_devWake);
            } catch { /* fail closed */ }
            LiteEventLog.Write("dev_wake_failed", new {
                errorType = ex.GetType().Name
            });
        }
        finally { _devWakePending = false; }
    }
}
