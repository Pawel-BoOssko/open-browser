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
                        "LiteDomOptimizer.js", "LiteDomHardPrune.js",
                        "LiteConversationSnapshot.js"
                    }) {
                        string script = File.ReadAllText(Path.Combine(
                            AppContext.BaseDirectory, name));
                        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
                    }
                    var navigation = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    ulong? startedNavigationId = null;
                    string? startedNavigationUri = null;
                    ulong? completedNavigationId = null;
                    void onStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
                    {
                        startedNavigationId ??= e.NavigationId;
                        startedNavigationUri ??= e.Uri;
                    }
                    void onNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
                        completedNavigationId = e.NavigationId;
                        navigation.TrySetResult(e.IsSuccess);
                    }
                    core.NavigationStarting += onStarting;
                    core.NavigationCompleted += onNavigated;
                    phase = "navigate_virtual_host";
                    core.Navigate("https://chatgpt.com/c/test/index.html");
                    bool success = await navigation.Task.WaitAsync(
                        TimeSpan.FromSeconds(18));
                    core.NavigationStarting -= onStarting;
                    core.NavigationCompleted -= onNavigated;
                    Ensure(success, "local virtual host navigation");
                    phase = "correlated_worker_navigation_completion";
                    const string navA = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
                    const string navB = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
                    Ensure(ChatGptLiteForm.MatchesWorkerNavigationStart(navA, navA),
                        "matching target chat URI binds the corresponding starting navigation");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationStart(navA, navB),
                        "late NavigationStarting from prior chat B cannot bind target chat A");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationStart(
                        navA, "https://chatgpt.com/auth/login"),
                        "authentication redirect cannot certify a chat navigation");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationStart(
                        navA, "https://other.example/c/11111111-1111-1111-1111-111111111111"),
                        "foreign host cannot masquerade as worker chat target");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationStart(
                        navA, navA + "?source=sidebar"),
                        "query-string navigation is not the exact scheduled chat");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationStart(navA, null),
                        "missing starting URI never binds navigation identity");
                    Ensure(startedNavigationUri is not null &&
                        !ChatGptLiteForm.MatchesWorkerNavigationStart(navA, startedNavigationUri),
                        "actual offline WebView2 fixture navigation is not a scheduled chat URL");
                    Ensure(startedNavigationId.HasValue && completedNavigationId.HasValue,
                        "actual WebView2 navigation provides starting and completed IDs");
                    Ensure(ChatGptLiteForm.MatchesWorkerNavigationCompletion(
                        startedNavigationId, completedNavigationId!.Value, true),
                        "real matching WebView2 navigation ID can certify completion");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationCompletion(
                        null, completedNavigationId.Value, true),
                        "completion before target navigation starts must be ignored");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationCompletion(
                        startedNavigationId, completedNavigationId.Value + 1, true),
                        "late completion from a different navigation cannot unblock target");
                    Ensure(!ChatGptLiteForm.MatchesWorkerNavigationCompletion(
                        startedNavigationId, completedNavigationId.Value, false),
                        "failed target navigation cannot certify usable history");
                    Ensure(ChatGptLiteForm.MatchesWorkerNavigationCompletion(
                        startedNavigationId, completedNavigationId.Value, true),
                        "matched successful navigation still allows hydration attempt");
                    phase = "hard_dom_idle_timer_lifecycle";
                    Ensure(!ChatGptLiteForm.ShouldPollHardPrune(false),
                        "WinForms hard-prune report timer stays stopped while OFF");
                    Ensure(ChatGptLiteForm.ShouldPollHardPrune(true),
                        "WinForms report timer can arm on deliberate owner opt-in");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__?.status().enabled") ==
                        "true", "hard DOM pruning defaults ON with archive gate in real WebView2");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__?.status().lastCheckedAt === null") ==
                        "true", "startup prune runs only after its initial delay");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.enable(true).enabled") ==
                        "true", "hard prune remains enabled after repeated owner opt-in");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.enable(false).enabled") ==
                        "false", "owner can turn hard prune back OFF");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('main [data-user-message-bubble],main [data-chatgpt-selection-message-id]').length") ==
                        "4", "short chat is preserved across OFF/ON/OFF cycle");
                    phase = "native_visibility_short_cooldown";
                    var cooldownBase = DateTimeOffset.UnixEpoch;
                    Ensure(ChatGptLiteForm.NativeVisibilityShortCooldownDelay(
                        cooldownBase, cooldownBase.AddSeconds(25)) ==
                        TimeSpan.FromSeconds(26),
                        "wake just before cooldown expiry waits briefly and audits");
                    Ensure(ChatGptLiteForm.NativeVisibilityShortCooldownDelay(
                        cooldownBase, cooldownBase.AddSeconds(60)) ==
                        TimeSpan.FromSeconds(61),
                        "short retry waits at most 61 seconds including safety margin");
                    Ensure(ChatGptLiteForm.NativeVisibilityShortCooldownDelay(
                        cooldownBase, cooldownBase.AddSeconds(61)) is null,
                        "long rate limit does not block wake loop");
                    Ensure(ChatGptLiteForm.NativeVisibilityShortCooldownDelay(
                        cooldownBase, cooldownBase.AddSeconds(-1)) is null,
                        "expired rate limit proceeds immediately");
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
                    phase = "two_snapshot_preflight_route_fence";
                    const string preflightA =
                        "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
                    const string preflightB =
                        "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
                    string simulatedSource = preflightA;
                    long simulatedEpoch = 5;
                    int captures = 0;
                    var observation = new LiteChatWakePreflight.Observation {
                        Location = "/c/11111111-1111-1111-1111-111111111111",
                        Role = "assistant", Text = "Completed",
                        ComposerReady = true, TurnCount = 4, AssistantNodeCount = 2
                    };
                    Task<LiteChatWakePreflight.Observation?> readObservation() {
                        captures++;
                        return Task.FromResult<LiteChatWakePreflight.Observation?>(observation);
                    }
                    var stablePreflight = await ChatGptLiteForm.CaptureStablePreflightAsync(
                        preflightA, () => simulatedSource, () => simulatedEpoch,
                        readObservation, () => Task.CompletedTask);
                    Ensure(stablePreflight.Stable && captures == 2,
                        "stable chat history accepts exactly two observations");
                    Ensure(LiteChatWakePreflight.Eligibility(
                        stablePreflight.First, stablePreflight.Second,
                        observation.Location, "") == "ready",
                        "stable preflight still permits a completed unchanged assistant answer");
                    captures = 0;
                    simulatedSource = preflightB;
                    var wrongPreflight = await ChatGptLiteForm.CaptureStablePreflightAsync(
                        preflightA, () => simulatedSource, () => simulatedEpoch,
                        readObservation, () => Task.CompletedTask);
                    Ensure(!wrongPreflight.Stable && captures == 0,
                        "different chat is rejected before any script execution");
                    simulatedSource = preflightA;
                    captures = 0;
                    var pendingSnapshot = new TaskCompletionSource<LiteChatWakePreflight.Observation?>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    var duringFirst = ChatGptLiteForm.CaptureStablePreflightAsync(
                        preflightA, () => simulatedSource, () => simulatedEpoch,
                        () => { captures++; return pendingSnapshot.Task; },
                        () => Task.CompletedTask);
                    Ensure(!duringFirst.IsCompleted && captures == 1,
                        "preflight starts one bounded asynchronous DOM observation");
                    simulatedSource = preflightB;
                    simulatedEpoch++;
                    pendingSnapshot.SetResult(observation);
                    Ensure(!(await duringFirst).Stable && captures == 1,
                        "navigation during first observation prevents a second script");
                    simulatedSource = preflightA;
                    captures = 0;
                    long beforeAba = simulatedEpoch;
                    var abaPreflight = await ChatGptLiteForm.CaptureStablePreflightAsync(
                        preflightA, () => simulatedSource, () => simulatedEpoch,
                        readObservation, () => {
                            simulatedEpoch += 2; // route A -> B -> A; same final URL
                            return Task.CompletedTask;
                        });
                    Ensure(!abaPreflight.Stable && captures == 1,
                        "A-B-A navigation during 1.5s delay cannot certify old answer");
                    Ensure(!ChatGptLiteForm.IsPreflightRouteStable(
                        preflightA, simulatedSource, beforeAba, simulatedEpoch),
                        "same URL with changed epoch is not a stable preflight");
                    captures = 0;
                    var secondPending = new TaskCompletionSource<LiteChatWakePreflight.Observation?>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    var duringSecond = ChatGptLiteForm.CaptureStablePreflightAsync(
                        preflightA, () => simulatedSource, () => simulatedEpoch,
                        () => {
                            captures++;
                            return captures == 1 ? readObservation() : secondPending.Task;
                        }, () => Task.CompletedTask);
                    Ensure(!duringSecond.IsCompleted,
                        "second observation remains pending during navigation");
                    simulatedEpoch += 2;
                    secondPending.SetResult(observation);
                    Ensure(!(await duringSecond).Stable,
                        "route A-B-A during second snapshot cannot confirm an old response");
                    phase = "rotation_foreground_follow";
                    const string oldLead = "https://chatgpt.com/c/6ae6112b-a58c-83e9-b2a4-3083ff5c995f";
                    const string newLead = "https://chatgpt.com/c/6ae62e2c-40e4-83e9-9057-1533aa227932";
                    const string foreignLead = "https://chatgpt.com/c/6ae5ce2b-9ecc-83e9-9ed8-c7c7b846029c";
                    Ensure(ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                        oldLead, oldLead, newLead), "old lead error page follows promoted lead");
                    Ensure(ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                        "https://chatgpt.com/", oldLead, newLead), "owned homepage follows promoted lead");
                    Ensure(!ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                        foreignLead, oldLead, newLead), "foreign conversation remains untouched");
                    Ensure(!ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                        "https://example.com/", oldLead, newLead), "unrelated site remains untouched");
                    Ensure(!ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                        oldLead, oldLead, "https://example.com/"), "invalid successor cannot be followed");
                    phase = "stall_two_slot_direct_recovery";
                    var firstStallSlot = new DateTimeOffset(2026,10,31,16,0,0,TimeSpan.Zero);
                    Ensure(!ChatGptLiteForm.HasStallPassedSecondSlot(
                        firstStallSlot, firstStallSlot.AddMinutes(14), 15),
                        "first slot still permits two-minute retry");
                    Ensure(ChatGptLiteForm.HasStallPassedSecondSlot(
                        firstStallSlot, firstStallSlot.AddMinutes(15), 15),
                        "second slot forces immediate numbered wake");
                    Ensure(!ChatGptLiteForm.HasStallPassedSecondSlot(
                        default, firstStallSlot.AddMinutes(30), 15),
                        "no phantom overdue without first scheduled slot");
                    Ensure(ChatGptLiteForm.IsStalledLeadReason("generating", false),
                        "active generation counts as busy");
                    Ensure(ChatGptLiteForm.IsStalledLeadReason("assistant_not_complete", true),
                        "unfinished lead answer counts as busy");
                    Ensure(!ChatGptLiteForm.IsStalledLeadReason("assistant_not_complete", false),
                        "unfinished non-lead must not force a wake");
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
                        "already_attempted", "not_submitted"),
                        "same receipt may have been clicked; missing ACK is not proof of no send");
                    Ensure(ChatGptLiteForm.IsUnconfirmedRecoveredDraftState(
                        "own_draft_clicked_unconfirmed"),
                        "durable unknown recovery click must block repeated click after restart");
                    Ensure(!ChatGptLiteForm.IsUnconfirmedRecoveredDraftState(
                        "own_draft_visible_ack"),
                        "visible user receipt removes unconfirmed recovery fence");
                    Ensure(!ChatGptLiteForm.IsUnconfirmedRecoveredDraftState(
                        "waiting_draft_present"),
                        "explicitly unsent draft can still attempt first recovery");
                    Ensure(ChatGptLiteForm.NextStateAfterWakeDeferral(
                        "own_draft_clicked_unconfirmed", "worker_backpressure") ==
                        "own_draft_clicked_unconfirmed",
                        "shared worker cooldown must not erase an uncertain old click");
                    Ensure(ChatGptLiteForm.NextStateAfterWakeDeferral(
                        "own_draft_clicked_unconfirmed", "generating") ==
                        "own_draft_clicked_unconfirmed",
                        "assistant still generating cannot erase own-draft duplicate fence");
                    Ensure(ChatGptLiteForm.NextStateAfterWakeDeferral(
                        "armed", "no_composer") == "waiting_no_composer",
                        "ordinary no-click deferrals retain existing retry semantics");
                    phase = "unconfirmed_recovered_draft_old_answer_fingerprint";
                    const string recoverPath = "/c/11111111-1111-1111-1111-111111111111";
                    var draftOld = new LiteChatWakePreflight.Observation {
                        Location = recoverPath, Role = "assistant",
                        Text = "Original assistant answer before saved own draft",
                        ComposerReady = true, TurnCount = 4,
                        AssistantNodeCount = 2, HasDraft = true
                    };
                    var draftOldAgain = new LiteChatWakePreflight.Observation {
                        Location = recoverPath, Role = "assistant",
                        Text = draftOld.Text, ComposerReady = true,
                        TurnCount = 4, AssistantNodeCount = 2, HasDraft = true
                    };
                    string oldHash = LiteChatWakePreflight.Fingerprint(draftOld.Text);
                    string nextHash = LiteChatWakePreflight.Fingerprint(
                        "A genuinely new assistant answer");
                    Ensure(LiteChatWakePreflight.Eligibility(
                        draftOld, draftOldAgain, recoverPath, "") == "draft_present",
                        "existing own draft remains a blocker to ordinary wake");
                    Ensure(LiteChatWakePreflight.StableAssistantFingerprint(
                        draftOld, draftOldAgain, recoverPath) == oldHash,
                        "stable assistant hash remains available behind a pending draft");
                    phase = "assistant_history_structural_stability";
                    Ensure(LiteChatWakePreflight.SameConversationHistory(
                        draftOld, draftOldAgain),
                        "unchanged turn and message counters define stable history");
                    draftOldAgain.TurnCount++;
                    Ensure(LiteChatWakePreflight.StableAssistantFingerprint(
                        draftOld, draftOldAgain, recoverPath) == "",
                        "new turn while assistant text is unchanged cannot certify baseline");
                    draftOldAgain.TurnCount--;
                    draftOldAgain.AssistantNodeCount++;
                    Ensure(LiteChatWakePreflight.StableAssistantFingerprint(
                        draftOld, draftOldAgain, recoverPath) == "",
                        "assistant node count changing cannot certify history");
                    draftOldAgain.AssistantNodeCount--;
                    draftOldAgain.UserNodeCount++;
                    Ensure(!LiteChatWakePreflight.SameConversationHistory(
                        draftOld, draftOldAgain),
                        "new user bubble invalidates two-snapshot history");
                    draftOldAgain.UserNodeCount--;
                    draftOldAgain.LastAssistantChars++;
                    Ensure(LiteChatWakePreflight.StableAssistantFingerprint(
                        draftOld, draftOldAgain, recoverPath) == "",
                        "assistant character count changing invalidates old hash");
                    draftOldAgain.LastAssistantChars--;
                    draftOld.HasDraft = false; draftOldAgain.HasDraft = false;
                    draftOldAgain.UserNodeCount++;
                    Ensure(LiteChatWakePreflight.Eligibility(
                        draftOld, draftOldAgain, recoverPath, "") == "history_not_stable",
                        "same assistant text does not permit wake as user history changes");
                    draftOldAgain.UserNodeCount--;
                    Ensure(LiteChatWakePreflight.Eligibility(
                        draftOld, draftOldAgain, recoverPath,
                        oldHash.ToLowerInvariant()) == "ready",
                        "owner allows another scheduled wake from an unchanged completed response");
                    draftOld.HasDraft = true; draftOldAgain.HasDraft = true;
                    Ensure(LiteChatWakePreflight.SameConversationHistory(
                        draftOld, draftOldAgain),
                        "stabilized history resumes eligibility normally");
                    Ensure(LiteChatWakePreflight.StableAssistantFingerprint(
                        draftOld, draftOldAgain, "/c/22222222-2222-2222-2222-222222222222") == "",
                        "foreign chat cannot provide recovery baseline fingerprint");
                    draftOldAgain.Text = "Streaming, still changing";
                    Ensure(LiteChatWakePreflight.StableAssistantFingerprint(
                        draftOld, draftOldAgain, recoverPath) == "",
                        "moving assistant answer cannot certify recovery baseline");
                    draftOldAgain.Text = draftOld.Text;
                    draftOldAgain.Generating = true;
                    Ensure(LiteChatWakePreflight.StableAssistantFingerprint(
                        draftOld, draftOldAgain, recoverPath) == "",
                        "generating assistant cannot certify recovery baseline");
                    Ensure(!ChatGptLiteForm.CanResumeAfterUnconfirmedRecovery(
                        "own_draft_clicked_unconfirmed", "", "ready", oldHash),
                        "old recovery without persisted baseline must remain fenced");
                    Ensure(!ChatGptLiteForm.CanResumeAfterUnconfirmedRecovery(
                        "own_draft_clicked_unconfirmed", oldHash, "ready", oldHash),
                        "same old answer after disappearing draft cannot send a duplicate");
                    Ensure(!ChatGptLiteForm.CanResumeAfterUnconfirmedRecovery(
                        "own_draft_clicked_unconfirmed", oldHash.ToLowerInvariant(),
                        "ready", oldHash),
                        "lowercase legacy hash cannot bypass the old-answer duplicate fence");
                    Ensure(!ChatGptLiteForm.CanResumeAfterUnconfirmedRecovery(
                        "own_draft_clicked_unconfirmed", oldHash, "draft_present", nextHash),
                        "even a changed draft does not prove a completed new answer");
                    Ensure(ChatGptLiteForm.CanResumeAfterUnconfirmedRecovery(
                        "own_draft_clicked_unconfirmed", oldHash, "ready", nextHash),
                        "distinct stable new assistant answer releases recovery hold");
                    Ensure(ChatGptLiteForm.CanResumeAfterUnconfirmedRecovery(
                        "armed", "", "ready", nextHash),
                        "ordinary schedules without uncertain recovery keep working");
                    phase = "terminal_completion_overrides_uncertain_draft";
                    var terminalOld = new LiteChatWakePreflight.Observation {
                        Location = recoverPath, Role = "assistant",
                        Text = "[OPENBROWSER-PLAN-COMPLETE]",
                        ComposerReady = true, TurnCount = 5,
                        AssistantNodeCount = 3
                    };
                    var terminalNew = new LiteChatWakePreflight.Observation {
                        Location = recoverPath, Role = "assistant",
                        Text = terminalOld.Text, ComposerReady = true,
                        TurnCount = 5, AssistantNodeCount = 3
                    };
                    string terminalEligibility = LiteChatWakePreflight.Eligibility(
                        terminalOld, terminalNew, recoverPath, "");
                    Ensure(terminalEligibility == "ready" &&
                        LiteChatWakePreflight.IsCompletion(terminalNew.Text),
                        "terminal marker only qualifies after stable correct-chat preflight");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(true,
                        "own_draft_clicked_unconfirmed", "", "ready", "") ==
                        ChatGptLiteForm.WakePreflightGate.Complete,
                        "terminal completion disables schedule despite unknown old fingerprint");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(true,
                        "own_draft_clicked_unconfirmed", oldHash, "ready", oldHash) ==
                        ChatGptLiteForm.WakePreflightGate.Complete,
                        "verified terminal response also supersedes identical recovery hash");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(true,
                        "armed", "", "ready", "") ==
                        ChatGptLiteForm.WakePreflightGate.Complete,
                        "normal schedule still stops on verified terminal answer");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(true,
                        "own_draft_clicked_unconfirmed", "", "wrong_chat", "") ==
                        ChatGptLiteForm.WakePreflightGate.HoldUnconfirmedRecovery,
                        "foreign chat cannot forge terminal completion");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(false,
                        "own_draft_clicked_unconfirmed", "", "ready", nextHash) ==
                        ChatGptLiteForm.WakePreflightGate.Continue,
                        "uncertain prior wake does not block the next stable answer");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(false,
                        "own_draft_clicked_unconfirmed", oldHash, "ready", oldHash) ==
                        ChatGptLiteForm.WakePreflightGate.Continue,
                        "duplicate wake is allowed even after an identical answer");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(false,
                        "own_draft_clicked_unconfirmed", oldHash, "ready", nextHash) ==
                        ChatGptLiteForm.WakePreflightGate.Continue,
                        "genuinely new stable reply resumes ordinary schedule");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(false,
                        "armed", "", "ready", nextHash) ==
                        ChatGptLiteForm.WakePreflightGate.Continue,
                        "unaffected chat remains eligible for normal wake");
                    Ensure(ChatGptLiteForm.ChooseWakePreflightGate(false,
                        "own_draft_clicked_unconfirmed", oldHash,
                        "draft_present", nextHash) ==
                        ChatGptLiteForm.WakePreflightGate.HoldUnconfirmedRecovery,
                        "own draft present never permits re-click without completion");
                    Ensure(ChatGptLiteForm.IsDefinitelyUnsentWake(
                        "blocked_navigation", "not_submitted"),
                        "target mismatch before injection releases the durable answer fence");
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
                    phase = "unhydrated_worker_shell_backpressure";
                    var shellGate = new LiteRendererBackpressure();
                    Ensure(ChatGptLiteForm.RecordWorkerProbeFailure(
                        shellGate, now) == TimeSpan.FromSeconds(30),
                        "first unhydrated ChatGPT shell applies backpressure");
                    Ensure(ChatGptLiteForm.RecordWorkerProbeFailure(
                        shellGate, now.AddSeconds(35)) == TimeSpan.FromSeconds(60),
                        "second unhydrated shell cannot immediately retry");
                    Ensure(ChatGptLiteForm.RecordWorkerProbeFailure(
                        shellGate, now.AddSeconds(100)) == TimeSpan.FromSeconds(120),
                        "third missing-history attempt doubles cooldown");
                    Ensure(ChatGptLiteForm.RecordWorkerProbeFailure(
                        shellGate, now.AddSeconds(225)) == TimeSpan.FromSeconds(240),
                        "fourth missing-history attempt increases cooldown");
                    Ensure(ChatGptLiteForm.RecordWorkerProbeFailure(
                        shellGate, now.AddSeconds(470)) == TimeSpan.FromSeconds(300),
                        "continued shell hydration failure is capped at five minutes");
                    Ensure(ChatGptLiteForm.ShouldDeferWorkerProbe(false, shellGate,
                        now.AddSeconds(700)),
                        "shared worker defers while missing-history cooldown is active");
                    Ensure(!ChatGptLiteForm.ShouldDeferWorkerProbe(true, shellGate,
                        now.AddSeconds(700)),
                        "foreground remains independent of missing-history failures");
                    shellGate.MarkNavigationRecovered();
                    Ensure(!ChatGptLiteForm.ShouldDeferWorkerProbe(false, shellGate,
                        now.AddSeconds(701)),
                        "verified background history restores normal scheduler");
                    phase = "multi_chat_fair_due_selection";
                    var fairnessNow = DateTimeOffset.UtcNow;
                    string fairFg = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
                    var fairChats = Enumerable.Range(1, 5).Select(i =>
                        new LiteChatWakeRegistry.Chat {
                            Url = i == 1 ? fairFg :
                                "https://chatgpt.com/c/" + i.ToString().PadLeft(8, '0') +
                                "-2222-2222-2222-222222222222",
                            Enabled = true,
                            DueUtc = fairnessNow.AddMinutes(i == 1 ? -1 : i - 11)
                        }).ToArray();
                    // Five independent schedules: oldest background must win
                    // when foreground is blocked for many wake ticks.
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) == fairChats[1],
                        "oldest background wake cannot starve under due foreground");
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        null) == fairChats[1], "no visible chat selects oldest due");
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        "https://chatgpt.com/") == fairChats[1],
                        "application home route selects oldest due");
                    fairChats[0].DueUtc = fairChats[1].DueUtc.AddSeconds(59);
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) == fairChats[0],
                        "foreground gets priority for nearly equal due times");
                    fairChats[0].DueUtc = fairChats[1].DueUtc.AddMinutes(1);
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) == fairChats[0],
                        "foreground preference ends after one-minute tolerance");
                    fairChats[0].DueUtc = fairChats[1].DueUtc.AddSeconds(61);
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) == fairChats[1],
                        "more overdue background still wins outside tolerance");
                    fairChats[0].DueUtc = fairnessNow.AddMinutes(-1);
                    fairChats[1].Enabled = false;
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) == fairChats[2],
                        "paused old background chat is never selected");
                    fairChats[2].DueUtc = fairnessNow.AddMinutes(2);
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) == fairChats[3],
                        "not-yet-due background chat cannot preempt older due");
                    fairChats[3].Enabled = false;
                    fairChats[4].Enabled = false;
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) == fairChats[0],
                        "foreground wakes normally without overdue background");
                    fairChats[0].DueUtc = fairnessNow.AddMinutes(20);
                    Ensure(ChatGptLiteForm.SelectDueChat(fairChats, fairnessNow,
                        fairFg) is null, "no eligible due chat remains after reschedule");
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
                    phase = "single_host_dom_poll_updates_state";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const extra = document.createElement('div');
                          extra.id='qa-new-turn';
                          extra.setAttribute('data-chatgpt-selection-message-id','qa-new-turn');
                          extra.textContent='Historical assistant turn added after document load';
                          document.querySelector('main').appendChild(extra);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().turnCount") == "4",
                        "read-only status remains stale until host checks");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.check().turnCount") == "5",
                        "host-driven check sees new DIL assistant without a JS timer");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().optimized") == "0",
                        "host-driven check does not mutate DIL by default");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('#qa-new-turn').remove()");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.check().turnCount") == "4",
                        "host-driven check reflects removed temporary turn");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().domVariant") == "dil_observe_only",
                        "single host poll preserves conservative modern DOM behavior");
                    phase = "check_last_role";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.role") == "assistant",
                        "DIL layout recognizes last assistant");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.text"))
                        .Contains("Czarny kompas"),
                        "read last DIL assistant content");
                    phase = "single_pass_resume_history_metadata";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().assistantNodeCount === " +
                        "document.querySelectorAll('main [data-chatgpt-selection-message-id]').length") == "true",
                        "single metadata pass preserves true DIL assistant count");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().userNodeCount === " +
                        "document.querySelectorAll('main [data-user-message-bubble]').length") == "true",
                        "single metadata pass preserves true DIL user count");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().lastAssistantChars === " +
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().text.length") == "true",
                        "newest DIL assistant char count matches the visible completed answer");
                    phase = "active_main_only_preflight_and_linear_dom_order";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const aside=document.querySelector('aside');
                          aside.setAttribute('data-chatgpt-selection-message-id','qa-sidebar-answer');
                          aside.textContent='UNTRUSTED SIDEBAR PREVIEW: do not wake from this';
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().role") == "assistant",
                        "sidebar reply marker does not replace main assistant role");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().text"))
                        .Contains("Czarny kompas"),
                        "sidebar content never replaces final main response");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().turnCount") == "4",
                        "sidebar marker excluded from active conversation turn count");
                    phase = "dom_optimization_sidebar_isolation";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.check().turnCount") == "4",
                        "DOM optimizer counts only the active main, not sidebar preview");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().domVariant") == "dil_observe_only",
                        "sidebar fake assistant cannot change conservative DIL mode");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelector('aside').hasAttribute('data-ob-lite-offscreen')") == "false",
                        "sidebar is never tagged for content-visibility optimization");
                    phase = "sidebar_tool_and_error_fail_closed";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const sidebar=document.querySelector('aside');
                          sidebar.setAttribute('data-testid','tool-running');
                          sidebar.setAttribute('role','alert');
                          sidebar.textContent='Failed to load this response';
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().toolBusy") == "false",
                        "sidebar tool-running status cannot delay current chat");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESPONSE_SIGNALS__.read().visibleError === null") == "true",
                        "sidebar false error alert cannot block current chat");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().error") == "false",
                        "scoped response signal does not leak sidebar error to scheduler");
                    phase = "sidebar_stop_button_isolation";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const stop=document.createElement('button');
                          stop.id='qa-sidebar-stop';
                          stop.dataset.testid='stop-button';
                          stop.textContent='Stop generating';
                          document.querySelector('aside').appendChild(stop);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.check().skippedBusy") == "false",
                        "sidebar Stop cannot suppress DOM optimization monitoring");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().generating") == "false",
                        "sidebar stop cannot misidentify active chat as generating");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESPONSE_SIGNALS__.read().generatingSignal") == "false",
                        "sidebar stop cannot fake centralized generation status");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.diagnose().reason") == "ready",
                        "sender does not block a wake due to a sidebar Stop button");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const stop=document.createElement('button');
                          stop.id='qa-main-stop';
                          stop.dataset.testid='stop-button';
                          stop.textContent='Stop generating';
                          document.querySelector('main').appendChild(stop);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.check().skippedBusy") == "true",
                        "active chat Stop blocks DOM CSS mutations even with sidebar Stop first");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().generating") == "true",
                        "active chat Stop still blocks preflight while sidebar Stop exists");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESPONSE_SIGNALS__.read().generatingSignal") == "true",
                        "active Stop is reported in centralized response status");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.diagnose().reason") == "generating",
                        "sender refuses to interrupt actual active generation");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          document.querySelector('#qa-main-stop').remove();
                          document.querySelector('#qa-sidebar-stop').remove();
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.check().skippedBusy") == "false",
                        "after Stop disappears the existing DOM monitor resumes normally");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          document.querySelector('aside').removeAttribute('role');
                          document.querySelector('aside').removeAttribute('data-testid');
                          const marker=document.createElement('span');
                          marker.id='qa-current-tool-running';
                          marker.dataset.testid='tool-running';
                          document.querySelector('main').appendChild(marker);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().toolBusy") == "true",
                        "active chat tool-running must still block auto-wake");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('#qa-current-tool-running').remove()");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().toolBusy") == "false",
                        "active chat tool finishing lifts busy flag");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          window.__qaMain=main;
                          window.__qaMainParent=main.parentNode;
                          window.__qaNext=main.nextSibling;
                          main.remove();
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().role") == "unknown",
                        "no hydrated main means unknown response, never sidebar fallback");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().turnCount") == "0",
                        "no active main produces zero observed turns");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.check().domVariant") == "unhydrated",
                        "missing main cannot trigger legacy DOM optimization");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().optimized") == "0",
                        "missing main has zero marked offscreen turns");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          window.__qaMainParent.insertBefore(window.__qaMain,window.__qaNext);
                          document.querySelector('aside')
                            .removeAttribute('data-chatgpt-selection-message-id');
                          delete window.__qaMain;
                          delete window.__qaMainParent;
                          delete window.__qaNext;
                        })()
                        """);
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

                    phase = "worker_navigation_during_async_hydration";
                    var heldProbe = new TaskCompletionSource<string>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    var inFlight = ChatGptLiteForm.VerifyWorkerHydrationAsync(
                        chatUrl, chatUrl, () => core.Source, () => heldProbe.Task);
                    Ensure(!inFlight.IsCompleted,
                        "hydration probe remains pending before route change");
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/22222222-2222-2222-2222-222222222222')");
                    heldProbe.SetResult("true");
                    Ensure(!await inFlight,
                        "true DOM probe cannot certify a URL that changed while awaiting");
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/11111111-1111-1111-1111-111111111111')");
                    string? activeVerifiedUrl = chatUrl;
                    var roundTripProbe = new TaskCompletionSource<string>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    var roundTrip = ChatGptLiteForm.VerifyWorkerHydrationAsync(
                        chatUrl, chatUrl, () => core.Source,
                        () => roundTripProbe.Task, () => activeVerifiedUrl);
                    Ensure(!roundTrip.IsCompleted,
                        "same-chat second hydration must await its probe");
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/22222222-2222-2222-2222-222222222222')");
                    activeVerifiedUrl = null; // production SourceChanged invalidates it
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/11111111-1111-1111-1111-111111111111')");
                    roundTripProbe.SetResult("true");
                    Ensure(!await roundTrip,
                        "SPA A-B-A must not restore revoked verified worker identity");
                    Ensure(await ChatGptLiteForm.VerifyWorkerHydrationAsync(
                        chatUrl, chatUrl, () => core.Source,
                        () => Task.FromResult("true"), () => chatUrl),
                        "unchanged verified chat remains reusable");
                    Ensure(!await ChatGptLiteForm.VerifyWorkerHydrationAsync(
                        chatUrl, chatUrl, () => core.Source,
                        () => Task.FromResult("false"), () => chatUrl),
                        "empty DOM still fails without route changes");
                    int invalidProbeCount = 0;
                    Ensure(!await ChatGptLiteForm.VerifyWorkerHydrationAsync(
                        otherUrl, otherUrl, () => core.Source, () => {
                            invalidProbeCount++;
                            return Task.FromResult("true");
                        }) && invalidProbeCount == 0,
                        "mismatching source rejects hydration without running JS");
                    phase = "worker_navigation_epoch_aba_race";
                    long testRouteEpoch = 7;
                    var epochProbe = new TaskCompletionSource<string>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    var awaitingEpoch = ChatGptLiteForm.VerifyWorkerHydrationAsync(
                        chatUrl, chatUrl, () => core.Source, () => epochProbe.Task,
                        () => chatUrl, () => testRouteEpoch);
                    Ensure(!awaitingEpoch.IsCompleted,
                        "in-flight hydration records current navigation epoch");
                    testRouteEpoch += 2; // SPA A -> B -> A; URL now unchanged
                    epochProbe.SetResult("true");
                    Ensure(!await awaitingEpoch,
                        "same URL after A-B-A cannot accept an old DOM result");
                    Ensure(!ChatGptLiteForm.IsWorkerRouteStable(
                        chatUrl, core.Source, 7, testRouteEpoch),
                        "route epoch invalidates reused result despite equal source");
                    Ensure(ChatGptLiteForm.IsWorkerRouteStable(
                        chatUrl, core.Source, testRouteEpoch, testRouteEpoch),
                        "unchanged navigation epoch preserves successful hydration");
                    Ensure(await ChatGptLiteForm.VerifyWorkerHydrationAsync(
                        chatUrl, chatUrl, () => core.Source,
                        () => Task.FromResult("true"), () => chatUrl, () => testRouteEpoch),
                        "verified source with stable epoch remains reusable");
                    phase = "worker_reuse_same_chat_isolated";
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
                        JsonSerializer.Serialize("Napisz Ăłsmy rozdziaĹ‚ tej testowej ksiÄ…ĹĽki.") +
                        "," + JsonSerializer.Serialize(receipt) + ")";
                    phase = "send_test_message";
                    // Encoded HTML entity survives editors with the wrong Windows codepage.
                    // The rendered aria-label must match the sender's exact Polish selector.
                    Ensure(FixtureHtml.Contains("aria-label=\"Wy&#347;lij\"") &&
                        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                            "LiteResumeSender.js")).Contains("aria-label=\"Wy\u015blij\""),
                        "fixture Send button label matches the real JS sender selector");
                    var state = await EvaluateAsync(core, js);
                    Ensure(state == "submission_requested", "one submission requested");
                    phase = "wait_and_check_ack";
                    // A hidden isolated WebView2 can throttle JS setTimeout(250).
                    // Observe for a bounded time; never submit the message twice.
                    bool ack = false;
                    for (int attempt = 0; attempt < 16 && !ack; attempt++)
                    {
                        await Task.Delay(400);
                        ack = await EvaluateAsync(core,
                            "window.__OPENBROWSER_RESUME_SENDER__?.hasAck('" + receipt + "')") == "true";
                    }
                    if (!ack) {
                        var senderPhase = await EvaluateAsync(core,
                            "window.__OPENBROWSER_RESUME_SENDER__.phase('" + receipt + "')");
                        var senderDiagnosis = await EvaluateAsync(core,
                            "JSON.stringify(window.__OPENBROWSER_RESUME_SENDER__.diagnose())");
                        LiteEventLog.Write("webview_sender_ack_diagnostic", new {
                            phase = senderPhase, diagnosis = senderDiagnosis, offline = true
                        });
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
                    phase = "minimal_numbered_wake_receipt";
                    const string minimalReceipt = "abcdef0123456789abcdef0123456789";
                    const string minimalWake = "[OB-WAKE-000208]\nKontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.sendDirect(" +
                        JsonSerializer.Serialize(minimalWake) + "," +
                        JsonSerializer.Serialize(minimalReceipt) + ",true," +
                        JsonSerializer.Serialize("/c/11111111-1111-1111-1111-111111111111") + ")") ==
                        "submission_requested", "minimal two-line OBL wake can be staged");
                    string beforeEditor = await EvaluateAsync(core,
                        "(() => {let e=document.querySelector('#prompt-textarea');" +
                        "let t=e?.value||e?.innerText||e?.textContent||'';" +
                        "return JSON.stringify({len:t.length,marker:t.includes('[OB-WAKE-000208]')});})()");
                    await Task.Delay(500);
                    string afterEditor = await EvaluateAsync(core,
                        "(() => {let e=document.querySelector('#prompt-textarea');" +
                        "let t=e?.value||e?.innerText||e?.textContent||'';" +
                        "let exp=" + JsonSerializer.Serialize(minimalWake) + ";" +
                        "let a=t.replace(/\\s+/g,' ').trim(),b=exp.replace(/\\s+/g,' ').trim();" +
                        "let i=0;while(i<Math.min(a.length,b.length)&&a[i]===b[i])i++;" +
                        "return JSON.stringify({len:t.length,marker:t.includes('[OB-WAKE-000208]')," +
                        "equal:a===b,normLen:a.length,expectedLen:b.length,firstDiff:i," +
                        "actualCode:a.charCodeAt(i),expectedCode:b.charCodeAt(i)});})()");
                    string minimalClick = await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.submitDirect(" +
                        JsonSerializer.Serialize(minimalReceipt) + ")");
                    Ensure(minimalClick is "click_invoked" or "form_submitted",
                        "minimal wake click actually invoked: " + minimalClick +
                        "; before=" + beforeEditor + "; after=" + afterEditor);
                    bool minimalAck = false;
                    for (int i=0; i<16 && !minimalAck; i++)
                    {
                        await Task.Delay(250);
                        minimalAck = await EvaluateAsync(core,
                            "window.__OPENBROWSER_RESUME_SENDER__.hasAck(" +
                            JsonSerializer.Serialize(minimalReceipt) + ")") == "true";
                    }
                    Ensure(minimalAck, "internal receipt correlates visible numeric wake marker");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('[data-user-message-bubble]').length") == "4",
                        "exactly one minimal numbered wake bubble");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelector('main').innerText.includes(" +
                        JsonSerializer.Serialize("[OB-" + minimalReceipt + "]") + ")") == "false",
                        "receipt never appears in human-visible wake message");
                    phase = "detect_and_clear_visible_failure";
                    await core.ExecuteScriptAsync("(() => {const n=document.createElement('div'); " +
                        "n.id='qa-error';n.setAttribute('data-chatgpt-selection-message-id','failure');" +
                        "n.innerText='Failed to load this response';" +
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
                    // Archive capture must not label ongoing output FINAL when
                    // the Stop button is temporarily absent from a real WebView2.
                    phase = "archive_snapshot_aria_busy_partial_guard";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          const node=document.createElement('div');
                          node.setAttribute('data-chatgpt-selection-message-id','qa-streaming');
                          node.textContent='Streaming durable assistant text for recovery';
                          main.appendChild(node);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().some(x=>" +
                        "x.turnId==='qa-streaming' && !x.partial)") == "true",
                        "a completed assistant is archived FINAL without busy markers");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          const shortAnswer=document.createElement('div');
                          shortAnswer.setAttribute('data-chatgpt-selection-message-id','qa-new-short');
                          shortAnswer.textContent='abc';
                          main.appendChild(shortAnswer);
                          main.setAttribute('aria-busy','true');
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().length") == "0",
                        "short newest assistant must not relabel previous FINAL as PARTIAL");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          document.querySelector('[data-chatgpt-selection-message-id="qa-new-short"]')
                            .textContent='New answer has now accumulated enough text';
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "JSON.stringify(window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive()" +
                        ".map(x=>[x.turnId,x.partial]))") ==
                        "[[\"qa-new-short\",true]]",
                        "longer newest assistant alone becomes PARTIAL");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          main.removeAttribute('aria-busy');
                          main.querySelector('[data-chatgpt-selection-message-id="qa-new-short"]').remove();
                        })()
                        """);
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const user=document.createElement('div');
                          user.id='qa-user-with-selection-id';
                          user.setAttribute('data-message-author-role','user');
                          const nested=document.createElement('div');
                          nested.setAttribute('data-chatgpt-selection-message-id','qa-user-id');
                          nested.textContent='A user question that must never be archived as assistant';
                          user.appendChild(nested);
                          document.querySelector('main').appendChild(user);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().some(x=>" +
                        "x.turnId==='qa-user-id')") == "false",
                        "a nested user selection ID is never archived as assistant FINAL");
                    await core.ExecuteScriptAsync("document.getElementById('qa-user-with-selection-id').remove()");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('main').setAttribute('aria-busy','true')");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().some(x=>" +
                        "x.turnId==='qa-streaming' && x.partial)") == "true",
                        "aria-busy main yields PARTIAL without Stop button");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          main.removeAttribute('aria-busy');
                          main.querySelector('[data-chatgpt-selection-message-id="qa-streaming"]')
                              .setAttribute('aria-busy','true');
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().some(x=>" +
                        "x.turnId==='qa-streaming' && x.partial)") == "true",
                        "descendant aria-busy yields PARTIAL without Stop button");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          main.querySelector('[data-chatgpt-selection-message-id="qa-streaming"]')
                            .removeAttribute('aria-busy');
                          const tool=document.createElement('div');
                          tool.id='qa-active-tool';
                          tool.setAttribute('data-state','running');
                          main.appendChild(tool);
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().some(x=>" +
                        "x.turnId==='qa-streaming' && x.partial)") == "true",
                        "running tool state yields PARTIAL without aria-busy or Stop");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const tool=document.getElementById('qa-active-tool');
                          tool.removeAttribute('data-state');
                          tool.setAttribute('data-testid','qa-tool-running');
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().some(x=>" +
                        "x.turnId==='qa-streaming' && x.partial)") == "true",
                        "running tool testid yields PARTIAL without Stop");
                    await core.ExecuteScriptAsync("document.getElementById('qa-active-tool').remove()");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().some(x=>" +
                        "x.turnId==='qa-streaming' && !x.partial)") == "true",
                        "completed tool allows FINAL after running marker disappears");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          const node=main.querySelector('[data-chatgpt-selection-message-id="qa-streaming"]');
                          node.removeAttribute('aria-busy');
                          const user=document.createElement('div');
                          user.id='qa-awaiting-assistant';
                          user.setAttribute('data-user-message-bubble','');
                          const nested=document.createElement('div');
                          nested.setAttribute('data-chatgpt-selection-message-id','qa-user-pending-id');
                          nested.textContent='Waiting for the next model answer';
                          user.appendChild(nested);
                          main.appendChild(user);
                          main.setAttribute('aria-busy','true');
                        })()
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_SNAPSHOT__.collectForArchive().length") == "0",
                        "generating with newest USER never mislabels prior assistant PARTIAL");
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          main.removeAttribute('aria-busy');
                          document.getElementById('qa-awaiting-assistant').remove();
                          main.querySelector('[data-chatgpt-selection-message-id="qa-streaming"]').remove();
                        })()
                        """);
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

                    // Real offline WebView2 check for the 10-turn bound, with a
                    // mandatory durable-archive acknowledgement before deletion.
                    phase = "hard_dom_owner_authorized_10_turns";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.enable(true).enabled") ==
                        "true", "owner can enable 10-turn hard pruning");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.prune().visibleTurns") ==
                        "10", "owner-authorized hard DOM pruning keeps ten newest");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.status().obsoleteDropped > 0") ==
                        "true", "obsolete older DOM is discarded without archive ACK");
                    await core.ExecuteScriptAsync("""
                        window.__OPENBROWSER_HARD_PRUNE__.ackSaved(
                          Array.from(document.querySelectorAll(
                            'main [data-chatgpt-selection-message-id]'))
                          .map(n => n.getAttribute('data-chatgpt-selection-message-id')))
                        """);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.prune().visibleTurns") ==
                        "10", "offline WebView2 leaves exactly ten messages after archive proof");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('main [data-user-message-bubble],main [data-chatgpt-selection-message-id]').length") ==
                        "10", "actual DOM contains ten retained messages");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelector('#prompt-textarea') !== null") ==
                        "true", "10-turn prune never removes composer");

                    // Real timer, NOT an explicit prune() call: create the 11th
                    // DIL message and time actual MutationObserver removal.
                    phase = "hard_dom_11_to_10_under_5s";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const main=document.querySelector('main');
                          const item=document.createElement('div');
                          item.setAttribute('data-chatgpt-selection-message-id',
                            'qa-automatic-eleventh');
                          item.textContent='Test automaticznej redukcji 11 do 10.';
                          window.__OBL_QA_PRUNE_START__=performance.now();
                          window.__OBL_QA_PRUNE_ELAPSED__=-1;
                          const watcher=new MutationObserver(() => {
                            const count=main.querySelectorAll(
                              '[data-user-message-bubble],[data-chatgpt-selection-message-id]').length;
                            if (count===10 && window.__OBL_QA_PRUNE_ELAPSED__<0) {
                              window.__OBL_QA_PRUNE_ELAPSED__=
                                performance.now()-window.__OBL_QA_PRUNE_START__;
                              watcher.disconnect();
                            }
                          });
                          watcher.observe(main,{childList:true,subtree:true});
                          main.appendChild(item);
                        })();
                        """);
                    double elapsedMs=-1;
                    for (int sample=0;sample<125;sample++)
                    {
                        await Task.Delay(50);
                        string elapsed=await EvaluateAsync(core,
                            "window.__OBL_QA_PRUNE_ELAPSED__");
                        if (double.TryParse(elapsed,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double measured) && measured>=0)
                        { elapsedMs=measured; break; }
                    }
                    string pruneResult=await EvaluateAsync(core,
                        "JSON.stringify(window.__OPENBROWSER_HARD_PRUNE__.status())");
                    LiteEventLog.Write("hard_dom_auto_prune_qa", new {
                        elapsedMs, targetMs=5000,
                        passed=elapsedMs>=0 && elapsedMs<=5000,
                        offline=true
                    });
                    Ensure(elapsedMs>=0 && elapsedMs<=5000,
                        "automatic 11->10 in actual WebView2 must finish within 5s; elapsedMs=" +
                        elapsedMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        "; status=" + pruneResult);
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HARD_PRUNE__.status().reinsertions") ==
                        "0", "automatic prune did not trigger React reinsertions");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelector('#prompt-textarea') !== null") ==
                        "true", "automatic pruning preserves the active composer");

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
                    phase = "recovered_draft_target_fence";
                    const string ownDraftReceipt = "0a1b2c3d4e5f60718293a4b5c6d7e8f9";
                    await core.ExecuteScriptAsync(
                        "document.querySelector('#prompt-textarea').innerText=" +
                        JsonSerializer.Serialize("[OB-" + ownDraftReceipt +
                            "]\nPrzywrĂłÄ‡ tylko testowÄ… rozmowÄ™ B."));
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.submitOwnDraft(" +
                        JsonSerializer.Serialize(new Uri(chatUrl).AbsolutePath) + ")") ==
                        "blocked_navigation",
                        "recovery for chat A must not click chat B's persisted OB draft");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelector('#prompt-textarea').innerText.includes('" +
                        ownDraftReceipt + "')") == "true",
                        "wrong target recovery leaves chat B draft untouched");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('main [data-user-message-bubble]').length") ==
                        "2", "wrong target recovery adds no user message");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('#prompt-textarea').innerText=''");
                    // Exercise the SAME host-managed sendDirect + submitDirect
                    // protocol as background waking, not only its URL guard.
                    phase = "background_second_chat_receipt_isolation";
                    const string receiptB = "fedcba0987654321fedcba0987654321";
                    const string receiptBlocked = "abcdef0123456789abcdef0123456789";
                    string payloadB = "[OB-" + receiptB +
                        "]\nKontynuuj wyĹ‚Ä…cznie drugÄ… testowÄ… rozmowÄ™.";
                    string directB = "window.__OPENBROWSER_RESUME_SENDER__.sendDirect(" +
                        JsonSerializer.Serialize(payloadB) + "," +
                        JsonSerializer.Serialize(receiptB) + ",true," +
                        JsonSerializer.Serialize(new Uri(otherUrl).AbsolutePath) + ")";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receipt + "')") ==
                        "false", "receipt from chat A is absent in chat B");
                    phase = "host_target_bound_before_draft_injection";
                    string badTargetJs = "window.__OPENBROWSER_RESUME_SENDER__.sendDirect(" +
                        JsonSerializer.Serialize(payloadB) + "," +
                        JsonSerializer.Serialize(receiptB) + ",true," +
                        JsonSerializer.Serialize(new Uri(chatUrl).AbsolutePath) + ")";
                    Ensure(await EvaluateAsync(core, badTargetJs) == "blocked_navigation",
                        "host preflight for chat A cannot write into navigated chat B");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelector('#prompt-textarea').innerText") == "",
                        "wrong target leaves real WebView2 composer untouched");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.diagnose().pendingReceipts") == "0",
                        "wrong target does not retain a pending message body");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('main [data-user-message-bubble]').length") == "2",
                        "wrong target cannot insert a new user bubble");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('form[data-chatgpt-composer]').dataset.qaDelayMs='1300'");
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
                        "false", "delayed React user bubble is not immediate");
                    Ensure(await ChatGptLiteForm.WaitForWakeReceiptAsync(
                        core, receiptB, otherUrl, () => 71L),
                        "host bounded receipt wait observes delayed real user bubble in selected chat");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.hasAck('" + receiptB + "')") ==
                        "true", "second chat has a real unique user-bubble receipt");
                    phase = "receipt_ack_exact_route_and_epoch";
                    Ensure(!await ChatGptLiteForm.WaitForWakeReceiptAsync(
                        core, receiptB, chatUrl, () => 71L),
                        "real user receipt in chat B cannot ACK an attempted send to chat A");
                    Ensure(!await ChatGptLiteForm.WaitForWakeReceiptAsync(
                        core, receiptB, otherUrl),
                        "missing route epoch is rejected instead of bypassing identity guard");
                    Ensure(ChatGptLiteForm.IsWakeReceiptRouteStable(
                        otherUrl, otherUrl, 71, 71),
                        "stable selected URL and epoch permit actual receipt inspection");
                    Ensure(!ChatGptLiteForm.IsWakeReceiptRouteStable(
                        otherUrl, chatUrl, 71, 71),
                        "different conversation cannot confirm selected chat receipt");
                    Ensure(!ChatGptLiteForm.IsWakeReceiptRouteStable(
                        otherUrl, otherUrl, 71, 73),
                        "A-to-B-to-A navigation rejects ACK despite identical final URL");
                    Ensure(!ChatGptLiteForm.IsWakeReceiptRouteStable(
                        otherUrl, null, 71, 71),
                        "missing WebView2 source fails closed for receipt checks");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('main [data-user-message-bubble]').length") ==
                        "3", "delayed ACK polling never triggers duplicate submission");
                    Ensure(!await ChatGptLiteForm.WaitForWakeReceiptAsync(core, "not-receipt"),
                        "invalid receipt rejected without touching the renderer");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__.diagnose().pendingReceipts") ==
                        "0", "clicked background wake releases the full pending payload");
                    Ensure(await EvaluateAsync(core, directB) == "already_visible",
                        "second chat receipt cannot be sent twice");
                    string blockedPayload = "[OB-" + receiptBlocked +
                        "]\nNie wysyĹ‚aj do rozmowy po zmianie adresu.";
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

                    phase = "worker_archive_timeout_retry_gate";
                    Ensure(ChatGptLiteForm.IsWorkerArchiveRetryAllowed(0),
                        "worker archive accepts first probe after route change");
                    Ensure(ChatGptLiteForm.IsWorkerArchiveRetryAllowed(1),
                        "worker archive allows one retry after stalled script");
                    Ensure(!ChatGptLiteForm.IsWorkerArchiveRetryAllowed(2),
                        "worker archive caps two unresolved WebView calls");
                    Ensure(!ChatGptLiteForm.IsWorkerArchiveRetryAllowed(3),
                        "worker archive cannot accumulate a third unresolved script");
                    Ensure(ChatGptLiteForm.IsWorkerArchivePendingForEpoch(
                        true, 10, 10),
                        "same-route capture remains exclusively pending");
                    Ensure(!ChatGptLiteForm.IsWorkerArchivePendingForEpoch(
                        true, 10, 11),
                        "old worker route pending cannot block successor chat");
                    Ensure(!ChatGptLiteForm.IsWorkerArchivePendingForEpoch(
                        false, 10, 10),
                        "completed worker archive is not marked pending");
                    phase = "archive_cross_navigation_fence";
                    var oldCapture = new TaskCompletionSource<string>(
                        TaskCreationOptions.RunContinuationsAsynchronously).Task;
                    var newCapture = new TaskCompletionSource<string>(
                        TaskCreationOptions.RunContinuationsAsynchronously).Task;
                    Ensure(ChatGptLiteForm.IsArchiveCaptureLeaseCurrent(
                        10, 10, oldCapture, oldCapture),
                        "current document owns its timed-out capture lease");
                    Ensure(!ChatGptLiteForm.IsArchiveCaptureLeaseCurrent(
                        10, 11, oldCapture, newCapture),
                        "stale page timeout cannot exhaust successor retry budget");
                    Ensure(!ChatGptLiteForm.IsArchiveCaptureLeaseCurrent(
                        10, 10, oldCapture, newCapture),
                        "stale timeout cannot clear another in-flight capture");
                    Ensure(!ChatGptLiteForm.IsArchiveCaptureLeaseCurrent(
                        10, 10, oldCapture, null),
                        "already reset capture slot cannot be claimed by stale timeout");
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
                    phase = "conversation_load_failed_full_probe";
                    Ensure(await EvaluateAsync(core,
                        "(() => { const m = document.querySelector('main'); " +
                        "m.textContent = 'Failed to load this conversation'; " +
                        "return m.textContent; })()") ==
                        "Failed to load this conversation",
                        "isolated broken-conversation shell is visible in main");
                    var probeOptions = new JsonSerializerOptions {
                        PropertyNameCaseInsensitive = true
                    };
                    string failedProbeJson = await EvaluateAsync(core,
                        "JSON.stringify(window.__OPENBROWSER_RESUME_PROBE__.observe())");
                    var failedProbeA = JsonSerializer.Deserialize<LiteChatWakePreflight.Observation>(
                        failedProbeJson, probeOptions);
                    Ensure(failedProbeA is not null && failedProbeA.Error &&
                        failedProbeA.FailureKind == "conversation_load_failed" &&
                        failedProbeA.TurnCount == 0 && !failedProbeA.HasDraft,
                        "real WebView2 JS exposes empty failed-conversation kind");
                    await Task.Delay(100);
                    string failedProbeJsonB = await EvaluateAsync(core,
                        "JSON.stringify(window.__OPENBROWSER_RESUME_PROBE__.observe())");
                    var failedProbeB = JsonSerializer.Deserialize<LiteChatWakePreflight.Observation>(
                        failedProbeJsonB, probeOptions);
                    Ensure(failedProbeB is not null && failedProbeB.Error &&
                        failedProbeB.FailureKind == "conversation_load_failed" &&
                        failedProbeB.TurnCount == 0 && !failedProbeB.HasDraft,
                        "real WebView2 second observation preserves failure kind");
                    string failedPath = new Uri(chatUrl).AbsolutePath;
                    Ensure(LiteChatWakePreflight.Eligibility(failedProbeA, failedProbeB,
                        failedPath, "") == "chatgpt_app_problem",
                        "real WebView2 failure drives emergency rotation eligibility");
                    failedProbeB!.HasDraft = true;
                    Ensure(LiteChatWakePreflight.Eligibility(failedProbeA, failedProbeB,
                        failedPath, "") == "response_error",
                        "cross-layer guard prevents rotation when a draft exists");
                    LiteEventLog.Write("webview_integration_selftest", new {
                        passed = true, checks = 242, offline = true
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
WysiĹ‚ek myĹ›lowy</button>
<aside aria-busy="true" style="display:none">Background spinner, not a response</aside>
<main id="conversation">
<div data-user-message-bubble>Napisz ksiÄ…ĹĽkÄ™ przygodowÄ….</div>
<div data-chatgpt-selection-message-id="chapter1">RozdziaĹ‚ pierwszy.</div>
<div data-user-message-bubble>Kontynuuj przygodÄ™.</div>
<div data-chatgpt-selection-message-id="chapter7">RozdziaĹ‚ 7. Czarny kompas.
ZostaĹ‚y wÄ…tki do rozwiniÄ™cia.</div>
</main>
<form data-chatgpt-composer>
<div id="prompt-textarea" contenteditable="true" aria-label="Napisz wiadomoĹ›Ä‡"></div>
<button type="submit" aria-label="Wy&#347;lij">Wy&#347;lij</button>
<button type="button" aria-label="Tryb gĹ‚osowy">MĂłw</button>
</form>
<script>
document.querySelector('form[data-chatgpt-composer]').addEventListener('submit',(e)=>{
 e.preventDefault();
 const editor=document.querySelector('#prompt-textarea');
 const item=document.createElement('div');
 item.setAttribute('data-user-message-bubble','');
 item.textContent=editor.innerText;
 editor.innerText='';
 const delay=Number(e.currentTarget.dataset.qaDelayMs||0);
 if (delay>0) setTimeout(()=>document.querySelector('main').appendChild(item),delay);
 else document.querySelector('main').appendChild(item);
});
</script>
</body></html>
""";
}

