using System.Globalization;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Optional compact marker on the last line of a stable assistant answer.
/// No valid marker -> no change to normal safe wake operation.
/// </summary>
internal static class LiteModelStatus
{
    internal sealed record Parsed(string Code, DateTimeOffset? UntilUtc);

    private static readonly Regex Marker = new(
        @"^\[OB-STATUS: (CONTINUE|WAIT_USER|WAIT_UNTIL|BLOCKED|DONE)(?: ([^\]\r\n]+))?\]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static Parsed? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string[] lines = text.TrimEnd().Split('\n');
        string line = lines[^1].TrimEnd('\r');
        var match = Marker.Match(line);
        if (!match.Success) return null;
        int fences = 0;
        for (int i = 0; i < lines.Length - 1; i++)
            if (lines[i].TrimStart().StartsWith("\u0060\u0060\u0060",
                StringComparison.Ordinal)) fences++;
        if ((fences & 1) != 0) return null;
        string code = match.Groups[1].Value;
        string value = match.Groups[2].Value;
        if (code != "WAIT_UNTIL")
            return value.Length == 0 ? new Parsed(code, null) : null;
        if (value.Length > 35 ||
            !DateTimeOffset.TryParseExact(value,
                new[] { "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.fffK" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
            return null;
        if (!value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) &&
            !System.Text.RegularExpressions.Regex.IsMatch(value,
                @"[+-]\d\d:\d\d$")) return null;
        return new Parsed(code, when.ToUniversalTime());
    }

    internal static string? ApplyDecision(Parsed? status, DateTimeOffset now,
        string preflightReason)
    {
        if (status is null || preflightReason != "ready") return null;
        if (status.Code == "WAIT_UNTIL")
        {
            var delay = status.UntilUtc - now;
            return delay >= TimeSpan.FromMinutes(1) &&
                delay <= TimeSpan.FromDays(30) ? "WAIT_UNTIL" : null;
        }
        return status.Code;
    }

    internal static bool ShouldRecoverBlock(string? decision, bool obLead) =>
        obLead && decision == "BLOCKED";

    internal static bool IsBlockedRetryDue(string? lastState,
        string? lastKey, string currentKey) =>
        lastState == "status_blocked_recovering" &&
        !string.IsNullOrEmpty(currentKey) &&
        string.Equals(lastKey, currentKey, StringComparison.Ordinal);

    internal static int SelfTest()
    {
        var t = DateTimeOffset.Parse("2026-10-22T17:00:00Z");
        foreach (string code in new[] { "CONTINUE", "WAIT_USER", "BLOCKED", "DONE" })
        {
            var result = Parse("Odpowiedź końcowa\n[OB-STATUS: " + code + "]");
            if (result is null || result.Code != code ||
                ApplyDecision(result, t, "ready") != code) return 1;
            if (ApplyDecision(result, t, "generating") is not null) return 2;
        }
        var hold = Parse("Kolejny etap jutro\n[OB-STATUS: WAIT_UNTIL 2026-10-23T12:00:00Z]");
        if (hold?.UntilUtc != DateTimeOffset.Parse("2026-10-23T12:00:00Z") ||
            ApplyDecision(hold, t, "ready") != "WAIT_UNTIL") return 3;
        if (Parse("Odpowiedź bez znacznika") is not null ||
            Parse("Tu jest [OB-STATUS: DONE] w treści.\nKontynuuję.") is not null ||
            Parse("\u0060\u0060\u0060txt\n[OB-STATUS: DONE]") is not null ||
            Parse("[OB-STATUS: WAIT_UNTIL tomorrow]") is not null ||
            Parse("[OB-STATUS: DONE now]") is not null ||
            Parse("[OB-STATUS: BAN]") is not null ||
            Parse("[OB-STATUS: WAIT_UNTIL 2026-10-23T12:00:00]") is not null)
            return 4;
        if (ApplyDecision(hold, DateTimeOffset.Parse("2026-11-24T00:00:00Z"), "ready")
            is not null) return 5;
        if (ApplyDecision(hold, t, "wrong_chat") is not null) return 6;
        if (!ShouldRecoverBlock("BLOCKED", true) ||
            ShouldRecoverBlock("BLOCKED", false) ||
            ShouldRecoverBlock("WAIT_USER", true) ||
            ShouldRecoverBlock("DONE", true)) return 7;
        if (IsBlockedRetryDue("status_blocked", "a", "a") ||
            IsBlockedRetryDue("status_blocked_recovering", "a", "b") ||
            !IsBlockedRetryDue("status_blocked_recovering", "a", "a"))
            return 8;
        return 0;
    }
}
