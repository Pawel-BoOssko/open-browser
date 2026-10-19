using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly System.Windows.Forms.Timer _chatWakeTimer = new() { Interval = 20000 };
    private bool _multiWakeEnabled;
    private bool _chatWakeBusy;
    private readonly WebView2 _chatWakeWorker = new()
    {
        Size = new Size(980, 700), Location = new Point(-1350, -900),
        Visible = true, TabStop = false
    };
    private readonly Button _chatWakeConfigure = new()
    {
        Text = "Wybudzenia", Width = 100
    };

    private void InitializeMultiWake()
    {
        try
        {
            var registered = LiteChatWakeRegistry.Read(); // migrates old pinned chat once
            _multiWakeEnabled = true;
            Controls.Add(_chatWakeWorker);
            _chatWakeWorker.SendToBack();
            _chatWakeConfigure.Click += (_, _) => ConfigureCurrentChatWake();
            _chatWakeTimer.Tick += async (_, _) => await PollAllChatWakesAsync();
            UpdateMultiWakeStatus();
            LiteEventLog.Write("multi_wake_loaded", new {
                chatCount = registered.Count, enabledCount = registered.Count(x => x.Enabled)
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("multi_wake_load_failed",
                new { errorType = ex.GetType().Name });
            _chatWakeConfigure.Text = "Błąd wybudzeń";
        }
    }

    private void ToggleCurrentChatWake()
    {
        var url = _browser.CoreWebView2?.Source;
        if (!LiteDevWakeState.IsChatUrl(url))
        {
            _devWakeStatus.Text = "Otwórz rozmowę, aby zmienić wybudzenia";
            return;
        }
        try
        {
            var entries = LiteChatWakeRegistry.Read();
            var entry = entries.FirstOrDefault(x => x.Url == LiteDevWakeState.NormalizeUrl(url!));
            if (entry is null) { ConfigureCurrentChatWake(); return; }
            entry.Enabled = !entry.Enabled;
            if (entry.Enabled) entry.DueUtc = DateTimeOffset.UtcNow.AddMinutes(entry.Minutes);
            entry.LastState = entry.Enabled ? "armed" : "paused";
            LiteChatWakeRegistry.Write(entries);
            UpdateMultiWakeStatus();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("multi_wake_toggle_failed",
                new { errorType = ex.GetType().Name });
        }
    }

    private void ConfigureCurrentChatWake()
    {
        string? url = _browser.CoreWebView2?.Source;
        if (!LiteDevWakeState.IsChatUrl(url))
        {
            _devWakeStatus.Text = "Otwórz czat, który chcesz budzić";
            return;
        }
        string normalized = LiteDevWakeState.NormalizeUrl(url!);
        var entries = LiteChatWakeRegistry.Read();
        var existing = entries.FirstOrDefault(x => x.Url == normalized);
        using var dialog = new Form
        {
            Text = "Wybudzenie bieżącego czatu",
            Width = 590, Height = 340, StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false
        };
        var explanation = new Label
        {
            Left = 15, Top = 12, Width = 530, Height = 36,
            Text = "Ustawienia dotyczą tylko aktualnej rozmowy. Inne czaty zachowują własny harmonogram."
        };
        var intervalLabel = new Label { Text = "Odstęp (20–1440 min):", Left = 15, Top = 60, Width = 180 };
        var interval = new NumericUpDown
        {
            Minimum = 20, Maximum = 1440, Value = existing?.Minutes ?? 25,
            Left = 205, Top = 58, Width = 90
        };
        var messageLabel = new Label { Text = "Polecenie wysyłane przy wznowieniu:", Left = 15, Top = 98, Width = 450 };
        var prompt = new TextBox
        {
            Multiline = true, Left = 15, Top = 124, Width = 540, Height = 135,
            ScrollBars = ScrollBars.Vertical,
            Text = existing?.Prompt is { Length: > 0 } p ? p :
                "Kontynuuj pracę nad tym czatem zgodnie z ostatnim zadaniem. Zapisuj postępy."
        };
        var save = new Button { Text = "Zapisz i włącz", Left = 390, Top = 268, Width = 165, DialogResult = DialogResult.OK };
        dialog.Controls.AddRange(new Control[] { explanation, intervalLabel, interval, messageLabel, prompt, save });
        dialog.AcceptButton = save;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            entries = LiteChatWakeRegistry.Read();
            LiteChatWakeRegistry.Configure(entries, normalized, (int)interval.Value,
                prompt.Text, DateTimeOffset.UtcNow);
            LiteChatWakeRegistry.Write(entries);
            LiteEventLog.Write("multi_wake_armed", new {
                intervalMinutes = (int)interval.Value, enabledCount = entries.Count(x => x.Enabled)
            });
            UpdateMultiWakeStatus();
        }
        catch (Exception ex)
        {
            _devWakeStatus.Text = "Błąd zapisu harmonogramu";
            LiteEventLog.Write("multi_wake_arm_failed", new { errorType = ex.GetType().Name });
        }
    }

    private void UpdateMultiWakeStatus()
    {
        if (!_multiWakeEnabled) return;
        try
        {
            var entries = LiteChatWakeRegistry.Read();
            var url = _browser.CoreWebView2?.Source;
            var entry = LiteDevWakeState.IsChatUrl(url)
                ? entries.FirstOrDefault(x => x.Url == LiteDevWakeState.NormalizeUrl(url!))
                : null;
            _devWakeToggle.Text = entry?.Enabled == true
                ? "Autopraca: WŁ. " + entry.Minutes + "m" : "Autopraca: WYŁ.";
            _devWakeStatus.Text = entry is null
                ? "Bieżący czat: bez wybudzenia | Inne aktywne: " + entries.Count(x => x.Enabled)
                : (entry.Enabled ? "Następne: " + entry.DueUtc.ToLocalTime().ToString("HH:mm") : "Pauza") +
                    " | Aktywne czaty: " + entries.Count(x => x.Enabled);
            _devWakePin.Text = "Ustaw ten czat";
        }
        catch { _devWakeStatus.Text = "Harmonogram: błąd odczytu"; }
    }

    private async Task EnsureWakeWorkerAsync()
    {
        if (_chatWakeWorker.CoreWebView2 is not null) return;
        var profile = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "OpenBrowser", "ChatGPT-Lite", "Profile");
        var env = await CoreWebView2Environment.CreateAsync(null, profile);
        await _chatWakeWorker.EnsureCoreWebView2Async(env);
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "LiteResumeSender.js");
        var script = await File.ReadAllTextAsync(scriptPath);
        var core = _chatWakeWorker.CoreWebView2
            ?? throw new InvalidOperationException("Background WebView2 not initialized");
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        core.Settings.AreDevToolsEnabled = false;
        LiteEventLog.Write("multi_wake_worker_initialized");
    }

    private async Task<bool> NavigateWorkerAsync(string url)
    {
        var core = _chatWakeWorker.CoreWebView2;
        if (core is null) return false;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
            completed.TrySetResult(e.IsSuccess);
        core.NavigationCompleted += OnCompleted;
        try
        {
            core.Navigate(url);
            var ready = await Task.WhenAny(completed.Task, Task.Delay(45000));
            if (ready != completed.Task || !await completed.Task) return false;
            for (int i = 0; i < 35; i++)
            {
                if (core.Source is null ||
                    !LiteDevWakeState.IsChatUrl(core.Source) ||
                    LiteDevWakeState.NormalizeUrl(core.Source) != url) return false;
                string found = await core.ExecuteScriptAsync(
                    "!!document.querySelector('#prompt-textarea,[data-testid=\"composer-text-input\"],textarea,[contenteditable=\"true\"]')");
                if (found == "true") return true;
                await Task.Delay(700);
            }
            return false;
        }
        finally { core.NavigationCompleted -= OnCompleted; }
    }

    private async Task<string> RetryWithNativeInputAsync(CoreWebView2 core,
        string url, string text)
    {
        if (!LiteDevWakeState.IsChatUrl(core.Source) ||
            LiteDevWakeState.NormalizeUrl(core.Source) != url)
            return "cdp_wrong_chat";
        // Select only the worker composer contents, never the visible user tab.
        const string selectComposer = @"(() => {
            const el = document.querySelector('#prompt-textarea,[data-testid=""composer-text-input""],textarea,[contenteditable=""true""]');
            if (!el) return false;
            el.focus();
            if (typeof el.select === 'function') el.select();
            else {
                const range = document.createRange();
                range.selectNodeContents(el);
                const s = window.getSelection();
                s.removeAllRanges(); s.addRange(range);
            }
            return true;
        })()";
        if (await core.ExecuteScriptAsync(selectComposer) != "true")
            return "cdp_no_composer";
        try
        {
            await core.CallDevToolsProtocolMethodAsync("Input.insertText",
                JsonSerializer.Serialize(new { text }));
            await Task.Delay(650);
            const string clickSend = @"(() => {
                const el = document.querySelector('#prompt-textarea,[data-testid=""composer-text-input""],textarea,[contenteditable=""true""]');
                const form = el?.closest('form');
                const btn = form?.querySelector('button[type=submit]:not([disabled])') ||
                    document.querySelector('[data-testid=""send-button""],button[aria-label=""Wyślij""],button[aria-label=""Send""],button[aria-label=""Send message""]');
                if (!btn || btn.disabled || btn.getAttribute('aria-disabled') === 'true')
                    return false;
                btn.click(); return true;
            })()";
            if (await core.ExecuteScriptAsync(clickSend) == "true")
                return "cdp_native_input_click";
            // Deliver a real browser-level Enter, not an untrusted JS KeyboardEvent.
            const string enterDown = "{\"type\":\"keyDown\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13}";
            const string enterUp = "{\"type\":\"keyUp\",\"key\":\"Enter\",\"code\":\"Enter\",\"windowsVirtualKeyCode\":13}";
            await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", enterDown);
            await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", enterUp);
            return "cdp_native_enter";
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("multi_wake_cdp_failed",
                new { errorType = ex.GetType().Name });
            return "cdp_failed";
        }
    }

    private async Task PollAllChatWakesAsync()
    {
        if (!_multiWakeEnabled || _chatWakeBusy || IsDisposed) return;
        _chatWakeBusy = true;
        try
        {
            var entries = LiteChatWakeRegistry.Read();
            var now = DateTimeOffset.UtcNow;
            var due = entries.Where(x => x.Enabled && x.DueUtc <= now)
                .OrderBy(x => x.DueUtc).FirstOrDefault();
            if (due is null) { UpdateMultiWakeStatus(); return; }
            if (!FlushNotes())
            {
                LiteEventLog.Write("multi_wake_deferred", new { reason = "notes_save_failed" });
                return;
            }
            // Commit a single attempt BEFORE touching a browser; the user tab is unaffected.
            due.LastAttemptUtc = now;
            due.DueUtc = now.AddMinutes(due.Minutes);
            due.LastState = "attempting";
            LiteChatWakeRegistry.Write(entries);
            string state = "unknown";
            try
            {
                await EnsureWakeWorkerAsync();
                if (!await NavigateWorkerAsync(due.Url))
                    state = "background_navigation_failed";
                else
                {
                    var key = LiteChatNotes.NormalizeKey(due.Url);
                    string text = LiteChatWakeRegistry.Message(due,
                        _noteStore.Load(key));
                    string receipt = Guid.NewGuid().ToString("N");
                    string js = "window.__OPENBROWSER_RESUME_SENDER__?.sendDirect(" +
                        JsonSerializer.Serialize(text) + "," +
                        JsonSerializer.Serialize(receipt) + ")";
                    var result = await _chatWakeWorker.CoreWebView2.ExecuteScriptAsync(js);
                    state = JsonSerializer.Deserialize<string>(result) ?? "sender_missing";
                    await Task.Delay(2800); // allow delayed Send and retry cycle to finish
                    string phaseJson = await _chatWakeWorker.CoreWebView2.ExecuteScriptAsync(
                        "window.__OPENBROWSER_RESUME_SENDER__?.phase(" +
                        JsonSerializer.Serialize(receipt) + ")");
                    string phase = JsonSerializer.Deserialize<string>(phaseJson) ?? "unknown";
                    if (phase == "draft_button_missing")
                    {
                        phase = await RetryWithNativeInputAsync(
                            _chatWakeWorker.CoreWebView2, due.Url, text);
                        await Task.Delay(1800);
                    }
                    bool visible = false;
                    if (state == "submission_requested")
                    {
                        string snippet = text[..Math.Min(65, text.Length)];
                        string check = "Array.from(document.querySelectorAll('" +
                            "[data-user-message-bubble],[data-message-author-role=\\\"user\\\"]'))" +
                            ".slice(-6).some(x => String(x.innerText || x.textContent || '').includes(" +
                            JsonSerializer.Serialize(snippet) + "))";
                        visible = await _chatWakeWorker.CoreWebView2.ExecuteScriptAsync(check) == "true";
                    }
                    LiteEventLog.Write("multi_wake_send", new {
                        state, phase, visibleUserBubble = visible,
                        includesNotes = !string.IsNullOrWhiteSpace(_noteStore.Load(key)),
                        intervalMinutes = due.Minutes
                    });
                }
            }
            catch (Exception ex)
            {
                state = "failed_" + ex.GetType().Name;
                LiteEventLog.Write("multi_wake_failed", new {
                    errorType = ex.GetType().Name
                });
            }
            // Merge against current DPAPI content: CLI/UI may have changed other chats meanwhile.
            entries = LiteChatWakeRegistry.Read();
            var current = entries.FirstOrDefault(x => x.Url == due.Url);
            if (current is not null && current.LastAttemptUtc == now)
            {
                current.LastState = state;
                LiteChatWakeRegistry.Write(entries);
            }
            UpdateMultiWakeStatus();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("multi_wake_scheduler_failed",
                new { errorType = ex.GetType().Name });
        }
        finally { _chatWakeBusy = false; }
    }
}
