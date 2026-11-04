using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Receipt loss is not proof of non-delivery. Only a wake may be retried.
/// A durable, precisely matched fence protects the sole current project lead.
/// </summary>
internal static class LiteRotationRetryPolicy
{
    internal static readonly TimeSpan ReinspectionBudget = TimeSpan.FromMinutes(3);
    internal static readonly TimeSpan OblReinspectionBudget = TimeSpan.FromMinutes(2);

    internal static bool MayReprepare(string phase, DateTimeOffset startedUtc,
        DateTimeOffset nowUtc, bool hasConfirmedProof, bool isOblLead = false, string? savedServerUrl = null)
    {
        // P0 2026-11-02: NIEAKTUALNE for OBL: retry after a known successor server URL exists.
        if (isOblLead && LiteHandoffPersistedRoute.IsServerConversation(savedServerUrl ?? "")) return false;
        if (phase != "send_uncertain" || hasConfirmedProof) return false;
        if (startedUtc == default || startedUtc > nowUtc) return true;
        // For OBL, receipt uncertainty is not a long-running send fence.
        // Retry ONLY the numbered wake, after a bounded evidence inspection.
        TimeSpan budget = isOblLead ? OblReinspectionBudget : ReinspectionBudget;
        return nowUtc - startedUtc >= budget;
    }

    internal static bool SameOutstandingFence(
        LiteChatWakeRegistry.Chat observed, LiteChatWakeRegistry.Chat due)
    {
        return observed.Enabled && due.Enabled &&
            observed.Url == due.Url &&
            observed.WakeSequence == due.WakeSequence &&
            observed.RotationPhase == "send_uncertain" &&
            due.RotationPhase == "send_uncertain" &&
            observed.RotationReceipt.Length == 32 &&
            observed.RotationReceipt == due.RotationReceipt &&
            observed.RotationStartedUtc == due.RotationStartedUtc &&
            observed.RotationSeedSha256 == due.RotationSeedSha256 &&
            observed.RotationServerUrl == due.RotationServerUrl;
    }

    // This exact in-memory transition is used by production after reading
    // current persisted state, and by the isolated persistence self-test.
    internal static bool TryRearm(LiteChatWakeRegistry.Chat observed,
        LiteChatWakeRegistry.Chat due, DateTimeOffset nowUtc,
        bool hasConfirmedProof = false, bool isOblLead = false)
    {
        // P0 OBL: saved server route proves a chat already exists. 
        // NIEAKTUALNE: rearm after 2m and create another successor.
        if (isOblLead && LiteHandoffPersistedRoute.IsServerConversation(due.RotationServerUrl))
            return false;
        if (!MayReprepare(due.RotationPhase, due.RotationStartedUtc,
            nowUtc, hasConfirmedProof, isOblLead, due.RotationServerUrl) ||
            !MayReprepare(observed.RotationPhase, observed.RotationStartedUtc,
            nowUtc, hasConfirmedProof, isOblLead, observed.RotationServerUrl) ||
            !SameOutstandingFence(observed, due))
            return false;
        observed.RotationPhase = "preparing";
        observed.RotationServerUrl = "";
        observed.RotationStartedUtc = nowUtc;
        // Keep the old receipt/seed SHA for guarded draft cleanup.
        // Next successful seed allocates a distinct receipt.
        return true;
    }

