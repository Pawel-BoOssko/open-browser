using System;
using System.Windows.Forms;

namespace BridgeBrowserAlpha0;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--lite-md-archive-crash-selftest")
        {
            Environment.ExitCode = LiteResponseMarkdownArchive.ProcessCrashRecoverySelfTest();
            return;
        }
        if (args.Length == 2 && args[0] == "--lite-md-archive-crash-child")
        {
            Environment.ExitCode = LiteResponseMarkdownArchive.CrashChild(args[1]);
            return;
        }
        if (args.Length == 1 && args[0] == "--lite-successor-composer-probe")
        {
            Environment.ExitCode = LiteSuccessorComposerProbe.Run();
            return;
        }
        if (args.Length > 0 && args[0] == "--lite-handoff-generation-rollover")
        {
            Environment.ExitCode = LiteHandoffGenerationBootstrap.Run(args,true);
            return;
        }        if (args.Length > 0 && args[0] == "--lite-handoff-generation-prepare")
        {
            Environment.ExitCode = LiteHandoffGenerationBootstrap.Run(args);
            return;
        }        if (args.Length > 0 && args[0] == "--lite-handoff-prepare")
        {
            Environment.ExitCode = LiteHandoffBootstrap.Run(args);
            return;
        }
        if (args.Length == 3 && args[0] == "--lite-handoff-document-verify")
        {
            Environment.ExitCode = LiteHandoffEvidence.VerifyDocument(args[1], @"D:\projects\open-browser\docs", args[2], DateTimeOffset.UtcNow, out _, out _, out _) ? 0 : 5;
            return;
        }
        if (args.Length >= 3 && string.Equals(args[0],
            "--lite-chat-notes-append", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string url = LiteDevWakeState.NormalizeUrl(args[1]);
                var store = new LiteChatNotes();
                string key = LiteChatNotes.NormalizeKey(url);
                string current = store.Load(key);
                string addition = string.Join(" ", args.Skip(2));
                if (string.IsNullOrWhiteSpace(addition))
                    throw new ArgumentException("Empty note");
                store.Save(key, string.IsNullOrWhiteSpace(current)
                    ? addition : current + Environment.NewLine + addition);
                Environment.ExitCode = 0;
            }
            catch { Environment.ExitCode = 4; }
            return;
        }
        if (args.Length > 0 &&
            string.Equals(args[0],"--lite-project-wake",StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = LiteProjectWakeCli.Run(args);
            return;
        }
        if (args.Length == 1 && args[0] == "--lite-project-wake-selftest")
        {
            Environment.ExitCode = LiteProjectWakeCli.SelfTest();
            return;
        }
        if (args.Length == 1 && args[0] == "--lite-emergency-selftest")
        {
            Environment.ExitCode = LiteWakeEmergency.SelfTest();
            return;
        }
        if (args.Length == 1 && args[0] == "--lite-first-wake-proof-selftest")
        {
            Environment.ExitCode = LiteSuccessorFirstWakeProof.SelfTest();
            return;
        }        // Chat-specific schedules can be configured by the chat itself through
        // the local command interface, or by the operator in the app.
        if (args.Length > 0 && string.Equals(args[0],
            "--lite-chat-wake-selftest", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = LiteChatWakeRegistry.SelfTest();
            return;
        }
        if (args.Length == 2 && string.Equals(args[0],
            "--lite-chat-wake-owner-rearm-now", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string url = LiteDevWakeState.NormalizeUrl(args[1]);
                var chats = LiteChatWakeRegistry.Read();
                if (!LiteChatWakeRegistry.TryOwnerRearmNow(chats, url,
                    DateTimeOffset.UtcNow))
                {
                    LiteEventLog.Write("multi_wake_owner_rearm_refused",
                        new { reason = "unsafe_or_not_stale_confirmed_response" });
                    Environment.ExitCode = 6;
                    return;
                }
                LiteChatWakeRegistry.Write(chats);
                LiteEventLog.Write("multi_wake_owner_explicit_rearm",
                    new { reason = "new_owner_request", intervalMinutes =
                        chats.Single(c => c.Url == url).EffectiveMinutes });
                Environment.ExitCode = 0;
            }
            catch
            {
                Environment.ExitCode = 5;
            }
            return;
        }
        if (args.Length >= 2 && string.Equals(args[0],
            "--lite-chat-wake-now", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var url = LiteDevWakeState.NormalizeUrl(args[1]);
                var chats = LiteChatWakeRegistry.Read();
                var entry = chats.FirstOrDefault(x => x.Url == url && x.Enabled);
                if (entry is null) { Environment.ExitCode = 3; return; }
                entry.DueUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                entry.LastState = "manual_test_due";
                LiteChatWakeRegistry.Write(chats);
                Environment.ExitCode = 0;
            }
            catch { Environment.ExitCode = 4; }
            return;
        }
        if (args.Length >= 2 && string.Equals(args[0],
            "--lite-chat-wake-off", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string url = LiteDevWakeState.NormalizeUrl(args[1]);
                var chats = LiteChatWakeRegistry.Read();
                var entry = chats.FirstOrDefault(x => x.Url == url);
                if (entry is null) { Environment.ExitCode = 3; return; }
                entry.Enabled = false;
                entry.LastState = "paused";
                LiteChatWakeRegistry.Write(chats);
                Environment.ExitCode = 0;
            }
            catch { Environment.ExitCode = 4; }
            return;
        }
        if (args.Length >= 4 && string.Equals(args[0],
            "--lite-chat-wake-set", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!int.TryParse(args[2], out int minutes))
                    throw new ArgumentException("Invalid interval");
                var chats = LiteChatWakeRegistry.Read();
                LiteChatWakeRegistry.Configure(chats, args[1], minutes,
                    string.Join(" ", args.Skip(3)), DateTimeOffset.UtcNow);
                LiteChatWakeRegistry.Write(chats);
                LiteEventLog.Write("multi_wake_cli_armed", new { intervalMinutes = minutes });
                Environment.ExitCode = 0;
            }
            catch { Environment.ExitCode = 5; }
            return;
        }
        if (args.Length > 0 && string.Equals(args[0], "--lite-await-upgrade", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = LiteUpgradeHelper.Run(args);
            return;
        }
        if (args.Length >= 2 && string.Equals(args[0], "--lite-dev-wake-arm", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var current = LiteDevWakeState.ArmForChat(
                    LiteDevWakeState.Read(), args[1], DateTimeOffset.UtcNow);
                LiteDevWakeState.Write(current);
                LiteEventLog.Write("dev_wake_armed", new { intervalMinutes = 25, boundToOneChat = true });
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("dev_wake_arm_failed", new { errorType = ex.GetType().Name });
                Environment.ExitCode = 3;
            }
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-dev-wake-rearm", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var state = LiteDevWakeState.Read();
                if (!LiteDevWakeState.IsChatUrl(state.TargetUrl))
                {
                    Environment.ExitCode = 3;
                    return;
                }
                var now = DateTimeOffset.UtcNow;
                LiteDevWakeState.RecoverLegacyDefinitelyNotSent(state, now);
                state.Enabled = true;
                state.NextDueUtc = now.AddMinutes(2);
                if (!LiteDevWakeState.MayRetryUncertain(state))
                    state.LastState = "armed";
                LiteDevWakeState.Write(state);
                LiteEventLog.Write("dev_wake_rearmed", new {
                    intervalMinutes = state.IntervalMinutes, firstCheckMinutes = 2
                });
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("dev_wake_rearm_failed", new {
                    errorType = ex.GetType().Name
                });
                Environment.ExitCode = 4;
            }
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-dev-wake-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteDevWakeState.SelfTest();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-version-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteBuildInfo.SelfTest();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-resume-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteResumeSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-luna-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteLunaSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-luna-connectivity-test", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteLunaConnectivitySelfTest.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-luna-novel-scenario", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteLunaNovelScenario.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-resume-ledger-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteResumeLedgerSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-project-wake-migrate", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var before = LiteChatWakeRegistry.Read();
                int count = before.Count;
                int enabled = before.Count(w => w.Enabled);
                LiteChatWakeRegistry.Write(before);
                var after = LiteProjectWakeStore.Read()
                    ?? throw new InvalidDataException("Project migration did not persist");
                if (after.Count != count || after.Count(w => w.Enabled) != enabled ||
                    before.Any(a => !after.Any(b => b.Url == a.Url &&
                        b.WakeSequence == a.WakeSequence &&
                        b.Enabled == a.Enabled && b.Prompt == a.Prompt &&
                        b.SlotAnchorUtc == a.SlotAnchorUtc)))
                    throw new InvalidDataException("Project migration lost a wake record");
                LiteEventLog.Write("project_wake_migrated",
                    new { retained = after.Count, enabled, source = "projects.json" });
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("project_wake_migration_failed",
                    new { errorType = ex.GetType().Name, errorCode = ex.HResult });
                Environment.ExitCode = 93;
            }
            return;
        }        if (args.Any(arg => string.Equals(arg, "--lite-projects-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-archive-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteArchiveSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-lease-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteLeadLeaseSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-journal-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteWorkJournalSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-notes-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteNotesSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-outbox-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteNotesOutboxSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-tabs-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteTabResidencySelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-transport-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteTransportSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-tab-policy-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteTabResidencySelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-delivery-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteDeliveryEvidenceSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-renderer-backpressure-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteRendererBackpressureSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-icon-export", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteBrandIcon.Export() ? 0 : 2;
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-icon-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteBrandIcon.SelfTest();
            return;
        }
        if (args.Length == 1 && args[0] == "--lite-markdown-partial-crash-selftest")
        {
            Environment.ExitCode = LiteResponseMarkdownPartialCrashSelfTest.Run();
            return;
        }
        if (args.Length == 3 && args[0] == "--lite-markdown-partial-crash-child")
        {
            Environment.ExitCode = LiteResponseMarkdownPartialCrashSelfTest.RunChild(args[1], args[2]);
            return;
        }
        if (args.Length == 1 && args[0] == "--lite-markdown-crash-selftest")
        {
            Environment.ExitCode = LiteResponseMarkdownCrashSelfTest.Run();
            return;
        }
        if (args.Length == 3 && args[0] == "--lite-markdown-crash-child")
        {
            Environment.ExitCode = LiteResponseMarkdownCrashSelfTest.RunChild(args[1], args[2]);
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-markdown-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteResponseMarkdownArchive.SelfTest();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-webview-integration-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationConfiguration.Initialize();
            Environment.ExitCode = LiteWebViewIntegrationSelfTest.Run();
            return;
        }
        if (args.Any(arg => string.Equals(arg, "--lite-dev-wake-send-now", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var state = LiteDevWakeState.Read();
                if (state.Enabled && LiteDevWakeState.IsChatUrl(state.TargetUrl))
                {
                    state.NextDueUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                    LiteDevWakeState.Write(state);
                }
            }
            catch { /* Startup must proceed even without local wake configuration. */ }
        }
        bool safeOpen = args.Any(arg => string.Equals(arg, "--lite-open-safely", StringComparison.OrdinalIgnoreCase));
        if (safeOpen && System.Diagnostics.Process.GetProcessesByName("BridgeBrowserAlpha0")
            .Any(process => process.Id != Environment.ProcessId && !process.HasExited))
            return; // Existing browser or pending upgrade: never start a second WebView2 profile.
        ApplicationConfiguration.Initialize();
        var lite = safeOpen || args.Any(arg => string.Equals(arg, "--chatgpt-lite", StringComparison.OrdinalIgnoreCase));
        Application.Run(lite ? new ChatGptLiteForm() : new MainForm());
    }
}
