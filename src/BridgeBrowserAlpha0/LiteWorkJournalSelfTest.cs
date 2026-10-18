using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>Headless synthetic append/recovery contract for the work journal.</summary>
internal static class LiteWorkJournalSelfTest
{
    internal static int Run()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "ob-lite-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var journal = new LiteWorkJournal(directory);
            var now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
            string first = journal.Append("project-1", "chat-A", "task-1",
                "task_started", 1, now);
            string second = journal.Append("project-1", "chat-A", "task-1",
                "task_checkpoint", 1, now.AddSeconds(1));
            Require(first != second, "unique event IDs");
            var initial = journal.ReadLast("project-1", now);
            Require(initial.Count == 2, "two durable events");
            Require(initial[0].Kind == "task_started" &&
                initial[1].Kind == "task_checkpoint", "append ordering");
            bool rejected = false;
            try { journal.Append("project-1", "chat-A", "task-1",
                "private-text-goes-here", 1, now); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "unknown payload field rejected");
            rejected = false;
            try { journal.Append("../other", "chat-A", "task-1",
                "task_started", 1, now); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "unsafe project ID rejected");
            string path = Directory.GetFiles(directory, "*.jsonl").Single();
            File.AppendAllText(path, "{truncated\n", new UTF8Encoding(false));
            journal.Append("project-1", "chat-B", "task-1",
                "handoff_confirmed", 2, now.AddSeconds(2));
            var recovered = new LiteWorkJournal(directory).ReadLast("project-1", now);
            Require(recovered.Count == 3, "skip damaged line, retain later events");
            Require(recovered[^1].LeaseEpoch == 2, "fencing epoch retained");
            Require(journal.ReadLast("project-1", now, 1).Count == 1,
                "bounded reader");
            Require(journal.ReadLast("project-2", now).Count == 0,
                "projects isolated");
            LiteEventLog.Write("lite_journal_selftest", new { passed = true, checks = 9 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("lite_journal_selftest", new {
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

    private static void Require(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Journal selftest: " + name);
    }
}
