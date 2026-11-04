namespace BridgeBrowserAlpha0;

// The project (projects.json), not a particular chat, owns WakeSequence.
// Every ...9 asks the OLD lead for a disk handoff.
// Every ...0 must go to a NEW verified lead, regardless of handoff file.
internal static class LiteWakeRotation
{
    internal static bool HandoffWake(long number) => number > 0 && number % 10 == 9;
    internal static bool BeforeNewLeadWake(long lastNumber) => HandoffWake(lastNumber);

    // A successor has a separate blank composer. Old-lead drafts, generation
    // and uncertain probes must never postpone its creation for OBL.
    internal static bool RequiresOldLeadPreflightForSuccessor(string projectKey) =>
        !string.Equals(projectKey, "obl", StringComparison.OrdinalIgnoreCase);

    // An unsent draft belongs to the OLD chat. Never send or erase it.
    // The legacy gate below applies only to projects that require an old-chat
    // preflight. OBL successor creation bypasses that gate entirely.
    internal static bool MayRotatePastUnavailableOldLead(string reason, bool draftPresent) =>
        (reason == "draft_present" && draftPresent) ||
        (!draftPresent && (reason is "no_composer" or "response_error"));

    internal static int SelfTest()
    {
        if (LiteRotationRetryPolicy.SelfTest() != 0) return 11;
        if (LiteRotationHandoffSnapshot.SelfTest() != 0) return 20;
        if (RequiresOldLeadPreflightForSuccessor("obl") ||
            RequiresOldLeadPreflightForSuccessor("OBL") ||
            !RequiresOldLeadPreflightForSuccessor("other")) return 21;
        foreach (long n in new long[] {9,19,49,99,109})
            if (!HandoffWake(n) || !BeforeNewLeadWake(n)) return 1;
        foreach (long n in new long[] {0,1,8,10,50,100})
            if (HandoffWake(n) || BeforeNewLeadWake(n)) return 2;
        var old = new LiteChatWakeRegistry.Chat{WakeSequence=49, Enabled=true};
        var transferred = new LiteChatWakeRegistry.Chat {
            WakeSequence=old.WakeSequence, Enabled=old.Enabled
        };
        if(transferred.WakeSequence+1!=50) return 3;
        // The canonical project schedule survives a role change intact:
        // no per-conversation reset at 49->50 or 59->60.
        const string a="https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
        const string b="https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
        var project=new LiteProjectRegistry.Project {
            Chats=new List<LiteProjectRegistry.Chat> {
                new(){Url=a,Role="lead"},new(){Url=b,Role="archive"}
            },
            Wake=System.Text.Json.JsonSerializer.SerializeToNode(new
            { Enabled=true,WakeSequence=49,Minutes=25,Prompt="Project" })!.AsObject()
        };
        var original=LiteProjectWakeStore.Expand(
            new[]{project},Array.Empty<LiteChatWakeRegistry.Chat>());
        if(original[0].WakeSequence!=49 || original[0].Url!=a)return 4;
        project.Chats[0].Role="archive";
        project.Chats[1].Role="lead";
        var next=LiteProjectWakeStore.Expand(
            new[]{project},Array.Empty<LiteChatWakeRegistry.Chat>());
        if(next[0].Url!=b || next[0].WakeSequence+1!=50)return 5;
        if (!MayRotatePastUnavailableOldLead("no_composer",false) ||
            !MayRotatePastUnavailableOldLead("response_error",false) ||
            MayRotatePastUnavailableOldLead("generating",false) ||
            MayRotatePastUnavailableOldLead("no_composer",true) ||
            MayRotatePastUnavailableOldLead("draft_present",false) ||
            !MayRotatePastUnavailableOldLead("draft_present",true) ||
            MayRotatePastUnavailableOldLead("generating",true)) return 6;
        return 0;
    }
}
