namespace BridgeBrowserAlpha0;

/// <summary>Disposable, headless fencing and handoff contract test.</summary>
internal static class LiteLeadLeaseSelfTest
{
    internal static int Run()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "ob-lite-leases-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = new LiteLeadLease(directory);
            var second = new LiteLeadLease(directory);
            var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
            var ttl = TimeSpan.FromMinutes(10);
            Require(first.TryClaim("project-1", "chat-A", ttl, out long e1, now), "initial claim");
            Require(e1 == 1, "initial fencing epoch");
            Require(!second.TryClaim("project-1", "chat-B", ttl, out long blocked, now), "exclusive lease");
            Require(blocked == 1, "rejection returns current epoch");
            Require(first.TryHandoff("project-1", "chat-A", "chat-B", e1,
                ttl, out long e2, now.AddMinutes(1)), "manual handoff");
            Require(e2 == 2, "handoff advances epoch");
            Require(!first.TryRelease("project-1", "chat-A", e1, now.AddMinutes(2)), "stale owner rejected");
            Require(second.TryClaim("project-1", "chat-B", ttl, out long renewed,
                now.AddMinutes(3)) && renewed == e2, "same lead renews without fencing change");
            var persisted = new LiteLeadLease(directory).Get("project-1");
            Require(persisted.LeadChatId == "chat-B" && persisted.History.Count == 2,
                "durable lead and history");
            Require(first.TryClaim("project-1", "chat-A", ttl, out long e3,
                now.AddMinutes(20)) && e3 == 3, "expired lease reclaimed");
            Require(!second.TryHandoff("project-1", "chat-B", "chat-C", e2,
                ttl, out _, now.AddMinutes(21)), "stale handoff rejected");
            Require(first.TryRelease("project-1", "chat-A", e3,
                now.AddMinutes(21)), "release");
            Require(second.TryClaim("project-1", "chat-C", ttl, out long e5,
                now.AddMinutes(22)) && e5 == 5, "claim after release");
            Require(first.Get("project-1").History.Count == 5, "history persisted");
            LiteEventLog.Write("lite_lease_selftest", new { passed = true, checks = 12 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("lite_lease_selftest", new {
                passed = false, errorType = ex.GetType().Name, errorCode = ex.HResult
            });
            return 2;
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch { }
        }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Lease selftest: " + name);
    }
}
