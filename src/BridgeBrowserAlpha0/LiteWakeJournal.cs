using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Single append-only wake lifecycle journal. Engine writes mechanical phases,
/// and the awakened chat may append chat_started/checkpoint/chat_finished records
/// to the SAME file via its authorized PBOX1 filesystem tool.
/// Never store chat URLs, prompts, cookies, headers, or response bodies.
/// </summary>
internal static class LiteWakeJournal
{
    internal static readonly string JournalPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "diagnostics", "wake-lifecycle.jsonl");

    private static readonly object Gate = new();
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly HashSet<string> Phases = new(StringComparer.Ordinal)
    {
        "due", "deferred", "attempt_reserved", "definitely_unsent",
        "submission_attempted", "receipt_confirmed", "receipt_uncertain",
        "recovery", "schedule_completed", "engine_failure"
    };

    internal static string MakeLine(string phase, long? sequence = null,
        string? receipt = null, string? result = null,
        DateTimeOffset? timestamp = null)
    {
        if (!Phases.Contains(phase)) throw new ArgumentException("Invalid event");
        if (sequence is < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (receipt is not null && (receipt.Length != 32 ||
            !receipt.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Invalid receipt");
        if (result is not null && (result.Length > 64 ||
            !result.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')))
            throw new ArgumentException("Invalid result label");
        return JsonSerializer.Serialize(new
        {
            schema = 1,
            utc = (timestamp ?? DateTimeOffset.UtcNow).ToString("O"),
            actor = "engine",
            stage = phase,
            wakeSequence = sequence,
            receipt,
            result
        });
    }

    internal static void Write(string phase, long? sequence = null,
        string? receipt = null, string? result = null)
    {
        try
        {
            string line = MakeLine(phase, sequence, receipt, result) +
                Environment.NewLine;
            byte[] bytes = Utf8.GetBytes(line);
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
                // Cross-process read/write sharing permits the model's filesystem
                // append to coexist; a single line is emitted per call.
                using var fs = new FileStream(JournalPath, FileMode.Append,
                    FileAccess.Write, FileShare.ReadWrite);
                fs.Write(bytes);
                fs.Flush(flushToDisk: true);
            }
        }
        catch (Exception ex)
        {
            // Journal failure must never interrupt a safely fenced wake.
            LiteEventLog.Write("wake_journal_error",
                new { phase, errorType = ex.GetType().Name });
        }
    }

    internal static int SelfTest()
    {
        try
        {
            string row = MakeLine("attempt_reserved", 27,
                "0123456789abcdef0123456789abcdef", "ready",
                DateTimeOffset.Parse("2026-10-22T15:00:00Z"));
            using var parsed = JsonDocument.Parse(row);
            var obj = parsed.RootElement;
            if (obj.GetProperty("schema").GetInt32() != 1 ||
                obj.GetProperty("stage").GetString() != "attempt_reserved" ||
                obj.GetProperty("wakeSequence").GetInt64() != 27 ||
                obj.GetProperty("actor").GetString() != "engine" ||
                !row.Contains("2026-10-22T15:00:00") ||
                row.Contains("chatgpt.com") || row.Contains("password"))
                return 1;
            string due = MakeLine("due");
            if (!due.Contains("\"receipt\":null")) return 2;
            try { MakeLine("attempt_reserved", receipt: "BAD-URL"); return 3; }
            catch (ArgumentException) { }
            try { MakeLine("deferred", result: "https://chatgpt.com/c/secret"); return 4; }
            catch (ArgumentException) { }
            return 0;
        }
        catch { return 5; }
    }
}
