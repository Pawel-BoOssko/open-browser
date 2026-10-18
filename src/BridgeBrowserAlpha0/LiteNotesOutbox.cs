using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Encrypted, crash-resistant notes inbox for each ChatGPT conversation.
/// No network calls. An uncertain delivery is never retried automatically.
/// </summary>
internal sealed class LiteNotesOutbox
{
    internal enum Phase { Queued, Prepared, DeliveryUncertain, Confirmed }

    internal sealed class Item
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public string FullDraftSnapshot { get; set; } = "";
        public Phase Status { get; set; } = Phase.Queued;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ChangedUtc { get; set; } = DateTimeOffset.UtcNow;
        public int DeliveryAttempts { get; set; }
    }

    private sealed class Envelope
    {
        public int Version { get; set; } = 1;
        public List<Item> Items { get; set; } = new();
    }

    private readonly string _folder;

    internal LiteNotesOutbox(string? folder = null)
    {
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "outbox");
    }

    private string FileFor(string chatKey)
    {
        if (string.IsNullOrWhiteSpace(chatKey) || chatKey.Length > 2000)
            throw new ArgumentException("Invalid conversation key");
        string digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(chatKey)));
        return Path.Combine(_folder, digest + ".dpapi");
    }

    private Envelope Read(string chatKey)
    {
        string path = FileFor(chatKey);
        if (!File.Exists(path)) return new Envelope();
        var encrypted = File.ReadAllBytes(path);
        byte[] clear = LiteSecureArchive.Unprotect(encrypted);
        try
        {
            var value = JsonSerializer.Deserialize<Envelope>(clear);
            if (value?.Version != 1 || value.Items == null ||
                value.Items.Any(item => item == null || item.Id.Length != 32))
                throw new InvalidDataException("Invalid notes outbox schema");
            return value;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    private void Write(string chatKey, Envelope envelope)
    {
        Directory.CreateDirectory(_folder);
        string path = FileFor(chatKey);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(envelope);
        byte[] encrypted;
        try { encrypted = LiteSecureArchive.Protect(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(staging, encrypted);
            if (File.Exists(path))
                File.Replace(staging, path, path + ".backup");
            else
                File.Move(staging, path);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    internal IReadOnlyList<Item> List(string chatKey)
    {
        return Read(chatKey).Items;
    }

    /// <summary>
    /// Queues only the suffix typed since the last queued snapshot.
    /// An edited earlier paragraph is queued as a complete replacement, never silently lost.
    /// </summary>
    internal Item? EnqueueDraft(string chatKey, string draft)
    {
        if (draft.Length > 200000) throw new ArgumentException("Note too large");
        if (string.IsNullOrWhiteSpace(draft)) return null;
        var data = Read(chatKey);
        var last = data.Items.LastOrDefault();
        if (last != null && last.FullDraftSnapshot == draft) return null;
        string delta = last != null && draft.StartsWith(last.FullDraftSnapshot,
            StringComparison.Ordinal) ? draft[last.FullDraftSnapshot.Length..] : draft;
        if (string.IsNullOrWhiteSpace(delta)) return null;
        var item = new Item
        {
            Text = delta, FullDraftSnapshot = draft
        };
        data.Items.Add(item);
        Write(chatKey, data);
        LiteEventLog.Write("notes_queued", new { characters = delta.Length });
        return item;
    }

    internal bool Transition(string chatKey, string itemId, Phase from, Phase to)
    {
        bool permitted = (from, to) switch
        {
            (Phase.Queued, Phase.Prepared) => true,
            (Phase.Prepared, Phase.DeliveryUncertain) => true,
            (Phase.DeliveryUncertain, Phase.Confirmed) => true,
            _ => false
        };
        if (!permitted) return false;
        var data = Read(chatKey);
        var item = data.Items.FirstOrDefault(i => i.Id == itemId && i.Status == from);
        if (item == null) return false;
        item.Status = to;
        item.ChangedUtc = DateTimeOffset.UtcNow;
        if (to == Phase.DeliveryUncertain) item.DeliveryAttempts++;
        Write(chatKey, data);
        LiteEventLog.Write("notes_queue_transition", new { from = from.ToString(), to = to.ToString() });
        return true;
    }

    internal Item? NextSafelyQueued(string chatKey)
    {
        var data = Read(chatKey);
        // A delivery of unknown outcome blocks further automatic dispatch.
        if (data.Items.Any(i => i.Status == Phase.DeliveryUncertain)) return null;
        return data.Items.FirstOrDefault(i => i.Status == Phase.Queued);
    }
}
