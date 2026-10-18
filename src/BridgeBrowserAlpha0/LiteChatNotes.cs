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
