namespace BridgeBrowserAlpha0;

/// <summary>
/// Headless smoke check for the local project data model. No browser is opened,
/// no user profile is modified, and no account credentials are accessed.
/// </summary>
internal static class LiteSelfTest
{
    internal static int Run()
    {
        string file = Path.Combine(Path.GetTempPath(),
            "ob-lite-project-selftest-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = LiteProjectRegistry.Open(file);
            Require(store.Projects.Count == 7, "seeded project count");
            var extra = store.AddProject("Eighth project");
            for (int n = 0; n < 25; n++)
                store.AddProject("More project " + n);
            Require(store.Projects.Count == 33, "unbounded number of projects");
            var c1 = store.AddChat(extra.Id, "A", "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111");
            var c2 = store.AddChat(extra.Id, "B", "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222");
            store.PromoteLead(extra.Id, c2.Id);
            Require(c1.Role == "worker" && c2.Role == "lead", "single lead");
            var second = LiteProjectRegistry.Open(file);
            var saved = second.Projects.Single(p => p.Id == extra.Id);
            Require(saved.Chats.Count == 2, "chat persistence");
            Require(saved.Chats.Count(c => c.Role == "lead") == 1, "lead persistence");
            bool invalidRejected = false;
            try { second.AddChat(extra.Id, "evil", "https://example.com/c/123"); }
            catch (ArgumentException) { invalidRejected = true; }
            Require(invalidRejected, "reject external URL");
            LiteEventLog.Write("lite_selftest", new { passed = true, checks = 6 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("lite_selftest", new { passed = false,
                errorType = ex.GetType().Name, errorCode = ex.HResult });
            return 2;
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }

    private static void Require(bool value, string description)
    {
        if (!value) throw new InvalidOperationException("Failed: " + description);
    }
}
