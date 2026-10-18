using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Encrypted, per-chat durable outbox. It NEVER sends a message.
/// An in-flight entry is not automatically retried after a crash.
/// </summary>
internal sealed class LiteNotesOutbox
{
    internal sealed class Item
    {
        public string Id { get; set; } = "";
        public long Sequence { get; set; }
        public string Digest { get; set; } = "";
        public string Text { get; set; } = "";
        public string State { get; set; } = "pending";
        public long LeaseEpoch { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
    }

    private sealed class Envelope
    {
        public int Version { get; set; } = 1;
        public string ChatKey { get; set; } = "";
        public long NextSequence { get; set; } = 1;
        public List<Item> Items { get; set; } = new();
    }

    internal readonly record struct IdleEvidence(
        string ChatKey, DateTimeOffset FirstUtc, DateTimeOffset SecondUtc,
        bool? Generating, bool? ToolActive, bool? ComposerReady,
        bool? NavigationActive, bool? ProjectLeadConfirmed, long LeaseEpoch);

    private readonly string _directory;
    internal LiteNotesOutbox(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "notes-outbox");
    }

    internal Item Enqueue(string chatKey, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 20000)
            throw new ArgumentException("Outbox note must contain 1..20000 characters");
        using var guard = Lock(chatKey, out string path);
        var box = Read(path, chatKey);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var duplicate = box.Items.FirstOrDefault(i => i.Digest == digest &&
            i.State is "pending" or "dispatching" or "delivered" or "uncertain");
        if (duplicate is not null) return duplicate;
        if (box.Items.Count >= 16)
            throw new InvalidOperationException("Outbox full; review existing items");
        var item = new Item {
            Id = Guid.NewGuid().ToString("N"), Sequence = box.NextSequence++,
            Digest = digest, Text = text, State = "pending",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        box.Items.Add(item);
        Write(path, box);
        return item;
    }

    internal IReadOnlyList<Item> List(string chatKey)
    {
        using var guard = Lock(chatKey, out string path);
        return Read(path, chatKey).Items;
    }

    internal bool TryBeginDispatch(string chatKey, string id,
        IdleEvidence evidence, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (evidence.ChatKey != chatKey || evidence.Generating != false ||
            evidence.ToolActive != false || evidence.ComposerReady != true ||
            evidence.NavigationActive != false ||
            evidence.ProjectLeadConfirmed != true || evidence.LeaseEpoch < 1 ||
            evidence.SecondUtc - evidence.FirstUtc < TimeSpan.FromSeconds(5) ||
            evidence.SecondUtc - evidence.FirstUtc > TimeSpan.FromSeconds(20) ||
            evidence.SecondUtc > now ||
            now - evidence.SecondUtc > TimeSpan.FromSeconds(10))
            return false;
        using var guard = Lock(chatKey, out string path);
        var box = Read(path, chatKey);
        var item = box.Items.FirstOrDefault(i => i.Id == id);
        if (item is null || item.State != "pending") return false;
        item.State = "dispatching";
        item.LeaseEpoch = evidence.LeaseEpoch;
        Write(path, box);
        return true;
    }

    internal bool MarkResult(string chatKey, string id, long epoch,
        bool externallyConfirmedDelivered)
    {
        using var guard = Lock(chatKey, out string path);
        var box = Read(path, chatKey);
        var item = box.Items.FirstOrDefault(i => i.Id == id);
        if (item is null || item.State != "dispatching" ||
            item.LeaseEpoch != epoch || epoch < 1) return false;
        item.State = externallyConfirmedDelivered ? "delivered" : "uncertain";
        Write(path, box);
        return true;
    }

    internal int RecoverInterrupted(string chatKey)
    {
        using var guard = Lock(chatKey, out string path);
        var box = Read(path, chatKey);
        int count = 0;
        foreach (var item in box.Items.Where(i => i.State == "dispatching"))
        {
            item.State = "uncertain";
            count++;
        }
        if (count > 0) Write(path, box);
        return count;
    }

    private FileStream Lock(string key, out string path)
    {
        if (key.Length is < 1 or > 300 ||
            !(key == "inbox" || key.StartsWith("chatgpt.com/c/", StringComparison.Ordinal) ||
              key.StartsWith("chatgpt.com/g/", StringComparison.Ordinal)))
            throw new ArgumentException("Invalid chat key");
        Directory.CreateDirectory(_directory);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        path = Path.Combine(_directory, hash + ".dpapi");
        return new FileStream(path + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }

    private static Envelope Read(string path, string chatKey)
    {
        if (!File.Exists(path)) return new Envelope { ChatKey = chatKey };
        byte[] plain = LiteSecureArchive.Unprotect(File.ReadAllBytes(path));
        try
        {
            var box = JsonSerializer.Deserialize<Envelope>(plain);
            if (box is null || box.Version != 1 || box.ChatKey != chatKey ||
                box.Items is null || box.NextSequence < 1 || box.Items.Count > 16 ||
                box.Items.Any(i => i.State is not ("pending" or "dispatching" or "delivered" or "uncertain")))
                throw new InvalidDataException("Invalid outbox schema");
            return box;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static void Write(string path, Envelope box)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(box);
        byte[] cipher;
        try { cipher = LiteSecureArchive.Protect(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                stream.Write(cipher);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
