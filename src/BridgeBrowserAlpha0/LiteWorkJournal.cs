using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Local append-only, metadata-only work evidence. Private prompts, answer text,
/// cookies and URLs are deliberately not part of the record schema.
/// </summary>
internal sealed class LiteWorkJournal
{
    internal sealed class Entry
    {
        public int Version { get; set; } = 1;
        public string EventId { get; set; } = Guid.NewGuid().ToString("N");
        public string ProjectId { get; set; } = "";
        public string ChatId { get; set; } = "";
        public string TaskId { get; set; } = "";
        public string Kind { get; set; } = "";
        public long LeaseEpoch { get; set; }
        public DateTimeOffset Utc { get; set; }
    }

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
    {
        "task_started", "task_checkpoint", "task_blocked", "task_completed",
        "handoff_prepared", "handoff_confirmed", "handoff_released"
    };
    private static readonly Regex Identifier = new(
        "^[a-zA-Z0-9_.:-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _directory;
    internal LiteWorkJournal(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "work-journal");
    }

    internal string Append(string projectId, string chatId, string taskId,
        string kind, long leaseEpoch, DateTimeOffset? nowUtc = null)
    {
        Validate(projectId, chatId, taskId, kind, leaseEpoch);
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_directory);
        string path = PathFor(projectId, now);
        var entry = new Entry {
            ProjectId = projectId, ChatId = chatId, TaskId = taskId,
            Kind = kind, LeaseEpoch = leaseEpoch, Utc = now
        };
        byte[] payload = new UTF8Encoding(false).GetBytes(
            JsonSerializer.Serialize(entry) + "\n");
        // FileShare.Read permits inspection but prevents simultaneous writers.
        using var stream = new FileStream(path, FileMode.Append,
            FileAccess.Write, FileShare.Read);
        if (stream.Length > 16 * 1024 * 1024)
            throw new IOException("Daily work journal size limit reached");
        stream.Write(payload);
        stream.Flush(flushToDisk: true);
        return entry.EventId;
    }

    internal IReadOnlyList<Entry> ReadLast(string projectId,
        DateTimeOffset dayUtc, int limit = 100)
    {
        if (!Identifier.IsMatch(projectId) || limit < 1 || limit > 1000)
            throw new ArgumentException("Invalid project ID or read limit");
        string path = PathFor(projectId, dayUtc);
        if (!File.Exists(path)) return Array.Empty<Entry>();
        var result = new Queue<Entry>();
        foreach (string line in File.ReadLines(path, Encoding.UTF8))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<Entry>(line);
                if (entry is null || entry.Version != 1 ||
                    entry.ProjectId != projectId || !Kinds.Contains(entry.Kind))
                    continue;
                result.Enqueue(entry);
                if (result.Count > limit) result.Dequeue();
            }
            catch (JsonException)
            {
                // A truncated record must not prevent reading later records.
            }
        }
        return result.ToArray();
    }

    private string PathFor(string projectId, DateTimeOffset date)
    {
        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(projectId)));
        return Path.Combine(_directory,
            hash + "_" + date.UtcDateTime.ToString("yyyyMMdd") + ".jsonl");
    }

    private static void Validate(string projectId, string chatId,
        string taskId, string kind, long epoch)
    {
        if (!Identifier.IsMatch(projectId) || !Identifier.IsMatch(chatId) ||
            !Identifier.IsMatch(taskId) || !Kinds.Contains(kind) || epoch < 0)
            throw new ArgumentException("Invalid work journal metadata");
    }
}
