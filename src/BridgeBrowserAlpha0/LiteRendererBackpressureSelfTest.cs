namespace BridgeBrowserAlpha0;

internal static class LiteRendererBackpressureSelfTest
{
    internal static int Run()
    {
        try
        {
            var t = new DateTimeOffset(2026, 10, 18, 17, 0, 0, TimeSpan.Zero);
            var gate = new LiteRendererBackpressure();
            Check(gate.MayProbe(t), "initially healthy");
            Check(gate.MarkFailed(t) == TimeSpan.FromSeconds(30), "first delay 30");
            Check(!gate.MayProbe(t.AddSeconds(29)), "blocked before deadline");
            Check(gate.MayProbe(t.AddSeconds(30)), "available after deadline");
            Check(gate.MarkFailed(t.AddSeconds(40)) == TimeSpan.FromSeconds(60),
                "second failure backs off");
            Check(!gate.MayProbe(t.AddSeconds(70)), "second cooldown");
            for (int i = 0; i < 10; i++) gate.MarkFailed(t.AddMinutes(i + 2));
            Check(gate.ConsecutiveFailures == 5, "failure cap");
            Check(gate.MarkFailed(t.AddMinutes(20)) == TimeSpan.FromSeconds(300),
                "maximum cooldown");
            gate.MarkNavigationRecovered();
            Check(gate.ConsecutiveFailures == 0, "recovered on navigation");
            Check(gate.MayProbe(t), "recovered without stale cooldown");
            LiteEventLog.Write("renderer_backpressure_selftest", new {
                passed = true, checks = 10
            });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("renderer_backpressure_selftest", new {
                passed = false, errorType = ex.GetType().Name
            });
            return 2;
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }
}
