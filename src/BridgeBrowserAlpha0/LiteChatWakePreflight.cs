using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>Pure guard for a completed, unchanged assistant answer in a specific chat.</summary>
internal static class LiteChatWakePreflight
{
    internal sealed class Observation
    {
        public string Location { get; set; } = "";
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
        public bool Generating { get; set; }
        public bool ToolBusy { get; set; }
        public bool HasDraft { get; set; }
        public bool Error { get; set; }
        public bool ComposerReady { get; set; }
        public int TurnCount { get; set; }
        public int AssistantNodeCount { get; set; }
    }

    internal static string Fingerprint(string answer) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(answer.Trim())));

    internal static string Eligibility(Observation? first, Observation? second,
        string expectedPath, string lastAttemptAnswerHash)
    {
        if (first is null || second is null) return "probe_failed";
        if (first.Location != expectedPath || second.Location != expectedPath)
            return "wrong_chat";
        if (second.Error || first.Error) return "response_error";
        if (second.Generating || first.Generating || second.ToolBusy || first.ToolBusy)
            return "generating";
        if (second.HasDraft || first.HasDraft) return "draft_present";
        if (!first.ComposerReady || !second.ComposerReady) return "no_composer";
        if (first.Role != "assistant" || second.Role != "assistant" ||
            first.TurnCount < 1 || second.TurnCount < 1 ||
            first.AssistantNodeCount < 1 || second.AssistantNodeCount < 1 ||
            string.IsNullOrWhiteSpace(first.Text) || string.IsNullOrWhiteSpace(second.Text))
            return "assistant_not_complete";
        string hash = Fingerprint(second.Text);
        if (hash != Fingerprint(first.Text)) return "answer_not_stable";
        if (hash == lastAttemptAnswerHash) return "same_answer_already_woken";
        return "ready";
    }

    internal static bool IsCompletion(string answer)
    {
        var trimmed = answer.Trim();
        return trimmed == "[OPENBROWSER-PLAN-COMPLETE]" ||
               trimmed == "[BOOK-COMPLETE]";
    }

    internal static int SelfTest()
    {
        const string path = "/c/11111111-1111-1111-1111-111111111111";
        var a = new Observation { Location = path, Role = "assistant",
            Text = "A completed assistant answer", ComposerReady = true,
            TurnCount = 4, AssistantNodeCount = 2 };
        var b = new Observation { Location = path, Role = "assistant",
            Text = a.Text, ComposerReady = true, TurnCount = 4, AssistantNodeCount = 2 };
        if (Eligibility(a, b, path, "") != "ready") return 2;
        if (Eligibility(a, b, path, Fingerprint(a.Text)) != "same_answer_already_woken") return 3;
        b.Generating = true;
        if (Eligibility(a, b, path, "") != "generating") return 4;
        b.Generating = false; b.HasDraft = true;
        if (Eligibility(a, b, path, "") != "draft_present") return 5;
        b.HasDraft = false; b.Location = "/c/22222222-2222-2222-2222-222222222222";
        if (Eligibility(a, b, path, "") != "wrong_chat") return 6;
        b.Location = path; b.Text = "An answer still changing";
        if (Eligibility(a, b, path, "") != "answer_not_stable") return 7;
        b.Text = a.Text; b.Role = "user";
        if (Eligibility(a, b, path, "") != "assistant_not_complete") return 8;
        if (!IsCompletion("[BOOK-COMPLETE]") ||
            !IsCompletion("[OPENBROWSER-PLAN-COMPLETE]") ||
            IsCompletion("Asked to say [BOOK-COMPLETE]")) return 9;
        return 0;
    }
}
