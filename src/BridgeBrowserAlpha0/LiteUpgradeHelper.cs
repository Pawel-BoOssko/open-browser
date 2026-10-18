using System.Diagnostics;

namespace BridgeBrowserAlpha0;

/// <summary>
/// A detached, no-WebView, no-window promotion helper. Waits for the former
/// browser process to EXIT before starting the version hosting this helper.
/// Never terminates the user's browser or shares its live WebView2 profile.
/// </summary>
internal static class LiteUpgradeHelper
{
    internal static int Run(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int priorPid) ||
            priorPid <= 0 || priorPid == Environment.ProcessId)
            return 2;
        try
        {
            using var old = Process.GetProcessById(priorPid);
            old.WaitForExit();
        }
        catch (ArgumentException)
        {
            // Prior browser already exited; proceed.
        }
        catch (InvalidOperationException)
        {
            // Exit occurred before event registration.
        }
        Thread.Sleep(1800);
        // Do not launch a duplicate if the user already opened the successor.
        foreach (var proc in Process.GetProcessesByName("BridgeBrowserAlpha0"))
        {
            using (proc)
            {
                if (proc.Id == Environment.ProcessId) continue;
                try
                {
                    if (!proc.HasExited) return 0;
                }
                catch { return 3; }
            }
        }
        string exe = Environment.ProcessPath ??
            throw new InvalidOperationException("No executable path");
        Process.Start(new ProcessStartInfo(exe, "--chatgpt-lite")
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!
        });
        return 0;
    }
}
