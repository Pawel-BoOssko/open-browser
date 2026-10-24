using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Portable, readable UTF-8 copies of model answers. Never stores credentials
/// or browser profile data. Private local AppData ACLs protect this directory.
/// </summary>
internal sealed class LiteResponseMarkdownArchive
{
    private readonly string _root;
    internal LiteResponseMarkdownArchive(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "responses");
    }

    internal static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Replace('\u00A0', ' ').Replace("\u200B", "")
            .Replace("\uFEFF", "").Replace("\0", "");
        // Keep code, paths, tables, lists and meaningful formatting intact.
        return Regex.Replace(normalized.Trim(), @"\n{4,}", "\n\n\n");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    internal bool Save(string conversationUrl, string turnId,
        string displayedText, bool partial)
    {
        if (string.IsNullOrWhiteSpace(conversationUrl) ||
            string.IsNullOrWhiteSpace(turnId)) return false;
        string cleaned = Clean(displayedText);
        if (cleaned.Length < 5 || cleaned.Length > 200_000) return false;
        string chatKey = Hash(conversationUrl);
        string turnKey = Hash(turnId)[..16];
        string digest = Hash(cleaned);
        string directory = Path.Combine(_root, chatKey);
        Directory.CreateDirectory(directory);
        string fileName = partial
            ? turnKey + "_partial.md"
            : turnKey + "_" + digest[..16] + ".md";
        string file = Path.Combine(directory, fileName);
        byte[] utf8 = Encoding.UTF8.GetBytes(cleaned + "\n");
        if (File.Exists(file) &&
            File.ReadAllBytes(file).AsSpan().SequenceEqual(utf8))
            return false;
        string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                stream.Write(utf8);
                stream.Flush(true);
            }
            File.Move(temp, file, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }

        // Index does not contain response content or private conversation URL.
        var record = new {
            utc = DateTimeOffset.UtcNow.ToString("O"),
            chatKey, turnKey, sha256 = digest,
            status = partial ? "PARTIAL" : "FINAL",
            fileName, characters = cleaned.Length, sanitizer = "v1"
        };
        File.AppendAllText(Path.Combine(directory, "index.jsonl"),
            JsonSerializer.Serialize(record) + "\n", new UTF8Encoding(false));
        LiteEventLog.Write("assistant_markdown_saved", new {
            status = partial ? "PARTIAL" : "FINAL",
            characters = cleaned.Length, digestPrefix = digest[..12]
        });
        return true;
    }

    internal static int SelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(),
            "obl-md-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LiteResponseMarkdownArchive(dir);
            const string url = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            if (Clean("  Kod:\r\nabc\u200B\r\n\r\n\r\n\r\nkoniec ") !=
                "Kod:\nabc\n\n\nkoniec") return 1;
            if (!store.Save(url, "turn1", "Pierwszy fragment", true)) return 2;
            if (!store.Save(url, "turn1", "Zakończona odpowiedź", false)) return 3;
            if (store.Save(url, "turn1", "Zakończona odpowiedź", false)) return 4;
            string[] files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
            if (files.Length != 2 ||
                !files.Any(x => File.ReadAllText(x).Contains("Zakończona odpowiedź")))
                return 5;
            string[] indexes = Directory.GetFiles(dir, "index.jsonl", SearchOption.AllDirectories);
            if (indexes.Length != 1 || File.ReadAllLines(indexes[0]).Length != 2)
                return 6;
            return 0;
        }
        catch { return 7; }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
