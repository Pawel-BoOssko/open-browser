namespace BridgeBrowserAlpha0;

/// <summary>
/// Pure planning policy for future per-chat WebView2 residency.
/// Never destroys, navigates or suspends a renderer. Protected tabs
/// may temporarily exceed the memory budget until they become idle.
/// </summary>
internal static class LiteTabResidencyPolicy
{
    internal sealed record Tab(
        string ChatKey, bool Active, bool Generating, bool ToolRunning,
        bool HasDraft, DateTimeOffset LastUsedUtc);

    internal sealed record Decision(
        int WarmLimit, IReadOnlyList<string> KeepAlive,
        IReadOnlyList<string> ParkWhenSafe, bool ProtectedOverBudget);

    internal static Decision Evaluate(
        IReadOnlyList<Tab> tabs, long availableMiB, double commitRatio)
    {
        if (availableMiB < 0 || !double.IsFinite(commitRatio) ||
            commitRatio < 0 || commitRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(availableMiB));
        if (tabs.Any(t => string.IsNullOrWhiteSpace(t.ChatKey)) ||
            tabs.Select(t => t.ChatKey).Distinct(StringComparer.Ordinal).Count() != tabs.Count ||
            tabs.Count(t => t.Active) > 1)
            throw new ArgumentException("Invalid tab identities or active count");

        int warm = availableMiB < 1024 || commitRatio >= .90 ? 0 :
            availableMiB < 2048 || commitRatio >= .80 ? 1 : 2;
        var protectedTabs = tabs.Where(t =>
            t.Active || t.Generating || t.ToolRunning || t.HasDraft)
            .Select(t => t.ChatKey).ToHashSet(StringComparer.Ordinal);
        var ordered = tabs.Where(t => !protectedTabs.Contains(t.ChatKey))
            .OrderByDescending(t => t.LastUsedUtc).ToArray();
        int backgroundProtected = tabs.Count(t => !t.Active &&
            (t.Generating || t.ToolRunning || t.HasDraft));
        int spareWarm = Math.Max(0, warm - backgroundProtected);
        var chosen = ordered.Take(spareWarm).Select(t => t.ChatKey)
            .ToHashSet(StringComparer.Ordinal);
        var keep = tabs.Where(t => protectedTabs.Contains(t.ChatKey) ||
            chosen.Contains(t.ChatKey)).Select(t => t.ChatKey).ToArray();
        var park = tabs.Where(t => !protectedTabs.Contains(t.ChatKey) &&
            !chosen.Contains(t.ChatKey)).Select(t => t.ChatKey).ToArray();
        return new Decision(warm, keep, park, backgroundProtected > warm);
    }
}
