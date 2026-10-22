using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Actual WebView2 integration test without an account, tokens, network ChatGPT
/// or access to the live profile. chatgpt.com is mapped to local fixture files
/// inside a brand-new, disposable user data folder.
/// </summary>
internal static class LiteWebViewIntegrationSelfTest
{
    internal static int Run()
    {
        string temp = Path.Combine(Path.GetTempPath(),
            "OpenBrowser-WebView2-QA-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        int code = 9;
        try
        {
            string site = Path.Combine(temp, "site");
            Directory.CreateDirectory(Path.Combine(site, "c", "test"));
            File.WriteAllText(Path.Combine(site, "c", "test", "index.html"),
                FixtureHtml, new System.Text.UTF8Encoding(false));
            const string firstChatId = "11111111-1111-1111-1111-111111111111";
            const string secondChatId = "22222222-2222-2222-2222-222222222222";
            string firstDir = Path.Combine(site, "c", firstChatId);
            string secondDir = Path.Combine(site, "c", secondChatId);
            Directory.CreateDirectory(firstDir);
            Directory.CreateDirectory(secondDir);
            File.WriteAllText(Path.Combine(firstDir, "index.html"),
                FixtureHtml, new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(secondDir, "index.html"),
                FixtureHtml.Replace("Czarny kompas", "Zielona latarnia",
                    StringComparison.Ordinal), new System.Text.UTF8Encoding(false));
            using var form = new Form {
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000), Size = new Size(640, 420),
                Text = "Open Browser isolated WebView QA", Opacity = 1
            };
            using var view = new WebView2 { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(55));
            deadline.Token.Register(() => {
                try { form.BeginInvoke(() => form.Close()); } catch { }
            });
            form.Shown += async (_, _) => {
                string phase = "form_shown";
                try {
                    phase = "create_webview_environment";
                    var env = await CoreWebView2Environment.CreateAsync(
                        userDataFolder: Path.Combine(temp, "profile"));
                    phase = "ensure_webview_core";
                    await view.EnsureCoreWebView2Async(env);
                    var core = view.CoreWebView2;
                    phase = "check_worker_memory_target";
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                    Ensure(core.MemoryUsageTargetLevel == CoreWebView2MemoryUsageTargetLevel.Low,
                        "WebView2 supports Low worker memory target");
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                    Ensure(core.MemoryUsageTargetLevel == CoreWebView2MemoryUsageTargetLevel.Normal,
                        "WebView2 restores Normal before wake navigation");
                    phase = "virtual_host_mapping";
                    core.SetVirtualHostNameToFolderMapping("chatgpt.com", site,
                        CoreWebView2HostResourceAccessKind.DenyCors);
                    phase = "install_document_scripts";
                    foreach (string name in new[] {
                        "LiteHighGuard.js", "LiteResponseSignal.js",
                        "LiteResumeProbe.js", "LiteResumeSender.js",
                        "LiteDomOptimizer.js", "LiteDomHardPrune.js"
                    }) {
                        string script = File.ReadAllText(Path.Combine(
                            AppContext.BaseDirectory, name));
                        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
                    }
                    var navigation = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    void onNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
                        navigation.TrySetResult(e.IsSuccess);
                    }
                    core.NavigationCompleted += onNavigated;
                    phase = "navigate_virtual_host";
                    core.Navigate("https://chatgpt.com/c/test/index.html");
                    bool success = await navigation.Task.WaitAsync(
                        TimeSpan.FromSeconds(18));
                    core.NavigationCompleted -= onNavigated;
                    Ensure(success, "local virtual host navigation");
                    phase = "hard_dom_idle_timer_lifecycle";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__?.status().enabled") ==
                        "false", "hard DOM pruning defaults OFF in real WebView2");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__?.status().lastCheckedAt === null") ==
                        "true", "disabled hard DOM script never performs startup pruning");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.enable(true).enabled") ==
                        "true", "hard prune can still be deliberately enabled");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.enable(false).enabled") ==
                        "false", "owner can turn hard prune back OFF");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('main [data-user-message-bubble],main [data-chatgpt-selection-message-id]').length") ==
                        "4", "short chat is preserved across OFF/ON/OFF cycle");
                    phase = "worker_hydration_total_deadline";
                    Ensure(ChatGptLiteForm.WorkerHistoryProbeBudgetMs(0) == 6500,
                        "first hydration probe has at most 6.5s budget");
                    Ensure(ChatGptLiteForm.WorkerHistoryProbeBudgetMs(18500) == 6500,
                        "script timeout cannot exceed remaining hydration window");
                    Ensure(ChatGptLiteForm.WorkerHistoryProbeBudgetMs(20000) == 5000,
                        "script deadline shrinks with the remaining window");
                    Ensure(ChatGptLiteForm.WorkerHistoryProbeBudgetMs(24999) == 1,
                        "last hydration probe cannot exceed total 25s window");
                    Ensure(ChatGptLiteForm.WorkerHistoryProbeBudgetMs(25000) == 0,
                        "total hydration deadline stops further probe attempts");
                    Ensure(ChatGptLiteForm.WorkerHistoryProbeBudgetMs(32000) == 0,
                        "already expired hydration never schedules another script");
                    phase = "wake_delivery_uncertainty_fence";
                    Ensure(ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "busy_generating", "not_submitted"),
                        "preflight busy never counts as a delivery attempt");
                    Ensure(ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "missing_composer", "not_submitted"),
                        "missing composer releases the answer fence");
                    Ensure(ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "submission_requested", "draft_button_missing"),
                        "unsubmitted own draft retries without a 20-minute stall");
                    Ensure(ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "submission_requested", "blocked_navigation"),
                        "SPA route change cannot preserve a no-click answer fence");
                    Ensure(ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "submission_requested", "blocked_editor_changed"),
                        "modified editor cannot become an assumed send");
                    Ensure(!ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "submission_requested", "click_invoked"),
                        "clicked but missing ACK remains uncertain");
                    Ensure(!ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "submission_requested", "sender_missing"),
                        "indeterminate sender outcome retains durable fence");
                    Ensure(!ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "failed_TimeoutException", "not_submitted"),
                        "script timeout cannot be treated as definitely unsent");
                    phase = "background_worker_circuit_breaker";
                    var workerGate = new LiteRendererBackpressure();
                    var now = DateTimeOffset.UtcNow;
                    Ensure(!ChatGptLiteForm.ShouldDeferWorkerProbe(false, workerGate, now),
                        "healthy background worker remains eligible");
                    Ensure(workerGate.MarkFailed(now) == TimeSpan.FromSeconds(30),
                        "initial background script timeout gets 30s cooldown");
                    Ensure(ChatGptLiteForm.ShouldDeferWorkerProbe(false, workerGate,
                        now.AddSeconds(29)), "background wake defers within cooldown");
                    Ensure(!ChatGptLiteForm.ShouldDeferWorkerProbe(true, workerGate,
                        now.AddSeconds(1)), "foreground chat never inherits worker cooldown");
                    Ensure(!ChatGptLiteForm.ShouldDeferWorkerProbe(false, workerGate,
                        now.AddSeconds(30)), "background wake retries after cooldown");
                    Ensure(workerGate.MarkFailed(now.AddSeconds(40)) ==
                        TimeSpan.FromSeconds(60), "repeated failure increases cooldown");
                    workerGate.MarkNavigationRecovered();
                    Ensure(!ChatGptLiteForm.ShouldDeferWorkerProbe(false, workerGate,
                        now.AddSeconds(41)), "verified worker recovery clears cooldown");
                    phase = "check_hydrated_worker";
                    Ensure(await EvaluateAsync(core,
                        ChatGptLiteForm.WakeWorkerHydratedScript) == "true",
                        "background worker readiness requires composer and message history");
                    phase = "check_high";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HIGH_GUARD__?.audit()?.mode") == "high",
                        "true WebView2 recognizes High data attribute");
                    phase = "check_dil_dom_count";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__?.check()?.turnCount") == "4",
                        "DIL turn counter counts all four messages");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__?.getStatus()?.domVariant") == "dil_observe_only",
                        "DIL DOM is observed without aggressive mutation");
                    phase = "check_last_role";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.role") == "assistant",
                        "DIL layout recognizes last assistant");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.text"))
                        .Contains("Czarny kompas"),
                        "read last DIL assistant content");
                    phase = "worker_reuse_same_chat_isolated";
                    const string chatUrl =
                        "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
                    const string otherUrl =
                        "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/11111111-1111-1111-1111-111111111111')");
                    Ensure(LiteDevWakeState.NormalizeUrl(core.Source) == chatUrl,
                        "offline fixture now has a valid normalized chat URL");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl, chatUrl),
                        "hydrated background chat may be reused without reload");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl, null),
                        "newly selected URL without verified history cannot reuse worker");
                    Ensure(!ChatGptLiteForm.VerifiedWorkerMatches(
                        chatUrl, otherUrl, chatUrl),
                        "verified worker identity is revoked by a different source");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, otherUrl, otherUrl),
                        "a different background chat may never reuse the current DOM");
                    await core.ExecuteScriptAsync("""
                        window.__qaSavedTurns = Array.from(document.querySelector('main').children);
                        document.querySelector('main').replaceChildren();
                        """);
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl, chatUrl),
                        "composer-only application shell cannot be reused");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('aside').setAttribute('data-user-message-bubble','')");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl, chatUrl),
                        "messages in sidebar are not accepted as chat history");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('aside').removeAttribute('data-user-message-bubble')");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('main').replaceChildren(...window.__qaSavedTurns)");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl, chatUrl),
                        "background chat becomes reusable when history returns");
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/22222222-2222-2222-2222-222222222222')");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(
                        core, otherUrl, chatUrl),
                        "SPA route change cannot reuse previous chat's history");
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/11111111-1111-1111-1111-111111111111')");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(
                        core, chatUrl, null),
                        "return after SPA route change needs fresh verified navigation");
                    const string receipt = "1234567890abcdef1234567890abcdef";
                    phase = "ack_scoped_to_active_main_user_bubbles";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const aside=document.querySelector('aside');
                          aside.setAttribute('data-user-message-bubble','');
                          aside.textContent='[OB-1234567890abcdef1234567890abcdef] Sidebar preview';
                          const n=document.createElement('div');
                          n.id='qa-assistant-quote';
                          n.setAttribute('data-chatgpt-selection-message-id','qa-quote');
                          n.textContent='I saw [OB-1234567890abcdef1234567890abcdef] in a quoted answer';
                          document.querySelector('main').appendChild(n);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receipt + "')") ==
                        "false", "sidebar marker and assistant quote cannot spoof ACK");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const a=document.querySelector('aside');
                          a.removeAttribute('data-user-message-bubble');
                          a.textContent='Background spinner';
                          document.getElementById('qa-assistant-quote').remove();
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receipt + "')") ==
                        "false", "no receipt before a real user message");
                    var js = "window.__OPENBROWSER_RESUME_SENDER__?.send(" +
                        JsonSerializer.Serialize("Napisz ósmy rozdział tej testowej książki.") +
                        "," + JsonSerializer.Serialize(receipt) + ")";
                    phase = "send_test_message";
                    var state = await EvaluateAsync(core, js);
                    Ensure(state == "submission_requested", "one submission requested");
                    phase = "wait_and_check_ack";
                    await Task.Delay(850);
                    bool ack = await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__?.hasAck('" + receipt + "')") == "true";
                    if (!ack) {
                        var bubbles = await EvaluateAsync(core,
                            "document.querySelectorAll('[data-user-message-bubble]').length");
                        var draftLength = await EvaluateAsync(core,
                            "document.querySelector('#prompt-textarea').innerText.length");
                        var includesMarker = await EvaluateAsync(core,
                            "document.querySelector('#prompt-textarea').innerText.includes('" + receipt + "')");
                        var bubbleInfo = await EvaluateAsync(core,
                            "(() => {let b=[...document.querySelectorAll('[data-user-message-bubble]')].at(-1); " +
                            "return JSON.stringify({len:b.textContent.length, marker:b.textContent.includes('" + receipt + "')," +
                            " visibleLen:b.innerText.length});})()");
                        throw new InvalidOperationException("ack_not_found; bubbles=" + bubbles +
                            "; draftLength=" + draftLength + "; markerInEditor=" + includesMarker +
                            "; lastBubble=" + bubbleInfo);
                    }
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('[data-user-message-bubble]').length") == "3",
                        "exactly one new user bubble");
                    Ensure(await EvaluateAsync(core, js) == "already_visible",
                        "replaying same receipt does not send twice");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('[data-user-message-bubble]').length") == "3",
                        "no duplicate bubble after repeated call");
                    phase = "detect_and_clear_visible_failure";
                    await core.ExecuteScriptAsync("(() => {const n=document.createElement('div'); " +
                        "n.id='qa-error';n.setAttribute('data-chatgpt-selection-message-id','failure');" +
                        "n.innerText='Nie udało się wczytać tej odpowiedzi';" +
                        "document.querySelector('main').appendChild(n);})()");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESPONSE_SIGNALS__.read().visibleError") ==
                        "response_load_failed", "DIL failure recognized as real UI error");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().error") == "true",
                        "supervisor stops when the UI shows a DIL failure");
                    await core.ExecuteScriptAsync("document.getElementById('qa-error').remove()");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().error") == "false",
                        "test fixture error can disappear without automatic retry");
                    phase = "check_dil_soft_pilot";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const root = document.querySelector('main');
                          for (let i = 0; i < 95; i++) {
                            const n = document.createElement('div');
                            n.style.minHeight = '145px';
                            n.setAttribute('data-chatgpt-selection-message-id', 'qa-' + i);
                            n.textContent = 'Offline historical assistant turn ' + i;
                            root.appendChild(n);
                          }
                        })()
                        """);
                    // Hidden sidebar aria-busy in the fixture must not block a
                    // deliberately enabled, offscreen-only CSS pilot.
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().modernSoftEnabled") == "false",
                        "modern soft optimizer remains disabled by default");
                    string elementCount = await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().totalDomElements");
                    Ensure(int.TryParse(elementCount, out int totalElements) && totalElements > 0,
                        "WebView2 periodically records DOM element count");
                    Ensure(await EvaluateAsync(core,
                        "Number.isFinite(window.__OPENBROWSER_LITE_DOM__.getStatus().domSampleAt) && window.__OPENBROWSER_LITE_DOM__.getStatus().domSampleAt > 0") == "true",
                        "WebView2 records time of the DOM census");
                    string softCount = await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.setModernSoftEnabled(true).optimized");
                    Ensure(int.TryParse(softCount, out int softTurns) &&
                        softTurns >= 50 && softTurns <= 60,
                        "real WebView2 applies soft CSS to old block DIL messages");
                    await core.ExecuteScriptAsync(
                        "window.__OPENBROWSER_LITE_DOM__.setModernSoftEnabled(false)");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('[data-ob-lite-offscreen]').length") == "0",
                        "soft CSS pilot is fully reversible");

                    // The same WebView2 worker must isolate two different
                    // chat histories after real document navigations.
                    phase = "background_two_chat_navigation";
                    var secondNavigation = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    void secondCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
                        secondNavigation.TrySetResult(e.IsSuccess);
                    core.NavigationCompleted += secondCompleted;
                    core.Navigate("https://chatgpt.com/c/" + secondChatId + "/index.html");
                    Ensure(await secondNavigation.Task.WaitAsync(TimeSpan.FromSeconds(18)),
                        "second offline chat loaded in existing worker");
                    core.NavigationCompleted -= secondCompleted;
                    await core.ExecuteScriptAsync(
                        "history.replaceState({}, '', '/c/" + secondChatId + "')");
                    Ensure(LiteDevWakeState.NormalizeUrl(core.Source) == otherUrl,
                        "second chat has its own exact UUID URL");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl, chatUrl),
                        "first chat cannot use second chat's hydrated DOM");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, otherUrl, otherUrl),
                        "second chat can reuse its own loaded history");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.text"))
                        .Contains("Zielona latarnia"),
                        "second chat has its distinct assistant answer");

                    // Exercise the SAME host-managed sendDirect + submitDirect
                    // protocol as background waking, not only its URL guard.
                    phase = "background_second_chat_receipt_isolation";
                    const string receiptB = "fedcba0987654321fedcba0987654321";
                    const string receiptBlocked = "abcdef0123456789abcdef0123456789";
                    string payloadB = "[OB-" + receiptB +
                        "]\nKontynuuj wyłącznie drugą testową rozmowę.";
                    string directB = "window.__OPENBROWSER_RESUME_SENDER__.sendDirect(" +
                        JsonSerializer.Serialize(payloadB) + "," +
                        JsonSerializer.Serialize(receiptB) + ",true)";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receipt + "')") ==
                        "false", "receipt from chat A is absent in chat B");
                    Ensure(await EvaluateAsync(core, directB) == "submission_requested",
                        "host prepares a unique wake draft in chat B");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receiptB + "')") ==
                        "false", "injected draft is not mistaken for delivery ACK");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.submitDirect('" + receiptB + "')") ==
                        "click_invoked", "host flushes chat B using its composer form");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receiptB + "')") ==
                        "true", "second chat has a real unique user-bubble receipt");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.diagnose().pendingReceipts") ==
                        "0", "clicked background wake releases the full pending payload");
                    Ensure(await EvaluateAsync(core, directB) == "already_visible",
                        "second chat receipt cannot be sent twice");
                    string blockedPayload = "[OB-" + receiptBlocked +
                        "]\nNie wysyłaj do rozmowy po zmianie adresu.";
                    string blockedJs = "window.__OPENBROWSER_RESUME_SENDER__.sendDirect(" +
                        JsonSerializer.Serialize(blockedPayload) + "," +
                        JsonSerializer.Serialize(receiptBlocked) + ",true)";
                    Ensure(await EvaluateAsync(core, blockedJs) == "submission_requested",
                        "second test draft prepared before SPA navigation");
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/" + firstChatId + "')");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.submitDirect('" +
                        receiptBlocked + "')") == "blocked_navigation",
                        "host refuses a pending send after SPA chat change");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" +
                        receiptBlocked + "')") == "false",
                        "wrong-chat pending wake was never delivered");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.diagnose().pendingReceipts") ==
                        "0", "blocked cross-chat pending payload is released");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('#prompt-textarea').innerText=''");
                    var firstNavigation = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    void firstCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
                        firstNavigation.TrySetResult(e.IsSuccess);
                    core.NavigationCompleted += firstCompleted;
                    core.Navigate("https://chatgpt.com/c/" + firstChatId + "/index.html");
                    Ensure(await firstNavigation.Task.WaitAsync(TimeSpan.FromSeconds(18)),
                        "first offline chat reloads in the same worker");
                    core.NavigationCompleted -= firstCompleted;
                    await core.ExecuteScriptAsync(
                        "history.replaceState({}, '', '/c/" + firstChatId + "')");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, otherUrl, otherUrl),
                        "second chat cannot use reloaded first chat DOM");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl, chatUrl),
                        "first chat hydrates again after switching back");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.text"))
                        .Contains("Czarny kompas"),
                        "original answer returns without cross-chat contamination");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receiptB + "')") ==
                        "false", "chat B receipt cannot appear inside reloaded chat A");

                    phase = "archive_cross_navigation_fence";
                    Ensure(ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 10, new Uri(chatUrl), chatUrl),
                        "archive accepts same chat and same navigation epoch");
                    Ensure(!ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 11, new Uri(chatUrl), chatUrl),
                        "archive rejects stale script after navigation");
                    Ensure(!ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 10, new Uri(chatUrl), otherUrl),
                        "archive rejects a completed script from another chat");
                    Ensure(!ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 10, new Uri(chatUrl), "about:blank"),
                        "archive rejects non-ChatGPT navigation result");
                    LiteEventLog.Write("webview_integration_selftest", new {
                        passed = true, checks = 83, offline = true
                    });
                    code = 0;
                }
                catch (Exception ex) {
                    LiteEventLog.Write("webview_integration_selftest", new {
                        passed = false, phase, errorType = ex.GetType().Name,
                        message = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message,
                        offline = true
                    });
                    code = 4;
                }
                finally { form.Close(); }
            };
            Application.Run(form);
        }
        catch (Exception ex) {
            LiteEventLog.Write("webview_integration_selftest", new {
                passed = false, errorType = ex.GetType().Name, offline = true
            });
            code = 5;
        }
        finally {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
        return code;
    }

    private static async Task<string> EvaluateAsync(CoreWebView2 core, string js)
    {
        var result = await core.ExecuteScriptAsync(js).WaitAsync(
            TimeSpan.FromSeconds(9));
        using var parsed = JsonDocument.Parse(result);
        return parsed.RootElement.ValueKind switch {
            JsonValueKind.String => parsed.RootElement.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => parsed.RootElement.ToString()
        };
    }
    private static void Ensure(bool ok, string name) {
        if (!ok) throw new InvalidOperationException(name);
    }

    private const string FixtureHtml = """
