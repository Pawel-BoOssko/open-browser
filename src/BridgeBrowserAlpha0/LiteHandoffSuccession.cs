using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Crash-fenced metadata ledger for a future autonomous HANDOFF. No WebView2
/// navigation, message injection or schedule change is performed here.
/// SeedReserved is irreversible: after a crash, never retry an uncertain send.
/// </summary>
internal static class LiteHandoffSuccession
{
    internal enum Phase
    {
        Prepared = 0,
        NewChatObserved = 1,
        SeedReserved = 2,
        SeedUncertain = 3,
        SeedAcked = 4,
        NewCheckpointVerified = 5,
        PromotionVerified = 6,
        Suspended = 7
    }

    internal sealed record State
    {
        public int Schema { get; init; } = 1;
        public string OwnerEpoch { get; init; } = "";
        public string ProjectId { get; init; } = "";
        public string OldChatUrl { get; init; } = "";
        public long OldWakeSequence { get; init; }
        public string ProofSha256 { get; init; } = "";
        public string NewChatUrl { get; init; } = "";
        public string SeedReceipt { get; init; } = "";
        public string CheckpointSha256 { get; init; } = "";
        public string CheckpointPath { get; init; } = "";
        public DateTimeOffset CreatedUtc { get; init; }
        public DateTimeOffset UpdatedUtc { get; init; }
        public int Revision { get; init; }
        public Phase Step { get; init; } = Phase.Prepared;
    }

    private static bool IsSha(string s) =>
        s.Length == 64 && s.All(Uri.IsHexDigit);

    private static bool IsReceipt(string s) =>
        s.Length == 32 && s.All(Uri.IsHexDigit);

    private static bool ValidUrl(string? url) =>
        LiteDevWakeState.IsChatUrl(url) &&
        string.Equals(url, LiteDevWakeState.NormalizeUrl(url!),
            StringComparison.Ordinal);

    internal static State Prepare(LiteHandoffEvidence.Proof proof, DateTimeOffset now)
    {
        if (proof.Schema != 1 || proof.Phase != "prepared" ||
            !IsReceipt(proof.OwnerEpoch) || proof.ProjectId.Length == 0 ||
            !ValidUrl(proof.OldChatUrl) || proof.WakeSequence < 1 ||
            !IsSha(proof.FileSha256))
            throw new InvalidDataException("Invalid independently verified proof");
        return new State
        {
            OwnerEpoch = proof.OwnerEpoch,
            ProjectId = proof.ProjectId,
            OldChatUrl = proof.OldChatUrl,
            OldWakeSequence = proof.WakeSequence,
            ProofSha256 = proof.FileSha256,
            CreatedUtc = now.ToUniversalTime(),
            UpdatedUtc = now.ToUniversalTime()
        };
    }

    internal static State ObserveNewChat(State previous, string observedUrl,
        bool composerEmpty, bool generating, bool routeStable,
        bool notRegisteredElsewhere, DateTimeOffset now)
    {
        Require(previous, Phase.Prepared);
        if (!composerEmpty || generating || !routeStable ||
            !ValidUrl(observedUrl) || observedUrl == previous.OldChatUrl)
            throw new InvalidOperationException(
                "New chat unverified or inherited draft/generation present");
        return Next(previous, Phase.NewChatObserved, now) with
        {
            NewChatUrl = observedUrl
        };
    }

    internal static State ReserveSeed(State previous, string receipt,
        DateTimeOffset now)
    {
        Require(previous, Phase.NewChatObserved);
        if (!IsReceipt(receipt))
            throw new InvalidDataException("Invalid durable receipt");
        // Persist this state BEFORE calling any external click. Even if the
        // next call never executes, a restart MUST NOT retry the same seed.
        return Next(previous, Phase.SeedReserved, now) with
        {
            SeedReceipt = receipt.ToLowerInvariant()
        };
    }

    internal static bool MaySendOnce(State state) =>
        // A durable reservation must never be automatically replayed.
        // Only the caller holding the freshly committed reservation may
        // execute its single allowed external attempt.
        false;

