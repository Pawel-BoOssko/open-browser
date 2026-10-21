using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly System.Windows.Forms.Timer _chatWakeTimer = new() { Interval = 20000 };
    private bool _multiWakeEnabled;
    private bool _chatWakeBusy;
    private System.Threading.Mutex? _multiWakeMutex;
    private bool _ownsMultiWakeMutex;
    private readonly WebView2 _chatWakeWorker = new()
    {
        Size = new Size(980, 700), Location = new Point(0, 42),
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
            // Only one upgraded Lite window may supervise the shared user profile.
            _multiWakeMutex = new System.Threading.Mutex(
                false, @"Local\OpenBrowserLiteChatWake_v1");
            try { _ownsMultiWakeMutex = _multiWakeMutex.WaitOne(0); }
            catch (AbandonedMutexException) { _ownsMultiWakeMutex = true; }
            _multiWakeEnabled = true; // suppress legacy sender in every instance
            Controls.Add(_chatWakeWorker);
            _chatWakeWorker.SendToBack();
            _chatWakeConfigure.Click += (_, _) => ConfigureCurrentChatWake();
            _chatWakeTimer.Tick += async (_, _) => await PollAllChatWakesAsync();
            UpdateMultiWakeStatus();
            if (!_ownsMultiWakeMutex)
                LiteEventLog.Write("multi_wake_secondary_window", new {
                    supervisorActiveElsewhere = true
                });
            LiteEventLog.Write("multi_wake_loaded", new {
                chatCount = registered.Count, enabledCount = registered.Count(x => x.Enabled)
            });
        }
        catch (Exception ex)
        {
            _multiWakeEnabled = true; // do not fall back to legacy single-chat sends
            DisposeMultiWakeMutex();
            LiteEventLog.Write("multi_wake_load_failed",
                new { errorType = ex.GetType().Name });
            _chatWakeConfigure.Text = "Błąd wybudzeń";
        }
    }

    private void DisposeMultiWakeMutex()
    {
        if (_ownsMultiWakeMutex && _multiWakeMutex is not null)
        {
            try { _multiWakeMutex.ReleaseMutex(); }
            catch (ApplicationException) { /* process recovery owns cleanup */ }
        }
        _ownsMultiWakeMutex = false;
        _multiWakeMutex?.Dispose();
        _multiWakeMutex = null;
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
        if (!_ownsMultiWakeMutex)
        {
            _devWakeStatus.Text = "Nadzór w innym oknie";
            return;
        }
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

    private static async Task<string> ExecuteWorkerScriptAsync(
        CoreWebView2 core, string script, int timeoutMs = 6500)
    {
        var execution = core.ExecuteScriptAsync(script);
        if (await Task.WhenAny(execution, Task.Delay(timeoutMs)) != execution)
            throw new TimeoutException("Background WebView2 script did not return");
        return await execution;
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
        var probeScript = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "LiteResumeProbe.js"));
        var core = _chatWakeWorker.CoreWebView2
            ?? throw new InvalidOperationException("Background WebView2 not initialized");
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(probeScript);
        core.Settings.AreDevToolsEnabled = false;
        // The hidden worker wakes chats only occasionally. Low is a best-effort
        // WebView2 memory target; scripts and network traffic still run.
        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
        LiteEventLog.Write("multi_wake_worker_initialized",
            new { memoryTarget = "low" });
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
                // A composer can render before the selected conversation
                // has loaded. Never type into a shell with zero chat turns.
                string found = await ExecuteWorkerScriptAsync(core,
                    "(() => !!document.querySelector('#prompt-textarea,[data-testid=\"composer-text-input\"],textarea,[contenteditable=\"true\"]') " +
                    "&& !!document.querySelector('[data-user-message-bubble],[data-message-author-role],[data-chatgpt-selection-message-id]'))()");
                if (found == "true") return true;
                await Task.Delay(700);
            }
            // Metadata-only diagnostic for app shell rendered without history.
            try
            {
                const string countJs = @"JSON.stringify({
                    bodyChars:document.body?.innerText?.length||0,
                    mainCount:document.querySelectorAll('main').length,
                    chatTurns:document.querySelectorAll('[data-testid^=""conversation-turn-""]').length,
                    articles:document.querySelectorAll('article').length,
                    userBubbles:document.querySelectorAll('[data-user-message-bubble]').length,
                    assistantBubbles:document.querySelectorAll('[data-chatgpt-selection-message-id]').length,
                    logInLinks:document.querySelectorAll('a[href*=""/auth/login""]').length,
                    ready:document.readyState
                })";
                var encoded = await ExecuteWorkerScriptAsync(core, countJs);
                var metadata = JsonSerializer.Deserialize<string>(encoded);
                if (!string.IsNullOrEmpty(metadata))
                {
                    using var parsed = JsonDocument.Parse(metadata);
                    var v = parsed.RootElement;
                    LiteEventLog.Write("multi_wake_history_not_ready", new {
                        domWaitSeconds = 25,
                        bodyChars = v.GetProperty("bodyChars").GetInt32(),
                        mainCount = v.GetProperty("mainCount").GetInt32(),
                        chatTurns = v.GetProperty("chatTurns").GetInt32(),
                        articles = v.GetProperty("articles").GetInt32(),
                        userBubbles = v.GetProperty("userBubbles").GetInt32(),
                        assistantBubbles = v.GetProperty("assistantBubbles").GetInt32(),
                        logInLinks = v.GetProperty("logInLinks").GetInt32(),
                        ready = v.GetProperty("ready").GetString()
                    });
                }
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("multi_wake_history_not_ready", new {
                    domWaitSeconds = 25, diagnosticError = ex.GetType().Name
                });
            }
            return false;
        }
        finally { core.NavigationCompleted -= OnCompleted; }
    }

    private async Task<LiteChatWakePreflight.Observation?> ReadWorkerObservationAsync(
        CoreWebView2 core)
    {
        const string script = "window.__OPENBROWSER_RESUME_PROBE__ ? " +
            "JSON.stringify(window.__OPENBROWSER_RESUME_PROBE__.observe()) : null";
        string result = await ExecuteWorkerScriptAsync(core, script);
        string? json = JsonSerializer.Deserialize<string>(result);
        return string.IsNullOrEmpty(json) ? null :
            JsonSerializer.Deserialize<LiteChatWakePreflight.Observation>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private async Task<(string Reason, string Hash, bool Complete)>
        CheckWorkerPreflightAsync(CoreWebView2 core, string url, string previousHash)
    {
        var first = await ReadWorkerObservationAsync(core);
        await Task.Delay(1500);
        var second = await ReadWorkerObservationAsync(core);
        string expectedPath = new Uri(url).AbsolutePath;
        string reason = LiteChatWakePreflight.Eligibility(
            first, second, expectedPath, previousHash);
        if (reason != "ready")
        {
            // Metadata only. Do not log response text, prompts or chat URLs.
            LiteEventLog.Write("multi_wake_preflight_observed", new {
                reason, firstRole = first?.Role, secondRole = second?.Role,
                firstChars = first?.Text?.Length ?? 0,
                secondChars = second?.Text?.Length ?? 0,
                assistantNodes = second?.AssistantNodeCount ?? 0,
                userNodes = second?.UserNodeCount ?? 0,
                lastAssistantChars = second?.LastAssistantChars ?? 0,
                turns = second?.TurnCount ?? 0,
                generating = second?.Generating,
                toolBusy = second?.ToolBusy,
                draft = second?.HasDraft,
                error = second?.Error,
                composer = second?.ComposerReady,
                targetMatches = second?.Location == expectedPath
            });
        }
        string answer = second?.Text ?? "";
        string hash = reason == "ready" ?
            LiteChatWakePreflight.Fingerprint(answer) : "";
        return (reason, hash,
            reason == "ready" && LiteChatWakePreflight.IsCompletion(answer));
    }

    private void DeferWakeWithoutSend(string url, DateTimeOffset now,
        string reason, int minutes = 2)
    {
        var registered = LiteChatWakeRegistry.Read();
        var entry = registered.FirstOrDefault(c => c.Url == url && c.Enabled);
        if (entry is null) return;
        entry.DueUtc = now.AddMinutes(minutes);
        entry.LastState = "waiting_" + reason;
        LiteChatWakeRegistry.Write(registered);
        LiteEventLog.Write("multi_wake_preflight_skipped", new {
            reason, retryMinutes = minutes, definitelyNotSent = true
        });
        UpdateMultiWakeStatus();
    }

    private async Task PollAllChatWakesAsync()
    {
        if (!_multiWakeEnabled || !_ownsMultiWakeMutex ||
            _chatWakeBusy || IsDisposed) return;
        _chatWakeBusy = true;
        try
        {
            var entries = LiteChatWakeRegistry.Read();
            var now = DateTimeOffset.UtcNow;
            var foregroundUrl = _browser.CoreWebView2?.Source;
            var dueChats = entries.Where(x => x.Enabled && x.DueUtc <= now)
                .OrderBy(x => x.DueUtc).ToArray();
            var due = dueChats.FirstOrDefault(x => foregroundUrl is not null &&
                LiteDevWakeState.IsChatUrl(foregroundUrl) &&
                LiteDevWakeState.NormalizeUrl(foregroundUrl) == x.Url)
                ?? dueChats.FirstOrDefault();
            if (due is null) { UpdateMultiWakeStatus(); return; }
            if (!FlushNotes())
            {
                LiteEventLog.Write("multi_wake_deferred", new { reason = "notes_save_failed" });
                return;
            }
            // Navigation and read-only checks do not count as send attempts.
            // Commit only after the target conversation is stable and eligible.
            string state = "unknown";
            bool attemptCommitted = false;
            try
            {
                var foreground = _browser.CoreWebView2;
                bool useForeground = foreground is not null &&
                    LiteDevWakeState.IsChatUrl(foreground.Source) &&
                    LiteDevWakeState.NormalizeUrl(foreground.Source) == due.Url;
                if (!useForeground) await EnsureWakeWorkerAsync();
                var wakeCore = useForeground ? foreground! : _chatWakeWorker.CoreWebView2;
                if (wakeCore is null || (!useForeground && !await NavigateWorkerAsync(due.Url)))
                {
                    DeferWakeWithoutSend(due.Url, now, "background_navigation_failed");
                    return;
                }
                else
                {
                    LiteEventLog.Write("multi_wake_target_selected", new { foreground = useForeground });
                    // Re-read the settings in case the operator paused the chat
                    // while the worker was navigating.
                    entries = LiteChatWakeRegistry.Read();
                    var selected = entries.FirstOrDefault(x => x.Url == due.Url);
                    if (selected is null || !selected.Enabled ||
                        selected.DueUtc > DateTimeOffset.UtcNow)
                        return;
                    due = selected;
                    var preflight = await CheckWorkerPreflightAsync(
                        wakeCore, due.Url,
                        due.LastAnswerFingerprint);
                    if (preflight.Complete)
                    {
                        due.Enabled = false;
                        due.LastState = "complete";
                        LiteChatWakeRegistry.Write(entries);
                        LiteEventLog.Write("multi_wake_completed");
                        UpdateMultiWakeStatus();
                        return;
                    }
                    if (preflight.Reason == "draft_present")
                    {
                        // A previous attempt may have left OUR unique OB wake
                        // in the editor. Retry only that draft, never user text.
                        var encoded = await ExecuteWorkerScriptAsync(wakeCore,
                            "window.__OPENBROWSER_RESUME_SENDER__?.submitOwnDraft()");
                        var recovery = JsonSerializer.Deserialize<string>(encoded)
                            ?? "sender_missing";
                        if (recovery is "click_invoked" or "already_visible")
                        {
                            // A click is not delivery. Wait briefly for the
                            // unique receipt in a real user message bubble.
                            bool receiptVisible = recovery == "already_visible";
                            if (!receiptVisible)
                            {
                                for (int check = 0; check < 6 && !receiptVisible; check++)
                                {
                                    await Task.Delay(650);
                                    try
                                    {
                                        receiptVisible = await ExecuteWorkerScriptAsync(wakeCore,
                                            "window.__OPENBROWSER_RESUME_SENDER__?.recoveredDraftVisible()")
                                            == "true";
                                    }
                                    catch (TimeoutException) { break; }
                                }
                            }
                            entries = LiteChatWakeRegistry.Read();
                            var active = entries.FirstOrDefault(x => x.Url == due.Url);
                            if (active is not null && active.Enabled)
                            {
                                active.LastState = receiptVisible
                                    ? "own_draft_visible_ack"
                                    : "own_draft_clicked_unconfirmed";
                                active.DueUtc = DateTimeOffset.UtcNow.AddMinutes(
                                    receiptVisible ? active.Minutes : 2);
                                LiteChatWakeRegistry.Write(entries);
                            }
                            LiteEventLog.Write("multi_wake_own_draft_recovery",
                                new { result = recovery, receiptConfirmed = receiptVisible });
                            UpdateMultiWakeStatus();
                            return;
                        }
                    }
                    if (preflight.Reason != "ready")
                    {
                        DeferWakeWithoutSend(due.Url, now, preflight.Reason,
                            preflight.Reason == "same_answer_already_woken"
                                ? due.Minutes : 2);
                        return;
                    }
                    // Durable fingerprint fences uncertain sends: never resend
                    // for an unchanged assistant answer after a crash/restart.
                    due.LastAnswerFingerprint = preflight.Hash;
                    due.LastAttemptUtc = now;
                    due.DueUtc = now.AddMinutes(due.Minutes);
                    due.LastState = "attempting";
                    LiteChatWakeRegistry.Write(entries);
                    attemptCommitted = true;
                    var key = LiteChatNotes.NormalizeKey(due.Url);
                    string text = LiteChatWakeRegistry.Message(due,
                        _noteStore.Load(key));
                    string receipt = Guid.NewGuid().ToString("N");
                    string payload = "[OB-" + receipt + "]\n" + text;
                    string js = "window.__OPENBROWSER_RESUME_SENDER__?.sendDirect(" +
                        JsonSerializer.Serialize(payload) + "," +
                        JsonSerializer.Serialize(receipt) + ",true)";
                    var result = await ExecuteWorkerScriptAsync(
                        wakeCore, js);
                    state = JsonSerializer.Deserialize<string>(result) ?? "sender_missing";
                    if (state.StartsWith("busy_", StringComparison.Ordinal) ||
                        state is "missing_composer" or "invalid" or "already_attempted")
                    {
                        // Confirmed no editor submission: release the answer fence,
                        // unlike an unacknowledged click where outcome is uncertain.
                        entries = LiteChatWakeRegistry.Read();
                        var unsent = entries.FirstOrDefault(x => x.Url == due.Url);
                        if (unsent is not null && unsent.LastAttemptUtc == now)
                        {
                            unsent.LastAnswerFingerprint = "";
                            unsent.DueUtc = now.AddMinutes(2);
                            unsent.LastState = "waiting_" + state;
                            LiteChatWakeRegistry.Write(entries);
                        }
                        LiteEventLog.Write("multi_wake_definitely_unsent",
                            new { reason = state });
                        return;
                    }
                    // Page timers may be throttled when the worker is occluded.
                    // Flush submission explicitly from the host, with no dependence
                    // on requestAnimationFrame or setTimeout in the page.
                    string phase = "draft_button_missing";
                    for (int attempt = 0; attempt < 7 && state == "submission_requested"; attempt++)
                    {
                        await Task.Delay(attempt == 0 ? 750 : 400);
                        string flush = await ExecuteWorkerScriptAsync(
                            wakeCore,
                            "window.__OPENBROWSER_RESUME_SENDER__?.submitDirect(" +
                            JsonSerializer.Serialize(receipt) + ")");
                        phase = JsonSerializer.Deserialize<string>(flush) ?? "sender_missing";
                        if (phase is "click_invoked" or "form_submitted" ||
                            phase.StartsWith("blocked_", StringComparison.Ordinal))
                            break;
                    }
                    if (phase == "draft_button_missing")
                    {
                        try
                        {
                            const string domState = @"JSON.stringify({
                                visibility:document.visibilityState,
                                width:innerWidth, height:innerHeight,
                                composerCount:document.querySelectorAll('#prompt-textarea,[contenteditable=""true""]').length,
                                submitCount:document.querySelectorAll('button[type=submit]').length,
                                sendCount:document.querySelectorAll('[data-testid=""send-button""],button[aria-label=""Send""],button[aria-label=""Wyślij""]').length
                            })";
                            string encoded = await ExecuteWorkerScriptAsync(
                                wakeCore, domState);
                            string metadata = JsonSerializer.Deserialize<string>(encoded) ?? "{}";
                            using var parsed = JsonDocument.Parse(metadata);
                            var root = parsed.RootElement;
                            LiteEventLog.Write("multi_wake_dom_diagnostic", new
                            {
                                visibility = root.GetProperty("visibility").GetString(),
                                width = root.GetProperty("width").GetInt32(),
                                height = root.GetProperty("height").GetInt32(),
                                composers = root.GetProperty("composerCount").GetInt32(),
                                submitButtons = root.GetProperty("submitCount").GetInt32(),
                                sendButtons = root.GetProperty("sendCount").GetInt32()
                            });
                        }
                        catch { /* diagnostics never prevent sending */ }
                        // Do not synthesize Enter or submit an unhydrated form.
                        // The unique receipt was NOT confirmed as submitted.
                    }
                    bool visible = false;
                    if (state == "submission_requested")
                    {
                        string snippet = "[OB-" + receipt + "]";
                        string check = "Array.from(document.querySelectorAll('" +
                            "[data-user-message-bubble],[data-message-author-role=\\\"user\\\"]'))" +
                            ".slice(-6).some(x => String(x.innerText || x.textContent || '').includes(" +
                            JsonSerializer.Serialize(snippet) + "))";
                        visible = await ExecuteWorkerScriptAsync(
                            wakeCore, check) == "true";
                    }
                    LiteEventLog.Write("multi_wake_send", new {
                        state, phase, visibleUserBubble = visible,
                        receiptConfirmed = visible, // unique receipt; never match older messages
                        includesNotes = !string.IsNullOrWhiteSpace(_noteStore.Load(key)),
                        intervalMinutes = due.Minutes
                    });
                    state = visible ? "visible_ack" : phase;
                }
            }
            catch (Exception ex)
            {
                state = "failed_" + ex.GetType().Name;
                LiteEventLog.Write("multi_wake_failed", new {
                    errorType = ex.GetType().Name,
                    submissionMayBeUncertain = attemptCommitted
                });
                if (!attemptCommitted)
                {
                    DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                        "worker_exception");
                    return;
                }
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
