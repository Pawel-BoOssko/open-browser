using System;
using System.Windows.Forms;

namespace BridgeBrowserAlpha0;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var lite = args.Any(arg => string.Equals(arg, "--chatgpt-lite", StringComparison.OrdinalIgnoreCase));
        Application.Run(lite ? new ChatGptLiteForm() : new MainForm());
    }
}
