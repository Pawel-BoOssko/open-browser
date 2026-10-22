using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Engine-owned durable *notification request* journal for verified new
/// WAIT_USER / BLOCKED events. No email is marked sent without transport ACK.
/// Does not read chat text, credentials, cookies or network endpoints.
/// </summary>
internal static class LiteOwnerInterventions
{
    private static readonly string OutboxPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "diagnostics",
        "owner-interventions.jsonl");

    internal static string EventKey(string conversationUrl, string status, string answerHash)
    {
        var data = Encoding.UTF8.GetBytes(conversationUrl + "\n" + status +
            "\n" + answerHash);
        return Convert.ToHexString(SHA256.HashData(data));
    }

    internal static bool IsIntervention(string? status) =>
        status is "WAIT_USER" or "BLOCKED";

    internal static int SelfTest()
    {
        const string url = "https://chatgpt.com/c/test";
        var a = EventKey(url, "BLOCKED", new string('A', 64));
        if (a.Length != 64 || !a.All(Uri.IsHexDigit) ||
            a != EventKey(url, "BLOCKED", new string('A', 64)) ||
            a == EventKey(url, "WAIT_USER", new string('A', 64)) ||
            !IsIntervention("BLOCKED") || !IsIntervention("WAIT_USER") ||
            IsIntervention("CONTINUE")) return 1;
        return 0;
    }

    internal static void QueueForOwner(string status, string id)
    {
        if (!IsIntervention(status) || id.Length != 64 ||
            !id.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid intervention");
        Directory.CreateDirectory(Path.GetDirectoryName(OutboxPath)!);
        string line = JsonSerializer.Serialize(new {
            schema = 1, utc = DateTimeOffset.UtcNow.ToString("O"),
            eventKey = id, status,
            appNotification = "shown_or_attempted",
            email = "pending_transport_configuration"
        }) + Environment.NewLine;
        using var fs = new FileStream(OutboxPath, FileMode.Append,
            FileAccess.Write, FileShare.ReadWrite);
        byte[] buffer = new UTF8Encoding(false).GetBytes(line);
        fs.Write(buffer);
        fs.Flush(true);
    }
}
