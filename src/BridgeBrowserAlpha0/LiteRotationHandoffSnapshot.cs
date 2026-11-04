using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

// A deterministic, atomic context handoff for a REAL automatic lead rotation.
// This writes documentation only: no chat creation, send, ACK or role change.
internal static class LiteRotationHandoffSnapshot
{
    private const string Root = @"D:\projects\open-browser\docs";
    internal static (bool Saved, string Reason, string Sha256) Save(
        string oldLead, long previousWake) => SaveTo(
        oldLead, previousWake,
        Path.Combine(Root, "OPENBROWSER_LITE_CHECKPOINT.md"),
        Path.Combine(Root, "OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md"),
        DateTimeOffset.UtcNow);

    private static (bool Saved, string Reason, string Sha256) SaveTo(
        string oldLead, long previousWake, string checkpoint,
        string destination, DateTimeOffset now)
    {
        string? temporary = null;
        try
        {
            oldLead = LiteDevWakeState.NormalizeUrl(oldLead);
            if (!LiteDevWakeState.IsChatUrl(oldLead) || previousWake < 1 ||
                previousWake == long.MaxValue) return (false, "invalid_identity", "");
            if (!File.Exists(checkpoint)) return (false, "checkpoint_missing", "");
            string checkpointHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(checkpoint))).ToLowerInvariant();
            string[] tail = File.ReadLines(checkpoint, Encoding.UTF8)
                .Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(24).ToArray();
            if (tail.Length == 0) return (false, "checkpoint_empty", "");
            var text = new StringBuilder();
            text.AppendLine("# OPEN BROWSER LITE AUTO HANDOFF CURRENT");
            text.AppendLine("Format: OBL_HANDOFF_SNAPSHOT_V2");
            text.AppendLine("Project: obl");
            text.AppendLine("Last wake: " + previousWake);
            text.AppendLine("Next wake: " + (previousWake + 1));
            text.AppendLine("Current chat URL: " + oldLead);
            text.AppendLine("Saved UTC: " + now.ToUniversalTime().ToString("O"));
            text.AppendLine("Host: SATANAMD");
            text.AppendLine("Source checkpoint: " + checkpoint);
            text.AppendLine("Checkpoint SHA256: " + checkpointHash);
            text.AppendLine("Canonical backlog: D:\\projects\\open-browser\\docs\\OBL_BACKLOG.md");
            text.AppendLine("Active task state: run scripts\\obl-beads.ps1 -Action scan");
            text.AppendLine("NEXT LEAD: verify the old URL and Last wake, then read the FULL current checkpoint and Beads. The excerpt below is supplementary, not a substitute.");
            text.AppendLine();
            text.AppendLine("## Last 24 nonempty checkpoint lines captured at rotation");
            foreach (string line in tail) text.AppendLine(line);
            byte[] bytes = new UTF8Encoding(false).GetBytes(text.ToString());
            string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, destination, overwrite: true);
            temporary = null;
            byte[] actual = File.ReadAllBytes(destination);
            if (!actual.AsSpan().SequenceEqual(bytes)) return (false, "readback_mismatch", "");
            return (true, "verified", sha);
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name, "");
        }
        finally
        {
            try { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
        }
    }

    internal static int SelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(), "obl-rotation-handoff-" +
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            string cp = Path.Combine(dir, "checkpoint.md");
            string output = Path.Combine(dir, "handoff.md");
            File.WriteAllText(cp, "First state\nWork completed\nNext task: test rotation\n");
            string url = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            var a = SaveTo(url, 209, cp, output,
                DateTimeOffset.Parse("2026-10-31T15:00:00Z"));
            if (!a.Saved || a.Reason != "verified" || a.Sha256.Length != 64) return 1;
            string content = File.ReadAllText(output);
            if (!content.Contains("Format: OBL_HANDOFF_SNAPSHOT_V2") ||
                !content.Contains("Last wake: 209") ||
                !content.Contains("Next wake: 210") ||
                !content.Contains("Current chat URL: " + url) ||
                !content.Contains("Next task: test rotation")) return 2;
            if (!LiteHandoffEvidence.VerifyDocument(output, dir, a.Sha256,
                DateTimeOffset.UtcNow, out var checkedSha, out _, out var why) ||
                checkedSha != a.Sha256) return 6;
            string oldSha = a.Sha256;
            var failed = SaveTo(url, 219, Path.Combine(dir, "missing"), output,
                DateTimeOffset.UtcNow);
            if (failed.Saved || failed.Reason != "checkpoint_missing" ||
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output)))
                    .ToLowerInvariant() != oldSha) return 3;
            File.AppendAllText(cp, "Updated milestone\n");
            var next = SaveTo(url, 219, cp, output, DateTimeOffset.UtcNow);
            if (!next.Saved || next.Sha256 == oldSha ||
                !File.ReadAllText(output).Contains("Last wake: 219") ||
                !File.ReadAllText(output).Contains("Updated milestone")) return 4;
            return 0;
        }
        catch { return 5; }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
        }
    }
}
