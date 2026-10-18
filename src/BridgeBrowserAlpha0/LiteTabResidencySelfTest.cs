namespace BridgeBrowserAlpha0;

internal static class LiteTabResidencySelfTest
{
    internal static int Run()
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            var tabs = new List<LiteTabResidencyPlanner.Tab>
            {
                new() { Id = "A", Foreground = true, LastUsedUtc = now },
                new() { Id = "B", IsWarm = true, LastUsedUtc = now.AddMinutes(-1) },
                new() { Id = "C", IsWarm = true, LastUsedUtc = now.AddMinutes(-2) },
                new() { Id = "D", IsWarm = true, LastUsedUtc = now.AddMinutes(-3) },
                new() { Id = "E", IsWarm = false, LastUsedUtc = now.AddMinutes(-10) }
            };
            var plenty = LiteTabResidencyPlanner.Compute(tabs, 8000);
            Check(plenty.WarmBudget == 2, "warm budget");
            Check(plenty.KeepHot.SequenceEqual(new[] { "A", "B", "C" }),
                "recent two warm");
            Check(plenty.Park.SequenceEqual(new[] { "D" }), "oldest warm parked");
            var low = LiteTabResidencyPlanner.Compute(tabs, 500);
            Check(low.WarmBudget == 0 && low.Park.Count == 3,
                "pressure parks all ordinary warm tabs");
            tabs[3].IsGenerating = true;
            var protectedPlan = LiteTabResidencyPlanner.Compute(tabs, 500);
            Check(protectedPlan.KeepHot.Contains("D"), "generating tab protected");
            Check(protectedPlan.ProtectedOverBudget.Contains("D"),
                "pressure reported not silently evicted");
            tabs[0].HasUnsentDraft = true;
            Check(!LiteTabResidencyPlanner.Compute(tabs, 8000)
                .CanSwitchAwayFromForeground, "draft blocks destructive navigation");
            tabs[0].HasUnsentDraft = false;
            tabs[0].HasPendingTool = true;
            Check(!LiteTabResidencyPlanner.Compute(tabs, 8000)
                .CanSwitchAwayFromForeground, "pending tool blocks navigation");
            tabs[0].HasPendingTool = false;
            Check(LiteTabResidencyPlanner.Compute(tabs, 8000)
                .CanSwitchAwayFromForeground, "idle foreground can switch");
            var oneWarm = LiteTabResidencyPlanner.Compute(tabs, 2500);
            Check(oneWarm.WarmBudget == 1, "medium memory budget");
            bool badSetRejected = false;
            try { LiteTabResidencyPlanner.Compute(tabs.Concat(new[] { tabs[0] }).ToArray(), 8000); }
            catch (ArgumentException) { badSetRejected = true; }
            Check(badSetRejected, "duplicate tab id rejected");
            LiteEventLog.Write("tab_residency_selftest", new { passed = true, checks = 10 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("tab_residency_selftest",
                new { passed = false, errorType = ex.GetType().Name });
            return 2;
        }
    }

    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
    }
}
