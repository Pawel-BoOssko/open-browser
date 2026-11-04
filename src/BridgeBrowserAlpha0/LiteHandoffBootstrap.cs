using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

// PREPARED ledger only. Never creates a chat, sends a seed, changes a wake
// schedule, or promotes a lead. Called by a short explicit CLI command.
internal static class LiteHandoffBootstrap
{
    internal static int Run(string[] args)
    {
        if (args.Length != 5 ||
            !string.Equals(Environment.MachineName, "SATANAMD",
                StringComparison.OrdinalIgnoreCase)) return 2;
        try
        {
            string docs = @"D:\projects\open-browser\docs";
            string handoff = Path.Combine(docs,
                "OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md");
            var now = DateTimeOffset.UtcNow;
            if (!long.TryParse(args[3], out long seq) || seq < 1) return 3;
            string url = LiteDevWakeState.NormalizeUrl(args[2]);
            if (!LiteDevWakeState.IsChatUrl(url)) return 4;
            if (!LiteHandoffEvidence.VerifyDocument(handoff, docs, args[1],
                now, out _, out _, out _)) return 5;

            string responseRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenBrowser", "ChatGPT-Lite", "responses");
            string chatHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
            string expected = Path.GetFullPath(Path.Combine(responseRoot, chatHash));
            string answerPath = Path.GetFullPath(args[4]);
            if (!answerPath.StartsWith(expected + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) ||
                !answerPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                answerPath.EndsWith("_partial.md", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(answerPath)) return 6;
            string answerSha = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(answerPath))).ToLowerInvariant();            string dir = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "OpenBrowser", "ChatGPT-Lite", "diagnostics", "handoff");
            Directory.CreateDirectory(dir);
            string prefix = chatHash[..16];
            string proofPath = Path.Combine(dir, prefix + "_proof.json");
            string ledgerPath = Path.Combine(dir, prefix + "_ledger.json");
            LiteHandoffEvidence.Proof proof;
            if (File.Exists(proofPath))
            {
                proof = JsonSerializer.Deserialize<LiteHandoffEvidence.Proof>(
                    File.ReadAllText(proofPath)) ??
                    throw new InvalidDataException("Unreadable prior proof");
                if (proof.FileSha256 != args[1].ToLowerInvariant() ||
                    proof.OldChatUrl != url ||
                    proof.WakeSequence != seq ||
                    proof.LastAnswerFingerprint != answerSha)
                    return 7;
            }
            else
            {
                proof = LiteHandoffEvidence.CreateVerified(
                    "open-browser-lite", url, seq, answerSha,
                    handoff, docs, args[1], now);
                LiteHandoffEvidence.PersistOnce(proof, proofPath);
            }
            if (!File.Exists(ledgerPath))
                LiteHandoffSuccession.PersistOnce(ledgerPath,
                    LiteHandoffSuccession.Prepare(proof, now));
            var ledger = LiteHandoffSuccession.Read(ledgerPath);
            if (ledger.OwnerEpoch != proof.OwnerEpoch ||
                ledger.ProofSha256 != proof.FileSha256 ||
                ledger.OldChatUrl != url ||
                ledger.Step != LiteHandoffSuccession.Phase.Prepared)
                return 8;
            return 0;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or
                                   InvalidDataException or UnauthorizedAccessException or
                                   JsonException or InvalidOperationException)
        {
            return 9;
        }
    }
}
