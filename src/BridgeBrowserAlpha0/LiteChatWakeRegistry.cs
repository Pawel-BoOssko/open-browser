using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>Independent, explicitly enabled wake schedules, encrypted per Windows user.</summary>
internal static class LiteChatWakeRegistry
{
    internal sealed class Chat
    {
        public string Url { get; set; } = "";
        public bool Enabled { get; set; }
        public int Minutes { get; set; } = 25;
        public DateTimeOffset DueUtc { get; set; }
        public DateTimeOffset LastAttemptUtc { get; set; }
        public string LastState { get; set; } = "armed";
        public string LastAnswerFingerprint { get; set; } = "";
        public string Prompt { get; set; } = "";
    }

    private sealed class Store
    {
        public int Schema { get; set; } = 1;
        public List<Chat> Chats { get; set; } = new();
    }

    private static readonly string PathOnDisk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "chat-wakes.dpapi");

    internal static List<Chat> Read()
    {
        if (!File.Exists(PathOnDisk))
        {
            var legacy = LiteDevWakeState.Read();
            if (LiteDevWakeState.IsChatUrl(legacy.TargetUrl))
            {
                var migration = new List<Chat> { new() {
                    Url = LiteDevWakeState.NormalizeUrl(legacy.TargetUrl),
                    Enabled = legacy.Enabled, Minutes = legacy.IntervalMinutes,
                    DueUtc = legacy.NextDueUtc > DateTimeOffset.UtcNow
                        ? legacy.NextDueUtc : DateTimeOffset.UtcNow.AddMinutes(2),
                    LastAttemptUtc = legacy.LastAttemptUtc,
                    LastState = "migrated", Prompt = LiteDevWakeState.WakeText
                }};
                Write(migration);
                return migration;
            }
            return new List<Chat>();
        }
        var cipher = File.ReadAllBytes(PathOnDisk);
        var clear = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
        try
        {
            var store = JsonSerializer.Deserialize<Store>(clear)
                ?? throw new InvalidDataException("Empty chat wake registry");
            if (store.Schema != 1 || store.Chats.Count > 48)
                throw new InvalidDataException("Invalid registry schema");
            var urls = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in store.Chats)
            {
                if (!LiteDevWakeState.IsChatUrl(c.Url) || !urls.Add(c.Url) ||
                    c.Minutes is < 20 or > 1440 || c.Prompt.Length > 16000 ||
                    (c.LastAnswerFingerprint.Length != 0 &&
                     (c.LastAnswerFingerprint.Length != 64 ||
                      !c.LastAnswerFingerprint.All(Uri.IsHexDigit))))
                    throw new InvalidDataException("Invalid scheduled chat");
            }
            return store.Chats;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    internal static void Write(List<Chat> chats)
    {
        if (chats.Count > 48 || chats.Any(c =>
            !LiteDevWakeState.IsChatUrl(c.Url) || c.Minutes is < 20 or > 1440 ||
            c.Prompt.Length > 16000 ||
            (c.LastAnswerFingerprint.Length != 0 &&
             (c.LastAnswerFingerprint.Length != 64 ||
              !c.LastAnswerFingerprint.All(Uri.IsHexDigit)))) ||
            chats.Select(c => c.Url).Distinct(StringComparer.Ordinal).Count() != chats.Count)
            throw new InvalidDataException("Invalid chat wakes");
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Store { Chats = chats });
        byte[] cipher;
        try { cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        var dir = System.IO.Path.GetDirectoryName(PathOnDisk)!;
        Directory.CreateDirectory(dir);
        var tmp = PathOnDisk + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, cipher);
            File.Move(tmp, PathOnDisk, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    internal static Chat Configure(List<Chat> chats, string url, int minutes,
        string prompt, DateTimeOffset now)
    {
        url = LiteDevWakeState.NormalizeUrl(url);
        if (minutes is < 20 or > 1440 || string.IsNullOrWhiteSpace(prompt) ||
            prompt.Length > 16000) throw new ArgumentException("Invalid wake options");
        var entry = chats.FirstOrDefault(c => c.Url == url);
        if (entry is null) { entry = new Chat { Url = url }; chats.Add(entry); }
        entry.Enabled = true;
        entry.Minutes = minutes;
        entry.Prompt = prompt;
        entry.DueUtc = now.AddMinutes(minutes);
        entry.LastAnswerFingerprint = ""; // explicit reconfiguration permits one fresh wake
        entry.LastState = "armed";
        return entry;
    }

    internal static string Message(Chat entry, string notes)
    {
        var msg = string.IsNullOrWhiteSpace(entry.Prompt)
            ? "Wróć do pracy nad tą rozmową i kontynuuj ostatnie zadanie."
            : entry.Prompt.Trim();
        if (!string.IsNullOrWhiteSpace(notes))
        {
            // Explicitly indicate truncation instead of falsely implying all notes were delivered.
            const int maxNotes = 24000;
            string body = notes.Length <= maxNotes ? notes : notes[..maxNotes] +
                "\n[Notatki dłuższe niż 24000 znaków. Dalsza część pozostała w aplikacji.]";
            msg += "\n\nNotatki właściciela przypisane do tego czatu:\n" + body;
        }
        return msg;
    }

    internal static int SelfTest()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            string a = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            string b = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            var list = new List<Chat>();
            Configure(list, a, 25, "Rozwijaj projekt", now);
            Configure(list, b, 30, "Pisz następny rozdział", now);
            if (list.Count != 2 || list[0].DueUtc != now.AddMinutes(25) ||
                list[1].DueUtc != now.AddMinutes(30)) return 2;
            Configure(list, a, 45, "Kontynuuj", now.AddMinutes(1));
            if (list.Count != 2 || list[1].Prompt != "Pisz następny rozdział" ||
                list[0].DueUtc != now.AddMinutes(46)) return 3;
            if (!Message(list[1], "Zarys bohatera").Contains("Zarys bohatera")) return 4;
            if (Message(list[1], "").Contains("Notatki właściciela")) return 5;
            if (!Message(list[0], new string('x', 25000)).Contains("Dalsza część")) return 6;
            list[0].LastAnswerFingerprint = new string('A', 64);
            Configure(list, a, 25, "Renewed project work", now);
            if (list[0].LastAnswerFingerprint != "") return 7;
            int preflight = LiteChatWakePreflight.SelfTest();
            return preflight == 0 ? 0 : 10 + preflight;
        }
        catch { return 7; }
    }
}
