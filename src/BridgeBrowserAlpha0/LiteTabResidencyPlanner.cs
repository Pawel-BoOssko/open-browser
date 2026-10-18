namespace BridgeBrowserAlpha0;

/// <summary>
/// Pure decision engine for a future bounded pool of WebView2 tabs.
/// It never closes a renderer itself and never assumes "no text" means idle.
/// </summary>
internal static class LiteTabResidencyPlanner
{
    internal sealed class Tab
    {
        public string Id { get; set; } = "";
        public bool Foreground { get; set; }
        public bool IsGenerating { get; set; }
        public bool HasUnsentDraft { get; set; }
        public bool HasPendingTool { get; set; }
        public bool HasUnflushedNotes { get; set; }
        public bool IsWarm { get; set; }
        public DateTimeOffset LastUsedUtc { get; set; }
        public bool IsProtected => IsGenerating || HasUnsentDraft ||
                                   HasPendingTool || HasUnflushedNotes;
    }

    internal sealed class Plan
    {
        public int WarmBudget { get; init; }
        public List<string> Park { get; init; } = new();
        public List<string> KeepHot { get; init; } = new();
        public List<string> ProtectedOverBudget { get; init; } = new();
        public bool CanSwitchAwayFromForeground { get; init; }
    }

    internal static Plan Compute(IReadOnlyList<Tab> tabs, long freeMemoryMiB,
        int absoluteMaxWarm = 2)
    {
        if (freeMemoryMiB < 0 || absoluteMaxWarm is < 0 or > 8)
            throw new ArgumentOutOfRangeException(nameof(freeMemoryMiB));
        if (tabs.Count > 10000 || tabs.Any(tab => string.IsNullOrWhiteSpace(tab.Id)) ||
            tabs.Select(tab => tab.Id).Distinct(StringComparer.Ordinal).Count() != tabs.Count ||
            tabs.Count(tab => tab.Foreground) > 1)
            throw new ArgumentException("Invalid tab residency set");

        int allowedWarm = freeMemoryMiB switch
        {
            < 1500 => 0,
            < 4096 => Math.Min(1, absoluteMaxWarm),
            _ => Math.Min(2, absoluteMaxWarm)
        };
        var active = tabs.FirstOrDefault(tab => tab.Foreground);
        var orderedWarm = tabs
            .Where(tab => !tab.Foreground && tab.IsWarm)
            .OrderByDescending(tab => tab.LastUsedUtc).ToList();
        var protectedWarm = orderedWarm.Where(tab => tab.IsProtected).ToList();
        var eligibleWarm = orderedWarm.Where(tab => !tab.IsProtected).ToList();
        var keep = protectedWarm.Concat(eligibleWarm.Take(
            Math.Max(0, allowedWarm - protectedWarm.Count))).ToList();
        var park = eligibleWarm.Except(keep).Select(tab => tab.Id).ToList();
        var allKept = new List<string>();
        if (active != null) allKept.Add(active.Id);
        allKept.AddRange(keep.Select(tab => tab.Id));
        return new Plan
        {
            WarmBudget = allowedWarm,
            Park = park,
            KeepHot = allKept,
            ProtectedOverBudget = protectedWarm.Skip(allowedWarm)
                .Select(tab => tab.Id).ToList(),
            CanSwitchAwayFromForeground = active is null || !active.IsProtected
        };
    }
}
