using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

/// <summary>
/// DPAPI-protected, single-chat development wake configuration and receipt.
/// This is not a ChatGPT scheduled task. Only a running interactive Lite window
/// can issue a wake. A submitted wake is never resent for the same answer hash.
/// </summary>
internal static class LiteDevWakeState
{
    internal sealed class Record
    {
        public int Schema { get; set; } = 1;
        public bool Enabled { get; set; }
        public string TargetUrl { get; set; } = "";
        public int IntervalMinutes { get; set; } = 25;
        public DateTimeOffset NextDueUtc { get; set; }
        public string LastAttemptHash { get; set; } = "";
        public string LastReceiptId { get; set; } = "";
        public DateTimeOffset LastAttemptUtc { get; set; }
        public string LastConfirmedReceiptId { get; set; } = "";
        public string LastObservedHash { get; set; } = "";
        public DateTimeOffset FirstObservedUtc { get; set; }
        public DateTimeOffset RateWindowUtc { get; set; }
        public int AttemptsInWindow { get; set; }
        public string LastState { get; set; } = "disabled";
    }

    internal const string WakeText =
        "Hej, wróć do rozwoju Open Browser. Otwórz własny plan " +
        @"D:\projects\open-browser\docs\design\openbrowser-master-roadmap-v3-2026-10-19.md" +
        " i najnowszy checkpoint. Samodzielnie wybierz kolejne niewykonane zadania " +
        "i realizuj je. Zapisuj postęp. Jeśli wszystko jest ukończone, " +
        "nie wymyślaj nowych zadań i napisz [OPENBROWSER-PLAN-COMPLETE].";