<!doctype html>
<html lang="pl"><head><meta charset="utf-8"><title>testowy</title></head>
<body>
<button aria-label="Wybierz model ChatGPT" data-selected-reasoning-effort="high">
Wysiłek myślowy</button>
<aside aria-busy="true" style="display:none">Background spinner, not a response</aside>
<main id="conversation">
<div data-user-message-bubble>Napisz książkę przygodową.</div>
<div data-chatgpt-selection-message-id="chapter1">Rozdział pierwszy.</div>
<div data-user-message-bubble>Kontynuuj przygodę.</div>
<div data-chatgpt-selection-message-id="chapter7">Rozdział 7. Czarny kompas.
Zostały wątki do rozwinięcia.</div>
</main>
<form data-chatgpt-composer>
<div id="prompt-textarea" contenteditable="true" aria-label="Napisz wiadomość"></div>
<button type="submit" aria-label="Wyślij">Wyślij</button>
<button type="button" aria-label="Tryb głosowy">Mów</button>
</form>
<script>
document.querySelector('form[data-chatgpt-composer]').addEventListener('submit',(e)=>{
 e.preventDefault();
 const editor=document.querySelector('#prompt-textarea');
 const item=document.createElement('div');
 item.setAttribute('data-user-message-bubble','');
 item.textContent=editor.innerText;
 document.querySelector('main').appendChild(item);
 editor.innerText='';
});
</script>
</body></html>
""";
}
