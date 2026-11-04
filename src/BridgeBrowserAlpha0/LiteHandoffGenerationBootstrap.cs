using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

// Makes a NEW immutable Prepared generation after an unretriable old attempt.
// It NEVER rewrites the old proof/ledger, clicks, or enables a second lead.
internal static class LiteHandoffGenerationBootstrap
{
    internal sealed record Active
    {
        public int Schema { get; init; } = 1;
        public string OldChatUrl { get; init; } = "";
        public long WakeSequence { get; init; }
        public string ProofFile { get; init; } = "";
        public string LedgerFile { get; init; } = "";
        public string OwnerEpoch { get; init; } = "";
        public string ProofSha256 { get; init; } = "";
        public string RetiredOwnerEpoch { get; init; } = "";
        public string RetiredReceipt { get; init; } = "";
        public string PreviousPointerFile { get; init; } = "";
        public string PreviousPointerSha256 { get; init; } = "";
        public string PreviousLedgerFile { get; init; } = "";
        public string PreviousOwnerEpoch { get; init; } = "";
        public string PreviousSeedReceipt { get; init; } = "";
    }

    internal static (string Proof, string Ledger) SelectCurrent(
        string handoffDir, string chatHash, string oldUrl)
    {
        string stem = chatHash[..16];
        string oldProof = Path.Combine(handoffDir, stem + "_proof.json");
        string oldLedger = Path.Combine(handoffDir, stem + "_ledger.json");
        string pointer = Path.Combine(handoffDir, stem + "_active.json");
        if (!File.Exists(pointer)) return (oldProof, oldLedger);
        var a = JsonSerializer.Deserialize<Active>(File.ReadAllText(pointer))
            ?? throw new InvalidDataException("Invalid active handoff pointer");
        if (a.Schema != 1 || a.OldChatUrl != oldUrl || a.WakeSequence < 1 ||
            !a.ProofFile.StartsWith(stem + "_seq", StringComparison.Ordinal) ||
            !a.LedgerFile.StartsWith(stem + "_seq", StringComparison.Ordinal) ||
            a.ProofFile.Contains("..") || a.LedgerFile.Contains("..") ||
            Path.GetFileName(a.ProofFile) != a.ProofFile ||
            Path.GetFileName(a.LedgerFile) != a.LedgerFile)
            throw new InvalidDataException("Unsafe active handoff pointer");
        var former = LiteHandoffSuccession.Read(oldLedger);
        var next = LiteHandoffSuccession.Read(Path.Combine(handoffDir,a.LedgerFile));
        var proof = JsonSerializer.Deserialize<LiteHandoffEvidence.Proof>(
            File.ReadAllText(Path.Combine(handoffDir,a.ProofFile)))
            ?? throw new InvalidDataException("Active proof unreadable");
        if (former.Step != LiteHandoffSuccession.Phase.SeedUncertain ||
            former.OwnerEpoch != a.RetiredOwnerEpoch ||
            former.SeedReceipt != a.RetiredReceipt ||
            next.OwnerEpoch != a.OwnerEpoch || proof.OwnerEpoch != a.OwnerEpoch ||
            next.ProofSha256 != a.ProofSha256 ||
            proof.FileSha256 != a.ProofSha256 ||
            next.OldChatUrl != oldUrl || proof.OldChatUrl != oldUrl ||
            next.OldWakeSequence != a.WakeSequence)
            throw new InvalidDataException("Active lineage / retired receipt mismatch");
        if (a.PreviousPointerFile.Length > 0)
        {
            // Sequence gaps are legal; derive the exact predecessor from
            // its immutable saved pointer rather than a sequence guess.
            if (Path.GetFileName(a.PreviousPointerFile)!=a.PreviousPointerFile ||
                a.PreviousPointerFile.Contains("..") ||
                !a.PreviousPointerFile.StartsWith(stem+"_seq",StringComparison.Ordinal) ||
                !a.PreviousPointerFile.EndsWith("_pointer.json",StringComparison.Ordinal) ||
                !a.PreviousLedgerFile.StartsWith(stem+"_seq",StringComparison.Ordinal) ||
                Path.GetFileName(a.PreviousLedgerFile)!=a.PreviousLedgerFile)
                throw new InvalidDataException("Invalid previous lineage filenames");
            string priorPath=Path.Combine(handoffDir,a.PreviousPointerFile);
            string priorHash=Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(priorPath))).ToLowerInvariant();
            if (!string.Equals(priorHash,a.PreviousPointerSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Prior pointer changed");
            var prior=JsonSerializer.Deserialize<Active>(File.ReadAllText(priorPath))
                ?? throw new InvalidDataException("Missing prior active pointer");
            var previous=LiteHandoffSuccession.Read(
                Path.Combine(handoffDir,a.PreviousLedgerFile));
            if (prior.LedgerFile!=a.PreviousLedgerFile ||
                prior.OwnerEpoch!=a.PreviousOwnerEpoch ||
                prior.WakeSequence>=a.WakeSequence ||
                previous.OwnerEpoch!=a.PreviousOwnerEpoch ||
                previous.SeedReceipt!=a.PreviousSeedReceipt ||
                previous.Step!=LiteHandoffSuccession.Phase.SeedAcked ||
                LiteHandoffPersistedRoute.IsServerConversation(previous.NewChatUrl) ||
                !previous.NewChatUrl.Contains("local-chatgpt",
                    StringComparison.OrdinalIgnoreCase) ||
                a.RetiredOwnerEpoch!=prior.RetiredOwnerEpoch ||
                a.RetiredReceipt!=prior.RetiredReceipt)
                throw new InvalidDataException("Prior provisional ACK lineage mismatch");
        }        return (Path.Combine(handoffDir,a.ProofFile),
            Path.Combine(handoffDir,a.LedgerFile));
    }

