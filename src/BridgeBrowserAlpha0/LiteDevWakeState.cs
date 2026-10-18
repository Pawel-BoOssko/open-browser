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
        if (answerHash.Length != 64 || !answerHash.All(Uri.IsHexDigit))
            return "answer_missing";
        if (!high) return "high_not_verified";
        if (!assistant) return "assistant_not_last";
        if (generating || tools || draft || error || !composerReady) return "not_idle";
        if (state.LastAttemptHash == answerHash) return "same_answer_already_woken";
        if (state.LastObservedHash != answerHash ||
            now - state.FirstObservedUtc < TimeSpan.FromSeconds(45))
            return "wait_stable";
        if (state.RateWindowUtc != default &&
            now - state.RateWindowUtc < TimeSpan.FromDays(1) &&
            state.AttemptsInWindow >= 40) return "daily_limit";
        return "ready";
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
            LiteEventLog.Write("dev_wake_selftest", new { passed = true, checks = 10 });
            return 0;
        }
        catch (Exception ex) {
            LiteEventLog.Write("dev_wake_selftest", new { passed = false, errorType = ex.GetType().Name });
            return 15;
        }
    }
}
