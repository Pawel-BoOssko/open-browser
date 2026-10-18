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
        ApplicationConfiguration.Initialize();
        var lite = args.Any(arg => string.Equals(arg, "--chatgpt-lite", StringComparison.OrdinalIgnoreCase));
        Application.Run(lite ? new ChatGptLiteForm() : new MainForm());
    }
}
