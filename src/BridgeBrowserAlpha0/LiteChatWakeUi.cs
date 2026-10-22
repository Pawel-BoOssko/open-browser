using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    internal const string WakeWorkerHydratedScript =
        "!!document.querySelector('#prompt-textarea,[data-testid=composer-text-input],textarea,[contenteditable=true]') " +
        "&& !!document.querySelector('main [data-user-message-bubble],main [data-message-author-role],main [data-chatgpt-selection-message-id]')";
    private readonly System.Windows.Forms.Timer _chatWakeTimer = new() { Interval = 20000 };
    private bool _multiWakeEnabled;
    private bool _chatWakeBusy;
    // One circuit breaker for the ONE shared background WebView2 worker.
    // Foreground chat waking must never depend on this background cooldown.
    private readonly LiteRendererBackpressure _workerProbeBackpressure = new();
    // Set only after a full worker navigation and observed chat history.
    // SPA SourceChanged and any full navigation revoke this identity.
    private string? _workerVerifiedChatUrl;
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

    // After a click, React can append the user bubble asynchronously.
    // Probe for a bounded interval without EVER submitting the receipt again.
    // A timed-out renderer gets no subsequent script on this attempt.
    internal static async Task<bool> WaitForWakeReceiptAsync(
        CoreWebView2 core, string receipt)
    {
        const int totalBudgetMs = 4200;
        if (receipt.Length != 32 || !receipt.All(c =>
            c is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        string check = "window.__OPENBROWSER_RESUME_SENDER__?.hasAck(" +
            JsonSerializer.Serialize(receipt) + ") === true";
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int probe = 0; probe < 8; probe++)
        {
            int left = totalBudgetMs - (int)clock.ElapsedMilliseconds;
            if (left <= 0) break;
            try
            {
                if (await ExecuteWorkerScriptAsync(core, check,
                    Math.Min(1200, left)) == "true") return true;
            }
            catch (TimeoutException)
            {
                LiteEventLog.Write("multi_wake_receipt_probe_timeout", new {
                    probe = probe + 1, elapsedMs = clock.ElapsedMilliseconds
                });
                return false;
            }
            int pause = Math.Min(400,
                totalBudgetMs - (int)clock.ElapsedMilliseconds);
            if (pause <= 0) break;
            await Task.Delay(pause);
        }
        return false;
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
        core.NavigationStarting += (_, _) => _workerVerifiedChatUrl = null;
        core.SourceChanged += (_, _) =>
        {
            if (_workerVerifiedChatUrl is not null &&
                (!LiteDevWakeState.IsChatUrl(core.Source) ||
                 LiteDevWakeState.NormalizeUrl(core.Source) != _workerVerifiedChatUrl))
                _workerVerifiedChatUrl = null;
        };
        core.ProcessFailed += (_, _) => _workerVerifiedChatUrl = null;
        core.Settings.AreDevToolsEnabled = false;
        // Keep worker responsive while loading a real conversation. Switch to
        // Low only once the wake attempt ends and the worker is idle.
        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
        LiteEventLog.Write("multi_wake_worker_initialized",
            new { memoryTarget = "normal_active" });
    }

    internal static bool VerifiedWorkerMatches(string? verifiedUrl,
        string? currentSource, string targetUrl)
    {
        return !string.IsNullOrEmpty(verifiedUrl) &&
            string.Equals(verifiedUrl, targetUrl, StringComparison.Ordinal) &&
            LiteDevWakeState.IsChatUrl(currentSource) &&
            LiteDevWakeState.NormalizeUrl(currentSource!) == targetUrl;
    }

    internal static async Task<bool> VerifyWorkerHydrationAsync(
        string url, string? verifiedUrl, Func<string?> readSource,
        Func<Task<string>> probe, Func<string?>? readVerifiedUrl = null)
    {
        // Check both sides of the asynchronous DOM read. A route can change
        // while the WebView2 script is awaiting its result. SourceChanged can
        // also revoke verifiedUrl, even if SPA navigates A->B->A.
        if (!VerifiedWorkerMatches(verifiedUrl, readSource(), url)) return false;
        string result = await probe();
        return result == "true" && VerifiedWorkerMatches(
            readVerifiedUrl?.Invoke() ?? verifiedUrl, readSource(), url);
    }

    internal static async Task<bool> IsWorkerHydratedForChatAsync(
        CoreWebView2 core, string url, string? verifiedUrl,
        Func<string?>? readVerifiedUrl = null)
    {
        // The current DOM may be from chat A during an SPA route change to B.
        // A matching address/composer/history alone is NOT proof of identity.
        try
        {
            return await VerifyWorkerHydrationAsync(
                url, verifiedUrl, () => core.Source,
                () => ExecuteWorkerScriptAsync(core, WakeWorkerHydratedScript),
                readVerifiedUrl);
        }
        catch (TimeoutException)
        {
            LiteEventLog.Write("multi_wake_worker_reuse_probe_timeout");
            // Never send a second script to this renderer in the same attempt.
            throw;
        }
    }

    internal static int WorkerHistoryProbeBudgetMs(long elapsedMs)
    {
        // A single broken chat must not occupy the shared worker for
        // 35 separate 6.5-second renderer timeouts.
        if (elapsedMs < 0 || elapsedMs >= 25000) return 0;
        return (int)Math.Min(6500L, 25000L - elapsedMs);
    }

    private async Task<bool> NavigateWorkerAsync(string url)
    {
        var core = _chatWakeWorker.CoreWebView2;
        if (core is null) return false;
        // Reuse this worker only after checking both URL and hydrated history.
        // A timed-out reuse probe MUST NOT trigger another navigation/script.
        try
        {
            if (await IsWorkerHydratedForChatAsync(core, url,
                _workerVerifiedChatUrl, () => _workerVerifiedChatUrl))
            {
                LiteEventLog.Write("multi_wake_worker_reused_hydrated");
                return true;
            }
        }
        catch (TimeoutException)
        {
            MarkWorkerProbeFailed("reuse_probe_timeout");
            return false;
        }
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
            completed.TrySetResult(e.IsSuccess);
        core.NavigationCompleted += OnCompleted;
        try
        {
            _workerVerifiedChatUrl = null;
            core.Navigate(url);
            var ready = await Task.WhenAny(completed.Task, Task.Delay(45000));
            if (ready != completed.Task || !await completed.Task)
            {
                MarkWorkerProbeFailed("navigation_not_completed");
                return false;
            }
            var hydrationClock = System.Diagnostics.Stopwatch.StartNew();
            int hydrationAttempts = 0;
            while (true)
            {
                int remaining = WorkerHistoryProbeBudgetMs(hydrationClock.ElapsedMilliseconds);
                if (remaining <= 0) break;
                if (core.Source is null ||
                    !LiteDevWakeState.IsChatUrl(core.Source) ||
                    LiteDevWakeState.NormalizeUrl(core.Source) != url) return false;
                // A composer can render before history, but may never hydrate.
                // Never type into an empty shell; bound TOTAL attempts to 25 s.
                hydrationAttempts++;
                string found;
                try
                {
                    found = await ExecuteWorkerScriptAsync(
                        core, WakeWorkerHydratedScript, remaining);
                }
                catch (TimeoutException)
                {
                    LiteEventLog.Write("multi_wake_history_probe_timeout", new {
                        elapsedMs = hydrationClock.ElapsedMilliseconds,
                        attempts = hydrationAttempts, waitBudgetMs = 25000
                    });
                    MarkWorkerProbeFailed("history_probe_timeout");
                    return false; // Do not queue another script on a hung renderer.
                }
                if (found == "true")
                {
                    // A SPA can change the worker route while the awaited
                    // history script is running; never certify stale history.
                    if (!VerifiedWorkerMatches(url, core.Source, url))
                    {
                        LiteEventLog.Write("multi_wake_worker_route_changed_during_probe");
                        return false;
                    }
                    _workerVerifiedChatUrl = url;
                    _workerProbeBackpressure.MarkNavigationRecovered();
                    return true;
                }
                int pause = Math.Min(700,
                    WorkerHistoryProbeBudgetMs(hydrationClock.ElapsedMilliseconds));
                if (pause > 0) await Task.Delay(pause);
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
                        hydrationAttempts,
                        elapsedMs = hydrationClock.ElapsedMilliseconds,
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
                    domWaitSeconds = 25,
                    hydrationAttempts,
                    elapsedMs = hydrationClock.ElapsedMilliseconds,
                    diagnosticError = ex.GetType().Name
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

    private void MarkWorkerProbeFailed(string reason)
    {
        var cooldown = _workerProbeBackpressure.MarkFailed(DateTimeOffset.UtcNow);
        _workerVerifiedChatUrl = null;
        LiteEventLog.Write("multi_wake_worker_backpressure", new {
            reason, consecutiveFailures = _workerProbeBackpressure.ConsecutiveFailures,
            cooldownSeconds = (int)cooldown.TotalSeconds
        });
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

    internal static bool ShouldDeferWorkerProbe(bool useForeground,
        LiteRendererBackpressure gate, DateTimeOffset now)
    {
        return !useForeground && !gate.MayProbe(now);
    }

    internal static bool IsDefinitelyUnsentWake(string result, string phase)
    {
        // A committed attempt is NOT synonymous with a clicked Send button.
        // Only explicit no-click states may release the durable answer fence.
        return result.StartsWith("busy_", StringComparison.Ordinal) ||
            result is "missing_composer" or "invalid" or "already_attempted" or
                "blocked_navigation" ||
            (result == "submission_requested" &&
                (phase == "draft_button_missing" ||
                 phase.StartsWith("blocked_", StringComparison.Ordinal)));
    }

    internal static LiteChatWakeRegistry.Chat? SelectDueChat(
        IEnumerable<LiteChatWakeRegistry.Chat> entries, DateTimeOffset now,
        string? foregroundSource)
    {
        // A foreground preference must not starve much older background wakes
        // while the foreground chat is generating or waiting for a new answer.
        var due = entries.Where(x => x.Enabled && x.DueUtc <= now)
            .OrderBy(x => x.DueUtc).ThenBy(x => x.Url, StringComparer.Ordinal)
            .ToArray();
        if (due.Length == 0) return null;
        if (!LiteDevWakeState.IsChatUrl(foregroundSource)) return due[0];
        string fg = LiteDevWakeState.NormalizeUrl(foregroundSource!);
        var foreground = due.FirstOrDefault(x => x.Url == fg);
        return foreground is not null &&
            foreground.DueUtc <= due[0].DueUtc.AddMinutes(1)
                ? foreground : due[0];
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
            var due = SelectDueChat(entries, now, foregroundUrl);
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
            bool backgroundAttempt = false;
            try
            {
                var foreground = _browser.CoreWebView2;
                bool useForeground = foreground is not null &&
                    LiteDevWakeState.IsChatUrl(foreground.Source) &&
                    LiteDevWakeState.NormalizeUrl(foreground.Source) == due.Url;
                backgroundAttempt = !useForeground;
                if (ShouldDeferWorkerProbe(useForeground,
                    _workerProbeBackpressure, DateTimeOffset.UtcNow))
                {
                    LiteEventLog.Write("multi_wake_worker_cooldown", new {
                        consecutiveFailures = _workerProbeBackpressure.ConsecutiveFailures,
                        remainingSeconds = Math.Max(0, (int)Math.Ceiling(
                            (_workerProbeBackpressure.BlockedUntilUtc -
                             DateTimeOffset.UtcNow).TotalSeconds))
                    });
                    DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                        "worker_backpressure");
                    return;
                }
                if (!useForeground)
                {
                    await EnsureWakeWorkerAsync();
                    if (_chatWakeWorker.CoreWebView2 is { } workerCore)
                        workerCore.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                }
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
                    // Bind target identity inside the *same* script invocation
                    // that injects the prompt. A changed SPA route must not send.
                    string js = "window.__OPENBROWSER_RESUME_SENDER__?.sendDirect(" +
                        JsonSerializer.Serialize(payload) + "," +
                        JsonSerializer.Serialize(receipt) + ",true," +
                        JsonSerializer.Serialize(new Uri(due.Url).AbsolutePath) + ")";
                    var result = await ExecuteWorkerScriptAsync(
                        wakeCore, js);
                    state = JsonSerializer.Deserialize<string>(result) ?? "sender_missing";
                    if (IsDefinitelyUnsentWake(state, "not_submitted"))
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
                    // Only after a successful submit click: wait briefly
                    // for React's real USER bubble in this exact chat.
                    // This never clicks Send a second time.
                    bool visible = state == "submission_requested" &&
                        phase is "click_invoked" or "form_submitted" &&
                        await WaitForWakeReceiptAsync(wakeCore, receipt);
                    if (!visible && IsDefinitelyUnsentWake(state, phase))
                    {
                        entries = LiteChatWakeRegistry.Read();
                        var unsent = entries.FirstOrDefault(x => x.Url == due.Url);
                        if (unsent is not null && unsent.Enabled &&
                            unsent.LastAttemptUtc == now)
                        {
                            unsent.LastAnswerFingerprint = "";
                            unsent.DueUtc = DateTimeOffset.UtcNow.AddMinutes(2);
                            unsent.LastState = "waiting_" + phase;
                            LiteChatWakeRegistry.Write(entries);
                        }
                        LiteEventLog.Write("multi_wake_definitely_unsent", new {
                            reason = phase, definitelyNotSent = true,
                            retryMinutes = 2
                        });
                        UpdateMultiWakeStatus();
                        return;
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
                if (backgroundAttempt &&
                    (ex is TimeoutException ||
                     ex is System.Runtime.InteropServices.COMException))
                    MarkWorkerProbeFailed(ex is TimeoutException
                        ? "worker_script_timeout" : "worker_com_failure");
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
        finally
        {
            try
            {
                // One shared background worker, memory target reduced between
                // chats. Never lower the visible chat's performance target.
                if (_chatWakeWorker.CoreWebView2 is { } idleCore)
                    idleCore.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("multi_wake_worker_memory_target_failed",
                    new { errorType = ex.GetType().Name });
            }
            _chatWakeBusy = false;
        }
    }
}
