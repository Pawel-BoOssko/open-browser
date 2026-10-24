using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Durable, independently verifiable HANDOFF artifact evidence.
/// This does NOT open a new chat, click Send or activate a second lead.
/// </summary>
internal static class LiteHandoffEvidence
{
    internal sealed class Proof
    {
        public int Schema { get; set; } = 1;
        public string OwnerEpoch { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string OldChatUrl { get; set; } = "";
        public long WakeSequence { get; set; }
        public string LastAnswerFingerprint { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FileSha256 { get; set; } = "";
        public long FileLength { get; set; }
        public DateTimeOffset VerifiedAtUtc { get; set; }
        public string Phase { get; set; } = "prepared";
    }

    private const int MinBytes = 300;
    private const int MaxBytes = 2 * 1024 * 1024;

    internal static bool VerifyDocument(string path, string allowedRoot,
        string expectedSha256, DateTimeOffset utcNow,
        out string actualSha256, out long fileLength, out string reason)
    {
        actualSha256 = "";
        fileLength = 0;
        reason = "invalid";
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(allowedRoot) ||
            string.IsNullOrWhiteSpace(expectedSha256) ||
            expectedSha256.Length != 64 ||
            !expectedSha256.All(Uri.IsHexDigit))
            return false;
        try
        {
            string folder = Path.GetFullPath(allowedRoot).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string file = Path.GetFullPath(path);
            if (!file.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(file), ".md",
                    StringComparison.OrdinalIgnoreCase))
            {
                reason = "outside_handoff_root";
                return false;
            }
            // Do not follow junctions or symlinks out of the trusted root.
            string dir = Path.GetDirectoryName(file)!;
            while (dir.Length >= folder.TrimEnd(Path.DirectorySeparatorChar).Length)
            {
                if (File.GetAttributes(dir).HasFlag(FileAttributes.ReparsePoint))
                {
                    reason = "reparse_path";
                    return false;
                }
                string? parent = Path.GetDirectoryName(dir);
                if (parent is null || parent == dir) break;
                dir = parent;
            }
            var info = new FileInfo(file);
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                reason = "missing_or_link";
                return false;
            }
            var stamp = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            if (stamp < utcNow.AddHours(-3) || stamp > utcNow.AddMinutes(2))
            {
                reason = "stale_or_future_document";
                return false;
            }
            if (info.Length < MinBytes || info.Length > MaxBytes)
            {
                reason = "invalid_length";
                return false;
            }
            long length = info.Length;
            DateTime originalWrite = info.LastWriteTimeUtc;
            using (var stream = new FileStream(file, FileMode.Open,
                       FileAccess.Read, FileShare.Read, 65536,
                       FileOptions.SequentialScan))
            {
                actualSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }
            info.Refresh();
            if (info.Length != length || info.LastWriteTimeUtc != originalWrite)
            {
                reason = "modified_during_verification";
                return false;
            }
            if (!string.Equals(actualSha256, expectedSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                reason = "sha_mismatch";
                return false;
            }
            fileLength = length;
            reason = "verified";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                    or ArgumentException or NotSupportedException)
        {
            reason = ex.GetType().Name;
            return false;
        }
    }

    internal static Proof CreateVerified(string projectId, string oldChatUrl,
        long sequence, string answerHash, string documentPath, string allowedRoot,
        string expectedSha, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(projectId) ||
            !LiteDevWakeState.IsChatUrl(oldChatUrl) || sequence < 1 ||
            answerHash.Length != 64 || !answerHash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid lead identity or fingerprint");
        if (!VerifyDocument(documentPath, allowedRoot, expectedSha, now,
                out string actualSha, out long length, out string reason))
            throw new InvalidDataException("HANDOFF evidence rejected: " + reason);
        return new Proof
        {
            OwnerEpoch = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            OldChatUrl = LiteDevWakeState.NormalizeUrl(oldChatUrl),
            WakeSequence = sequence,
            LastAnswerFingerprint = answerHash.ToLowerInvariant(),
            FilePath = Path.GetFullPath(documentPath),
            FileSha256 = actualSha,
            FileLength = length,
            VerifiedAtUtc = now.ToUniversalTime()
        };
    }

    // An existing proof is never overwritten. Subsequent stages must use a
    // separate fenced transition, so crash recovery cannot reseed a new chat.
    internal static void PersistOnce(Proof proof, string destination)
    {
        if (proof.Schema != 1 || proof.Phase != "prepared" ||
            proof.OwnerEpoch.Length != 32 || !proof.OwnerEpoch.All(Uri.IsHexDigit) ||
            proof.FileSha256.Length != 64)
            throw new InvalidDataException("Invalid handoff proof");
        string path = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(proof),
                new UTF8Encoding(false));
            File.Move(tmp, path, false);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    internal static int SelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(),
            "OBLiteHandoffProof_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var now = DateTimeOffset.UtcNow;
            string good = Path.Combine(dir, "HANDOFF.md");
            File.WriteAllText(good,
                "# HANDOFF\n" + new string('a', 550), new UTF8Encoding(false));
            string hash = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(good))).ToLowerInvariant();
            if (!VerifyDocument(good, dir, hash, now,
                out var actual, out var len, out var why) ||
                actual != hash || len < MinBytes || why != "verified")
                return 1;
            if (VerifyDocument(good, dir, new string('f', 64), now,
                out _, out _, out _)) return 2;
            if (VerifyDocument(good, dir, hash, now.AddHours(4),
                out _, out _, out _)) return 3;
            string outside = Path.Combine(Path.GetTempPath(), "outside.md");
            if (VerifyDocument(outside, dir, hash, now,
                out _, out _, out _)) return 4;
            const string old = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            var proof = CreateVerified("project-a", old, 6,
                new string('b', 64), good, dir, hash, now);
            if (proof.Phase != "prepared" || proof.OwnerEpoch.Length != 32 ||
                proof.FileSha256 != hash || proof.WakeSequence != 6)
                return 5;
            string ledger = Path.Combine(dir, "proof.json");
            PersistOnce(proof, ledger);
            if (!File.Exists(ledger)) return 6;
            var restored = JsonSerializer.Deserialize<Proof>(
                File.ReadAllText(ledger));
            if (restored?.OwnerEpoch != proof.OwnerEpoch) return 7;
            try { PersistOnce(proof, ledger); return 8; }
            catch (IOException) { /* no overwrite */ }
            if (JsonSerializer.Deserialize<Proof>(
                File.ReadAllText(ledger))?.OwnerEpoch != proof.OwnerEpoch)
                return 9;
            File.WriteAllText(good, "# truncated");
            if (VerifyDocument(good, dir, hash, now,
                out _, out _, out _)) return 10;
            return 0;
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
