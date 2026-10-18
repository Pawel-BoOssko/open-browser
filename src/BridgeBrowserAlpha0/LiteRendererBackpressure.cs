namespace BridgeBrowserAlpha0;

/// <summary>
/// Pure back-pressure state for renderer/JS failures. No automatic reload,
/// restart or message sending. Wall clock can be injected in offline tests.
/// </summary>
internal sealed class LiteRendererBackpressure
{
    private DateTimeOffset _blockedUntilUtc = DateTimeOffset.MinValue;
    private int _consecutiveFailures;
    internal int ConsecutiveFailures => _consecutiveFailures;
    internal DateTimeOffset BlockedUntilUtc => _blockedUntilUtc;

    internal TimeSpan MarkFailed(DateTimeOffset now)
    {
        _consecutiveFailures = Math.Min(_consecutiveFailures + 1, 5);
        var seconds = Math.Min(300, 30 * (1 << (_consecutiveFailures - 1)));
        var span = TimeSpan.FromSeconds(seconds);
        _blockedUntilUtc = now + span;
        return span;
    }

    internal bool MayProbe(DateTimeOffset now) => now >= _blockedUntilUtc;

    internal void MarkNavigationRecovered()
    {
        _blockedUntilUtc = DateTimeOffset.MinValue;
        _consecutiveFailures = 0;
    }
}
