namespace BridgeBrowserAlpha0;

internal static class LiteLunaSelfTest
{
    internal static int Run()
    {
        try
        {
            Require(!LiteLunaAdvisor.Validate(new("continue", "Continue working", "n/a", "low")),
                "low confidence cannot dispatch");
            Require(!LiteLunaAdvisor.Validate(new("continue", "next", "go", "high")),
                "minimum prompt length");
            Require(LiteLunaAdvisor.Validate(new("continue",
                "Kontynuuj kolejny rozdział książki i nie powtarzaj już ukończonego.",
                "Next chapter remains", "high")), "high-confidence suggestion accepted");
            Require(LiteLunaAdvisor.Validate(new("wait", "", "Model tool running", "low")),
                "waiting allowed");
            Require(LiteLunaAdvisor.Validate(new("stop", "", "All goals met", "high")),
                "stop allowed");
            Require(!LiteLunaAdvisor.Validate(new("delete", "erase all", "unknown", "high")),
                "invalid decision rejected");
            Require(!LiteLunaAdvisor.Validate(null), "no planner reply waits");
            Require(LiteLunaAdvisor.SystemPrompt.Contains("NIE wymyślaj nowych kroków"),
                "no invented backlog");
            LiteEventLog.Write("luna_selftest", new { passed = true, checks = 8 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("luna_selftest",
                new { passed = false, errorType = ex.GetType().Name });
            return 2;
        }
    }
    private static void Require(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException(why);
    }
}
