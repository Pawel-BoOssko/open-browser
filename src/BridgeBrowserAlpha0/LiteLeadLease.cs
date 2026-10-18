using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Per-project, local single-writer lead lease with a monotonically increasing
/// fencing epoch. It never sends prompts or reads conversation contents.
/// </summary>
internal sealed class LiteLeadLease
{
    internal sealed class Transition
    {
        public long Epoch { get; set; }
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTimeOffset AtUtc { get; set; }
    }

    internal sealed class State
    {
        public int Version { get; set; } = 1;
        public string ProjectId { get; set; } = "";
        public string LeadChatId { get; set; } = "";
        public long Epoch { get; set; }
        public DateTimeOffset ExpiresUtc { get; set; }
        public List<Transition> History { get; set; } = new();
    }

    private readonly string _directory;
    internal LiteLeadLease(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "coordination");
    }

    internal State Get(string projectId)
    {
        using var guard = Lock(projectId, out string path);
        return Read(path, projectId);
    }

    internal bool TryClaim(string projectId, string chatId, TimeSpan ttl,
        out long epoch, DateTimeOffset? nowUtc = null)
    {
        Validate(chatId, ttl);
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        using var guard = Lock(projectId, out string path);
        var state = Read(path, projectId);
        if (state.LeadChatId.Length > 0 && state.ExpiresUtc > now &&
            state.LeadChatId != chatId)
        {
            epoch = state.Epoch;
            return false;
        }
        if (state.LeadChatId != chatId || state.ExpiresUtc <= now)
            Change(state, chatId, "claim", now);
        state.ExpiresUtc = now.Add(ttl);
        Write(path, state);
        epoch = state.Epoch;
        return true;
    }

    internal bool TryHandoff(string projectId, string fromChatId, string toChatId,
        long expectedEpoch, TimeSpan ttl, out long newEpoch, DateTimeOffset? nowUtc = null)
    {
        Validate(toChatId, ttl);
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        using var guard = Lock(projectId, out string path);
        var state = Read(path, projectId);
        if (state.Epoch != expectedEpoch || state.LeadChatId != fromChatId ||
            state.ExpiresUtc <= now || fromChatId == toChatId)
        {
            newEpoch = state.Epoch;
            return false;
        }
        Change(state, toChatId, "handoff", now);
        state.ExpiresUtc = now.Add(ttl);
        Write(path, state);
        newEpoch = state.Epoch;
        return true;
    }

    internal bool TryRelease(string projectId, string chatId, long expectedEpoch,
        DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        using var guard = Lock(projectId, out string path);
        var state = Read(path, projectId);
        if (state.Epoch != expectedEpoch || state.LeadChatId != chatId ||
            state.ExpiresUtc <= now) return false;
        Change(state, "", "release", now);
        state.ExpiresUtc = now;
        Write(path, state);
        return true;
    }

    private static void Change(State state, string next, string reason, DateTimeOffset now)
    {
        string previous = state.LeadChatId;
        state.Epoch = checked(state.Epoch + 1);
        state.LeadChatId = next;
        state.History.Add(new Transition {
            Epoch = state.Epoch, From = previous, To = next, Reason = reason, AtUtc = now
        });
        if (state.History.Count > 1000)
            state.History.RemoveRange(0, state.History.Count - 1000);
    }

    private FileStream Lock(string projectId, out string path)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Length > 200)
            throw new ArgumentException("Invalid project ID");
        Directory.CreateDirectory(_directory);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectId)));
        path = Path.Combine(_directory, key + ".json");
        return new FileStream(path + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }

    private static State Read(string path, string projectId)
    {
        if (!File.Exists(path)) return new State { ProjectId = projectId };
        var state = JsonSerializer.Deserialize<State>(File.ReadAllText(path));
        if (state is null || state.Version != 1 || state.ProjectId != projectId ||
            state.Epoch < 0 || state.History is null || state.LeadChatId is null)
            throw new InvalidDataException("Invalid lead lease state");
        return state;
    }

    private static void Write(string path, State state)
    {
        string temp = path + ".tmp";
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state));
            using (var stream = new FileStream(temp, FileMode.Create,
                FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void Validate(string chatId, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(chatId) || chatId.Length > 200 ||
            ttl <= TimeSpan.Zero || ttl > TimeSpan.FromDays(1))
            throw new ArgumentException("Invalid chat ID or lease duration");
    }
}
