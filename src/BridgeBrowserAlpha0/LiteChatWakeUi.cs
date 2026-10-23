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
            if (entry.Enabled) entry.DueUtc = DateTimeOffset.UtcNow.AddMinutes(entry.EffectiveMinutes);
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
    internal static bool IsWakeReceiptRouteStable(string targetUrl,
        string? source, long startEpoch, long currentEpoch) =>
        IsWorkerRouteStable(targetUrl, source, startEpoch, currentEpoch);

    internal static async Task<bool> WaitForWakeReceiptAsync(
        CoreWebView2 core, string receipt, string? targetUrl = null,
        Func<long>? readEpoch = null)
    {
        const int totalBudgetMs = 4200;
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
        string check = "window.__OPENBROWSER_RESUME_SENDER__?.hasAck(" +
            JsonSerializer.Serialize(receipt) + ") === true";
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int probe = 0; probe < 8; probe++)
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
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(probeScript);
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
        Func<string?>? readVerifiedUrl = null, Func<long>? readEpoch = null)
    {
        // The current DOM may be from chat A during an SPA route change to B.
        // A matching address/composer/history alone is NOT proof of identity.
        try
        {
            return await VerifyWorkerHydrationAsync(
                url, verifiedUrl, () => core.Source,
                () => ExecuteWorkerScriptAsync(core, WakeWorkerHydratedScript),
                readVerifiedUrl, readEpoch);
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

    private async Task<bool> NavigateWorkerAsync(string url)
    {
        var core = _chatWakeWorker.CoreWebView2;
        if (core is null) return false;
        // Reuse this worker only after checking both URL and hydrated history.
        // A timed-out reuse probe MUST NOT trigger another navigation/script.
        try
        {
            if (await IsWorkerHydratedForChatAsync(core, url,
                _workerVerifiedChatUrl, () => _workerVerifiedChatUrl,
                () => _workerRouteEpoch))
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
                long probeRouteEpoch = _workerRouteEpoch;
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
                // A renderer that could not hydrate in 25s must not incur
                // another full 6.5s diagnostic timeout on the same attempt.
                var encoded = await ExecuteWorkerScriptAsync(core, countJs, 1500);
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
            catch (TimeoutException)
            {
                LiteEventLog.Write("multi_wake_history_not_ready", new {
                    domWaitSeconds = 25,
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
                    domWaitSeconds = 25,
                    hydrationAttempts,
                    elapsedMs = hydrationClock.ElapsedMilliseconds,
                    diagnosticError = ex.GetType().Name
                });
            }
            // Successful NavigationCompleted is NOT a successful history
            // hydration. Apply escalating cooldown to avoid repeatedly
            // paying 25s for the same unhydrated ChatGPT application shell.
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

    private async Task<(string Reason, string Hash, bool Complete,
        LiteModelStatus.Parsed? Status)>
        CheckWorkerPreflightAsync(CoreWebView2 core, string url, string previousHash)
    {
        bool foreground = ReferenceEquals(core, _browser.CoreWebView2);
        long epoch() => foreground ? _foregroundWakeRouteEpoch : _workerRouteEpoch;
        var result = await CaptureStablePreflightAsync(
            url, () => core.Source, epoch,
            () => ReadWorkerObservationAsync(core),
            () => Task.Delay(1500));
        if (!result.Stable)
        {
            LiteEventLog.Write("multi_wake_preflight_navigation_changed",
                new { foreground, definitelyNotSent = true });
            return ("navigation_changed", "", false, null);
        }
        var (first, second, _) = result;
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
        // Stable assistant fingerprint is also useful for an OWN pending
        // draft, even though draft_present correctly blocks normal sending.
        string hash = reason is "ready" or "draft_present" ?
            LiteChatWakePreflight.StableAssistantFingerprint(
                first, second, expectedPath) : "";
        var status = reason == "ready" ? LiteModelStatus.Parse(answer) : null;
        return (reason, hash,
            reason == "ready" && LiteChatWakePreflight.IsCompletion(answer),
            status);
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
        return CanResumeAfterUnconfirmedRecovery(
            priorState, priorHash, preflightReason, observedHash)
                ? WakePreflightGate.Continue
                : WakePreflightGate.HoldUnconfirmedRecovery;
    }

    internal static string NextStateAfterWakeDeferral(string priorState,
        string reason) =>
        IsUnconfirmedRecoveredDraftState(priorState) ? priorState : "waiting_" + reason;

    // Explicit model status is advisory: only a stable, fully hydrated
    // two-snapshot preflight in the EXACT expected chat can apply it.
    // The ordinary wake path remains unchanged with no/invalid status.
    private bool HandleVerifiedModelStatus(LiteChatWakeRegistry.Chat due,
        List<LiteChatWakeRegistry.Chat> entries,
        LiteModelStatus.Parsed? parsed, string reason, string answerHash)
    {
        var now = DateTimeOffset.UtcNow;
        string? decision = LiteModelStatus.ApplyDecision(parsed, now, reason);
        if (decision is null or "CONTINUE") return false;
        if (answerHash.Length != 64) return false;
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
        if (decision == "DONE")
        {
            due.Enabled = false;
            due.LastState = "status_done";
            LiteChatWakeRegistry.Write(entries);
            LiteWakeJournal.Write("schedule_completed", due.WakeSequence,
                result: "model_done");
            LiteEventLog.Write("multi_wake_model_status", new { status = decision });
            UpdateMultiWakeStatus();
            return true;
        }
        if (!LiteOwnerInterventions.IsIntervention(decision)) return false;
        string eventKey = LiteOwnerInterventions.EventKey(
            due.Url, decision, answerHash);
        bool newEvent = !string.Equals(due.LastInterventionKey, eventKey,
            StringComparison.Ordinal);
        due.Enabled = false;
        due.LastState = decision == "WAIT_USER" ? "status_wait_user" : "status_blocked";
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

    private void DeferWakeWithoutSend(string url, DateTimeOffset now,
        string reason, int minutes = 2)
    {
        minutes = WakeDeferralMinutes(reason, minutes);
        var registered = LiteChatWakeRegistry.Read();
        var entry = registered.FirstOrDefault(c => c.Url == url && c.Enabled);
        if (entry is null) return;
        entry.DueUtc = now.AddMinutes(minutes);
        // Backpressure, generation and hydration deferrals are NOT proof
        // that an earlier own-draft recovery did not click. Keep the
        // ambiguity marker durable through every unrelated deferral.
        entry.LastState = NextStateAfterWakeDeferral(entry.LastState, reason);
        LiteChatWakeRegistry.Write(registered);
        LiteWakeJournal.Write("deferred", entry.WakeSequence + 1, result: reason);
        LiteEventLog.Write("multi_wake_preflight_skipped", new {
            reason, retryMinutes = minutes, definitelyNotSent = true
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
        int retryMinutes = Math.Clamp((int)Math.Ceiling(remaining.TotalMinutes), 1, 5);
        DeferWakeWithoutSend(due.Url, now, "backend_http_429", retryMinutes);
        return true;
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
        // already_attempted MAY have clicked previously; never auto-retry it.
        return result.StartsWith("busy_", StringComparison.Ordinal) ||
            result is "missing_composer" or "invalid" or "blocked_navigation" ||
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
            LiteWakeJournal.Write("due", due.WakeSequence + 1);
            // The read-only API cooldown applies to automated probes only.
            // Post submission and user-initiated foreground browsing are not retried.
            if (HoldWakeOnBackendRead429(due)) return;
            if (!FlushNotes())
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
                    // Navigation can receive HTTP 429 after the first gate.
                    if (HoldWakeOnBackendRead429(due)) return;
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
                    // A blocked history request during preflight must not be
                    // misinterpreted as a hydrated, eligible conversation.
                    if (HoldWakeOnBackendRead429(due)) return;
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
                    if (gate == WakePreflightGate.HoldUnconfirmedRecovery)
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
                        preflight.Status, preflight.Reason, preflight.Hash))
                        return;
                    if (preflight.Reason == "draft_present")
                    {
                        // A previous attempt may have left OUR unique OB wake
                        // in the editor. Retry only that draft, never user text.
                        var encoded = await ExecuteWorkerScriptAsync(wakeCore,
                            "window.__OPENBROWSER_RESUME_SENDER__?.submitOwnDraft(" +
                            JsonSerializer.Serialize(new Uri(due.Url).AbsolutePath) + ")");
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
                    if (preflight.Reason != "ready")
                    {
                        DeferWakeWithoutSend(due.Url, now, preflight.Reason,
                            preflight.Reason == "same_answer_already_woken"
                                ? due.EffectiveMinutes : 2);
                        return;
                    }
                    // Durable fingerprint fences uncertain sends: never resend
                    // for an unchanged assistant answer after a crash/restart.
                    due.LastAnswerFingerprint = preflight.Hash;
                    due.LastAttemptUtc = now;
                    due.DueUtc = now.AddMinutes(due.EffectiveMinutes);
                    due.LastState = "attempting";
                    due.WakeSequence = checked(due.WakeSequence + 1);
                    LiteChatWakeRegistry.Write(entries);
                    attemptCommitted = true;
                    var key = LiteChatNotes.NormalizeKey(due.Url);
                    string receipt = Guid.NewGuid().ToString("N");
                    LiteWakeJournal.Write("attempt_reserved", due.WakeSequence, receipt);
                    string payload = LiteChatWakeRegistry.NumberedMessage(
                        due, _noteStore.Load(key), receipt, DateTimeOffset.UtcNow);
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
                        LiteWakeJournal.Write("definitely_unsent",
                            due.WakeSequence, receipt, "not_submitted");
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
                    if (phase is "click_invoked" or "form_submitted")
                        LiteWakeJournal.Write("submission_attempted",
                            due.WakeSequence, receipt, phase);
                    // Only after a successful submit click: wait briefly
                    // for React's real USER bubble in this exact chat.
                    // This never clicks Send a second time.
                    bool visible = state == "submission_requested" &&
                        phase is "click_invoked" or "form_submitted" &&
                        await WaitForWakeReceiptAsync(wakeCore, receipt, due.Url,
                            () => useForeground ?
                                _foregroundWakeRouteEpoch : _workerRouteEpoch);
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
                        LiteWakeJournal.Write("definitely_unsent",
                            due.WakeSequence, receipt, phase);
                        LiteEventLog.Write("multi_wake_definitely_unsent", new {
                            reason = phase, definitelyNotSent = true,
                            retryMinutes = 2
                        });
                        UpdateMultiWakeStatus();
                        return;
                    }
                    LiteWakeJournal.Write(visible ? "receipt_confirmed" :
                        "receipt_uncertain", due.WakeSequence, receipt,
                        visible ? "visible_ack" : phase);
                    LiteEventLog.Write("multi_wake_send", new {
                        wakeSequence = due.WakeSequence,
                        state, phase, visibleUserBubble = visible,
                        receiptConfirmed = visible, // unique receipt; never match older messages
                        includesNotes = !string.IsNullOrWhiteSpace(_noteStore.Load(key)),
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
