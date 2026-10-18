namespace BridgeBrowserAlpha0;

/// <summary>Headless per-chat note persistence smoke test with temporary DPAPI files.</summary>
internal static class LiteNotesSelfTest
{
    internal static int Run()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "openbrowser-notes-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var notes = new LiteChatNotes(directory);
            string chatA = LiteChatNotes.NormalizeKey(
                "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111?utm=x");
            string chatB = LiteChatNotes.NormalizeKey(
                "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222");
            Assert(chatA != chatB, "different notes per chat");
            Assert(notes.Load(chatA) == "", "new notes empty");
            notes.Save(chatA, "Notatka pierwsza");
            notes.Save(chatB, "Notatka druga");
            Assert(notes.Load(chatA) == "Notatka pierwsza", "first note persisted");
            Assert(notes.Load(chatB) == "Notatka druga", "second note persisted");
            notes.Save(chatA, "Nowa wersja");
            Assert(notes.Load(chatA) == "Nowa wersja", "atomic replacement");
            Assert(Directory.GetFiles(directory, "*.dpapi").Length == 2, "one file per chat");
            byte[][] blobs = Directory.GetFiles(directory, "*.dpapi")
                .Select(File.ReadAllBytes).ToArray();
            Assert(blobs.All(b => !System.Text.Encoding.UTF8.GetString(b)
                .Contains("Notatka")), "disk files encrypted");
            Assert(LiteChatNotes.NormalizeKey("https://example.com") == "inbox",
                "unrecognized domain isolated");
            LiteEventLog.Write("notes_selftest", new { passed = true, checks = 8 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("notes_selftest", new {
                passed = false, errorType = ex.GetType().Name
            });
            return 2;
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch { }
        }
    }

    private static void Assert(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
    }
}