    internal static int SelfTest()
    {
        var now = new DateTimeOffset(2026, 10, 31, 0, 30, 0, TimeSpan.Zero);
        var start = now.AddMinutes(-3);
        if (!MayReprepare("send_uncertain", start, now, false)) return 1;
        if (MayReprepare("send_uncertain", start, now, true)) return 2;
        if (MayReprepare("send_uncertain", now.AddMinutes(-2), now, false)) return 3;
        if (MayReprepare("preparing", start, now, false)) return 4;
        if (MayReprepare("verified", start, now, false)) return 5;
        if (!MayReprepare("send_uncertain", default, now, false)) return 6;
        if (!MayReprepare("send_uncertain", now.AddMinutes(1), now, false)) return 7;
        if (MayReprepare("send_uncertain", now.AddSeconds(-179), now, false)) return 8;
        if (MayReprepare("send_uncertain", now.AddSeconds(-119), now, false,
                isOblLead:true) ||
            !MayReprepare("send_uncertain", now.AddMinutes(-2), now, false,
                isOblLead:true) ||
            MayReprepare("send_uncertain", now.AddMinutes(-2), now, true,
                isOblLead:true)) return 24;

        const string a = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
        if (MayReprepare("send_uncertain", start, now, false, isOblLead:true, savedServerUrl:a)) return 27;
        if (!MayReprepare("send_uncertain", start, now, false, isOblLead:false, savedServerUrl:a)) return 28;
        var due = new LiteChatWakeRegistry.Chat {
            Url=a, Enabled=true, WakeSequence=155,
            RotationPhase="send_uncertain",
            RotationReceipt="0123456789abcdef0123456789abcdef",
            RotationSeedSha256=new string('a', 64),
            RotationStartedUtc=start
        };
        static LiteChatWakeRegistry.Chat Clone(LiteChatWakeRegistry.Chat row) =>
            JsonSerializer.Deserialize<LiteChatWakeRegistry.Chat>(
                JsonSerializer.Serialize(row)) ??
                throw new InvalidDataException("Missing cloned retry fixture");
        var candidate=Clone(due);
        if (!SameOutstandingFence(candidate,due)) return 9;
        if (TryRearm(Clone(due),due,now.AddSeconds(-1))) return 10;
        if (TryRearm(Clone(due),due,now,true)) return 11;
        var mismatch=Clone(due);
        mismatch.RotationPhase="verified";
        if (TryRearm(mismatch,due,now)) return 12;
        mismatch=Clone(due);
        mismatch.RotationReceipt="abcdef0123456789abcdef0123456789";
        if (TryRearm(mismatch,due,now)) return 13;
        mismatch=Clone(due);
        mismatch.WakeSequence++;
        if (TryRearm(mismatch,due,now)) return 14;
        mismatch=Clone(due);
        mismatch.Url="https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
        if (TryRearm(mismatch,due,now)) return 15;
        mismatch=Clone(due);
        mismatch.Enabled=false;
        if (TryRearm(mismatch,due,now)) return 16;
        mismatch=Clone(due);
        mismatch.RotationStartedUtc=now.AddMinutes(-2);
        if (TryRearm(mismatch,due,now)) return 17;
        mismatch=Clone(due);
        mismatch.RotationServerUrl=
            "https://chatgpt.com/c/33333333-3333-3333-3333-333333333333";
        if (TryRearm(mismatch,due,now)) return 18;
        mismatch=Clone(due);
        mismatch.RotationSeedSha256=new string('b',64);
        if (TryRearm(mismatch,due,now)) return 19;

        var oblDue=Clone(due);
        oblDue.RotationStartedUtc=now.AddMinutes(-2);
        if (TryRearm(Clone(oblDue), oblDue, now)) return 25;
        var oblCandidate=Clone(oblDue);
        if (!TryRearm(oblCandidate, oblDue, now, isOblLead:true) ||
            oblCandidate.RotationPhase!="preparing" ||
            oblCandidate.RotationReceipt!=oblDue.RotationReceipt ||
            oblCandidate.WakeSequence!=oblDue.WakeSequence) return 26;

        if (!TryRearm(candidate,due,now)) return 20;
        if (candidate.RotationPhase!="preparing" ||
            candidate.RotationReceipt!=due.RotationReceipt ||
            candidate.RotationSeedSha256!=due.RotationSeedSha256 ||
            candidate.WakeSequence!=due.WakeSequence ||
            candidate.Url!=due.Url || !candidate.Enabled ||
            candidate.RotationStartedUtc!=now ||
            candidate.RotationServerUrl!="") return 21;
        if (TryRearm(candidate,due,now.AddMinutes(3))) return 22;
        string path=Path.Combine(Path.GetTempPath(),
            "obl-retry-isolated-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            File.WriteAllText(path,JsonSerializer.Serialize(candidate));
            var persisted=JsonSerializer.Deserialize<LiteChatWakeRegistry.Chat>(
                File.ReadAllText(path));
            if (persisted is null || persisted.RotationPhase!="preparing" ||
                persisted.RotationReceipt!=due.RotationReceipt ||
                persisted.RotationStartedUtc!=now ||
                persisted.WakeSequence!=155 ||
                !persisted.Enabled) return 23;
        }
        finally
        {
            try { File.Delete(path); } catch(IOException) { }
        }
        return 0;
    }
}
