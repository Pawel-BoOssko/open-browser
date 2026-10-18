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
            var first = outbox.Enqueue(chat, sample);
            Check(first.State == "pending" && first.Sequence == 1, "first queued");
            Check(outbox.Enqueue(chat, sample).Id == first.Id, "duplicate suppressed");
            Check(outbox.List(other).Count == 0, "chat isolation");
            Check(outbox.List(chat).Single().Text == sample, "roundtrip DPAPI");
            string path = Directory.GetFiles(directory, "*.dpapi").Single();
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(sample),
                "no plaintext on disk");

            var now = new DateTimeOffset(2026, 10, 18, 14, 0, 0, TimeSpan.Zero);
            var valid = new LiteNotesOutbox.IdleEvidence(chat,
                now.AddSeconds(-8), now, false, false, true, false, true, 5);
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid with { ToolActive = null }, now),
                "unknown tool state blocks dispatch");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid with { Generating = true }, now),
                "generation blocks dispatch");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid with { ChatKey = other }, now),
                "wrong chat blocks dispatch");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid with { ProjectLeadConfirmed = null }, now),
                "unconfirmed lead blocks dispatch");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid with { LeaseEpoch = 0 }, now),
                "missing fencing blocks dispatch");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid with { FirstUtc = now.AddSeconds(-1) }, now),
                "single transient idle blocks dispatch");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid, now.AddMinutes(1)),
                "stale evidence blocks dispatch");
            Check(outbox.TryBeginDispatch(chat, first.Id, valid, now),
                "two observations with lease allow claim");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid, now),
                "claimed message never claimed twice");
            Check(!outbox.MarkResult(chat, first.Id, 4, true),
                "wrong lease epoch cannot acknowledge");
            Check(outbox.RecoverInterrupted(chat) == 1, "crash becomes uncertain");
            Check(outbox.RecoverInterrupted(chat) == 0, "recovery idempotent");
            Check(!outbox.TryBeginDispatch(chat, first.Id, valid, now),
                "uncertain never automatically retried");
            Check(outbox.Enqueue(chat, sample).Id == first.Id,
                "uncertain not silently duplicated");
            var second = outbox.Enqueue(chat, "DRUGA SYNTETYCZNA NOTATKA");
            Check(second.Sequence == 2, "sequence persisted");
            Check(outbox.TryBeginDispatch(chat, second.Id, valid, now), "next dispatch claim");
            Check(outbox.MarkResult(chat, second.Id, 5, true), "explicit delivery confirmation");
            Check(outbox.List(chat).Last().State == "delivered", "delivered persisted");
            LiteEventLog.Write("notes_outbox_selftest", new { passed = true, checks = 21 });
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