    internal static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "dev-wake-settings.dpapi");

    // Explicit operator-controlled handoff to the currently open conversation.
    // Switching conversations starts a clean attempt ledger for the new chat.
    internal static Record ArmForChat(Record current, string url,
        DateTimeOffset now)
    {
        string normalized = NormalizeUrl(url);
        var next = string.Equals(current.TargetUrl, normalized, StringComparison.Ordinal)
            ? current : new Record { TargetUrl = normalized };
        next.Enabled = true;
        next.IntervalMinutes = 25;
        next.NextDueUtc = now.AddMinutes(25);
        next.LastState = "armed";
        return next;
    }

    internal static bool IsChatUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
        return Regex.IsMatch(uri.AbsolutePath,
            @"^/c/[a-z0-9-]{30,80}$", RegexOptions.CultureInvariant);
    }

    internal static string NormalizeUrl(string url)
    {
        if (!IsChatUrl(url)) throw new ArgumentException("Not a ChatGPT chat URL");
        return new Uri(url).GetLeftPart(UriPartial.Path);
    }

    internal static Record Read()
    {
        if (!File.Exists(FilePath)) return new Record();
        var raw = File.ReadAllBytes(FilePath);
        var clear = ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser);
        var state = JsonSerializer.Deserialize<Record>(clear)
                    ?? throw new InvalidDataException("Invalid wake settings");
        if (state.Schema != 1 || state.IntervalMinutes is < 20 or > 60 ||
            state.AttemptsInWindow is < 0 or > 48 ||
            (state.Enabled && !IsChatUrl(state.TargetUrl)))
            throw new InvalidDataException("Invalid wake configuration");
        return state;
    }

    internal static void Write(Record state)
    {
        if (state.Schema != 1 || state.IntervalMinutes is < 20 or > 60 ||
            (state.Enabled && !IsChatUrl(state.TargetUrl)))
            throw new InvalidDataException("Invalid wake configuration");
        var clear = JsonSerializer.SerializeToUtf8Bytes(state);
        var cipher = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        var tmp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllBytes(tmp, cipher);
            File.Move(tmp, FilePath, overwrite: true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    internal static string Eligibility(Record state, string? url,
        DateTimeOffset now, string answerHash, bool high, bool assistant,
        bool generating, bool tools, bool draft, bool error, bool composerReady)
    {
        if (!state.Enabled) return "disabled";
        if (!IsChatUrl(url) ||
            NormalizeUrl(url!) != NormalizeUrl(state.TargetUrl)) return "other_chat";
        if (now < state.NextDueUtc) return "not_due";
        // A last user turn with no assistant text is not an empty assistant reply.
        // Classify it first for actionable, content-free wake diagnostics.
        if (!assistant) return "assistant_not_last";
        if (answerHash.Length != 64 || !answerHash.All(Uri.IsHexDigit))
            return "answer_missing";
        if (!high) return "high_not_verified";
        if (generating || tools || draft || error || !composerReady) return "not_idle";
        // A delivery with unknown outcome must not be automatically repeated.
        // Resume work only after a NEW completed assistant answer is observed.
        if (state.LastAttemptHash == answerHash)
            return "same_answer_already_woken";
        if (state.LastObservedHash != answerHash ||
            now - state.FirstObservedUtc < TimeSpan.FromSeconds(45))
            return "wait_stable";
        if (state.RateWindowUtc != default &&
            now - state.RateWindowUtc < TimeSpan.FromDays(1) &&
            state.AttemptsInWindow >= 40) return "daily_limit";
        return "ready";
    }

    internal static bool MayRetryUncertain(Record state) =>
        state.LastReceiptId.Length == 32 &&
        state.LastReceiptId != state.LastConfirmedReceiptId &&
        (state.LastState == "sending_uncertain" ||
         state.LastState.StartsWith("uncertain_", StringComparison.Ordinal) ||
         state.LastState == "exception");

    internal static bool RecoverUncertainAfterRestart(Record state,
        DateTimeOffset now, bool persist = true)
    {
        if (state.Enabled || !IsChatUrl(state.TargetUrl) ||
            !MayRetryUncertain(state) ||
            state.LastAttemptUtc == default ||
            state.LastAttemptUtc > now.AddMinutes(2)) return false;
        state.Enabled = true;
        state.NextDueUtc = now.AddMinutes(state.IntervalMinutes);
        state.LastObservedHash = "";
        state.FirstObservedUtc = default;
        state.LastState = "uncertain_renderer_timeout"; // retains retry eligibility
        if (persist) Write(state);
        return true;
    }

    internal static void DeferUncertain(Record state, DateTimeOffset now,
        string cause, bool persist = true)
    {
        if (!state.Enabled || !MayRetryUncertain(state))
            throw new InvalidOperationException("Expected pending uncertain wake");
        state.LastState = "uncertain_" + cause;
        state.NextDueUtc = now.AddMinutes(state.IntervalMinutes);
        state.LastObservedHash = "";
        state.FirstObservedUtc = default;
        if (persist) Write(state);
    }

    // A late DOM acknowledgment may resolve an uncertain delivery, but never
    // authorizes another send for the same assistant answer.
    internal static bool CanReconcileReceipt(Record s, DateTimeOffset now) =>
        s.Enabled && MayRetryUncertain(s) &&
        s.LastAttemptUtc != default &&
        now >= s.LastAttemptUtc &&
        now - s.LastAttemptUtc <= TimeSpan.FromHours(2) &&
        s.LastAttemptHash.Length == 64 && s.LastAttemptHash.All(Uri.IsHexDigit);

    internal static bool ConfirmVisibleReceipt(Record s, string receipt,
        DateTimeOffset now, bool persist = true)
    {
        if (!CanReconcileReceipt(s, now) ||
            !string.Equals(s.LastReceiptId, receipt, StringComparison.Ordinal))
            return false;
        s.LastConfirmedReceiptId = receipt;
        s.LastState = "confirmed_late";
        if (persist) Write(s);
        return true;
    }

    internal static bool ContainsCompletionMarker(string answer) =>
        answer.Contains("[OPENBROWSER-PLAN-COMPLETE]", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("[OPENBROWSER-NEEDS-USER]", StringComparison.OrdinalIgnoreCase);

    internal static void CommitAttempt(Record s, DateTimeOffset now,
        string answerHash, string receipt)
    {
        if (receipt.Length != 32 || !receipt.All(Uri.IsHexDigit))
            throw new ArgumentException("Bad wake receipt");
        if (s.RateWindowUtc == default ||
            now - s.RateWindowUtc >= TimeSpan.FromDays(1)) {
            s.RateWindowUtc = now;
            s.AttemptsInWindow = 0;
        }
        s.AttemptsInWindow++;
        s.LastAttemptHash = answerHash;
        s.LastReceiptId = receipt;
        s.LastAttemptUtc = now;
        s.NextDueUtc = now.AddMinutes(s.IntervalMinutes);
        s.LastObservedHash = "";
        s.FirstObservedUtc = default;
        s.LastState = "sending_uncertain";
        Write(s); // disk BEFORE WebView ExecuteScriptAsync: no crash retry
    }

    /// <summary>
    /// Sender's "busy" is definitive pre-send rejection: its JavaScript
    /// returns before marking a receipt attempted or touching the editor.
    /// No network submission occurred. Allow a later fresh stable observation,
    /// rather than permanently disabling the development wake.
    /// Do not use this method for "unsupported", timeout, or uncertain results.
    /// </summary>
    internal static void DeferDefinitelyUnsentBusy(Record s, string receipt,
        DateTimeOffset now, bool persist = true)
    {
        if (s.LastState != "sending_uncertain" ||
            s.LastReceiptId != receipt ||
            receipt.Length != 32 || !receipt.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Wake attempt receipt/state mismatch");
        s.LastAttemptHash = "";
        s.LastReceiptId = "";
        s.LastAttemptUtc = default;
        s.LastObservedHash = "";
        s.FirstObservedUtc = default;
        s.AttemptsInWindow = Math.Max(0, s.AttemptsInWindow - 1);
        s.NextDueUtc = now.AddMinutes(2);
        s.LastState = "deferred_busy_before_send";
        if (persist) Write(s);
    }

    /// <summary>
    /// v1.68 persisted "uncertain_send_busy" even though ResumeSender.send
    /// returned before any DOM modification. Recover ONLY that exact proven
    /// legacy pre-send condition, within one day of the failed attempt.
    /// Never recover renderer timeouts, unsupported, or no-ACK states.
    /// </summary>
    internal static bool RecoverLegacyDefinitelyNotSent(Record s,
        DateTimeOffset now, bool persist = true)
    {
        if (s.Enabled || s.LastState != "uncertain_send_busy" ||
            s.LastReceiptId.Length != 32 ||
            !s.LastReceiptId.All(Uri.IsHexDigit) ||
            s.LastAttemptHash.Length != 64 ||
            !s.LastAttemptHash.All(Uri.IsHexDigit) ||
            s.LastAttemptUtc > now.AddMinutes(2) ||
            s.LastAttemptUtc < now.AddDays(-1) ||
            s.LastConfirmedReceiptId == s.LastReceiptId ||
            !IsChatUrl(s.TargetUrl))
            return false;
        string receipt = s.LastReceiptId;
        s.LastState = "sending_uncertain";
        DeferDefinitelyUnsentBusy(s, receipt, now, persist: false);
        s.Enabled = true;
        s.LastState = "legacy_busy_recovered";
        if (persist) Write(s);
        return true;
    }

    internal static int SelfTest()
    {
        try {
            var now = DateTimeOffset.UtcNow;
            var s = new Record {
                Enabled = true,
                TargetUrl = "https://chatgpt.com/c/00000000-1111-2222-3333-444444444444",
                IntervalMinutes = 25,
                NextDueUtc = now.AddSeconds(-1),
                LastObservedHash = new string('a', 64),
                FirstObservedUtc = now.AddSeconds(-70)
            };
            var good = s.TargetUrl;
            var hash = s.LastObservedHash;
            if (Eligibility(s, good, now, hash, true, true, false, false, false, false, true) != "ready") return 2;
            if (Eligibility(s, good.Replace("00000000", "99999999"), now, hash, true, true, false, false, false, false, true) != "other_chat") return 3;
            if (Eligibility(s, good, now, hash, false, true, false, false, false, false, true) != "high_not_verified") return 4;
            if (Eligibility(s, good, now, "", true, false, false, false, false, false, true) != "assistant_not_last") return 31;
            if (Eligibility(s, good, now, "", true, true, false, false, false, false, true) != "answer_missing") return 32;
            if (Eligibility(s, good, now, hash, true, true, true, false, false, false, true) != "not_idle") return 5;
            if (Eligibility(s, good, now, hash, true, true, false, false, true, false, true) != "not_idle") return 6;
            if (Eligibility(s, good, now, hash, true, true, false, false, false, true, true) != "not_idle") return 7;
            s.LastAttemptHash = hash;
            if (Eligibility(s, good, now, hash, true, true, false, false, false, false, true) != "same_answer_already_woken") return 8;
            s.LastAttemptHash = "";
            s.FirstObservedUtc = now;
            if (Eligibility(s, good, now, hash, true, true, false, false, false, false, true) != "wait_stable") return 9;
            if (!ContainsCompletionMarker("Wszystko zakończone [OPENBROWSER-PLAN-COMPLETE]")) return 10;
            if (ContainsCompletionMarker("Wykonano jeszcze 4 kroki, są następne.")) return 11;
            var receipt = new string('a', 32);
            s.LastState = "sending_uncertain";
            s.LastReceiptId = receipt;
            s.LastAttemptHash = hash;
            s.AttemptsInWindow = 1;
            DeferDefinitelyUnsentBusy(s, receipt, now, persist: false);
            if (!s.Enabled || s.LastState != "deferred_busy_before_send" ||
                s.LastAttemptHash.Length > 0 || s.AttemptsInWindow != 0 ||
                s.NextDueUtc != now.AddMinutes(2)) return 12;
            if (Eligibility(s, good, now.AddMinutes(2), hash, true, true,
                    false, false, false, false, true) != "wait_stable") return 13;
            bool mismatchRejected = false;
            try { DeferDefinitelyUnsentBusy(s, receipt, now, persist: false); }
            catch (InvalidOperationException) { mismatchRejected = true; }
            if (!mismatchRejected) return 14;
            var legacy = new Record {
                Enabled = false,
                TargetUrl = good,
                LastState = "uncertain_send_busy",
                LastReceiptId = receipt,
                LastAttemptHash = hash,
                LastAttemptUtc = now,
                AttemptsInWindow = 1
            };
            if (!RecoverLegacyDefinitelyNotSent(legacy, now, persist: false) ||
                !legacy.Enabled || legacy.LastAttemptHash.Length != 0 ||
                legacy.LastReceiptId.Length != 0 ||
                legacy.LastState != "legacy_busy_recovered" ||
                legacy.AttemptsInWindow != 0) return 15;
            if (RecoverLegacyDefinitelyNotSent(legacy, now, persist: false))
                return 16;
            legacy.Enabled = false;
            legacy.LastState = "uncertain_renderer_timeout";
            legacy.LastReceiptId = receipt;
            legacy.LastAttemptHash = hash;
            legacy.LastAttemptUtc = now;
            if (RecoverLegacyDefinitelyNotSent(legacy, now, persist: false))
                return 17;
            var amb = new Record {
                Enabled = true, TargetUrl = good, IntervalMinutes = 25,
                LastState = "uncertain_no_ack",
                LastReceiptId = new string('b', 32),
                LastAttemptHash = hash,
                LastAttemptUtc = now.AddMinutes(-26),
                NextDueUtc = now.AddSeconds(-1),
                LastObservedHash = hash,
                FirstObservedUtc = now.AddMinutes(-2)
            };
            if (Eligibility(amb, good, now, hash, true, true,
                false, false, false, false, true) != "same_answer_already_woken") return 18;
            if (!MayRetryUncertain(amb)) return 19;
            DeferUncertain(amb, now, "renderer_timeout", persist: false);
            if (!amb.Enabled || amb.LastState != "uncertain_renderer_timeout" ||
                amb.NextDueUtc != now.AddMinutes(25)) return 20;
            amb.Enabled = false;
            amb.LastAttemptUtc = now.AddMinutes(-3);
            if (!RecoverUncertainAfterRestart(amb, now, persist: false) ||
                !amb.Enabled || amb.NextDueUtc != now.AddMinutes(25)) return 21;
            var nextAnswerHash = new string('c', 64);
            amb.LastObservedHash = nextAnswerHash;
            amb.FirstObservedUtc = now.AddMinutes(20);
            if (Eligibility(amb, good, now.AddMinutes(26), nextAnswerHash,
                true, true, false, false, false, false, true) != "ready") return 22;
            if (Eligibility(amb, good, now.AddMinutes(26), hash,
                true, true, false, false, false, false, true) != "same_answer_already_woken") return 23;
            amb.LastConfirmedReceiptId = amb.LastReceiptId;
            if (MayRetryUncertain(amb)) return 24;
            var late = new Record {
                Enabled = true, TargetUrl = good, IntervalMinutes = 25,
                LastState = "uncertain_no_ack",
                LastReceiptId = new string('d', 32),
                LastAttemptHash = hash,
                LastAttemptUtc = now.AddMinutes(-4),
                NextDueUtc = now.AddMinutes(21)
            };
            if (!CanReconcileReceipt(late, now)) return 25;
            if (ConfirmVisibleReceipt(late, new string('e', 32), now, persist: false)) return 26;
            if (!ConfirmVisibleReceipt(late, late.LastReceiptId, now, persist: false) ||
                late.LastConfirmedReceiptId != late.LastReceiptId ||
                late.LastState != "confirmed_late") return 27;
            if (ConfirmVisibleReceipt(late, late.LastReceiptId, now, persist: false)) return 28;
            if (Eligibility(late, good, now.AddMinutes(30), hash,
                true, true, false, false, false, false, true) !=
                "same_answer_already_woken") return 29;
            late.LastState = "uncertain_no_ack";
            late.LastConfirmedReceiptId = "";
            late.LastAttemptUtc = now.AddHours(-3);
            if (CanReconcileReceipt(late, now)) return 30;
            var oldBound = new Record {
                Enabled = true, TargetUrl = good,
                IntervalMinutes = 25, AttemptsInWindow = 4,
                LastReceiptId = receipt, LastAttemptHash = hash,
                LastState = "direct_submission_requested"
            };
            string switchedUrl = good.Replace("00000000", "99999999");
            var retargeted = ArmForChat(oldBound, switchedUrl, now);
            if (ReferenceEquals(retargeted, oldBound) ||
                retargeted.TargetUrl != switchedUrl ||
                !retargeted.Enabled || retargeted.IntervalMinutes != 25 ||
                retargeted.NextDueUtc != now.AddMinutes(25) ||
                retargeted.AttemptsInWindow != 0 ||
                retargeted.LastReceiptId.Length != 0 ||
                retargeted.LastAttemptHash.Length != 0 ||
                retargeted.LastState != "armed") return 31;
            var rearmed = ArmForChat(retargeted, switchedUrl, now.AddMinutes(3));
            if (!ReferenceEquals(rearmed, retargeted) ||
                rearmed.NextDueUtc != now.AddMinutes(28) ||
                rearmed.LastState != "armed") return 32;
            bool invalidChatRejected = false;
            try { ArmForChat(rearmed, "https://example.org/c/not-a-chat", now); }
            catch (ArgumentException) { invalidChatRejected = true; }
            if (!invalidChatRejected) return 33;
            LiteEventLog.Write("dev_wake_selftest", new { passed = true, checks = 33 });
            return 0;
        }
        catch (Exception ex) {
            LiteEventLog.Write("dev_wake_selftest", new { passed = false, errorType = ex.GetType().Name });
            return 15;
        }
    }
}
