using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    internal const string WakeWorkerHydratedScript =
        "!!document.querySelector('#prompt-textarea,[data-testid=composer-text-input],textarea,[contenteditable=true]') " +
        "&& !!document.querySelector('main [data-user-message-bubble],main [data-message-author-role],main [data-chatgpt-selection-message-id]')";
    // P0 wake396: NIEAKTUALNE for OBL: a history bubble was required
    // before a newly rotated EMPTY lead could send its first message.
    // After exact NavigationId and URL, an editor under main suffices.
    // sendDirect still validates URL, draft, Stop and payload before click.
    internal const string WakeWorkerOblComposerReadyScript =
        "!!document.querySelector('main') && " +
        "!!document.querySelector('#prompt-textarea,[data-testid=composer-text-input],textarea,[contenteditable=true]')";
    private readonly System.Windows.Forms.Timer _chatWakeTimer = new() { Interval = 20000 };
    private bool _multiWakeEnabled;
    private bool _chatWakeBusy;
    // One circuit breaker for the ONE shared background WebView2 worker.
    // Foreground chat waking must never depend on this background cooldown.
    private readonly LiteRendererBackpressure _workerProbeBackpressure = new();
    // Set after full navigation and observed history, or for OBL a ready composer.
    // SPA SourceChanged and any full navigation revoke this identity.
    private string? _workerVerifiedChatUrl;
    // Monotonic per-worker route epoch prevents A->B->A during an awaited
    // hydration probe from re-certifying a DOM from an obsolete navigation.
    private long _workerRouteEpoch;
    // Foreground SPA changes must also invalidate two-step assistant preflight.
    private long _foregroundWakeRouteEpoch;
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
            _chatWakeTimer.Tick += async (_, _) => {
                try {
                    await PollAllChatWakesAsync();
                    await ProbeSuccessorFromWorkerOnceAsync();
                    await ProbeSeedAckedSuccessorReadbackOnceAsync();
                    await ProbeUncertainSuccessorReadbackOnceAsync();
                    await CaptureExistingUserBubbleHashOnceAsync();
                    await ProbeExistingUserBubblePairOnceAsync();
                }
                catch (Exception ex) {
                    LiteEventLog.Write("successor_probe_timer_failure",
                        new { errorType=ex.GetType().Name });
                }
            };
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
        string auditId = LiteUiActionAudit.Requested("current_chat_wake_toggle");
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
            if (entry.Enabled) entry.DueUtc = DateTimeOffset.UtcNow.AddMinutes(entry.EffectiveMinutes);
            entry.LastState = entry.Enabled ? "armed" : "paused";
            LiteUiActionAudit.Executed(auditId, "current_chat_wake_toggle", "registry_write_invoked");
            LiteChatWakeRegistry.Write(entries);
            var stored = LiteChatWakeRegistry.Read().FirstOrDefault(x => x.Url == entry.Url);
            LiteUiActionAudit.Result(auditId, "current_chat_wake_toggle",
                stored?.Enabled == entry.Enabled ? "persisted_verified" : "readback_mismatch",
                stored?.Enabled == entry.Enabled);
            UpdateMultiWakeStatus();
        }
        catch (Exception ex)
        {
            LiteUiActionAudit.Result(auditId, "current_chat_wake_toggle",
                "exception_" + ex.GetType().Name, false);
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
        var intervalLabel = new Label { Text = "Odstęp (15–1440 min):", Left = 15, Top = 60, Width = 180 };
        var interval = new NumericUpDown
        {
            Minimum = 15, Maximum = 1440, Value = existing?.EffectiveMinutes ?? 15,
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

    // Display the LAST ACTUALLY RESERVED wake number, never infer from
    // the next clock slot. Deferral must not increment the displayed number.
    internal static string FormatWakeStatus(long wakeSequence,
        DateTimeOffset dueUtc, bool enabled)
    {
        string previous = wakeSequence > 0
            ? "#" + wakeSequence.ToString("D6",
                System.Globalization.CultureInfo.InvariantCulture)
            : "brak";
        return enabled
            ? "Poprz. " + previous + " | Nast. " +
                dueUtc.ToLocalTime().ToString("HH:mm")
            : "Poprz. " + previous + " | Pauza";
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
                ? "Autopraca: WŁ. " + entry.EffectiveMinutes + "m" : "Autopraca: WYŁ.";
            _devWakeStatus.Text = entry is null
                ? "Bieżący czat: bez wybudzenia | Aktywne: " +
                    entries.Count(x => x.Enabled)
                : FormatWakeStatus(entry.WakeSequence, entry.DueUtc,
                    entry.Enabled);
            string details = entry is null ? "Brak harmonogramu bieżącego czatu."
                : LiteWakeStatusExplanation.Display(entry);
            if (entry is not null &&
                entry.LastState.StartsWith("waiting_", StringComparison.Ordinal))
            {
                string reasonCode = entry.LastState["waiting_".Length..];
                string compact = reasonCode == "draft_present"
                    ? "szkic starego czatu" : LiteWakeStatusExplanation.Reason(reasonCode);
                _devWakeStatus.Text += " | Odroczono: " + compact;
            }
            _devWakeStatus.AccessibleDescription =
                "Aktywne czaty: " + entries.Count(x => x.Enabled) +
                Environment.NewLine + details;
            _wakeStatusTooltip.SetToolTip(_devWakeStatus, details);
            // Surface server backpressure instead of a misleading OK state.
            var cooldown = _wakeRead429Throttle.Remaining(DateTimeOffset.UtcNow);
            if (cooldown > TimeSpan.Zero)
                _devWakeStatus.Text += " | HTTP 429: limit serwera, " +
                    Math.Ceiling(cooldown.TotalMinutes) + " min";
            else
            {
                string healthPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "OpenBrowser", "ChatGPT-Lite", "diagnostics",
                    "obl-wake-health.json");
                if (File.Exists(healthPath))
                {
                    using var health = System.Text.Json.JsonDocument.Parse(
                        File.ReadAllText(healthPath));
                    var h = health.RootElement;
                    if (h.TryGetProperty("status", out var status) &&
                        h.TryGetProperty("utc", out var stamp) &&
                        status.GetString() is "degraded" or "needs_attention" &&
                        DateTimeOffset.TryParse(stamp.GetString(), out var seen) &&
                        (DateTimeOffset.UtcNow-seen).TotalMinutes < 20)
                        _devWakeStatus.Text +=
                            " | Wybudzanie: wymaga uwagi (backend/odpowiedz)";
                }
            }
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
    internal static bool IsWakeReceiptRouteStable(string targetUrl,
        string? source, long startEpoch, long currentEpoch) =>
        IsWorkerRouteStable(targetUrl, source, startEpoch, currentEpoch);

    internal static async Task<bool> WaitForWakeReceiptAsync(
        CoreWebView2 core, string receipt, string? targetUrl = null,
        Func<long>? readEpoch = null, string? expectedWakeMarker = null)
    {
        // P0 wake356: a natural OBL wake reached click_invoked but its
        // 15-second DOM receipt probe expired without visible ACK. NIEAKTUALNE
        // for numbered OBL wakes: treating 15 seconds as the final observation
        // window. Allow late React hydration, never a second send click.
        int totalBudgetMs = expectedWakeMarker is null ? 15000 : 30000;
        if (receipt.Length != 32 || !receipt.All(c =>
            c is >= '0' and <= '9' or >= 'a' and <= 'f')) return false;
        // Callers outside the offline fixture supply BOTH target and route
        // epoch. An A->B->A during awaited JS must never certify a receipt.
        if ((targetUrl is null) != (readEpoch is null)) return false;
        long initialEpoch = readEpoch?.Invoke() ?? 0;
        bool stable() => targetUrl is null ||
            IsWakeReceiptRouteStable(targetUrl, core.Source,
                initialEpoch, readEpoch!());
        if (!stable()) return false;
        // P0 wake345: pass OBL's visible numeric marker explicitly. A SPA
        // reload can clear the JS receipt-to-marker map after a real click.
        // NIEAKTUALNE: depend exclusively on the transient page map.
        string check = "window.__OPENBROWSER_RESUME_SENDER__?.hasAck(" +
            JsonSerializer.Serialize(receipt) + "," +
            JsonSerializer.Serialize(expectedWakeMarker) + ") === true";
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int probe = 0; clock.ElapsedMilliseconds < totalBudgetMs; probe++)
        {
            int left = totalBudgetMs - (int)clock.ElapsedMilliseconds;
            if (left <= 0) break;
            if (!stable()) return false;
            try
            {
                bool acknowledged = await ExecuteWorkerScriptAsync(core, check,
                    Math.Min(1200, left)) == "true";
                // Even a true USER bubble from an old SPA document is not
                // evidence of an ACK in the selected scheduled conversation.
                if (!stable()) return false;
                if (acknowledged) return true;
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
            if (!stable()) return false;
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
        // The background worker needs the same structural terminal-error
        // detector as foreground, otherwise max-chat-length may stay invisible
        // to the very wake engine expected to recognize it.
        var responseScript = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "LiteResponseSignal.js"));
        await core.AddScriptToExecuteOnDocumentCreatedAsync(responseScript);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(probeScript);
        await InstallCaptureScriptAsync(core);
        string pruneScript = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "LiteDomHardPrune.js"));
        await core.AddScriptToExecuteOnDocumentCreatedAsync(pruneScript);
        // Capture safe metadata for hidden-worker failures too. Main WebView
        // telemetry alone cannot identify which route triggered HTTP 429.
        core.WebResourceResponseReceived += (_, e) =>
            LogBackendResponseMetadata(e, "worker");
        core.NavigationStarting += (_, _) => {
            _workerRouteEpoch++;
            _workerVerifiedChatUrl = null;
        };
        core.SourceChanged += (_, _) =>
        {
            _workerRouteEpoch++;
            if (_workerVerifiedChatUrl is not null &&
                (!LiteDevWakeState.IsChatUrl(core.Source) ||
                 LiteDevWakeState.NormalizeUrl(core.Source) != _workerVerifiedChatUrl))
                _workerVerifiedChatUrl = null;
        };
        core.ProcessFailed += (_, _) => {
            _workerRouteEpoch++;
            _workerVerifiedChatUrl = null;
        };
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

    internal static bool IsWorkerRouteStable(string url, string? source,
        long beforeEpoch, long afterEpoch)
    {
        return beforeEpoch == afterEpoch &&
            VerifiedWorkerMatches(url, source, url);
    }

    internal static async Task<bool> VerifyWorkerHydrationAsync(
        string url, string? verifiedUrl, Func<string?> readSource,
        Func<Task<string>> probe, Func<string?>? readVerifiedUrl = null,
        Func<long>? readEpoch = null)
    {
        // Check both sides of the asynchronous DOM read. A route can change
        // while the WebView2 script is awaiting its result. SourceChanged can
        // also revoke verifiedUrl, even if SPA navigates A->B->A.
        long beforeEpoch = readEpoch?.Invoke() ?? 0;
        if (!VerifiedWorkerMatches(verifiedUrl, readSource(), url)) return false;
        string result = await probe();
        return result == "true" && VerifiedWorkerMatches(
            readVerifiedUrl is null ? verifiedUrl : readVerifiedUrl(),
            readSource(), url) &&
            (readEpoch is null || IsWorkerRouteStable(
                url, readSource(), beforeEpoch, readEpoch()));
    }

    internal static async Task<bool> IsWorkerHydratedForChatAsync(
        CoreWebView2 core, string url, string? verifiedUrl,
        Func<string?>? readVerifiedUrl = null, Func<long>? readEpoch = null,
        bool isOblLead = false)
    {
        // The current DOM may be from chat A during an SPA route change to B.
        // A matching address/composer/history alone is NOT proof of identity.
        try
        {
            return await VerifyWorkerHydrationAsync(
                url, verifiedUrl, () => core.Source,
                () => ExecuteWorkerScriptAsync(core,
                    isOblLead ? WakeWorkerOblComposerReadyScript :
                        WakeWorkerHydratedScript),
                readVerifiedUrl, readEpoch);
        }
        catch (TimeoutException)
        {
            LiteEventLog.Write("multi_wake_worker_reuse_probe_timeout");
            // Never send a second script to this renderer in the same attempt.
            throw;
        }
    }

    internal static int WorkerHistoryProbeBudgetMs(long elapsedMs,
        int maxBudgetMs = 25000)
    {
        // P0 obl-5w3 wake347: NIEAKTUALNE for OBL: 25 seconds and
        // 36 repeated probes of an empty 0-turn worker app shell.
        // Other projects retain their previous 25-second budget.
        if (elapsedMs < 0 || elapsedMs >= maxBudgetMs) return 0;
        return (int)Math.Min(6500L, maxBudgetMs - elapsedMs);
    }

    // NavigationCompleted is not necessarily for our latest Navigate(url):
    // WebView2 can deliver a delayed completion from a previous document.
    // Only a completion with the ID of THIS NavigationStarting is trusted.
    internal static bool MatchesWorkerNavigationCompletion(
        ulong? requestedNavigationId, ulong completedNavigationId,
        bool isSuccess)
    {
        return requestedNavigationId.HasValue &&
            requestedNavigationId.Value == completedNavigationId && isSuccess;
    }

    // A previous or unrelated NavigationStarting event may arrive after
    // subscription and before our Navigate(url) event. Bind the ID only
    // to the exact chat URL that the scheduler is trying to visit.
    internal static bool MatchesWorkerNavigationStart(
        string requestedChatUrl, string? startingUrl)
    {
        return LiteDevWakeState.IsChatUrl(startingUrl) &&
            string.Equals(
                LiteDevWakeState.NormalizeUrl(startingUrl!),
                requestedChatUrl, StringComparison.Ordinal);
    }

    private async Task<bool> NavigateWorkerAsync(string url,
        bool isOblLead = false)
    {
        var core = _chatWakeWorker.CoreWebView2;
        if (core is null) return false;
        // P0 wake397 NIEAKTUALNE for OBL: worker reuse required history
        // bubble even after a validated empty lead had hydrated its editor.
        // Reuse on matching URL/epoch and clean composer; sender verifies
        // draft/Stop/route at submission. Others still require history.
        // A timed-out reuse probe MUST NOT trigger another navigation/script.
        try
        {
            if (await IsWorkerHydratedForChatAsync(core, url,
                _workerVerifiedChatUrl, () => _workerVerifiedChatUrl,
                () => _workerRouteEpoch, isOblLead))
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
        ulong? requestedNavigationId = null;
        bool navigationIssued = false;
        void OnStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // An unrelated older NavigationStarting can be delivered late.
            // Bind only the URL of this Navigate(url), and ignore redirects
            // with other paths rather than certifying a foreign conversation.
            if (navigationIssued && requestedNavigationId is null &&
                MatchesWorkerNavigationStart(url, e.Uri))
                requestedNavigationId = e.NavigationId;
        }
        void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (requestedNavigationId is not { } expected ||
                expected != e.NavigationId) return;
            completed.TrySetResult(MatchesWorkerNavigationCompletion(
                requestedNavigationId, e.NavigationId, e.IsSuccess));
        }
        core.NavigationStarting += OnStarting;
        core.NavigationCompleted += OnCompleted;
        try
        {
            _workerVerifiedChatUrl = null;
            navigationIssued = true;
            core.Navigate(url);
            // P0 obl-5w3 wake367: NIEAKTUALNE for OBL: wait 45 seconds for
            // NavigationCompleted before trying the same registered chat again.
            // The OBL hydration budget is already 8 seconds. Keep strict
            // NavigationId and URL checks; cap this earlier wait at 12 seconds.
            var ready = await Task.WhenAny(completed.Task,
                Task.Delay(isOblLead ? 12000 : 45000));
            if (ready != completed.Task || !await completed.Task)
            {
                MarkWorkerProbeFailed("navigation_not_completed");
                return false;
            }
            var hydrationClock = System.Diagnostics.Stopwatch.StartNew();
            // P0 wake347: OBL retries the same URL sooner when the worker
            // stays on an empty shell. Never bypass chat identity or draft checks.
            int hydrationBudgetMs = isOblLead ? 8000 : 25000;
            int hydrationAttempts = 0;
            while (true)
            {
                int remaining = WorkerHistoryProbeBudgetMs(
                    hydrationClock.ElapsedMilliseconds, hydrationBudgetMs);
                if (remaining <= 0) break;
                if (core.Source is null ||
                    !LiteDevWakeState.IsChatUrl(core.Source) ||
                    LiteDevWakeState.NormalizeUrl(core.Source) != url) return false;
                // A composer can render before history, but may never hydrate.
                // Never type into an empty shell; bound attempts per project.
                hydrationAttempts++;
                long probeRouteEpoch = _workerRouteEpoch;
                string found;
                try
                {
                    found = await ExecuteWorkerScriptAsync(
                        core, isOblLead ? WakeWorkerOblComposerReadyScript :
                            WakeWorkerHydratedScript, remaining);
                }
                catch (TimeoutException)
                {
                    LiteEventLog.Write("multi_wake_history_probe_timeout", new {
                        elapsedMs = hydrationClock.ElapsedMilliseconds,
                        attempts = hydrationAttempts, waitBudgetMs = hydrationBudgetMs
                    });
                    MarkWorkerProbeFailed("history_probe_timeout");
                    return false; // Do not queue another script on a hung renderer.
                }
                if (found == "true")
                {
                    // A SPA can change the worker route while the awaited
                    // history script is running; never certify stale history.
                    if (!IsWorkerRouteStable(url, core.Source,
                        probeRouteEpoch, _workerRouteEpoch))
                    {
                        LiteEventLog.Write("multi_wake_worker_route_changed_during_probe");
                        return false;
                    }
                    _workerVerifiedChatUrl = url;
                    _workerProbeBackpressure.MarkNavigationRecovered();
                    return true;
                }
                int pause = Math.Min(700,
                    WorkerHistoryProbeBudgetMs(
                        hydrationClock.ElapsedMilliseconds, hydrationBudgetMs));
                if (pause > 0) await Task.Delay(pause);
            }
            // P0 obl-5w3 wake366: NIEAKTUALNE for OBL: after the bounded
            // hydration wait, run another WebView2 script for metadata only.
            // That probe cannot make the missing history eligible to send;
            // retry the same URL without risking a second renderer timeout.
            // Historical detailed diagnosis remains available for non-OBL.
            if (isOblLead)
            {
                LiteEventLog.Write("multi_wake_history_not_ready", new {
                    domWaitSeconds = hydrationBudgetMs / 1000,
                    hydrationAttempts,
                    elapsedMs = hydrationClock.ElapsedMilliseconds,
                    diagnosticProbeSkipped = true,
                    retrySameChat = true
                });
                MarkWorkerProbeFailed("history_not_ready");
                return false;
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
                // A renderer that could not hydrate in 25s must not incur
                // another full 6.5s diagnostic timeout on the same attempt.
                var encoded = await ExecuteWorkerScriptAsync(core, countJs, 1500);
                var metadata = JsonSerializer.Deserialize<string>(encoded);
                if (!string.IsNullOrEmpty(metadata))
                {
                    using var parsed = JsonDocument.Parse(metadata);
                    var v = parsed.RootElement;
                    LiteEventLog.Write("multi_wake_history_not_ready", new {
                        domWaitSeconds = hydrationBudgetMs / 1000,
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
            catch (TimeoutException)
            {
                LiteEventLog.Write("multi_wake_history_not_ready", new {
                    domWaitSeconds = hydrationBudgetMs / 1000,
                    hydrationAttempts,
                    diagnosticError = "diagnostic_timeout"
                });
                // Do not enqueue more JavaScript on a renderer that timed out.
                MarkWorkerProbeFailed("history_metadata_timeout");
                return false;
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("multi_wake_history_not_ready", new {
                    domWaitSeconds = hydrationBudgetMs / 1000,
                    hydrationAttempts,
                    elapsedMs = hydrationClock.ElapsedMilliseconds,
                    diagnosticError = ex.GetType().Name
                });
            }
            // Successful NavigationCompleted is NOT a successful history
            // hydration. NIEAKTUALNE for OBL: spend 25 s per empty shell.
            // Preserve explicit same-URL retry without typing into an empty page.
            MarkWorkerProbeFailed("history_not_ready");
            return false;
        }
        finally
        {
            core.NavigationStarting -= OnStarting;
            core.NavigationCompleted -= OnCompleted;
        }
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

    internal static bool IsPreflightRouteStable(
        string targetUrl, string? source, long initialEpoch, long currentEpoch)
    {
        return IsWorkerRouteStable(targetUrl, source, initialEpoch, currentEpoch);
    }

    internal static async Task<(LiteChatWakePreflight.Observation? First,
        LiteChatWakePreflight.Observation? Second, bool Stable)>
        CaptureStablePreflightAsync(string targetUrl,
            Func<string?> readSource, Func<long> readEpoch,
            Func<Task<LiteChatWakePreflight.Observation?>> capture,
            Func<Task> delay)
    {
        // A two-snapshot assistant check is only valid when the navigation
        // identity remains unchanged across both awaited scripts and delay.
        long epoch = readEpoch();
        bool stable() => IsPreflightRouteStable(
            targetUrl, readSource(), epoch, readEpoch());
        if (!stable()) return (null, null, false);
        var first = await capture();
        if (!stable()) return (null, null, false);
        await delay();
        if (!stable()) return (null, null, false);
        var second = await capture();
        return stable() ? (first, second, true) : (null, null, false);
    }

    // P0 obl-5w3: ordinary OBL wakes do not need a second read-only
    // assistant-history probe separated by 1500ms. The sender still checks
    // route, editor draft and Stop at the point of its actual click.
    // NIEAKTUALNE for ordinary OBL wake: mandatory two-read preflight delay.
    internal static async Task<(LiteChatWakePreflight.Observation? First,
        LiteChatWakePreflight.Observation? Second, bool Stable)>
        CaptureFastOblPreflightAsync(string targetUrl,
            Func<string?> readSource, Func<long> readEpoch,
            Func<Task<LiteChatWakePreflight.Observation?>> capture)
    {
        long epoch = readEpoch();
        bool stable() => IsPreflightRouteStable(
            targetUrl, readSource(), epoch, readEpoch());
        if (!stable()) return (null, null, false);
        var observed = await capture();
        return stable() ? (observed, observed, true) : (null, null, false);
    }

    private async Task<(string Reason, string Hash, bool Complete,
        LiteModelStatus.Parsed? Status, int ObservedTurns, bool DraftPresent)>
        CheckWorkerPreflightAsync(CoreWebView2 core, string url, string previousHash,
            bool isOblLead = false)
    {
        bool foreground = ReferenceEquals(core, _browser.CoreWebView2);
        long epoch() => foreground ? _foregroundWakeRouteEpoch : _workerRouteEpoch;
        var result = isOblLead
            ? await CaptureFastOblPreflightAsync(url, () => core.Source, epoch,
                () => ReadWorkerObservationAsync(core))
            : await CaptureStablePreflightAsync(url, () => core.Source, epoch,
                () => ReadWorkerObservationAsync(core), () => Task.Delay(1500));
        // A single unhydrated WebView2 snapshot is not proof that the
        // lead is broken. Confirm page failures and absent editors before
        // any emergency rotation, just as terminal model decisions are
        // independently confirmed. NIEAKTUALNE: one empty/error snapshot
        // can immediately classify the OBL lead as permanently failed.
        // P0 obl-5w3 wake362: NIEAKTUALNE for OBL: generic Error=true
        // alone forced a second, delayed preflight. Response_error already
        // qualifies for the direct sender's fresh URL, STOP and draft checks.
        // Confirm only terminal failures; missing composer goes directly to no-click rotation without a second 1500ms read.
        if (isOblLead && result.Stable && result.Second is { } quick &&
            (quick.FailureKind == "conversation_length_exceeded" ||
             // P0 wake391 NIEAKTUALNE: foreground error double-read.
             // P0 wake395 NIEAKTUALNE for OBL worker: second 1500ms read
             // for transient app/connection shells. Existing worker path
             // defers same URL without rotation and never sends into it.
             // P0 obl-5w3 wake387: NIEAKTUALNE for OBL: optional model DONE
              // forced an unnecessary 1500ms preflight although it cannot
              // stop the OBL project. Confirm actual whole-plan completion.
              LiteChatWakePreflight.IsCompletion(quick.Text)))
            result = await CaptureStablePreflightAsync(url,
                () => core.Source, epoch, () => ReadWorkerObservationAsync(core),
                () => Task.Delay(1500));
        // P0 obl-5w3 wake363: NIEAKTUALNE for OBL: after the two-sample
        // confirmation, an empty first assistant text forced up to TWO more
        // 1500ms preflight passes. The OBL sender already checks route,
        // STOP and draft at click time, so uncertainty must reach it now.
        // Keep historical hydration retries for other projects only.
        for (int retry = 0; !isOblLead && retry < 2 && result.Stable &&
             result.First?.Role == "assistant" &&
             result.Second?.Role == "assistant" &&
             string.IsNullOrWhiteSpace(result.First.Text) &&
             !string.IsNullOrWhiteSpace(result.Second.Text); retry++)
        {
            LiteEventLog.Write("multi_wake_preflight_hydration_retry", new {
                attempt = retry + 1, foreground,
                firstChars = result.First.Text.Length,
                secondChars = result.Second.Text.Length
            });
            result = await CaptureStablePreflightAsync(
                url, () => core.Source, epoch,
                () => ReadWorkerObservationAsync(core),
                () => Task.Delay(1500));
        }
        if (!result.Stable)
        {
            LiteEventLog.Write("multi_wake_preflight_navigation_changed",
                new { foreground, definitelyNotSent = true });
            return ("navigation_changed", "", false, null, 0, false);
        }
        var (first, second, _) = result;
        string expectedPath = new Uri(url).AbsolutePath;
        string reason = LiteChatWakePreflight.Eligibility(
            first, second, expectedPath, previousHash);
        // P0 wake398 NIEAKTUALNE for OBL: one fast preflight snapshot
        // duplicated as first/second was mistaken for TWO independent
        // failed conversation loads, classifying a visible clean composer
        // as a terminal app failure. Only the exact URL, visible primary
        // composer, zero turns, no draft and no Stop/tool activity qualify.
        // Mark read uncertainty and let sendDirect independently validate
        // the actual editor, route and Send button before trying a click.
        if (isOblLead && reason == "chatgpt_app_problem" &&
            first is { Error: true, FailureKind: "conversation_load_failed",
                TurnCount: 0, ComposerReady: true, EditorVisible: true,
                HasDraft: false, Generating: false, ToolBusy: false } editorReady &&
            editorReady.Location == expectedPath &&
            editorReady.EditorSource is "prompt-textarea" or "composer-text-input" &&
            second is { Error: true, FailureKind: "conversation_load_failed",
                TurnCount: 0, ComposerReady: true, EditorVisible: true,
                HasDraft: false, Generating: false, ToolBusy: false } &&
            second.Location == expectedPath)
            reason = "response_error";        // P0 obl-5w3 wake370: NIEAKTUALNE for OBL: read error or
        // generation can hide a positively observed owner draft.
        // Prioritize draft: rotate to a separate lead, never overwrite it.
        // Non-OBL projects retain their original preflight ordering.
        if (isOblLead && (first?.HasDraft == true || second?.HasDraft == true) &&
            reason is ("response_error" or "generating"))
            reason = "draft_present";
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
                draftChars = second?.DraftChars ?? 0,
                editorSource = second?.EditorSource,
                editorVisible = second?.EditorVisible,
                error = second?.Error,
                composer = second?.ComposerReady,
                targetMatches = second?.Location == expectedPath
            });
        }
        string answer = second?.Text ?? "";
        // Stable assistant fingerprint is also useful for an OWN pending
        // draft, even though draft_present correctly blocks normal sending.
        string hash = reason is "ready" or "draft_present" ?
            LiteChatWakePreflight.StableAssistantFingerprint(
                first, second, expectedPath) : "";
        var status = reason == "ready" ? LiteModelStatus.Parse(answer) : null;
        return (reason, hash,
            reason == "ready" && LiteChatWakePreflight.IsCompletion(answer),
            status, second?.TurnCount ?? 0,
            first?.HasDraft == true || second?.HasDraft == true);
    }

    internal static TimeSpan RecordWorkerProbeFailure(
        LiteRendererBackpressure gate, DateTimeOffset now)
    {
        return gate.MarkFailed(now);
    }

    private void MarkWorkerProbeFailed(string reason)
    {
        var cooldown = RecordWorkerProbeFailure(
            _workerProbeBackpressure, DateTimeOffset.UtcNow);
        _workerVerifiedChatUrl = null;
        LiteEventLog.Write("multi_wake_worker_backpressure", new {
            reason, consecutiveFailures = _workerProbeBackpressure.ConsecutiveFailures,
            cooldownSeconds = (int)cooldown.TotalSeconds
        });
    }

    // A recovered OB draft may have clicked but still be present because
    // React did not expose its user bubble. Its persisted state must fence
    // any re-click after a process restart or a new SPA document.
    internal static bool IsUnconfirmedRecoveredDraftState(string? state) =>
        state == "own_draft_clicked_unconfirmed";

    internal static bool CanResumeAfterUnconfirmedRecovery(
        string? priorState, string? priorHash, string preflightReason,
        string? observedHash)
    {
        if (!IsUnconfirmedRecoveredDraftState(priorState)) return true;
        // A "ready" answer alone is insufficient: without the hash of
        // the preceding assistant response this may be the SAME answer
        // which produced the recovered draft and the earlier click.
        return preflightReason == "ready" &&
            priorHash is { Length: 64 } &&
            observedHash is { Length: 64 } &&
            !string.Equals(priorHash, observedHash, StringComparison.OrdinalIgnoreCase);
    }

    internal enum WakePreflightGate
    {
        Continue,
        HoldUnconfirmedRecovery,
        Complete
    }

    internal static WakePreflightGate ChooseWakePreflightGate(
        bool verifiedComplete, string? priorState, string? priorHash,
        string preflightReason, string? observedHash)
    {
        // A verified terminal response MUST disable its schedule even when
        // a previous recovery click lacked an ACK or fingerprint. Completion
        // requires a "ready" two-snapshot preflight in the intended chat.
        if (verifiedComplete && preflightReason == "ready")
            return WakePreflightGate.Complete;
        // Owner 2026-10-30: prior unconfirmed wake is not a suppression
        // fence. A stable reply in the verified chat must allow the NEXT wake,
        // even when that causes a duplicate message. Retain route/draft checks.
        return preflightReason == "ready"
            ? WakePreflightGate.Continue
            : WakePreflightGate.HoldUnconfirmedRecovery;
    }

    // OBL identity is the registered project lead, never a phrase in Prompt:
    // production wake prompts intentionally omit project names and paths.
    internal static bool ShouldHoldUnconfirmedRecoveryForProject(
        WakePreflightGate gate, bool isOblLead) =>
        gate == WakePreflightGate.HoldUnconfirmedRecovery && !isOblLead;

    internal static string NextStateAfterWakeDeferral(string priorState,
        string reason) =>
        IsUnconfirmedRecoveredDraftState(priorState) ? priorState : "waiting_" + reason;

    // Explicit model status is advisory: only a stable, fully hydrated
    // two-snapshot preflight in the EXACT expected chat can apply it.
    // The ordinary wake path remains unchanged with no/invalid status.
    private bool HandleVerifiedModelStatus(LiteChatWakeRegistry.Chat due,
        List<LiteChatWakeRegistry.Chat> entries,
        LiteModelStatus.Parsed? parsed, string reason, string answerHash,
        bool isOblLead)
    {
        var now = DateTimeOffset.UtcNow;
        string? decision = LiteModelStatus.ApplyDecision(parsed, now, reason);
        if (decision is null or "CONTINUE") return false;
        if (answerHash.Length != 64) return false;
        if (LiteModelStatus.IgnoreModelRequestedHold(decision, isOblLead))
        {
            LiteEventLog.Write("multi_wake_model_hold_ignored", new {
                status = decision, project = "obl",
                assistantRequestOnly = true, nextWake = due.WakeSequence + 1
            });
            return false;
        }
        // P0 obl-5w3 wake361: NIEAKTUALNE for OBL: BLOCKED model status
        // imposed an unconditional two-minute wait before attempting a wake.
        // A tool failure concerns that specific action, not the scheduler.
        // Continue the ordinary safe sender now, retaining exact URL, live
        // STOP/draft checks and independently enforced tool permissions.
        if (isOblLead && decision == "BLOCKED")
        {
            LiteEventLog.Write("multi_wake_model_block_continue", new {
                wakeSequence = due.WakeSequence + 1,
                retryMinutes = 0, status = decision,
                toolRefusalNotOverridden = true
            });
            return false;
        }
        // NIEAKTUALNE for OBL: recoverable-model-block cooldown below.
        // Non-OBL scheduling behavior remains unchanged.
        if (LiteModelStatus.ShouldRecoverBlock(decision, isOblLead))
        {
            string blockKey = LiteOwnerInterventions.EventKey(
                due.Url, decision, answerHash);
            if (LiteModelStatus.IsBlockedRetryDue(
                    due.LastState, due.LastInterventionKey, blockKey))
            {
                // The previously blocked answer was already acknowledged
                // and cooled down; allow the normal route/preflight/receipt
                // gate to send ONE new wake. No immediate resend.
                LiteEventLog.Write("multi_wake_model_block_recovery_ready");
                return false;
            }
            due.LastState = "status_blocked_recovering";
            due.LastInterventionKey = blockKey;
            due.DueUtc = now.AddMinutes(2);
            LiteChatWakeRegistry.Write(entries);
            LiteWakeJournal.Write("deferred", due.WakeSequence + 1,
                result: "recoverable_model_block");
            LiteEventLog.Write("multi_wake_model_block_recoverable",
                new { retryMinutes = 2, scheduleEnabled = due.Enabled });
            UpdateMultiWakeStatus();
            return true;
        }
        if (decision == "WAIT_UNTIL")
        {
            due.DueUtc = parsed!.UntilUtc!.Value;
            due.LastState = "status_wait_until";
            LiteChatWakeRegistry.Write(entries);
            LiteWakeJournal.Write("deferred", due.WakeSequence + 1,
                result: "model_wait_until");
            LiteEventLog.Write("multi_wake_model_status", new {
                status = decision, untilUtc = due.DueUtc.ToString("O")
            });
            UpdateMultiWakeStatus();
            return true;
        }
        // P0 wake384: NIEAKTUALNE OBL: stage DONE disables whole-project wake.
        if (isOblLead && decision == "DONE") return false;
        if (decision == "DONE")
        {
            string completedKey = LiteOwnerInterventions.EventKey(due.Url, decision, answerHash);
            bool newCompletion = due.LastState != "status_done";
            due.Enabled = false;
            due.LastState = "status_done";
            due.LastInterventionKey = completedKey;
            LiteChatWakeRegistry.Write(entries);
            LiteWakeJournal.Write("schedule_completed", due.WakeSequence,
                result: "model_done");
            LiteEventLog.Write("multi_wake_model_status", new { status = decision, newEvent = newCompletion });
            if (newCompletion)
                _tray?.ShowBalloonTip(12000, "Open Browser: projekt ukończony",
                    "Projekt zakończył pracę. Harmonogram wybudzeń wyłączono.",
                    ToolTipIcon.Info);
            UpdateMultiWakeStatus();
            return true;
        }
        if (!LiteOwnerInterventions.IsIntervention(decision)) return false;
        string eventKey = LiteOwnerInterventions.EventKey(
            due.Url, decision, answerHash);
        string nextStatus = decision == "WAIT_USER" ? "status_wait_user" : "status_blocked";
        // Alert only on a real state transition. Changed answer hashes in the
        // same WAIT_USER/BLOCKED state do not create new owner alerts.
        // Keep the answer-specific event key for durable outbox dedup.
        bool newEvent = !string.Equals(due.LastState, nextStatus, StringComparison.Ordinal);
        due.Enabled = false;
        due.LastState = nextStatus;
        string projectTitle = LiteProjectRegistry.OpenDefault().Projects
            .FirstOrDefault(p => p.Chats.Any(c => c.Url == due.Url))?.Title
            ?? "Projekt";
        bool queued = false;
        if (newEvent)
        {
            try
            {
                // Write the durable event BEFORE advancing the DPAPI dedup key.
                // The outbox itself deduplicates by eventKey after a crash.
                queued = LiteOwnerInterventions.QueueForOwner(
                    decision, eventKey, projectTitle);
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("owner_intervention_queue_failed", new {
                    errorType = ex.GetType().Name, status = decision
                });
            }
        }
        due.LastInterventionKey = eventKey;
        LiteChatWakeRegistry.Write(entries);
        LiteEventLog.Write("multi_wake_model_status", new {
            status = decision, newEvent, queued, emailDelivered = false
        });
        if (newEvent)
        {
            _tray?.ShowBalloonTip(12000,
                "Open Browser — wymagana interwencja",
                decision == "WAIT_USER" ?
                    "Projekt oczekuje na odpowiedź właściciela. Sprawdź projekty." :
                    "Projekt jest zablokowany. Sprawdź projekty.",
                ToolTipIcon.Warning);
            if (queued)
            {
                // Outlook receives no untrusted model text or credentials.
                // Durable delivery_started prevents automated replays.
                LiteOwnerInterventions.DispatchWithClassicOutlook(
                    decision, eventKey, projectTitle);
            }
        }
        UpdateMultiWakeStatus();
        return true;
    }

    // A persistent visible ChatGPT response error cannot be repaired by
    // probing the same failed history every two minutes. Preserve the
    // external scheduler and defer at least one normal 15-minute cycle;
    // never turn a UI failure into an attempted message resend.
    // When a chat ends on a USER turn with no complete assistant reply,
    // probing every two minutes cannot make the assistant complete it.
    // Preserve its state without a resend, and let the normal 15-minute
    // supervisory cadence try again. Genuine generating work stays at 2m.
    internal static int WakeDeferralMinutes(string reason, int requested) =>
        reason is "response_error" or "assistant_not_complete"
            ? Math.Max(requested, 15) : requested;

    // Recovery failures are one bounded class, even when the observed
    // DOM symptom changes between consecutive polls. Do not let alternating
    // failure labels keep an OBL lead blocked indefinitely.
    internal static bool IsOblRecoverableDeferral(string reason) =>
        reason is "definitely_unsent" or "receipt_uncertain" or
            "draft_present" or "probe_failed" or "no_composer" or
            "wrong_chat" or "response_error" or "worker_exception" or
            "background_navigation_failed" or "worker_conversation_list_429" or
            "worker_app_problem" or "view_recovery_unready" or
            "view_recovery_navigation_failed" or
            "forced_wake_composer_not_ready";

    internal static int NextOblDeferralCount(string previousReason,
        int previousCount, string currentReason, bool isOblLead)
    {
        bool sameReason = string.Equals(previousReason, currentReason,
            StringComparison.Ordinal);
        // P0 obl-5w3 wake340: HTTP429 has its own six-attempt budget.
        // NIEAKTUALNE: mixing earlier editor failures into HTTP429 retries.
        bool bothRecoverable = isOblLead &&
            previousReason != "worker_conversation_list_429" &&
            currentReason != "worker_conversation_list_429" &&
            // P0 wake348: NIEAKTUALNE combining navigation failures with
            // editor failures into one three-strike rotation counter.
            previousReason != "background_navigation_failed" &&
            currentReason != "background_navigation_failed" &&
            // P0 wake349: transient worker error shells are not editor
            // failures and must not inherit their rotation counter.
            previousReason != "worker_app_problem" &&
            currentReason != "worker_app_problem" &&
            // P0 wake353: response_error is not proof of a missing composer.
            previousReason != "response_error" &&
            currentReason != "response_error" &&
            // P0 wake354: a transient worker exception cannot prove a lost
            // composer. NIEAKTUALNE: mixing worker exceptions with editor
            // failures to trigger a three-strike successor rotation.
            previousReason != "worker_exception" &&
            currentReason != "worker_exception" &&
            IsOblRecoverableDeferral(previousReason) &&
            IsOblRecoverableDeferral(currentReason);
        return sameReason || bothRecoverable
            ? Math.Min(9999, previousCount + 1) : 1;
    }

    internal static bool RotateOblAfterRepeatedDeferral(string reason, int count) =>
        count >= 3 && IsOblRecoverableDeferral(reason);

    // A reserved N may be followed by an ambiguous JS/DOM result. Probe
    // on the next poll after receipt checks, including uncertain clicks.
    // A known click is not evidence of delivery and is NOT counted as a
    // failed composer; an unknown click outcome retries the same chat.
    internal static bool RecordOblUncertainSend(
        LiteChatWakeRegistry.Chat entry, DateTimeOffset now,
        bool clickInvoked)
    {
        var originalDue = entry.DueUtc;
        // P0 obl-5w3 wake383: NIEAKTUALNE OBL: 2-minute hold when no Send click was confirmed.
        // P0 wake385: NIEAKTUALNE OBL: 2m post-click hold after 30s receipt probe.
        entry.DueUtc = now.AddSeconds(-1);
        entry.LastState = clickInvoked
            ? "receipt_uncertain_after_click" : "send_result_uncertain";
        entry.LastDeferralUtc = now;
        entry.LastDeferralPreviousDueUtc = originalDue;
        entry.LastDeferralUntilUtc = entry.DueUtc;
        if (clickInvoked)
        {
            entry.ConsecutiveDeferrals = 0;
            entry.LastDeferralReason = "receipt_uncertain_click";
            return false;
        }
        int count = NextOblDeferralCount(entry.LastDeferralReason,
            entry.ConsecutiveDeferrals, "receipt_uncertain", true);
        entry.LastDeferralReason = "receipt_uncertain";
        entry.ConsecutiveDeferrals = count;
        // P0 wake352: NIEAKTUALNE for OBL: three uncertain observations
        // forced a new lead although none proved a missing editor or lost chat.
        // Preserve the same URL and retry at the next poll. A possible click
        // must never be mistaken for a proven click-free composer failure.
        return false;
    }

    // A click-free outcome is a recoverable failure, not a sent wake.
    // Keep a durable counter across reserved sequence numbers and across
    // alternating preflight/post-send failures. Never touch the old draft.
    internal static bool RecordDefinitelyUnsentWake(
        LiteChatWakeRegistry.Chat entry, DateTimeOffset now,
        string reason, bool isOblLead)
    {
        var originalDue = entry.DueUtc;
        entry.LastAnswerFingerprint = "";
        // P0 obl-5w3 wake374: NIEAKTUALNE for OBL: a generic 2-minute
        // cooldown after an explicitly verified no-click sender result.
        // P0 obl-5w3 wake382: NIEAKTUALNE for OBL: two-minute hold when
        // sendDirect rejected input as invalid BEFORE any click. Retry on the
        // next ordinary poll with fresh routing/payload; do not remove JS
        // validation, route, draft, Stop or click safety guards.
        // This never processes receipt_uncertain/attempted clicks.
        entry.DueUtc = isOblLead ? now.AddSeconds(-1) : now.AddMinutes(2);
        entry.LastState = "waiting_" + reason;
        if (!isOblLead) return false;
        // P0 obl-5w3 wake373: NIEAKTUALNE for OBL: treating a returned
        // blocked_editor_changed as uncertain delivery although no click ran.
        // The editor changed under our staged payload: leave it untouched
        // and continue in a separate lead rather than overwrite or retry it.
        if (reason == "blocked_editor_changed")
        {
            entry.ConsecutiveDeferrals = 0;
            entry.LastDeferralReason = "editor_changed_before_click";
            entry.LastDeferralUtc = now;
            entry.LastDeferralPreviousDueUtc = originalDue;
            if (entry.EmergencyPhase.Length != 0)
            {
                entry.LastDeferralUntilUtc = entry.DueUtc;
                return false;
            }
            entry.EmergencyPhase = "rotate";
            entry.EmergencyReason = "changed_editor_separate_lead";
            entry.EmergencyHandoffPath = "";
            entry.DueUtc = now.AddSeconds(-1);
            entry.LastDeferralUntilUtc = entry.DueUtc;
            entry.LastState = "recovery_rotating_changed_editor";
            return true;
        }
        if (reason == "busy_draft_present" && entry.EmergencyPhase.Length == 0)
        {
            // P0 2026-11-02: no click was made, and this editor holds a draft.
            // Preserve it untouched; rotate now, not after three 2-minute holds.
            // This keeps the default decision to attempt a wake under uncertainty.
            entry.LastDeferralReason = "draft_preserved_rotate";
            entry.LastDeferralUtc = now;
            entry.LastDeferralPreviousDueUtc = originalDue;
            entry.EmergencyPhase = "rotate";
            entry.EmergencyReason = "occupied_draft_separate_lead";
            entry.EmergencyHandoffPath = "";
            entry.DueUtc = now.AddSeconds(-1);
            entry.LastDeferralUntilUtc = entry.DueUtc;
            entry.LastState = "recovery_rotating_preserved_draft";
            entry.ConsecutiveDeferrals = 0;
            return true;
        }
        // A late Stop control can appear after the earlier preflight.
        // Active generation is NOT a failed composer-recovery attempt.
        if (reason is "busy_generating" or "blocked_generation")
        {
            // P0 obl-5w3 wake358: NIEAKTUALNE for OBL: two-minute
            // retry on a generating editor. Do not interrupt generation
            // or touch the draft; continue with a separate new lead.
            entry.ConsecutiveDeferrals = 0;
            entry.LastDeferralReason = "generating";
            entry.LastDeferralUtc = now;
            entry.LastDeferralPreviousDueUtc = originalDue;
            if (entry.EmergencyPhase.Length != 0)
            {
                entry.LastDeferralUntilUtc = entry.DueUtc;
                return false;
            }
            entry.EmergencyPhase = "rotate";
            entry.EmergencyReason = "busy_generating_separate_lead";
            entry.EmergencyHandoffPath = "";
            entry.DueUtc = now.AddSeconds(-1);
            entry.LastDeferralUntilUtc = entry.DueUtc;
            entry.LastState = "recovery_rotating_busy_generating";
            return true;
        }
        // P0 wake355: NIEAKTUALNE: blocked_navigation was treated as proof
        // of a missing composer and forced immediate lead rotation.
        // Route changes before click require retry on the original URL,
        // not creation of another chat. Keep sender URL fencing intact.
        // P0 obl-5w3 wake372: NIEAKTUALNE for OBL: waiting two minutes
        // after submitDirect proves the composer was replaced BEFORE click.
        // Reprobe the same registered chat on the normal poll; the fresh
        // sender still fences owner draft, Stop, URL and composer identity.
        if (reason is "blocked_navigation" or "blocked_editor_replaced")
        {
            // P0 obl-5w3 wake371: NIEAKTUALNE for OBL: two-minute
            // hold after a proven no-click sender route change.
            // Retry the registered URL at the next normal 20s poll.
            // Do not rotate or click twice on an uncertain receipt.
            entry.DueUtc = now.AddSeconds(-1);
            entry.ConsecutiveDeferrals = 0;
            entry.LastDeferralReason = reason == "blocked_navigation"
                ? "navigation_changed" : "editor_replaced_before_click";
            entry.LastDeferralUtc = now;
            entry.LastDeferralPreviousDueUtc = originalDue;
            entry.LastDeferralUntilUtc = entry.DueUtc;
            return false;
        }
        int count = NextOblDeferralCount(entry.LastDeferralReason,
            entry.ConsecutiveDeferrals, "definitely_unsent", true);
        entry.ConsecutiveDeferrals = count;
        entry.LastDeferralReason = "definitely_unsent";
        entry.LastDeferralUtc = now;
        entry.LastDeferralPreviousDueUtc = originalDue;
        entry.LastDeferralUntilUtc = entry.DueUtc;
        // P0 obl-5w3 2026-11-02: the JS sender explicitly reported no editor
        // and no click, so waiting for three further attempts cannot help.
        // NIEAKTUALNE for these two causes: repeated-deferral threshold.
        bool missingComposer = reason is "missing_composer" or "busy_missing_composer" or "draft_button_missing";
        // P0 wake360: NIEAKTUALNE for OBL: generic three-strike rotation
        // on an explicit no-click result that did NOT prove a lost editor.
        // Only a concrete missing composer warrants a separate lead here.
        // Other definite no-click states retain diagnostics and retry the
        // same target URL; never overwrite an existing user-owned draft.
        if (!missingComposer || entry.EmergencyPhase.Length != 0) return false;
        entry.EmergencyPhase = "rotate";
        entry.EmergencyReason = "sender_missing_composer_separate_lead";
        entry.EmergencyHandoffPath = "";
        entry.DueUtc = now.AddSeconds(-1);
        entry.LastDeferralUntilUtc = entry.DueUtc;
        entry.LastState = "recovery_rotating_sender_no_composer";
        return true;
    }

    private void DeferWakeWithoutSend(string url, DateTimeOffset now,
        string reason, int minutes = 2)
    {
        bool oblLead = LiteProjectRegistry.OpenDefault().Projects.Any(p =>
            p.Key == "obl" && p.Chats.Any(c => c.Url == url && c.Role == "lead"));
        if (!oblLead) minutes = WakeDeferralMinutes(reason, minutes);
        var registered = LiteChatWakeRegistry.Read();
        var entry = registered.FirstOrDefault(c => c.Url == url && c.Enabled);
        if (entry is null) return;
        var originalDue = entry.DueUtc;
        entry.DueUtc = now.AddMinutes(minutes);
        int consecutive = NextOblDeferralCount(entry.LastDeferralReason,
            entry.ConsecutiveDeferrals, reason, oblLead);
        entry.LastDeferralReason = reason;
        entry.LastDeferralUtc = now;
        entry.LastDeferralPreviousDueUtc = originalDue;
        entry.LastDeferralUntilUtc = entry.DueUtc;
        entry.ConsecutiveDeferrals = consecutive;
        // Backpressure, generation and hydration deferrals are NOT proof
        // that an earlier own-draft recovery did not click. Keep the
        // ambiguity marker durable through every unrelated deferral.
        entry.LastState = NextStateAfterWakeDeferral(entry.LastState, reason);
        bool draftRequiresNewLead = oblLead && reason == "draft_present";
        // NIEAKTUALNE for OBL: rotation on view_recovery_unready,
        // view_recovery_navigation_failed or forced_wake_composer_not_ready.
        // These are navigation/preflight states, not proof that the editor
        // is absent. Only explicit no_composer warrants immediate rotation.
        bool composerUnavailable = oblLead && reason == "no_composer";
        // P0 wake360: NIEAKTUALNE for OBL: generic repeated-deferral
        // threshold could create a new chat without proving editor loss.
        // Only a known draft or concretely unavailable composer rotates;
        // all other preflight uncertainties retry the same registered URL.
        bool rotate = oblLead && entry.EmergencyPhase.Length == 0 &&
            (draftRequiresNewLead || composerUnavailable);
        if (rotate)
        {
            // Do not touch the unreadable /c/ or its possible user draft.
            // The next poll creates a separate successor and retains N+1.
            entry.EmergencyPhase = "rotate";
            entry.EmergencyReason = draftRequiresNewLead
                ? "occupied_draft_separate_lead" : composerUnavailable
                ? "editor_unavailable_separate_lead" : "repeated_" + reason;
            entry.EmergencyHandoffPath = "";
            entry.DueUtc = now.AddSeconds(-1);
            entry.LastDeferralUntilUtc = entry.DueUtc;
            entry.LastState = draftRequiresNewLead
                ? "recovery_rotating_preserved_draft" : composerUnavailable
                ? "recovery_rotating_no_composer" : "recovery_rotating_after_deferrals";
        }
        LiteChatWakeRegistry.Write(registered);
        LiteWakeJournal.Write("deferred", entry.WakeSequence + 1,
            result: rotate ? "rotation_after_" + reason : reason);
        LiteEventLog.Write("multi_wake_preflight_skipped", new {
            reason, retryMinutes = rotate ? 0 : minutes,
            consecutive, rotateToSeparateChat = rotate, definitelyNotSent = true
        });
        UpdateMultiWakeStatus();
    }

    // Called before navigation, after navigation and after preflight:
    // a 429 can arrive while WebView2 is hydrating the selected chat.
    private bool HoldWakeOnBackendRead429(LiteChatWakeRegistry.Chat due)
    {
        var now = DateTimeOffset.UtcNow;
        TimeSpan remaining = _wakeRead429Throttle.Remaining(now);
        if (remaining <= TimeSpan.Zero) return false;
        int retryMinutes = Math.Clamp((int)Math.Ceiling(remaining.TotalMinutes), 1, 30);
        DeferWakeWithoutSend(due.Url, now, "backend_http_429", retryMinutes);
        return true;
    }

    internal static bool ShouldDeferWorkerProbe(bool useForeground,
        LiteRendererBackpressure gate, DateTimeOffset now)
    {
        return !useForeground && !gate.MayProbe(now);
    }

    internal static bool IsDefinitelyUnsentWake(string result, string phase, bool isOblLead = false)
    {
        // A committed attempt is NOT synonymous with a clicked Send button.
        // Only explicit no-click states may release the durable answer fence.
        // already_attempted MAY have clicked previously; never auto-retry it.
        // P0 wake393: NIEAKTUALNE for OBL: pre-injection editor replacement
        // or generation was treated as uncertain delivery. sendDirect returns
        // these states before any submitDirect/click; preserve owner draft.
        return (isOblLead && (result is "blocked_editor_replaced" or "blocked_generation")) ||
            result.StartsWith("busy_", StringComparison.Ordinal) ||
            // P0 obl-5w3 wake379: NIEAKTUALNE for OBL: missing JS sender
            // classified as uncertain delivery. The optional sendDirect call
            // returns null when the sender is absent: no click was possible.
            // This does NOT include already_attempted or a timed-out click.
            result is "missing_composer" or "invalid" or "blocked_navigation" or
                "blocked_editor_changed" or "sender_missing" ||
            (result == "submission_requested" &&
                // P0 wake392 NIEAKTUALNE for OBL: missing submitDirect after
                // host-managed staging was marked uncertain despite no click.
                (phase == "draft_button_missing" || phase == "sender_missing" ||
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

    private readonly Dictionary<string, DateTimeOffset> _wakeViewRecoveryUtc =
        new(StringComparer.Ordinal);

    internal static bool ShouldRestoreChatView(string reason, bool draftPresent,
        DateTimeOffset now, DateTimeOffset lastRestore, TimeSpan? cooldown = null)
    {
        if (draftPresent || reason is not ("no_composer" or "response_error"))
            return false;
        return now - lastRestore >= (cooldown ?? TimeSpan.FromMinutes(5));
    }
    private async Task PollAllChatWakesAsync()
    {
        if (!_multiWakeEnabled || !_ownsMultiWakeMutex ||
            _chatWakeBusy || IsDisposed) return;
        _chatWakeBusy = true;
        try
        {
            LiteWakeEvidenceReconciler.Observe();
            LiteCheckpointRetention.Observe();
            var entries = LiteChatWakeRegistry.Read();
            var now = DateTimeOffset.UtcNow;
            var foregroundUrl = _browser.CoreWebView2?.Source;
            var due = SelectDueChat(entries, now, foregroundUrl);
            if (due is null) { UpdateMultiWakeStatus(); return; }
            bool minimalOblWake = LiteProjectRegistry.OpenDefault().Projects.Any(p => p.Key == "obl" && p.Chats.Any(c => c.Url == due.Url && c.Role == "lead"));
            // Emergency N.1 is independent of normal N+1 numbering.
            if (await HandleEmergencyBeforeWakeAsync(due))
            { UpdateMultiWakeStatus(); return; }
            // Boundary ...0 or emergency takeover: first message to successor
            // is the next ordinary numbered wake, never an extra seed.
            if (await RotateLeadBeforeWakeAsync(due))
            { UpdateMultiWakeStatus(); return; }
            LiteWakeJournal.Write("due", due.WakeSequence + 1);
            // The read-only API cooldown applies to automated probes only.
            // Post submission and user-initiated foreground browsing are not retried.
            if (!minimalOblWake && HoldWakeOnBackendRead429(due)) return;
            if (!minimalOblWake && !FlushNotes())
            {
                LiteWakeJournal.Write("deferred", due.WakeSequence + 1,
                    result: "notes_save_failed");
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
                if (!minimalOblWake && ShouldDeferWorkerProbe(useForeground,
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
                if (wakeCore is null || (!useForeground && !await NavigateWorkerAsync(due.Url, minimalOblWake)))
                {
                    // P0 incident rollback: NIEAKTUALNE: immediate OBL rotation for navigation failure.
                    // Preserve the two-minute retry instead of creating another chat.
                    // P0 obl-5w3: HTTP 429 on GET /conversations is not proof
                    // the old lead is gone. NIEAKTUALNE: rotate after three
                    // fast failed hydration reads under this backend limit.
                    bool workerList429 = minimalOblWake &&
                        DateTimeOffset.UtcNow - _lastWorkerConversationList429Utc <
                            TimeSpan.FromMinutes(2);
                    // P0 obl-5w3 wake369: NIEAKTUALNE: one-minute OBL hold
                    // after ordinary worker navigation failure without 429.
                    // Retry the same URL on the next 20s poll. Keep the 1m
                    // backoff only for a real worker conversation-list 429.
                    // P0 wake388: NIEAKTUALNE OBL: 1m worker-429 hold even
                    // though no Send click occurred; retry next normal poll.
                    DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                        workerList429 ? "worker_conversation_list_429" :
                        "background_navigation_failed",
                        minimalOblWake ? 0 : workerList429 ? 1 : 2);
                    return;
                }
                else
                {
                    // Navigation can receive HTTP 429 after the first gate.
                    if (!minimalOblWake && HoldWakeOnBackendRead429(due)) return;
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
                        due.LastAnswerFingerprint, minimalOblWake);
                    // P0 2026-11-02 wake334: foreground conversation can be an
                    // unhydrated error shell for minutes while the same URL
                    // remains usable in the existing background worker.
                    // NIEAKTUALNE for OBL: poll the broken foreground shell
                    // on every tick without attempting the same-chat worker.
                    // This fallback does not open a new conversation or rotate.
                    // P0 obl-5w3 wake344: NIEAKTUALNE immediate foreground
                    // connection_interrupted emergency before same-chat worker fallback.
                    // P0 obl-5w3 wake400: NIEAKTUALNE for OBL: a foreground
                    // no_composer forces a new lead without first trying the
                    // existing background worker on the SAME verified URL.
                    // A missing foreground editor is not proof that the chat
                    // cannot send from its isolated worker. No owner draft is
                    // touched; sendDirect still checks route, Stop and draft.
                    if (minimalOblWake && useForeground &&
                        preflight.Reason is ("chatgpt_app_problem" or "connection_interrupted" or "no_composer") &&
                        !preflight.DraftPresent)
                    {
                        LiteEventLog.Write("multi_wake_foreground_error_worker_fallback",
                            new { wakeSequence = due.WakeSequence + 1,
                                reason = preflight.Reason, sameChat = true });
                        await EnsureWakeWorkerAsync();
                        var fallbackCore = _chatWakeWorker.CoreWebView2;
                        if (fallbackCore is null ||
                            !await NavigateWorkerAsync(due.Url, minimalOblWake))
                        {
                            // P0 obl-5w3 wake341: NIEAKTUALNE for worker 429:
                            // generic two-minute navigation deferral in this fallback.
                            // [NIEAKTUALNE for OBL no_composer 2026-11-03]
                            // Keep retrying the same lead, never create another chat here.
                            // [END NIEAKTUALNE] Applies only to transient worker errors.
                            bool workerList429 = DateTimeOffset.UtcNow -
                                _lastWorkerConversationList429Utc < TimeSpan.FromMinutes(2);
                            // P0 wake401: NIEAKTUALNE for OBL: after a visible
                            // foreground no_composer and failed same-chat worker,
                            // retrying that unsendable pair forever. No draft
                            // was observed and no Send click was made. Without
                            // HTTP 429, rotate to a clean lead on next poll.
                            // With actual 429, keep the original lead: a rate
                            // limit alone does not prove editor loss.
                            if (minimalOblWake && preflight.Reason == "no_composer" &&
                                !workerList429)
                            {
                                DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                                    "no_composer", 0);
                                return;
                            }
                            // P0 obl-5w3 wake369: same no-429 fast retry
                            // in foreground error -> worker fallback.
                            // No duplicate click: this path is pre-send.
                            // P0 wake388 NIEAKTUALNE OBL: 1m worker-429 hold.
                            DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                                workerList429 ? "worker_conversation_list_429" :
                                "background_navigation_failed",
                                minimalOblWake ? 0 : workerList429 ? 1 : 2);
                            return;
                        }
                        // The owner may disable the schedule while navigating.
                        entries = LiteChatWakeRegistry.Read();
                        var currentTarget = entries.FirstOrDefault(
                            x => x.Url == due.Url && x.Enabled);
                        if (currentTarget is null ||
                            currentTarget.DueUtc > DateTimeOffset.UtcNow) return;
                        due = currentTarget;
                        wakeCore = fallbackCore;
                        useForeground = false;
                        backgroundAttempt = true;
                        preflight = await CheckWorkerPreflightAsync(
                            wakeCore, due.Url, due.LastAnswerFingerprint,
                            minimalOblWake);
                    }
                    // A blocked history request during preflight must not be
                    // misinterpreted as a hydrated, eligible conversation.
                    if (!minimalOblWake && HoldWakeOnBackendRead429(due)) return;
                    // P0 obl-5w3: worker navigation can finish before a
                    // rate-limited conversation-list request is resolved.
                    // An unhydrated worker page under a recent HTTP 429 is
                    // not proof that the existing lead was lost. Retry its
                    // exact URL before allowing emergency chat rotation.
                    // NIEAKTUALNE: rotate on this transient worker 429 shell.
                    // P0 wake343: interrupted connection under actual worker 429
                    // is the same transient hydration failure, not a lost chat.
                    // P0 wake388 NIEAKTUALNE OBL: 1m hold for this pre-send shell.
                    if (minimalOblWake && !useForeground &&
                        preflight.Reason is ("chatgpt_app_problem" or "connection_interrupted") &&
                        DateTimeOffset.UtcNow - _lastWorkerConversationList429Utc <
                            TimeSpan.FromMinutes(2))
                    {
                        DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                            "worker_conversation_list_429", 0);
                        return;
                    }
                    // P0 obl-5w3 wake342: a worker app-error shell without
                    // HTTP429 is still not evidence that this chat is gone.
                    // NIEAKTUALNE for OBL: immediate emergency rotation on
                    // the first worker chatgpt_app_problem observation.
                    // P0 wake349: NIEAKTUALNE for a transient worker app
                    // shell: rotate the lead after three such deferrals.
                    // Retry the SAME URL; do not touch user drafts.
                    // P0 wake343: NIEAKTUALNE immediate new-lead rotation on
                    // a single worker connection_interrupted observation.
                    if (minimalOblWake && !useForeground &&
                        preflight.Reason is ("chatgpt_app_problem" or "connection_interrupted"))
                    {
                        DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                            // P0 obl-5w3 wake376: NIEAKTUALNE for OBL:
                            // 1m cooldown for transient non-429 app shell.
                            // Keep HTTP 429 throttle on its separate path.
                            "worker_app_problem", 0);
                        return;
                    }
                    if (preflight.Reason is "conversation_length_exceeded" or
                        "chatgpt_app_problem" or "connection_interrupted")
                    {
                        MarkWakeEmergency(due.Url, preflight.Reason, "wake_preflight");
                        return;
                    }
                    if (await HandleStalledLeadAsync(due, wakeCore,
                        preflight.Reason)) return;
                    var gate = ChooseWakePreflightGate(
                        preflight.Complete, due.LastState,
                        due.LastAnswerFingerprint, preflight.Reason, preflight.Hash);
                    if (gate == WakePreflightGate.Complete)
                    {
                        due.Enabled = false;
                        due.LastState = "complete";
                        LiteChatWakeRegistry.Write(entries);
                        LiteWakeJournal.Write("schedule_completed", due.WakeSequence);
                        LiteEventLog.Write("multi_wake_completed");
                        UpdateMultiWakeStatus();
                        return;
                    }
                    // An uncertain previous wake is NOT a reason to hold OBL.
                    // Extra wake is allowed; continuity has priority.
                    if (ShouldHoldUnconfirmedRecoveryForProject(
                        gate, minimalOblWake))
                    {
                        // Only verified terminal completion may override the
                        // recovery fence without the old answer's fingerprint.
                        // Any ordinary reply must prove a NEW stable answer.
                        due.DueUtc = DateTimeOffset.UtcNow.AddMinutes(5);
                        LiteChatWakeRegistry.Write(entries);
                        LiteWakeJournal.Write("deferred", due.WakeSequence + 1,
                            result: "unconfirmed_recovery");
                        LiteEventLog.Write("multi_wake_own_draft_unconfirmed_held",
                            new { retryMinutes = 5, clickMayHaveOccurred = true });
                        UpdateMultiWakeStatus();
                        return;
                    }
                    if (HandleVerifiedModelStatus(due, entries,
                        preflight.Status, preflight.Reason, preflight.Hash,
                        minimalOblWake))
                        return;
                    // NIEAKTUALNE dla OBL od 2026-11-02: analiza/odzyskiwanie szkicow
                    // nie jest bramka wybudzenia. Stara sciezka dotyczy innych projektow.
                    if (!minimalOblWake && preflight.Reason == "draft_present")
                    {
                        // A previous attempt may have left OUR unique OB wake
                        // in the editor. Retry only that draft, never user text.
                        var encoded = await ExecuteWorkerScriptAsync(wakeCore,
                            "window.__OPENBROWSER_RESUME_SENDER__?.submitOwnDraft(" +
                            JsonSerializer.Serialize(new Uri(due.Url).AbsolutePath) + "," +
                            JsonSerializer.Serialize(due.WakeSequence) + ")");
                        var recovery = JsonSerializer.Deserialize<string>(encoded)
                            ?? "sender_missing";
                        if (recovery is "click_invoked" or "already_visible" or
                            "click_failed" or "already_attempted")
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
                                // Persist the old response fingerprint before
                                // recording an unconfirmed recovery click.
                                // Without this, a later "ready" observation
                                // could be the same answer and resend.
                                if (active.LastAnswerFingerprint.Length == 0 &&
                                    preflight.Hash.Length == 64)
                                    active.LastAnswerFingerprint = preflight.Hash;
                                active.LastState = receiptVisible
                                    ? "own_draft_visible_ack"
                                    : "own_draft_clicked_unconfirmed";
                                active.DueUtc = DateTimeOffset.UtcNow.AddMinutes(
                                    receiptVisible ? active.EffectiveMinutes : 2);
                                LiteChatWakeRegistry.Write(entries);
                            }
                            LiteWakeJournal.Write("recovery", due.WakeSequence,
                                result: receiptVisible ? "visible_ack" : "unconfirmed");
                            LiteEventLog.Write("multi_wake_own_draft_recovery",
                                new { result = recovery, receiptConfirmed = receiptVisible });
                            UpdateMultiWakeStatus();
                            return;
                        }
                    }
                    // P0 obl-5w3 2026-11-02: NIEAKTUALNE dla OBL: view reload and 45-second cooldown.
                    // A missing or failed editor means rotate to a clean lead now, without touching the old draft.
                    if (minimalOblWake && preflight.Reason == "no_composer")
                    {
                        DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow, preflight.Reason, 0);
                        return;
                    }
                    // P0: ChatGPT's full-page failure or missing editor can
                    // recover by reopening the exact same chat URL.
                    // Navigation is NOT a send; do not issue a second click.
                    var restoreNow = DateTimeOffset.UtcNow;
                    _wakeViewRecoveryUtc.TryGetValue(due.Url, out var lastRestore);
                    if (!minimalOblWake && ShouldRestoreChatView(preflight.Reason,
                        preflight.DraftPresent, restoreNow, lastRestore,
                        minimalOblWake ? TimeSpan.FromSeconds(45) : null) &&
                        LiteDevWakeState.IsChatUrl(wakeCore.Source) &&
                        LiteDevWakeState.NormalizeUrl(wakeCore.Source) == due.Url)
                    {
                        var latest = LiteChatWakeRegistry.Read()
                            .FirstOrDefault(c => c.Url == due.Url);
                        if (latest is not null && latest.Enabled)
                        {
                            _wakeViewRecoveryUtc[due.Url] = restoreNow;
                            if (!minimalOblWake)
                            {
                                wakeCore.Navigate(due.Url);
                                DeferWakeWithoutSend(due.Url, restoreNow,
                                    "view_recovery_navigation", 2);
                                return;
                            }
                            // Only OBL: one bounded reload and immediate stable
                            // preflight, instead of a separate 2-minute hold.
                            var navigation = new TaskCompletionSource<bool>(
                                TaskCreationOptions.RunContinuationsAsynchronously);
                            void OnReload(object? _, CoreWebView2NavigationCompletedEventArgs e)
                                => navigation.TrySetResult(e.IsSuccess);
                            bool reloadOk = false;
                            wakeCore.NavigationCompleted += OnReload;
                            try
                            {
                                wakeCore.Navigate(due.Url);
                                reloadOk = await navigation.Task.WaitAsync(
                                    TimeSpan.FromSeconds(12));
                            }
                            catch (TimeoutException)
                            {
                                LiteEventLog.Write("multi_wake_view_recovery_timeout",
                                    new { wakeSequence = due.WakeSequence + 1 });
                            }
                            finally { wakeCore.NavigationCompleted -= OnReload; }
                            if (reloadOk && LiteDevWakeState.IsChatUrl(wakeCore.Source) &&
                                LiteDevWakeState.NormalizeUrl(wakeCore.Source) == due.Url)
                            {
                                preflight = await CheckWorkerPreflightAsync(
                                    wakeCore, due.Url, due.LastAnswerFingerprint,
                                    minimalOblWake);
                                if (preflight.Reason is "conversation_length_exceeded" or
                                    "chatgpt_app_problem" or "connection_interrupted")
                                {
                                    MarkWakeEmergency(due.Url, preflight.Reason,
                                        "wake_restored_preflight");
                                    return;
                                }
                                if (preflight.Reason == "ready" && !preflight.DraftPresent &&
                                    !preflight.Complete)
                                {
                                    entries = LiteChatWakeRegistry.Read();
                                    var restored = entries.FirstOrDefault(
                                        c => c.Url == due.Url);
                                    if (restored is null || !restored.Enabled ||
                                        restored.DueUtc > DateTimeOffset.UtcNow) return;
                                    due = restored;
                                    LiteEventLog.Write("multi_wake_view_recovered_same_tick",
                                        new { wakeSequence = due.WakeSequence + 1,
                                            noAdditionalWait = true });
                                }
                                else
                                {
                                    DeferWakeWithoutSend(due.Url,
                                        DateTimeOffset.UtcNow, "view_recovery_unready", 1);
                                    return;
                                }
                            }
                            else
                            {
                                DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                                    "view_recovery_navigation_failed", 1);
                                return;
                            }
                        }
                    }
                    // P0 2026-11-02: a pre-existing draft is not an excuse to
                    // postpone the wake and must never be overwritten. Switch
                    // to a separate clean lead before reserving a wake number.
                    if (minimalOblWake && preflight.Reason == "draft_present")
                    {
                        DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                            "draft_present", 0);
                        return;
                    }
                    // P0 obl-5w3 2026-11-02: history/answer instability is
                    // not proof that the editor cannot send. When both reads
                    // succeed Eligibility confirms route, composer and no draft.
                    // A failed read is likewise not evidence of an unsafe send.
                    // The sender itself must verify exact URL, composer, Stop
                    // and draft immediately before injecting the wake.
                    bool uncertainHistorySend = minimalOblWake &&
                        preflight.Reason is "response_error" or "history_not_stable" or
                        "answer_not_stable" or "probe_failed" or
                        "assistant_not_complete"; // NIEAKTUALNE OBL: waiting for a reply before trying.
                    // P0 2026-11-02: probe_failed is read uncertainty, not
                    // proof that the JS sender cannot inspect its own editor.
                    // sendDirect fences the exact URL and preserves drafts.
                    // NIEAKTUALNE dla OBL: a failed read always deferred.
                    // NIEAKTUALNE dla OBL: unstable history always deferred.
                    // NIEAKTUALNE dla OBL: draft_present nie wstrzymuje wake.
                    if (preflight.Reason != "ready" && !uncertainHistorySend &&
                        !(minimalOblWake && preflight.Reason == "draft_present") &&
                        due.StallPhase != "force")
                    {
                        // P0 obl-5w3 wake377: NIEAKTUALNE for OBL: 2m hold
                        // after wrong-chat or navigation-changed preflight.
                        // No click was made; retry the registered URL next poll.
                        // Never submit into a mismatched SPA route.
                        // P0 obl-5w3 wake380: NIEAKTUALNE for OBL: generic 2m delay after a failed preflight before any click.
                        // Retry the same exact registered URL at the next normal poll; sendDirect still enforces URL, Stop and draft guards.
                        int preflightRetryMinutes = minimalOblWake ? 0 : 2;
                        DeferWakeWithoutSend(due.Url, now, preflight.Reason,
                            preflightRetryMinutes);
                        return;
                    }
                    // An explicitly forced wake continues after the single bounded STOP.
                    if (due.StallPhase == "force" &&
                        ((!minimalOblWake && preflight.DraftPresent) ||
                         preflight.Reason is "wrong_chat" or "no_composer" ||
                         (!minimalOblWake && preflight.Reason == "probe_failed")))
                    {
                        // P0 obl-5w3 wake381: NIEAKTUALNE for OBL: 2m hold after forced wake preflight rejects wrong route.
                        // Keep the no-send safety fence; retry the registered lead at the next poll.
                        DeferWakeWithoutSend(due.Url, now,
                            "forced_wake_composer_not_ready", minimalOblWake ? 0 : 2);
                        return;
                    }
                    // Automatic project rotation is now by sequence % 10:
                    // N...9 receives HANDOFF instructions, N...0 is sent
                    // only after RotateLeadBeforeWakeAsync verified a new lead.
                    // The old age-based automatic WAIT_USER path is obsolete.
                    // Durable fingerprint fences uncertain sends: never resend
                    // for an unchanged assistant answer after a crash/restart.
                    due.LastAnswerFingerprint = preflight.Hash;
                    due.LastAttemptUtc = now;
                    // Preserve configured clock slots across delayed attempts.
                    if (due.SlotAnchorUtc == default)
                        due.SlotAnchorUtc = due.DueUtc;
                    due.DueUtc = LiteChatWakeRegistry.NextFixedSlotUtc(
                        due.SlotAnchorUtc, now, due.EffectiveMinutes);
                    due.LastState = "attempting";
                    due.WakeSequence = checked(due.WakeSequence + 1);
                    // OBL keeps its recovery budget until a real outcome:
                    // a reserved number is NOT evidence of a send click.
                    if (!minimalOblWake) due.ConsecutiveDeferrals = 0;
                    due.StallFirstDueUtc = default;
                    due.StallPhase = "";
                    due.StallResumeUtc = default;
                    due.EmergencyOrdinal = 0;
                    LiteChatWakeRegistry.Write(entries);
                    attemptCommitted = true;
                    // P0 obl-5w3 wake402: NIEAKTUALNE for registered OBL
                    // leads: loading notes before each numbered wake. The
                    // OBL NumberedMessage branch never includes notes, so
                    // that disk read cannot affect the payload and can fail
                    // or delay the send. Other projects still attach notes.
                    string notesSnapshot = minimalOblWake ? string.Empty :
                        _noteStore.Load(LiteChatNotes.NormalizeKey(due.Url));
                    string receipt = Guid.NewGuid().ToString("N");
                    LiteWakeJournal.Write("attempt_reserved", due.WakeSequence, receipt);
                    string payload = LiteChatWakeRegistry.NumberedMessage(
                        due, notesSnapshot, receipt, DateTimeOffset.UtcNow,
                        registeredOblLead: minimalOblWake);
                    // Evidence of the immutable notes snapshot used for THIS receipt.
                    // Log only length/hash of included characters, never note contents.
                    LiteEventLog.Write("multi_wake_notes_snapshot", new {
                        wakeSequence = due.WakeSequence, receipt,
                        sourceChars = notesSnapshot.Length,
                        includedChars = string.IsNullOrWhiteSpace(notesSnapshot)
                            ? 0 : Math.Min(24000, notesSnapshot.Length),
                        truncated = notesSnapshot.Length > 24000,
                        includedSha256 = LiteChatWakeRegistry.DeliveredNotesFingerprint(
                            notesSnapshot),
                        payloadUtf8Bytes = System.Text.Encoding.UTF8.GetByteCount(payload)
                    });                    // Bind target identity inside the *same* script invocation
                    // that injects the prompt. A changed SPA route must not send.
                    string js = "window.__OPENBROWSER_RESUME_SENDER__?.sendDirect(" +
                        JsonSerializer.Serialize(payload) + "," +
                        JsonSerializer.Serialize(receipt) + ",true," +
                        JsonSerializer.Serialize(new Uri(due.Url).AbsolutePath) + ")";
                    var result = await ExecuteWorkerScriptAsync(
                        wakeCore, js);
                    state = JsonSerializer.Deserialize<string>(result) ?? "sender_missing";
                    if (IsDefinitelyUnsentWake(state, "not_submitted", minimalOblWake))
                    {
                        // Confirmed no editor submission: release the answer fence,
                        // unlike an unacknowledged click where outcome is uncertain.
                        entries = LiteChatWakeRegistry.Read();
                        var unsent = entries.FirstOrDefault(x => x.Url == due.Url);
                        bool rotated = false;
                        int failedCount = 0;
                        if (unsent is not null && unsent.Enabled &&
                            unsent.EmergencyPhase.Length == 0 &&
                            unsent.LastAttemptUtc == now &&
                            unsent.WakeSequence == due.WakeSequence)
                        {
                            rotated = RecordDefinitelyUnsentWake(unsent,
                                DateTimeOffset.UtcNow, state, minimalOblWake);
                            failedCount = unsent.ConsecutiveDeferrals;
                            LiteChatWakeRegistry.Write(entries);
                        }
                        LiteWakeJournal.Write("definitely_unsent",
                            due.WakeSequence, receipt, "not_submitted");
                        LiteEventLog.Write("multi_wake_definitely_unsent",
                            new { reason = state, definitelyNotSent = true,
                                failedCount, rotateToSeparateChat = rotated,
                                retryMinutes = rotated ? 0 : 2 });
                        UpdateMultiWakeStatus();
                        return;
                    }
                    // Page timers may be throttled when the worker is occluded.
                    // Flush submission explicitly from the host, with no dependence
                    // on requestAnimationFrame or setTimeout in the page.
                    string phase = "draft_button_missing";
                    for (int attempt = 0; attempt < 7 && state == "submission_requested"; attempt++)
                    {
                        // P0 obl-5w3 wake389: NIEAKTUALNE for OBL: mandatory 750ms
                        // pre-submit hold. Try submitDirect immediately; its live
                        // URL, Stop, draft, composer and payload guards remain.
                        // If React has not hydrated Send yet, retain 400ms retries.
                        if (!minimalOblWake || attempt > 0)
                            await Task.Delay(attempt == 0 ? 750 : 400);
                        string flush = await ExecuteWorkerScriptAsync(
                            wakeCore,
                            "window.__OPENBROWSER_RESUME_SENDER__?.submitDirect(" +
                            JsonSerializer.Serialize(receipt) + ")");
                        phase = JsonSerializer.Deserialize<string>(flush) ?? "sender_missing";
                        // P0 wake394 NIEAKTUALNE for OBL: repeatedly querying a lost
                        // per-page receipt cannot recreate its pending entry.
                        // Keep uncertain-delivery handling, never repeat a click.
                        if (phase is "click_invoked" or "form_submitted" or "sender_missing" ||
                            (minimalOblWake && phase == "unknown_receipt") ||
                            phase.StartsWith("blocked_", StringComparison.Ordinal))
                            break;
                    }
                    // P0 wake399 NIEAKTUALNE for OBL: after bounded
                    // submitDirect retries exhaust with draft_button_missing,
                    // a second DOM metadata script cannot create a Send
                    // control. Return the already known no-click result
                    // without diagnostics. Other projects retain the probe.
                    if (!minimalOblWake && phase == "draft_button_missing")
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
                    if (phase is "click_invoked" or "form_submitted")
                    {
                        LiteWakeJournal.Write("submission_attempted",
                            due.WakeSequence, receipt, phase);
                        // This is a click-attempt, NOT proof of delivery.
                        // Hash/length of exact prepared notes can be joined
                        // by receipt with multi_wake_notes_snapshot.
                        LiteWakeJournal.Write("notes_submitted",
                            due.WakeSequence, receipt, phase);
                        LiteEventLog.Write("multi_wake_notes_submitted", new {
                            wakeSequence=due.WakeSequence, receipt,
                            includedChars=string.IsNullOrWhiteSpace(notesSnapshot)
                                ? 0 : Math.Min(24000,notesSnapshot.Length),
                            includedSha256=LiteChatWakeRegistry.DeliveredNotesFingerprint(
                                notesSnapshot), submissionPhase=phase,
                            deliveryNotYetConfirmed=true
                        });
                        // Prestage handoff at the real ...9 click. The ...0
                        // rotation refreshes this snapshot independently.
                        // Snapshot errors are reported but never hold the wake.
                        if (minimalOblWake &&
                            LiteWakeRotation.HandoffWake(due.WakeSequence))
                        {
                            var handoff = LiteRotationHandoffSnapshot.Save(
                                due.Url, due.WakeSequence);
                            LiteEventLog.Write("rotation_handoff_prestaged_on_send",
                                new {
                                    oldWake=due.WakeSequence,
                                    nextWake=due.WakeSequence+1,
                                    saved=handoff.Saved,
                                    reason=handoff.Reason,
                                    sha256=handoff.Sha256
                                });
                        }
                    }
                    // Only after a successful submit click: wait briefly
                    // for React's real USER bubble in this exact chat.
                    // This never clicks Send a second time.
                    var receiptClock = System.Diagnostics.Stopwatch.StartNew();
                    bool visible = state == "submission_requested" &&
                        phase is "click_invoked" or "form_submitted" &&
                        await WaitForWakeReceiptAsync(wakeCore, receipt, due.Url,
                            () => useForeground ?
                                _foregroundWakeRouteEpoch : _workerRouteEpoch,
                            minimalOblWake ? "[OB-WAKE-" +
                                due.WakeSequence.ToString("D6") + "]" : null);
                    long receiptWaitMs = receiptClock.ElapsedMilliseconds;
                    if (!visible && IsDefinitelyUnsentWake(state, phase, minimalOblWake))
                    {
                        entries = LiteChatWakeRegistry.Read();
                        var unsent = entries.FirstOrDefault(x => x.Url == due.Url);
                        bool rotated = false;
                        int failedCount = 0;
                        if (unsent is not null && unsent.Enabled &&
                            unsent.EmergencyPhase.Length == 0 &&
                            unsent.LastAttemptUtc == now &&
                            unsent.WakeSequence == due.WakeSequence)
                        {
                            rotated = RecordDefinitelyUnsentWake(unsent,
                                DateTimeOffset.UtcNow, phase, minimalOblWake);
                            failedCount = unsent.ConsecutiveDeferrals;
                            LiteChatWakeRegistry.Write(entries);
                        }
                        LiteWakeJournal.Write("definitely_unsent",
                            due.WakeSequence, receipt, phase);
                        LiteEventLog.Write("multi_wake_definitely_unsent", new {
                            reason = phase, definitelyNotSent = true,
                            failedCount, rotateToSeparateChat = rotated,
                            retryMinutes = rotated ? 0 : 2
                        });
                        UpdateMultiWakeStatus();
                        return;
                    }
                    LiteWakeJournal.Write(visible ? "receipt_confirmed" :
                        "receipt_uncertain", due.WakeSequence, receipt,
                        visible ? "visible_ack" : phase);
                    if (visible)
                        LiteEventLog.Write("multi_wake_delivery_confirmed", new {
                            wakeSequence = due.WakeSequence, receipt,
                            source = "DOM_user_bubble", elapsedMs = receiptWaitMs
                        });
                    LiteEventLog.Write("multi_wake_send", new {
                        wakeSequence = due.WakeSequence,
                        state, phase, visibleUserBubble = visible,
                        receiptConfirmed = visible, // unique receipt; never match older messages
                        includesNotes = !string.IsNullOrWhiteSpace(notesSnapshot),
                        receiptWaitMs, receiptEvidence = visible ? "DOM_user_bubble" : "none",
                        receiptBudgetMs = minimalOblWake ? 30000 : 15000,
                        intervalMinutes = due.EffectiveMinutes
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
                LiteWakeJournal.Write("engine_failure",
                    attemptCommitted ? due.WakeSequence : due.WakeSequence + 1,
                    result: LiteWakeJournal.FailureCode(ex, attemptCommitted));
                LiteEventLog.Write("multi_wake_failed", new {
                    errorType = ex.GetType().Name,
                    submissionMayBeUncertain = attemptCommitted
                });
                if (!attemptCommitted)
                {
                    // P0 rollback: NIEAKTUALNE: immediate OBL rotation on worker_exception.
                    // Preserve former deferral; no new chat is created for a transient error.
                    // P0 obl-5w3 wake378: NIEAKTUALNE for OBL: two-minute
                    // hold on an exception before reservation or any Send click.
                    // Retry the registered URL on the normal next poll.
                    // Exceptions after attemptCommitted retain uncertain receipts.
                    DeferWakeWithoutSend(due.Url, DateTimeOffset.UtcNow,
                        "worker_exception", minimalOblWake ? 0 : 2);
                    return;
                }
            }
            // Merge against current DPAPI content: CLI/UI may have changed other chats meanwhile.
            entries = LiteChatWakeRegistry.Read();
            var current = entries.FirstOrDefault(x => x.Url == due.Url);
            if (current is not null && current.LastAttemptUtc == now &&
                current.WakeSequence == due.WakeSequence)
            {
                // Never overwrite an owner pause or an emergency handoff
                // that another action registered during the send attempt.
                if (!minimalOblWake ||
                    (current.Enabled && current.EmergencyPhase.Length == 0))
                {
                    LiteChatHandoff.MarkAttempted(current, attemptCommitted);
                    bool clicked = state is "click_invoked" or "form_submitted";
                    bool uncertain = minimalOblWake && attemptCommitted &&
                        state != "visible_ack";
                    bool rotate = false;
                    if (uncertain)
                    {
                        rotate = RecordOblUncertainSend(current,
                            DateTimeOffset.UtcNow, clicked);
                        if (state.StartsWith("failed_", StringComparison.Ordinal))
                            LiteWakeJournal.Write("receipt_uncertain",
                                due.WakeSequence, result: "exception_after_reservation");
                        LiteEventLog.Write("multi_wake_uncertain_retry", new {
                            wakeSequence = due.WakeSequence,
                            state, clicked, rotate, retryMinutes = 0,
                            recoveryCount = current.ConsecutiveDeferrals
                        });
                    }
                    else
                    {
                        current.LastState = state;
                        if (minimalOblWake && (state == "visible_ack" || clicked))
                        {
                            current.ConsecutiveDeferrals = 0;
                            current.LastDeferralReason = "";
                        }
                    }
                    LiteChatWakeRegistry.Write(entries);
                }
            }
            if (state == "visible_ack")
            {
                await ArchiveOlderNativeChatBatchAsync(due.Url);
                await AuditNativeArchiveVisibilityAsync(due.Url);
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
