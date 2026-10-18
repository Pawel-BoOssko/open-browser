using System;
using System.Windows.Forms;

namespace BridgeBrowserAlpha0;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
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
        if (args.Any(arg => string.Equals(arg, "--lite-icon-export", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = LiteBrandIcon.Export() ? 0 : 2;
            return;
        }
        ApplicationConfiguration.Initialize();
        var lite = args.Any(arg => string.Equals(arg, "--chatgpt-lite", StringComparison.OrdinalIgnoreCase));
        Application.Run(lite ? new ChatGptLiteForm() : new MainForm());
    }
}
