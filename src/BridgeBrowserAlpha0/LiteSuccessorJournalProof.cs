using System.Text.Json;

namespace BridgeBrowserAlpha0;

// Read-only corroboration of an already one-shot sent successor wake.
// A chat start/finish can complete uncertain delivery, but NEVER authorizes a resend.
internal static class LiteSuccessorJournalProof
{
    internal readonly record struct Evidence(string Actor, string Stage, long Number,
        string Receipt, DateTimeOffset At);

    internal static bool Verify(IEnumerable<Evidence> evidence, string receipt,
        long number, DateTimeOffset rotationStartedUtc, DateTimeOffset now)
    {
        if (receipt.Length != 32 || !receipt.All(c =>
                c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            number < 1 || rotationStartedUtc == default ||
            rotationStartedUtc > now.AddMinutes(1))
            return false;
        var rows = evidence.Where(x => x.Receipt == receipt && x.Number == number)
            .OrderBy(x => x.At).ToArray();
        var reserved = rows.FirstOrDefault(x => x.Actor == "engine" &&
            x.Stage == "attempt_reserved" &&
            x.At >= rotationStartedUtc.AddSeconds(-20) &&
            x.At <= rotationStartedUtc.AddMinutes(2));
        if (reserved == default) return false;
        var started = rows.FirstOrDefault(x => x.Actor == "chat" &&
            x.Stage == "chat_started" && x.At >= reserved.At &&
            x.At <= now.AddMinutes(1));
        if (started == default) return false;
        var finished = rows.FirstOrDefault(x => x.Actor == "chat" &&
            x.Stage == "chat_finished" && x.At >= started.At &&
            x.At <= now.AddMinutes(1));
        return finished != default;
    }

    // A persisted successor URL and a matched start/finish journal are
    // sufficient to promote without navigating the WebView2 renderer.
    // Journal-only proof must never be labeled a DOM visible_ack.
    internal static bool CanPromotePersistedRoute(
        string oldUrl, string nextUrl, bool exactJournalProof)
    {
        return exactJournalProof &&
            LiteHandoffPersistedRoute.IsServerConversation(oldUrl) &&
            LiteHandoffPersistedRoute.IsServerConversation(nextUrl) &&
            LiteDevWakeState.NormalizeUrl(oldUrl) !=
                LiteDevWakeState.NormalizeUrl(nextUrl);
    }
    internal static bool VerifyPersisted(string receipt, long number,
        DateTimeOffset rotationStartedUtc)
    {
        try
        {
            string file = LiteWakeJournal.JournalPath;
            if (!File.Exists(file)) return false;
            // Bounded memory; recent journal records only, no message content.
            var last = File.ReadLines(file).TakeLast(1800).ToArray();
            var records = new List<Evidence>();
            foreach (string line in last)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var x = doc.RootElement;
                    if (!x.TryGetProperty("receipt", out var r) ||
                        r.ValueKind != JsonValueKind.String ||
                        r.GetString() != receipt) continue;
                    if (!x.TryGetProperty("wakeSequence", out var n) ||
                        n.ValueKind != JsonValueKind.Number ||
                        !n.TryGetInt64(out long seq) || seq != number) continue;
                    if (!x.TryGetProperty("actor", out var a) ||
                        !x.TryGetProperty("stage", out var s) ||
                        !x.TryGetProperty("utc", out var ts) ||
                        !DateTimeOffset.TryParse(ts.GetString(), out var at))
                        continue;
                    records.Add(new Evidence(a.GetString() ?? "",
                        s.GetString() ?? "", seq, receipt, at));
                }
                catch (JsonException) { /* incomplete append is not evidence */ }
            }
            return Verify(records, receipt, number, rotationStartedUtc,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("rotation_chat_journal_read_failed",
                new { errorType = ex.GetType().Name, hresult = ex.HResult });
            return false;
        }
    }

    internal static int SelfTest()
    {
        var t = new DateTimeOffset(2026, 10, 29, 19, 59, 47, TimeSpan.Zero);
        const string r = "d7903f68c57f48979c67981c2f40c5f5";
        var records = new[] {
            new Evidence("engine", "attempt_reserved", 50, r, t.AddMilliseconds(500)),
            new Evidence("chat", "chat_started", 50, r, t.AddMinutes(2)),
            new Evidence("chat", "chat_finished", 50, r, t.AddMinutes(9))
        };
        var now = t.AddMinutes(13);
        const string oldUrl="https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
        const string successorUrl="https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
        if (!CanPromotePersistedRoute(oldUrl, successorUrl, true)) return 8;
        if (CanPromotePersistedRoute(oldUrl, successorUrl, false)) return 9;
        if (CanPromotePersistedRoute(oldUrl, oldUrl, true)) return 10;
        if (CanPromotePersistedRoute(oldUrl, "https://chatgpt.com/", true))
            return 11;        if (!Verify(records, r, 50, t, now)) return 1;
        if (Verify(records, r, 51, t, now)) return 2;
        if (Verify(records, new string('a',32), 50, t, now)) return 3;
        if (Verify(records.Skip(1), r, 50, t, now)) return 4;
        if (Verify(records.Take(2), r, 50, t, now)) return 5;
        if (Verify(records, r, 50, t.AddDays(1), now)) return 6;
        if (Verify(records.Select(x => x with { At = t.AddDays(-1) }),
            r, 50, t, now)) return 7;
        return 0;
    }
}