    internal static State MarkUncertain(State previous, DateTimeOffset now)
    {
        Require(previous, Phase.SeedReserved);
        return Next(previous, Phase.SeedUncertain, now);
    }

    internal static State ConfirmVisibleAck(State previous, string receipt,
        string observedUrl, bool visibleUserBubble, DateTimeOffset now)
    {
        if (previous.Step is not (Phase.SeedReserved or Phase.SeedUncertain) ||
            !IsReceipt(receipt) ||
            !string.Equals(receipt, previous.SeedReceipt,
                StringComparison.OrdinalIgnoreCase) ||
            !visibleUserBubble || !ValidUrl(observedUrl) ||
            observedUrl != previous.NewChatUrl)
            throw new InvalidOperationException("No verified exact seed ACK");
        return Next(previous, Phase.SeedAcked, now);
    }

    internal static State ConfirmNewCheckpoint(State previous,
        string path, string root, string expectedSha, DateTimeOffset now)
    {
        Require(previous, Phase.SeedAcked);
        if (!LiteHandoffEvidence.VerifyDocument(path, root,
                expectedSha, now, out string actualSha, out _, out var reason))
            throw new InvalidDataException("New checkpoint rejected: " + reason);
        return Next(previous, Phase.NewCheckpointVerified, now) with
        {
            CheckpointSha256 = actualSha,
            CheckpointPath = Path.GetFullPath(path)
        };
    }

    internal static State ConfirmPromotion(State previous,
        bool oldLeadEnabled, bool newLeadEnabled,
        bool oldArchived, bool newIsOnlyLead, DateTimeOffset now)
    {
        Require(previous, Phase.NewCheckpointVerified);
        if (oldLeadEnabled || !newLeadEnabled || !oldArchived ||
            !newIsOnlyLead)
            throw new InvalidOperationException(
                "Promotion must have exactly one new owner");
        return Next(previous, Phase.PromotionVerified, now);
    }

    private static State Next(State state, Phase next, DateTimeOffset now) =>
        state with
        {
            Step = next,
            UpdatedUtc = now.ToUniversalTime(),
            Revision = checked(state.Revision + 1)
        };

    private static void Require(State current, Phase phase)
    {
        if (current.Step != phase)
            throw new InvalidOperationException(
                "Illegal handoff transition from " + current.Step);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    internal static State Read(string path) =>
        JsonSerializer.Deserialize<State>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Invalid handoff ledger");

    private static void ValidateStableIdentity(State old, State next)
    {
        if (old.Schema != 1 || next.Schema != 1 ||
            !IsReceipt(old.OwnerEpoch) ||
            old.OwnerEpoch != next.OwnerEpoch ||
            old.ProjectId != next.ProjectId ||
            old.OldChatUrl != next.OldChatUrl ||
            old.OldWakeSequence != next.OldWakeSequence ||
            old.ProofSha256 != next.ProofSha256 ||
            old.NewChatUrl.Length > 0 && old.NewChatUrl != next.NewChatUrl ||
            old.SeedReceipt.Length > 0 && old.SeedReceipt != next.SeedReceipt ||
            old.Revision + 1 != next.Revision ||
            next.UpdatedUtc < old.UpdatedUtc)
            throw new InvalidDataException("Handoff identity/epoch/revision changed");
    }

    // The caller supplies a unique ledger file in the private local state
    // directory. The mutex makes read-check-replace one local operation.
    internal static State PersistOnce(string path, State initial)
    {
        Require(initial, Phase.Prepared);
        if (initial.Revision != 0) throw new InvalidDataException("Not initial");
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using var mutex = new Mutex(false, "Local\\OBLiteHandoffSuccession_v1");
        if (!mutex.WaitOne(TimeSpan.FromSeconds(3)))
            throw new IOException("Handoff state lock busy");
        try
        {
            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp,
                    JsonSerializer.Serialize(initial, JsonOptions),
                    new UTF8Encoding(false));
                File.Move(temp, full, overwrite: false);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return Read(full);
        }
        finally { mutex.ReleaseMutex(); }
    }