    internal static int Run(string[] args, bool rollover=false)
    {
        if (args.Length != 6 || !string.Equals(Environment.MachineName,
            "SATANAMD", StringComparison.OrdinalIgnoreCase)) return 2;
        try { return Prepare(args,rollover); }
        catch (Exception ex) when (ex is IOException or ArgumentException or
            InvalidDataException or InvalidOperationException or
            UnauthorizedAccessException or JsonException) { return 9; }
    }    private static int Prepare(string[] args, bool rollover)
    {
        const string docs = @"D:\projects\open-browser\docs";
        var now = DateTimeOffset.UtcNow;
        string oldUrl = LiteDevWakeState.NormalizeUrl(args[3]);
        if (!LiteDevWakeState.IsChatUrl(oldUrl) ||
            !long.TryParse(args[4], out long seq) || seq < 1) return 3;
        string expectedDoc = Path.Combine(docs,
            $"OPENBROWSER_LITE_AUTO_HANDOFF_SEQ{seq:000000}.md");
        if (!string.Equals(Path.GetFullPath(args[1]), expectedDoc,
            StringComparison.OrdinalIgnoreCase)) return 4;
        if (!LiteHandoffEvidence.VerifyDocument(expectedDoc, docs,
            args[2], now, out _, out _, out _)) return 5;

        string hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(oldUrl))).ToLowerInvariant();
        string responseRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser","ChatGPT-Lite","responses",hash);
        string response = Path.GetFullPath(args[5]);
        if (!response.StartsWith(Path.GetFullPath(responseRoot) +
            Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(response) != ".md" ||
            response.EndsWith("_partial.md",StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(response) || File.GetLastWriteTimeUtc(response) <
            now.UtcDateTime.AddHours(-3)) return 6;
        string responseSha = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(response))).ToLowerInvariant();
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser","ChatGPT-Lite","diagnostics","handoff");
        Directory.CreateDirectory(dir);
        string stem = hash[..16];
        string retiredPath = Path.Combine(dir,stem+"_ledger.json");
        var retired = LiteHandoffSuccession.Read(retiredPath);
        if (retired.Step != LiteHandoffSuccession.Phase.SeedUncertain ||
            retired.SeedReceipt.Length != 32 ||
            retired.OldChatUrl != oldUrl ||
            retired.OldWakeSequence >= seq ||
            !string.IsNullOrEmpty(retired.NewChatUrl)) return 7;
        var active = Path.Combine(dir,stem+"_active.json");
        Active? predecessor = null;
        LiteHandoffSuccession.State? priorLedger = null;
        string priorPointerBackup = "";
        string priorPointerSha = "";
        if (rollover)
        {
            if (!File.Exists(active)) return 8;
            byte[] oldPointerBytes = File.ReadAllBytes(active);
            predecessor = JsonSerializer.Deserialize<Active>(oldPointerBytes)
                ?? throw new InvalidDataException("Active pointer unreadable");
            if (predecessor.Schema!=1 || predecessor.PreviousPointerFile.Length>0 ||
                predecessor.OldChatUrl!=oldUrl || predecessor.WakeSequence>=seq)
                return 8;
            priorLedger = LiteHandoffSuccession.Read(
                Path.Combine(dir,predecessor.LedgerFile));
            if (priorLedger.Step!=LiteHandoffSuccession.Phase.SeedAcked ||
                priorLedger.OwnerEpoch!=predecessor.OwnerEpoch ||
                priorLedger.SeedReceipt.Length!=32 ||
                LiteHandoffPersistedRoute.IsServerConversation(priorLedger.NewChatUrl) ||
                !priorLedger.NewChatUrl.Contains("local-chatgpt",
                    StringComparison.OrdinalIgnoreCase)) return 8;
            string priorClaim = Path.Combine(dir,"successor-seed-trial."+
                Path.GetFileNameWithoutExtension(predecessor.LedgerFile)+
                ".request.claimed");
            if (!File.Exists(priorClaim) ||
                File.Exists(priorClaim[..^8])) return 8;
            priorPointerSha=Convert.ToHexString(
                SHA256.HashData(oldPointerBytes)).ToLowerInvariant();
            priorPointerBackup=Path.Combine(dir,
                $"{stem}_seq{predecessor.WakeSequence:000000}_pointer.json");
            if (File.Exists(priorPointerBackup)) return 8;
        }
        else if (File.Exists(active)) return 8; // Never overwrite an epoch.
        string proofName = $"{stem}_seq{seq:000000}_proof.json";
        string ledgerName = $"{stem}_seq{seq:000000}_ledger.json";
        string proofPath = Path.Combine(dir, proofName);
        string ledgerPath = Path.Combine(dir, ledgerName);
        if (File.Exists(proofPath) || File.Exists(ledgerPath)) return 8;

        var proof = LiteHandoffEvidence.CreateVerified(
            "open-browser-lite", oldUrl, seq, responseSha,
            expectedDoc, docs, args[2], now);
        if (proof.OwnerEpoch == retired.OwnerEpoch) return 9;
        LiteHandoffEvidence.PersistOnce(proof,proofPath);
        LiteHandoffSuccession.PersistOnce(ledgerPath,
            LiteHandoffSuccession.Prepare(proof,now));
        var next = LiteHandoffSuccession.Read(ledgerPath);
        if (next.Step != LiteHandoffSuccession.Phase.Prepared ||
            next.Revision != 0 || next.OwnerEpoch == retired.OwnerEpoch ||
            next.ProofSha256 != proof.FileSha256 ||
            !string.IsNullOrEmpty(next.SeedReceipt)) return 9;
        var pointer = new Active
        {
            OldChatUrl=oldUrl, WakeSequence=seq,
            ProofFile=proofName, LedgerFile=ledgerName,
            OwnerEpoch=next.OwnerEpoch, ProofSha256=next.ProofSha256,
            RetiredOwnerEpoch=retired.OwnerEpoch,
            RetiredReceipt=retired.SeedReceipt,
            PreviousPointerFile=predecessor is null ? "" :
                Path.GetFileName(priorPointerBackup),
            PreviousPointerSha256=priorPointerSha,
            PreviousLedgerFile=predecessor?.LedgerFile ?? "",
            PreviousOwnerEpoch=predecessor?.OwnerEpoch ?? "",
            PreviousSeedReceipt=priorLedger?.SeedReceipt ?? ""
        };
        string tmp = active+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(tmp,JsonSerializer.Serialize(pointer),
                new UTF8Encoding(false));
            if (rollover)
                File.Replace(tmp,active,priorPointerBackup,ignoreMetadataErrors:true);
            else File.Move(tmp,active,overwrite:false);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
        var selected = SelectCurrent(dir,hash,oldUrl);
        if (selected.Ledger != ledgerPath || selected.Proof != proofPath)
            throw new InvalidDataException("Wrong selected successor epoch");
        return 0;
    }
}
