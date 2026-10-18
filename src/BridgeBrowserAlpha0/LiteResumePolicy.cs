using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Conservative evidence gate for a finished ChatGPT turn.
/// No page timeout, HTTP 200 or absent spinner proves that work is done.
/// </summary>
internal sealed class LiteResumePolicy
{
    internal sealed record Observation(
        string ChatKey, string LastRole, string VisibleText,
        bool Generating, bool ToolBusy, bool HasDraft, bool Error,
        bool ComposerReady, DateTimeOffset AtUtc);

    internal sealed record Decision(
        string Code, string Reason, string ResponseHash, bool MayAskLuna);

    private string _lastChat = "";
    private string _lastDigest = "";
    private DateTimeOffset _sinceUtc;
    private int _samples;

    internal Decision Check(Observation value)
    {
        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value.VisibleText ?? "")));
        if (value.ChatKey != _lastChat || hash != _lastDigest)
        {
            _lastChat = value.ChatKey;
            _lastDigest = hash;
            _sinceUtc = value.AtUtc;
            _samples = 1;
            return new("observing", "The assistant text changed", hash, false);
        }
        _samples++;
        if (value.Error)
            return new("error", "A visible page error must be investigated", hash, false);
        if (value.Generating || value.ToolBusy)
            return new("busy", "Model or tool is still running", hash, false);
        if (value.HasDraft)
            return new("draft", "Do not overwrite an unsent user message", hash, false);
        if (!value.ComposerReady)
            return new("unavailable", "Composer not ready", hash, false);
        if (value.LastRole != "assistant" || string.IsNullOrWhiteSpace(value.VisibleText) ||
            value.VisibleText.Length < 12)
            return new("no_final_answer", "Last assistant answer is not available", hash, false);
        if (_samples < 3 || (value.AtUtc - _sinceUtc) < TimeSpan.FromSeconds(40))
            return new("stabilizing", "Waiting for three stable observations over >=40s", hash, false);
        return new("candidate", "Assistant final-looking turn remained stable", hash, true);
    }

    internal static string NormalizeCandidate(string content)
    {
        // The result will be sent to a separate model only if the user has
        // explicitly enabled a model and given a project-specific scope.
        string text = (content ?? "").Trim();
        return text.Length > 6000 ? text[^6000..] : text;
    }
}
