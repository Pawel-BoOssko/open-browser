using System;
using System.Windows.Forms;

namespace BridgeBrowserAlpha0;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--lite-await-upgrade", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = LiteUpgradeHelper.Run(args);
            return;
        }
        if (args.Length >= 2 && string.Equals(args[0], "--lite-dev-wake-arm", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var chat = LiteDevWakeState.NormalizeUrl(args[1]);
                var current = LiteDevWakeState.Read();
                if (!string.Equals(current.TargetUrl, chat, StringComparison.Ordinal))
                    current = new LiteDevWakeState.Record { TargetUrl = chat };
                current.Enabled = true;
                current.IntervalMinutes = 25;
                current.NextDueUtc = DateTimeOffset.UtcNow.AddMinutes(25);
                current.LastState = "armed";
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
        if (args.Any(arg => string.Equals(arg, "--lite-projects-selftest", StringComparison.OrdinalIgnoreCase)))
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
        if (args.Any(arg => string.Equals(arg, "--lite-webview-integration-selftest", StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationConfiguration.Initialize();
            Environment.ExitCode = LiteWebViewIntegrationSelfTest.Run();
            return;
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
