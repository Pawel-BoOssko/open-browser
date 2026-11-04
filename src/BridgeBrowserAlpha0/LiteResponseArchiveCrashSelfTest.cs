using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

// A true process boundary, not an in-process exception: child exits directly
// after the atomic PARTIAL rename and before the JSONL append. Never uses
// the user's archive or terminates the production browser.
internal sealed partial class LiteResponseMarkdownArchive
{
    private const string CrashUrl =
        "https://chatgpt.com/c/f7654321-aaaa-bbbb-cccc-123456789abc";
    private const string CrashTurn = "crash-partial-turn";
    private const string CrashPartial = "Durable unfinished response before abrupt process exit";
    private const string CrashFinal = "Durable complete response after isolated restart";

    internal static int CrashChild(string isolatedRoot)
    {
        if (!Path.GetFileName(isolatedRoot).StartsWith("obl-md-crash-", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(isolatedRoot)) return 120;
        CrashTestAfterAtomicRename = () => Environment.Exit(121);
        new LiteResponseMarkdownArchive(isolatedRoot).Save(
            CrashUrl, CrashTurn, CrashPartial, partial: true);
        return 122; // Crash hook did not fire.
    }

    internal static int ProcessCrashRecoverySelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "obl-md-crash-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath
                    ?? throw new InvalidOperationException("Missing process executable"),
                UseShellExecute = false, CreateNoWindow = true
            };
            psi.ArgumentList.Add("--lite-md-archive-crash-child");
            psi.ArgumentList.Add(root);
            using var child = Process.Start(psi)
                ?? throw new InvalidOperationException("Crash child did not start");
            if (!child.WaitForExit(15000))
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit(5000);
                return 110;
            }
            if (child.ExitCode != 121) return 111;
            string directory = Path.Combine(root, Hash(CrashUrl));
            string index = Path.Combine(directory, "index.jsonl");
            var files = Directory.GetFiles(directory, "*_partial.md");
            if (files.Length != 1 || File.Exists(index)) return 112;
            if (File.ReadAllText(files[0], Encoding.UTF8) != CrashPartial + "\n")
                return 113;

            // New instance, as if the foreground browser just restarted.
            var restarted = new LiteResponseMarkdownArchive(root);
            if (restarted.IsPersisted(CrashUrl, CrashTurn, CrashPartial))
                return 114; // A PARTIAL never authorizes FINAL pruning ACK.
            if (restarted.RecoverMissingIndex(CrashUrl) != 1) return 115;
            string[] rows = File.ReadAllLines(index);
            if (rows.Length != 1) return 116;
            using (var row = JsonDocument.Parse(rows[0]))
            {
                var fields = row.RootElement;
                if (fields.GetProperty("status").GetString() != "PARTIAL"
                    || fields.GetProperty("sha256").GetString() != Hash(CrashPartial)
                    || fields.GetProperty("fileName").GetString() != Path.GetFileName(files[0]))
                    return 117;
            }
            if (restarted.RecoverMissingIndex(CrashUrl) != 0) return 118;
            if (!restarted.Save(CrashUrl, CrashTurn, CrashFinal, partial: false))
                return 119;
            if (!restarted.IsPersisted(CrashUrl, CrashTurn, CrashFinal))
                return 123;
            rows = File.ReadAllLines(index);
            if (rows.Length != 2 || Directory.GetFiles(directory, "*.md").Length != 2)
                return 124;
            Console.WriteLine("PROCESS_CRASH_PARTIAL_RECOVERY_PASS childExit=121 orphan=1 recovered=1 finalAck=verified rows=2");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("PROCESS_CRASH_PARTIAL_RECOVERY_FAILED " + ex);
            return 125;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