    internal static State Advance(string path, int expectedRevision,
        Func<State, State> transition)
    {
        using var mutex = new Mutex(false, "Local\\OBLiteHandoffSuccession_v1");
        if (!mutex.WaitOne(TimeSpan.FromSeconds(3)))
            throw new IOException("Handoff state lock busy");
        try
        {
            string full = Path.GetFullPath(path);
            State original = Read(full);
            if (original.Revision != expectedRevision)
                throw new InvalidOperationException("Stale or concurrent handoff step");
            State replacement = transition(original);
            ValidateStableIdentity(original, replacement);
            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(replacement, JsonOptions),
                    new UTF8Encoding(false));
                // Atomic replacement, old generation remains recoverable.
                File.Move(temp, full, overwrite: true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return Read(full);
        }
        finally { mutex.ReleaseMutex(); }
    }

    internal static int SelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(),
            "OBLiteSuccessor_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var now = DateTimeOffset.UtcNow;
            string handoff = Path.Combine(dir, "HANDOFF.md");
            File.WriteAllText(handoff, "# Handoff\n" + new string('x', 600));
            string sha = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(handoff))).ToLowerInvariant();
            const string old = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            const string newer = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            var proof = LiteHandoffEvidence.CreateVerified(
                "project", old, 7, new string('a', 64),
                handoff, dir, sha, now);
            string ledger = Path.Combine(dir, "ledger.json");
            var initial = PersistOnce(ledger, Prepare(proof, now));
            try { PersistOnce(ledger, initial); return 1; }
            catch (IOException) { }
            try { ObserveNewChat(initial, newer, false, false, true, now); return 2; }
            catch (InvalidOperationException) { }
            try { ObserveNewChat(initial, newer, true, true, true, now); return 3; }
            catch (InvalidOperationException) { }
            try { ObserveNewChat(initial, old, true, false, true, now); return 4; }
            catch (InvalidOperationException) { }
            var observed = Advance(ledger, 0, s =>
                ObserveNewChat(s, newer, true, false, true, now));
            if (observed.Step != Phase.NewChatObserved || observed.Revision != 1)
                return 5;
            try { Advance(ledger, 0, s => ReserveSeed(s, new string('b', 32), now)); return 6; }
            catch (InvalidOperationException) { }
            var reserved = Advance(ledger, 1,
                s => ReserveSeed(s, new string('b', 32), now));
            // Crash/reload after reservation: no auto resend, no second reserve.
            if (Read(ledger).Step != Phase.SeedReserved ||
                MaySendOnce(Read(ledger))) return 7;
            try { Advance(ledger, 2, s => ReserveSeed(s, new string('c', 32), now)); return 8; }
            catch (InvalidOperationException) { }
            var uncertain = Advance(ledger, 2, s => MarkUncertain(s, now));
            try { ConfirmVisibleAck(uncertain, new string('c', 32), newer, true, now); return 9; }
            catch (InvalidOperationException) { }
            try { ConfirmVisibleAck(uncertain, new string('b', 32), newer, false, now); return 10; }
            catch (InvalidOperationException) { }
            var ack = Advance(ledger, 3, s =>
                ConfirmVisibleAck(s, new string('b', 32), newer, true, now));
            try { ConfirmPromotion(ack, false, true, true, true, now); return 11; }
            catch (InvalidOperationException) { }
            var checkpoint = Advance(ledger, 4, s =>
                ConfirmNewCheckpoint(s, handoff, dir, sha, now));
            try { ConfirmPromotion(checkpoint, true, true, true, true, now); return 12; }
            catch (InvalidOperationException) { }
            var finished = Advance(ledger, 5, s =>
                ConfirmPromotion(s, false, true, true, true, now));
            if (finished.Step != Phase.PromotionVerified ||
                Read(ledger).Revision != 6) return 13;
            try { Advance(ledger, 5, s => MarkUncertain(s, now)); return 14; }
            catch (InvalidOperationException) { }
            return 0;
        }
        finally { Directory.Delete(dir, true); }
    }
}
