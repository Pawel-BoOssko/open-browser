using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Atomic per-chat notes, encrypted using the current Windows user's DPAPI key.
/// The note's plaintext never enters source control or diagnostic logs.
/// </summary>
internal sealed class LiteChatNotes
{
    private readonly string _directory;

    internal LiteChatNotes(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "notes");
    }

    private sealed class Note
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public string Text { get; set; } = "";
    }

    // Regression: notes cannot leak from one conversation into another.
    internal static int SelfTest()
    {
        string path = Path.Combine(Path.GetTempPath(),
            "obl_notes_isolation_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LiteChatNotes(path);
            string a = NormalizeKey("https://chatgpt.com/c/11111111-1111-1111-1111-111111111111");
            string b = NormalizeKey("https://chatgpt.com/c/22222222-2222-2222-2222-222222222222");
            if (a == b || a == "inbox" || b == "inbox") return 1;
            store.Save(a, "TEST_NOTES_PRIVATE_A");
            if (store.Load(a) != "TEST_NOTES_PRIVATE_A" ||
                store.Load(b) != "") return 2;
            if (!LiteChatWakeRegistry.Message(
                    new LiteChatWakeRegistry.Chat { Prompt = "continue" },
                    store.Load(a)).Contains("TEST_NOTES_PRIVATE_A"))
                return 3;
            if (LiteChatWakeRegistry.Message(
                    new LiteChatWakeRegistry.Chat { Prompt = "continue" },
                    store.Load(b)).Contains("TEST_NOTES_PRIVATE_A"))
                return 4;
            return 0;
        }
        catch { return 5; }
        finally { try { Directory.Delete(path, true); } catch { } }
    }

    internal static string NormalizeKey(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Host == "chatgpt.com" || uri.Host == "www.chatgpt.com"))
        {
            string path = uri.AbsolutePath.TrimEnd('/');
            if (path.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/g/", StringComparison.OrdinalIgnoreCase))
                return "chatgpt.com" + path;
        }
        return "inbox";
    }

    private string FilePath(string key)
    {
        string hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_directory, hex + ".dpapi");
    }

    internal string Load(string key)
    {
        string file = FilePath(key);
        if (!File.Exists(file)) return "";
        byte[] data = File.ReadAllBytes(file);
        byte[] clear = LiteSecureArchive.Unprotect(data);
        try
        {
            var note = JsonSerializer.Deserialize<Note>(Encoding.UTF8.GetString(clear));
            if (note?.Version != 1) throw new InvalidDataException("Invalid notes schema");
            return note.Text ?? "";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
        }
    }

    internal void Save(string key, string text)
    {
        if (text.Length > 1_000_000)
            throw new ArgumentException("Notes cannot exceed one million characters");
        Directory.CreateDirectory(_directory);
        var note = new Note { Text = text, UpdatedUtc = DateTimeOffset.UtcNow };
        byte[] plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(note));
        byte[] encrypted;
        try { encrypted = LiteSecureArchive.Protect(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        string file = FilePath(key);
        string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, encrypted);
            File.Move(tmp, file, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
