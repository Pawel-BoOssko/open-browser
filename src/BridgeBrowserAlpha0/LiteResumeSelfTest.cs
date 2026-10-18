namespace BridgeBrowserAlpha0;

/// <summary>Deterministic offline tests for the chat continuation state gate.</summary>
internal static class LiteResumeSelfTest
{
    internal static int Run()
    {
        try
        {
            var policy = new LiteResumePolicy();
            var begin = DateTimeOffset.UtcNow;
            LiteResumePolicy.Observation O(int secs, string txt = "Rozdział pierwszy napisany. Pozostały następne rozdziały.",
                bool busy = false, bool tool = false, bool draft = false,
                bool error = false, string role = "assistant") =>
                new("chatgpt.com/c/synthetic", role, txt, busy, tool, draft, error, true,
                    begin.AddSeconds(secs));
            Check(!policy.Check(O(0)).MayAskLuna, "first sample");
            Check(!policy.Check(O(20)).MayAskLuna, "second sample");
            Check(policy.Check(O(45)).MayAskLuna, "stable third sample");
            Check(!policy.Check(O(46, busy: true)).MayAskLuna, "generating");
            Check(!policy.Check(O(47, tool: true)).MayAskLuna, "tool pending");
            Check(!policy.Check(O(48, draft: true)).MayAskLuna, "draft present");
            Check(!policy.Check(O(49, error: true)).MayAskLuna, "visible error");
            Check(!policy.Check(O(50, role: "user")).MayAskLuna, "last turn not assistant");
            Check(!policy.Check(O(51, txt: "Nowy tekst zaczęty")).MayAskLuna, "response changed");
            Check(!policy.Check(O(85, txt: "Nowy tekst zaczęty")).MayAskLuna, "not old enough");
            Check(policy.Check(O(93, txt: "Nowy tekst zaczęty")).MayAskLuna, "re-stabilized");
            Check(LiteResumePolicy.NormalizeCandidate(new string('x', 8000)).Length == 6000,
                "max tokens");
            LiteEventLog.Write("resume_policy_selftest", new { passed = true, checks = 12 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("resume_policy_selftest",
                new { passed = false, errorType = ex.GetType().Name });
            return 2;
        }
    }
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
    }
}
