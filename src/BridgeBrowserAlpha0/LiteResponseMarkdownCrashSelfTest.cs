using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

// An isolated, real-process crash at the exact production atomic-rename/JSONL
// boundary. No WebView, active project, browser profile, or LIVE PID is touched.
internal static class LiteResponseMarkdownCrashSelfTest
{
    private const string Url = "https://chatgpt.com/c/cccccccc-cccc-cccc-cccc-cccccccccccc";
    private const string Turn = "crash-test-long-stream";
    private const string Partial1 = "First durable fragment of ongoing answer";
    private const string Partial2 = "Second durable fragment of ongoing answer";
    private const string Final = "Final durable answer before index append";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    internal static int RunChild(string root, string marker)
    {
        if (!Path.GetFullPath(root).StartsWith(
                Path.Combine(Path.GetTempPath(), "obl-md-crash-"),
                StringComparison.OrdinalIgnoreCase))
            return 91;
        var store = new LiteResponseMarkdownArchive(root);
        if (!store.Save(Url, Turn, Partial1, true)) return 92;
        if (!store.Save(Url, Turn, Partial2, true)) return 93;
        LiteResponseMarkdownArchive.CrashTestAfterAtomicRename = () =>
        {
            // The signal occurs only after the FINAL file is closed, flushed
            // and atomically renamed, but BEFORE its JSONL row is appended.
            File.WriteAllText(marker, Environment.ProcessId.ToString(),
                new UTF8Encoding(false));
            Thread.Sleep(Timeout.Infinite);
        };
        store.Save(Url, Turn, Final, false);
        return 94; // The parent must kill this child at the signal boundary.
    }

    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "obl-md-crash-" + Guid.NewGuid().ToString("N"));
        string marker = Path.Combine(root, "after-rename.pid");
        Directory.CreateDirectory(root);
        bool passed = false;
        using var child = new Process();
        try
        {
            child.StartInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ??
                    throw new InvalidOperationException("Missing process executable"),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            child.StartInfo.ArgumentList.Add("--lite-markdown-crash-child");
            child.StartInfo.ArgumentList.Add(root);
            child.StartInfo.ArgumentList.Add(marker);
            if (!child.Start()) return 101;
            var watch = Stopwatch.StartNew();
            while (!File.Exists(marker) && watch.Elapsed < TimeSpan.FromSeconds(15))
            {
                if (child.HasExited) return 102;
                Thread.Sleep(50);
            }
            if (!File.Exists(marker)) return 103;
            if (!int.TryParse(File.ReadAllText(marker), out int markerPid) ||
                markerPid != child.Id) return 104;
            if (child.HasExited) return 105;
            // Kill exactly the child process we spawned; never a process-name
            // pattern or the production GUI. No regular graceful exit occurs.
            child.Kill();
            if (!child.WaitForExit(10000)) return 106;

            string directory = Path.Combine(root, Hash(Url));
            string index = Path.Combine(directory, "index.jsonl");
            string[] before = File.ReadAllLines(index);
            if (before.Length != 2) return 107;
            if (before.Count(x => JsonDocument.Parse(x).RootElement
                    .GetProperty("status").GetString() == "PARTIAL") != 2)
                return 108;

            string turnKey = Hash(Turn)[..16];
            string digest = Hash(Final);
            string immutable = Path.Combine(directory,
                turnKey + "_" + digest[..16] + ".md");
            if (!File.Exists(immutable) ||
                File.ReadAllText(immutable, Encoding.UTF8) != Final + "\n")
                return 109;

            var restarted = new LiteResponseMarkdownArchive(root);
            // Before recovery, the orphaned final file must NOT justify the
            // ACK used to prune the only renderer copy.
            if (restarted.IsPersisted(Url, Turn, Final)) return 110;
            if (restarted.RecoverMissingIndex(Url) != 1) return 111;
            if (!restarted.IsPersisted(Url, Turn, Final)) return 112;
            string[] after = File.ReadAllLines(index);
            if (after.Length != 3) return 113;
            if (after.Count(x => JsonDocument.Parse(x).RootElement
                    .GetProperty("status").GetString() == "FINAL") != 1)
                return 114;
            if (restarted.RecoverMissingIndex(Url) != 0) return 115;
            if (restarted.Save(Url, Turn, Final, false)) return 116;
            if (File.ReadAllLines(index).Length != 3) return 117;
            passed = true;
            LiteEventLog.Write("markdown_process_crash_selftest",
                new { passed = true, childPid = child.Id, partials = 2,
                    recoveredFinals = 1, noDuplicate = true, isolated = true });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("markdown_process_crash_selftest",
                new { passed = false, errorType = ex.GetType().Name,
                    message = ex.Message, root });
            return 118;
        }
        finally
        {
            if (child.Id != 0 && !child.HasExited)
            {
                child.Kill();
                child.WaitForExit(10000);
            }
            if (passed) Directory.Delete(root, recursive: true);
        }
    }
}
