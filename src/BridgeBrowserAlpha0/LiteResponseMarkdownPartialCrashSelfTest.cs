using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

// Actual child-process termination while an assistant turn is PARTIAL.
// It never touches a live browser, profile, project, or real chat.
internal static class LiteResponseMarkdownPartialCrashSelfTest
{
    private const string Url = "https://chatgpt.com/c/abababab-abab-abab-abab-abababababab";
    private const string Turn = "streaming-answer";
    private const string First = "First durable partial of a long answer";
    private const string Second = "Second durable partial interrupted at rename";

    private static string Hash(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))
            .ToLowerInvariant();

    internal static int RunChild(string root, string signal)
    {
        string canonical = Path.GetFullPath(root);
        string fixturePrefix = Path.Combine(Path.GetTempPath(), "obl-md-partial-crash-");
        if (!canonical.StartsWith(fixturePrefix, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFullPath(signal) != Path.Combine(canonical, "after-partial-rename.pid"))
            return 201;
        var archive = new LiteResponseMarkdownArchive(root);
        if (!archive.Save(Url, Turn, First, true)) return 202;
        LiteResponseMarkdownArchive.CrashTestAfterAtomicRename = () =>
        {
            File.WriteAllText(signal, Environment.ProcessId.ToString(),
                new UTF8Encoding(false));
            Thread.Sleep(Timeout.Infinite);
        };
        archive.Save(Url, Turn, Second, true);
        return 203; // Must only be reachable if the simulated crash failed.
    }

    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "obl-md-partial-crash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string signal = Path.Combine(root, "after-partial-rename.pid");
        using var child = new Process();
        bool passed = false;
        try
        {
            child.StartInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ??
                    throw new InvalidOperationException("No process executable"),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            child.StartInfo.ArgumentList.Add("--lite-markdown-partial-crash-child");
            child.StartInfo.ArgumentList.Add(root);
            child.StartInfo.ArgumentList.Add(signal);
            if (!child.Start()) return 211;
            var elapsed = Stopwatch.StartNew();
            while (!File.Exists(signal) && elapsed.Elapsed < TimeSpan.FromSeconds(15))
            {
                if (child.HasExited) return 212;
                Thread.Sleep(50);
            }
            if (!File.Exists(signal)) return 213;
            if (!int.TryParse(File.ReadAllText(signal), out int pid) ||
                pid != child.Id || child.HasExited) return 214;

            child.Kill(); // Only own child at exact production rename boundary.
            if (!child.WaitForExit(10000)) return 215;

            string dir = Path.Combine(root, Hash(Url));
            string index = Path.Combine(dir, "index.jsonl");
            string[] before = File.ReadAllLines(index);
            if (before.Length != 1) return 216;
            using (var json = JsonDocument.Parse(before[0]))
                if (json.RootElement.GetProperty("status").GetString() != "PARTIAL")
                    return 217;

            string key = Hash(Turn)[..16];
            string firstFile = Path.Combine(dir,
                key + "_" + Hash(First)[..16] + "_partial.md");
            string secondFile = Path.Combine(dir,
                key + "_" + Hash(Second)[..16] + "_partial.md");
            if (!File.Exists(firstFile) || !File.Exists(secondFile) ||
                File.ReadAllText(firstFile, Encoding.UTF8) != First + "\n" ||
                File.ReadAllText(secondFile, Encoding.UTF8) != Second + "\n")
                return 218;

            var restarted = new LiteResponseMarkdownArchive(root);
            if (restarted.IsPersisted(Url, Turn, Second)) return 219;
            if (restarted.RecoverMissingIndex(Url) != 1) return 220;
            if (restarted.IsPersisted(Url, Turn, Second)) return 221;
            string[] after = File.ReadAllLines(index);
            if (after.Length != 2) return 222;
            foreach (string line in after)
            {
                using var json = JsonDocument.Parse(line);
                if (json.RootElement.GetProperty("status").GetString() != "PARTIAL")
                    return 223;
            }
            if (!after.Any(x => x.Contains(Path.GetFileName(secondFile),
                StringComparison.Ordinal))) return 224;
            if (restarted.RecoverMissingIndex(Url) != 0) return 225;
            if (restarted.Save(Url, Turn, Second, true)) return 226;
            if (File.ReadAllLines(index).Length != 2) return 227;
            passed = true;
            LiteEventLog.Write("markdown_partial_process_crash_selftest",
                new { passed = true, childPid = child.Id,
                    partials = 2, recoveredPartials = 1,
                    finalAcknowledged = false, noDuplicate = true,
                    isolated = true });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("markdown_partial_process_crash_selftest",
                new { passed = false, errorType = ex.GetType().Name,
                    message = ex.Message });
            return 228;
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
