using System.Text;

namespace BridgeBrowserAlpha0;

internal static class LiteNotesOutboxSelfTest
{
    internal static int Run()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "ob-lite-outbox-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var outbox = new LiteNotesOutbox(directory);
            const string chat = "chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            const string other = "chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            const string sample = "SYNTETYCZNA NOTATKA TESTOWA";
            var first = outbox.EnqueueDraft(chat, sample);
            Check(first is not null && first.Status == LiteNotesOutbox.Phase.Queued,
                "first queued");
            Check(outbox.EnqueueDraft(chat, sample) is null, "duplicate suppressed");
            Check(outbox.List(other).Count == 0, "chat isolation");
            Check(outbox.List(chat).Single().Text == sample, "roundtrip DPAPI");
            string path = Directory.GetFiles(directory, "*.dpapi").Single();
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(sample),
                "no plaintext on disk");
            var second = outbox.EnqueueDraft(chat, sample + " DODATEK");
            Check(second is not null && second.Text == " DODATEK", "append-only delta");
            var third = outbox.EnqueueDraft(chat, "ZMIENIONA NOTATKA");
            Check(third is not null && third.Text == "ZMIENIONA NOTATKA",
                "edited earlier paragraph kept in full");
            Check(outbox.NextSafelyQueued(chat)?.Id == first!.Id,
                "first pending item");
            Check(!outbox.Transition(chat, first.Id,
                LiteNotesOutbox.Phase.Queued, LiteNotesOutbox.Phase.Confirmed),
                "cannot skip preparation");
            Check(outbox.Transition(chat, first.Id,
                LiteNotesOutbox.Phase.Queued, LiteNotesOutbox.Phase.Prepared),
                "prepare");
            Check(!outbox.Transition(chat, first.Id,
                LiteNotesOutbox.Phase.Queued, LiteNotesOutbox.Phase.Prepared),
                "no double prepare");
            Check(outbox.Transition(chat, first.Id,
                LiteNotesOutbox.Phase.Prepared, LiteNotesOutbox.Phase.DeliveryUncertain),
                "uncertain before delivery");
            Check(outbox.NextSafelyQueued(chat) is null,
                "uncertain delivery blocks further dispatch");
            Check(outbox.List(chat).First().DeliveryAttempts == 1,
                "attempt recorded");
            Check(outbox.Transition(chat, first.Id,
                LiteNotesOutbox.Phase.DeliveryUncertain, LiteNotesOutbox.Phase.Confirmed),
                "explicit confirmation");
            Check(outbox.NextSafelyQueued(chat)?.Id == second!.Id,
                "pending resumes only after confirmation");
            Check(outbox.List(chat).Count == 3, "three entries persisted");
            LiteEventLog.Write("notes_outbox_selftest", new { passed = true, checks = 17 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("notes_outbox_selftest", new {
                passed = false, errorType = ex.GetType().Name, errorCode = ex.HResult
            });
            return 2;
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch { }
        }
    }

    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException("Outbox selftest: " + description);
    }
}
