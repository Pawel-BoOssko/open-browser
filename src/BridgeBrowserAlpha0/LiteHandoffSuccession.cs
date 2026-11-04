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
        Suspended = 7,
        BlankComposerObserved = 8
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
        public long NewComposerNavigationEpoch { get; init; }
        public int NewComposerRendererPid { get; init; }
        public long LastComposerProbeEpoch { get; init; }
        public DateTimeOffset LastComposerProbeUtc { get; init; }
        public bool LastComposerProbeNonActionable { get; init; }
        public string SeedReceipt { get; init; } = "";
        public string SeedTextSha256 { get; init; } = "";
        public bool SeedStartedOnBlankRoute { get; init; }
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
            !notRegisteredElsewhere || !ValidUrl(observedUrl) || observedUrl == previous.OldChatUrl)
            throw new InvalidOperationException(
                "New chat unverified or inherited draft/generation present");
        return Next(previous, Phase.NewChatObserved, now) with
        {
            NewChatUrl = observedUrl
        };
    }

    internal static State ObserveNewChatVerified(
        State previous, LiteHandoffRouteEvidence.Sample first,
        LiteHandoffRouteEvidence.Sample second,
        IReadOnlyCollection<string> currentlyRegistered,
        DateTimeOffset now)
    {
        Require(previous, Phase.Prepared);
        string verifiedUrl = LiteHandoffRouteEvidence.VerifyPair(
            previous.OldChatUrl, first, second, currentlyRegistered, now);
        return ObserveNewChat(previous, verifiedUrl, true, false, true,
            true, now);
    }
    // Proof of a completed diagnostic, NOT an actionable composer lease.
    // Worker was navigated back to the old lead; do not change Phase.
    internal static State RecordNonActionableComposerProbe(
        State previous, LiteHandoffRouteEvidence.Sample first,
        LiteHandoffRouteEvidence.Sample second, DateTimeOffset now)
    {
        Require(previous, Phase.Prepared);
        long epoch = LiteHandoffRouteEvidence.VerifyBlankComposerPair(
            first, second, now);
        if (now < previous.UpdatedUtc)
            throw new InvalidDataException("Observation precedes ledger");
        return Next(previous, Phase.Prepared, now) with
        {
            LastComposerProbeEpoch = epoch,
            LastComposerProbeUtc = second.Utc.ToUniversalTime(),
            LastComposerProbeNonActionable = true
        };
    }
    internal static State ObserveBlankComposer(
        State previous, LiteHandoffRouteEvidence.Sample first,
        LiteHandoffRouteEvidence.Sample second, DateTimeOffset now)
    {
        Require(previous, Phase.Prepared);
        long epoch = LiteHandoffRouteEvidence.VerifyBlankComposerPair(
            first, second, now);
        return Next(previous, Phase.BlankComposerObserved, now) with
        {
            NewComposerNavigationEpoch = epoch,
            NewComposerRendererPid = second.RendererPid
        };
    }
    internal static State ReserveSeed(State previous, string receipt,
        DateTimeOffset now)
    {
        if (previous.Step is not (Phase.NewChatObserved or Phase.BlankComposerObserved))
            throw new InvalidOperationException("No verified composer for seed");
        if (!IsReceipt(receipt))
            throw new InvalidDataException("Invalid durable receipt");
        // Persist this state BEFORE calling any external click. Even if the
        // next call never executes, a restart MUST NOT retry the same seed.
        return Next(previous, Phase.SeedReserved, now) with
        {
            SeedReceipt = receipt.ToLowerInvariant(),
            SeedStartedOnBlankRoute = previous.Step == Phase.BlankComposerObserved
        };
    }

    // Reserve exact seed body fingerprint BEFORE one external attempt.
    // Does not click; the caller must NEVER replay this reservation.
    internal static State ReserveSeedWithFingerprint(
        State previous, string receipt, string seedTextSha256,
        DateTimeOffset now)
    {
        if (!IsSha(seedTextSha256))
            throw new InvalidDataException("Invalid seeded body SHA");
        return ReserveSeed(previous, receipt, now) with
        {
            SeedTextSha256 = seedTextSha256.ToLowerInvariant()
        };
    }

    // ChatGPT can assign /c/<id> only AFTER the first user message.
    // Both samples must come from the original renderer, show the exact
    // first user bubble fingerprint and independently agree on new /c/ URL.
    // This function never clicks, sends, retries, or changes an owner.
    internal static State ConfirmPostSeedVisibleAck(
        State previous, string receipt,
        LiteHandoffPostSeedEvidence.Sample first,
        LiteHandoffPostSeedEvidence.Sample second,
        IReadOnlyCollection<string> registeredUrls, DateTimeOffset now)
    {
        if (!IsReceipt(receipt) ||
            !string.Equals(receipt, previous.SeedReceipt,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seed receipt mismatch");
        string url = LiteHandoffPostSeedEvidence.Verify(
            previous, first, second, registeredUrls, now);
        if (previous.NewChatUrl.Length > 0 && previous.NewChatUrl != url)
            throw new InvalidOperationException("Conflicting successor URL");
        return Next(previous, Phase.SeedAcked, now) with
        {
            NewChatUrl = url
        };
    }
    // Unlike optimistic SeedAcked, this transition requires the assistant
    // to have actually answered in the canonical server conversation.
    internal static State ConfirmPersistedAssistantAck(
        State previous, string receipt,
        LiteHandoffPostSeedEvidence.Sample first,
        LiteHandoffPostSeedEvidence.Sample second,
        LiteSuccessorDurableReadback.Sample replyFirst,
        LiteSuccessorDurableReadback.Sample replySecond,
        IReadOnlyCollection<string> registeredUrls, DateTimeOffset now)
    {
        if (!LiteSuccessorDurableReadback.VerifyStableCanonical(
                replyFirst, replySecond, previous.SeedTextSha256, now) ||
            !LiteHandoffPersistedRoute.IsServerConversation(first.Url) ||
            first.Url != second.Url ||
            first.Url != replyFirst.Url || first.Url != replySecond.Url ||
            first.RendererPid != replyFirst.BrowserPid ||
            second.RendererPid != replySecond.BrowserPid)
            throw new InvalidDataException("No persistent server ACK and exact assistant response");
        return ConfirmPostSeedVisibleAck(previous, receipt,
            first, second, registeredUrls, now);
    }
    // A spent/uncertain first seed may be acknowledged LATER only by
    // TWO fresh independent reads of the SAME persisted server /c/ UUID.
    // No receipt replay, no click and no lead scheduling side effects.
    internal static State RecoverUncertainServerAck(
        State previous, string receipt, string verifiedUrl,
        LiteSuccessorDurableReadback.Sample first,
        LiteSuccessorDurableReadback.Sample second,
        IReadOnlyCollection<string> registeredUrls, DateTimeOffset now)
    {
        Require(previous, Phase.SeedUncertain);
        if (!IsReceipt(receipt) ||
            !string.Equals(previous.SeedReceipt, receipt,
                StringComparison.OrdinalIgnoreCase) ||
            !IsSha(previous.SeedTextSha256) ||
            !LiteHandoffPersistedRoute.IsServerConversation(verifiedUrl) ||
            !LiteSuccessorDurableReadback.VerifyStableCanonical(
                first, second, previous.SeedTextSha256, now) ||
            first.Url != verifiedUrl || second.Url != verifiedUrl ||
            verifiedUrl == previous.OldChatUrl ||
            registeredUrls.Any(u =>
                LiteDevWakeState.IsChatUrl(u) &&
                LiteDevWakeState.NormalizeUrl(u) == verifiedUrl) ||
            (previous.NewChatUrl.Length > 0 &&
             !string.Equals(previous.NewChatUrl, verifiedUrl,
                 StringComparison.Ordinal)))
            throw new InvalidDataException(
                "Uncertain seed lacks independent persisted server ACK");
        return Next(previous, Phase.SeedAcked, now) with
        {
            NewChatUrl = verifiedUrl
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
        if (!LiteHandoffPersistedRoute.IsServerConversation(previous.NewChatUrl))
            throw new InvalidOperationException("Provisional successor URL cannot be checkpointed");
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
        if (!LiteHandoffPersistedRoute.IsServerConversation(previous.NewChatUrl))
            throw new InvalidOperationException("Provisional successor URL cannot be promoted");
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
            (old.NewComposerNavigationEpoch > 0 &&
             old.NewComposerNavigationEpoch != next.NewComposerNavigationEpoch) ||
            (old.NewComposerRendererPid > 0 &&
             old.NewComposerRendererPid != next.NewComposerRendererPid) ||
            (old.SeedTextSha256.Length > 0 &&
             old.SeedTextSha256 != next.SeedTextSha256) ||
            (old.SeedStartedOnBlankRoute && !next.SeedStartedOnBlankRoute) ||
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
            try { ObserveNewChat(initial, newer, false, false, true, true, now); return 2; }
            catch (InvalidOperationException) { }
            try { ObserveNewChat(initial, newer, true, true, true, true, now); return 3; }
            catch (InvalidOperationException) { }
            try { ObserveNewChat(initial, old, true, false, true, true, now); return 4; }
            catch (InvalidOperationException) { }
            try { ObserveNewChat(initial, newer, true, false, true, false, now); return 15; }
            catch (InvalidOperationException) { }
            var first = new LiteHandoffRouteEvidence.Sample(
                newer, 8122, now.AddSeconds(-3), true, true,
                false, false, "webview_response_probe");
            var second = first with { Utc = now.AddSeconds(-1) };
            try { LiteHandoffRouteEvidence.VerifyPair(old, first,
                second with { Url = old }, Array.Empty<string>(), now); return 16; }
            catch (InvalidDataException) { }
            try { LiteHandoffRouteEvidence.VerifyPair(old, first,
                second, new[] { newer }, now); return 17; }
            catch (InvalidDataException) { }
            try { LiteHandoffRouteEvidence.VerifyPair(old, first,
                second with { Generating = true }, Array.Empty<string>(), now); return 18; }
            catch (InvalidDataException) { }
            try { LiteHandoffRouteEvidence.VerifyPair(old, first,
                first with { Utc = first.Utc.AddMilliseconds(100) },
                Array.Empty<string>(), now); return 19; }
            catch (InvalidDataException) { }
            try { LiteHandoffRouteEvidence.VerifyPair(old, first,
                second with { Evidence = "manual_string" },
                Array.Empty<string>(), now); return 20; }
            catch (InvalidDataException) { }
            var blankA = new LiteHandoffRouteEvidence.Sample(
                "https://chatgpt.com/", 8122, now.AddSeconds(-3),
                true, true, false, false, "webview_response_probe", true, 77);
            var blankB = blankA with { Utc = now.AddSeconds(-1) };
            try { LiteHandoffRouteEvidence.VerifyBlankComposerPair(
                blankA, blankB with { ExplicitNewChatAction = false }, now);
                return 21; }
            catch (InvalidDataException) { }
            try { LiteHandoffRouteEvidence.VerifyBlankComposerPair(
                blankA, blankB with { Url = newer }, now); return 22; }
            catch (InvalidDataException) { }
            try { LiteHandoffRouteEvidence.VerifyBlankComposerPair(
                blankA, blankB with { NavigationEpoch = 78 }, now); return 23; }
            catch (InvalidDataException) { }
            try { LiteHandoffRouteEvidence.VerifyBlankComposerPair(
                blankA, blankB with { Generating = true }, now); return 24; }
            catch (InvalidDataException) { }
            var held = LiteSuccessorComposerLease.FromVerifiedPair(blankA, blankB, now);
            var fresh = blankB with { Utc = now };
            held.VerifyStillHeld(fresh, 8122, 77, now);
            try { held.VerifyStillHeld(fresh with { ComposerEmpty = false }, 8122, 77, now);
                return 30; } catch (InvalidOperationException) { }
            try { held.VerifyStillHeld(fresh, 8122, 78, now);
                return 31; } catch (InvalidOperationException) { }
            try { held.VerifyStillHeld(fresh, 8123, 77, now);
                return 32; } catch (InvalidOperationException) { }
            try { held.VerifyStillHeld(fresh, 8122, 77, now.AddSeconds(9));
                return 33; } catch (InvalidOperationException) { }
            held.Revoke();
            try { held.VerifyStillHeld(fresh, 8122, 77, now);
                return 34; } catch (InvalidOperationException) { }            var diagnostic = RecordNonActionableComposerProbe(
                initial, blankA, blankB, now);
            if (diagnostic.Step != Phase.Prepared ||
                diagnostic.Revision != 1 ||
                diagnostic.LastComposerProbeEpoch != 77 ||
                !diagnostic.LastComposerProbeNonActionable ||
                diagnostic.NewChatUrl != "") return 28;
            try { ReserveSeed(diagnostic, new string('a', 32), now);
                return 29; }
            catch (InvalidOperationException) { }            var preparedComposer = ObserveBlankComposer(initial, blankA, blankB, now);
            if (preparedComposer.Step != Phase.BlankComposerObserved ||
                preparedComposer.NewComposerNavigationEpoch != 77 ||
                preparedComposer.NewChatUrl != "") return 25;
            var exactSeed = ReserveSeedWithFingerprint(preparedComposer,
                new string('f', 32), new string('b', 64), now);
            var seedA = new LiteHandoffPostSeedEvidence.Sample(
                newer, 8122, 78, now.AddMilliseconds(500), true,
                new string('b', 64), "webview_response_probe");
            var seedB = seedA with { Utc = now.AddMilliseconds(1800) };
            LiteHandoffPostSeedWebViewCapture.VerifyReadOnlyPair(
                seedA, seedB, now.AddSeconds(2));
            try { LiteHandoffPostSeedWebViewCapture.VerifyReadOnlyPair(
                seedA, seedB with { UserBubbleSha256 = new string('c', 64) },
                now.AddSeconds(2)); return 44; }
            catch (InvalidDataException) { }
            try { LiteHandoffPostSeedWebViewCapture.VerifyReadOnlyPair(
                seedA, seedB with { NavigationEpoch = 79 },
                now.AddSeconds(2)); return 45; }
            catch (InvalidDataException) { }
            try { LiteHandoffPostSeedWebViewCapture.VerifyReadOnlyPair(
                seedA, seedB with { RendererPid = 123 },
                now.AddSeconds(2)); return 46; }
            catch (InvalidDataException) { }
            try { LiteHandoffPostSeedWebViewCapture.VerifyReadOnlyPair(
                seedA, seedB with { Utc = seedA.Utc.AddMilliseconds(50) },
                now.AddSeconds(2)); return 47; }
            catch (InvalidDataException) { }            var readA = new LiteSuccessorDurableReadback.Sample(
                newer, 8122, now.AddMilliseconds(500), new string('b',64),
                true, "webview_response_probe");
            var readB = readA with { Utc=now.AddMilliseconds(1800) };
            var spent = MarkUncertain(exactSeed, now);
            var recovered = RecoverUncertainServerAck(
                spent, new string('f',32), newer, readA, readB,
                new[] {old}, now.AddSeconds(2));
            if (recovered.Step != Phase.SeedAcked ||
                recovered.Revision != spent.Revision+1 ||
                recovered.NewChatUrl != newer ||
                recovered.SeedReceipt != spent.SeedReceipt) return 66;
            try { RecoverUncertainServerAck(
                recovered, new string('f',32), newer, readA, readB,
                new[] {old}, now.AddSeconds(2)); return 67; }
            catch (InvalidOperationException) { }
            try { RecoverUncertainServerAck(
                spent, new string('e',32), newer, readA, readB,
                new[] {old}, now.AddSeconds(2)); return 68; }
            catch (InvalidDataException) { }
            try { RecoverUncertainServerAck(
                spent, new string('f',32), newer, readA,
                readB with {ExactAssistantReply=false},
                new[] {old}, now.AddSeconds(2)); return 69; }
            catch (InvalidDataException) { }
            try { RecoverUncertainServerAck(
                spent, new string('f',32), newer, readA,
                readB with {UserSha256=new string('a',64)},
                new[] {old}, now.AddSeconds(2)); return 70; }
            catch (InvalidDataException) { }
            try { RecoverUncertainServerAck(
                spent, new string('f',32), newer, readA, readB,
                new[] {newer}, now.AddSeconds(2)); return 71; }
            catch (InvalidDataException) { }
            try { RecoverUncertainServerAck(
                spent, new string('f',32),
                "https://chatgpt.com/c/local-chatgpt%3A1234",
                readA, readB, new[] {old}, now.AddSeconds(2)); return 72; }
            catch (InvalidDataException) { }
            if (!LiteSuccessorDurableReadback.VerifyStableCanonical(
                    readA,readB,new string('b',64),now.AddSeconds(2)))
                return 56;
            var provisionalRead=readA with {
                Url="https://chatgpt.com/c/local-chatgpt%3A26168a1f-877e-4052-aee5-18d7f350f37e"};
            if (LiteSuccessorDurableReadback.VerifyStableCanonical(
                provisionalRead,provisionalRead with {
                    Utc=now.AddMilliseconds(1800)},
                new string('b',64),now.AddSeconds(2))) return 57;
            if (LiteSuccessorDurableReadback.VerifyStableCanonical(
                readA,readB with { ExactAssistantReply=false },
                new string('b',64),now.AddSeconds(2))) return 58;
            if (LiteSuccessorDurableReadback.VerifyStableCanonical(
                readA,readB,new string('c',64),now.AddSeconds(2))) return 59;            var optimistic = seedA with {
                Url="https://chatgpt.com/c/local-chatgpt%3A26168a1f-877e-4052-aee5-18d7f350f37e"};
            try { ConfirmPostSeedVisibleAck(exactSeed,new string('f',32),
                optimistic,optimistic with {Utc=seedB.Utc},
                new[] {old},now.AddSeconds(2));return 60; }
            catch (InvalidDataException) { }            var strictBound = ConfirmPersistedAssistantAck(exactSeed,
                new string('f',32), seedA, seedB, readA, readB,
                new[] { old }, now.AddSeconds(2));
            if (strictBound.Step != Phase.SeedAcked ||
                strictBound.NewChatUrl != newer) return 61;
            try { ConfirmPersistedAssistantAck(exactSeed,
                new string('f',32), seedA, seedB,
                readA, readB with {ExactAssistantReply=false},
                new[] { old }, now.AddSeconds(2)); return 62;
            } catch (InvalidDataException) { }
            try { ConfirmPersistedAssistantAck(exactSeed,
                new string('f',32), seedA, seedB,
                readA, readB with {Url=old},
                new[] { old }, now.AddSeconds(2)); return 63;
            } catch (InvalidDataException) { }
            try { ConfirmPersistedAssistantAck(exactSeed,
                new string('f',32), seedA, seedB,
                readA, readB with {UserSha256=new string('a',64)},
                new[] { old }, now.AddSeconds(2)); return 64;
            } catch (InvalidDataException) { }
            try { ConfirmPersistedAssistantAck(exactSeed,
                new string('f',32), seedA, seedB,
                readA, readB with {BrowserPid=13},
                new[] { old }, now.AddSeconds(2)); return 65;
            } catch (InvalidDataException) { }            var bound = ConfirmPostSeedVisibleAck(exactSeed,
                new string('f', 32), seedA, seedB, new[] { old },
                now.AddSeconds(2));
            if (bound.Step != Phase.SeedAcked ||
                bound.NewChatUrl != newer || bound.Revision !=
                exactSeed.Revision + 1) return 35;
            try { ConfirmPostSeedVisibleAck(bound, new string('f', 32),
                seedA, seedB, new[] { old }, now.AddSeconds(2));
                return 36; } catch (InvalidOperationException) { }
            try { ConfirmPostSeedVisibleAck(exactSeed, new string('e', 32),
                seedA, seedB, new[] { old }, now.AddSeconds(2));
                return 37; } catch (InvalidOperationException) { }
            try { ConfirmPostSeedVisibleAck(exactSeed, new string('f', 32),
                seedA with { UserBubbleSha256 = new string('c', 64) },
                seedB, new[] { old }, now.AddSeconds(2));
                return 38; } catch (InvalidDataException) { }            try { ConfirmPostSeedVisibleAck(exactSeed, new string('f', 32),
                seedA, seedB with { Url = old }, new[] { old },
                now.AddSeconds(2)); return 39; }
            catch (InvalidDataException) { }
            try { ConfirmPostSeedVisibleAck(exactSeed, new string('f', 32),
                seedA, seedB with { RendererPid = 13 }, new[] { old },
                now.AddSeconds(2)); return 40; }
            catch (InvalidDataException) { }
            try { ConfirmPostSeedVisibleAck(exactSeed, new string('f', 32),
                seedA, seedB, new[] { newer }, now.AddSeconds(2));
                return 41; } catch (InvalidDataException) { }
            try { ConfirmPostSeedVisibleAck(exactSeed, new string('f', 32),
                seedA, seedB with { Utc = seedA.Utc.AddMilliseconds(50) },
                new[] { old }, now.AddSeconds(2));
                return 42; } catch (InvalidDataException) { }
            try { ConfirmPostSeedVisibleAck(
                ReserveSeed(preparedComposer, new string('f', 32), now),
                new string('f', 32), seedA, seedB, new[] { old },
                now.AddSeconds(2)); return 43; }
            catch (InvalidOperationException) { }            var reservedFromBlank = ReserveSeed(preparedComposer,
                new string('f', 32), now);
            if (reservedFromBlank.Step != Phase.SeedReserved ||
                MaySendOnce(reservedFromBlank)) return 26;
            try { ConfirmVisibleAck(reservedFromBlank,
                new string('f', 32), newer, true, now); return 27; }
            catch (InvalidOperationException) { }

            // End-to-end in a separate TEMP ledger: one durable CAS, no click.
            string seedLedger = Path.Combine(dir, "one-shot-ledger.json");
            PersistOnce(seedLedger, Prepare(proof, now));
            var seedLease = LiteSuccessorComposerLease.FromVerifiedPair(
                blankA, blankB, now);
            var permit = LiteHandoffOneShotReservation.Arm(
                seedLedger, 0, seedLease, blankA, blankB, fresh,
                8122, 77, new string('d', 32), new string('b', 64), now);
            var seedPersisted = Read(seedLedger);
            if (seedPersisted.Revision != 1 ||
                seedPersisted.Step != Phase.SeedReserved ||
                seedPersisted.SeedTextSha256 != new string('b', 64) ||
                !seedPersisted.SeedStartedOnBlankRoute ||
                MaySendOnce(seedPersisted)) return 48;
            if (!permit.ConsumeOnce(fresh, 8122, 77, now) ||
                permit.ConsumeOnce(fresh, 8122, 77, now)) return 49;
            try { LiteHandoffOneShotReservation.Arm(
                seedLedger, 0, seedLease, blankA, blankB, fresh,
                8122, 77, new string('e', 32), new string('b', 64), now);
                return 50; }
            catch (InvalidOperationException) { }
            // A crash before click leaves SeedReserved on disk, never auto retry.
            if (Read(seedLedger).Step != Phase.SeedReserved ||
                MaySendOnce(Read(seedLedger))) return 51;
            seedLease.Revoke();
            if (permit.ConsumeOnce(fresh, 8122, 77, now)) return 52;
            var observed = Advance(ledger, 0, s =>
                ObserveNewChatVerified(s, first, second, new[] { old }, now));
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
            const string provisional = "https://chatgpt.com/c/local-chatgpt%3A26168a1f-877e-4052-aee5-18d7f350f37e";
            if (!LiteHandoffPersistedRoute.IsServerConversation(newer) ||
                LiteHandoffPersistedRoute.IsServerConversation(provisional))
                return 53;
            try { ConfirmNewCheckpoint(
                ack with { NewChatUrl = provisional },
                handoff, dir, sha, now); return 54;
            } catch (InvalidOperationException) { }
            try { ConfirmPromotion(
                ack with { Step=Phase.NewCheckpointVerified,
                           NewChatUrl=provisional },
                false, true, true, true, now); return 55;
            } catch (InvalidOperationException) { }            var checkpoint = Advance(ledger, 4, s =>
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
