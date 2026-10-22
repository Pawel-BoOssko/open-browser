namespace BridgeBrowserAlpha0;

/// <summary>
/// Only automatic conversation-reading probes are delayed after a real
/// GET /backend-api/...conversation... HTTP 429. Does not retry messages,
/// intercept responses, throttle foreground browsing or alter scheduling.
/// </summary>
internal sealed class LiteHttpReadThrottle
{
    private long _untilUtcTicks;
    internal static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(3);

    internal DateTimeOffset Record429(DateTimeOffset now)
    {
        long wanted = now.Add(Cooldown).UtcTicks;
        while (true)
        {
            long existing = System.Threading.Interlocked.Read(ref _untilUtcTicks);
            if (existing >= wanted) return new DateTimeOffset(existing, TimeSpan.Zero);
            long prev = System.Threading.Interlocked.CompareExchange(
                ref _untilUtcTicks, wanted, existing);
            if (prev == existing) return new DateTimeOffset(wanted, TimeSpan.Zero);
        }
    }

    internal TimeSpan Remaining(DateTimeOffset now)
    {
        long ticks = System.Threading.Interlocked.Read(ref _untilUtcTicks) - now.UtcTicks;
        return ticks > 0 ? TimeSpan.FromTicks(ticks) : TimeSpan.Zero;
    }

    internal static bool IsAutomatedRead429(
        int status, string method, string endpoint, string pathKind) =>
        status == 429 && method == "GET" && endpoint == "conversation" &&
        pathKind is "conversation_list" or "conversation_detail" or "conversation_other";

    internal static int SelfTest()
    {
        var clock = DateTimeOffset.Parse("2026-10-22T16:00:00Z");
        var limit = new LiteHttpReadThrottle();
        if (limit.Remaining(clock) != TimeSpan.Zero) return 1;
        if (!IsAutomatedRead429(429, "GET", "conversation", "conversation_detail") ||
            !IsAutomatedRead429(429, "GET", "conversation", "conversation_list") ||
            IsAutomatedRead429(429, "POST", "conversation", "conversation_root") ||
            IsAutomatedRead429(404, "GET", "conversation", "conversation_detail") ||
            IsAutomatedRead429(429, "GET", "other_backend", "other_backend"))
            return 2;
        if (limit.Record429(clock) != clock.AddMinutes(3) ||
            limit.Remaining(clock.AddMinutes(1)) != TimeSpan.FromMinutes(2))
            return 3;
        if (limit.Record429(clock.AddMinutes(1)) != clock.AddMinutes(4) ||
            limit.Remaining(clock.AddMinutes(3)) != TimeSpan.FromMinutes(1))
            return 4;
        // A stale/earlier callback must not shorten the active cooldown.
        if (limit.Record429(clock.AddMinutes(-1)) != clock.AddMinutes(4))
            return 5;
        if (limit.Remaining(clock.AddMinutes(4)) != TimeSpan.Zero)
            return 6;
        return 0;
    }
}
