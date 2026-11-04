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
        "submission_attempted", "notes_submitted", "receipt_confirmed", "receipt_uncertain",
        "recovery", "schedule_completed", "engine_failure",
        "cross_source_evidence", "receipt_resolved",
        "rotation_retry_rearmed"
    };

    // Bounded, privacy-safe diagnostic codes. Never write exception messages,
    // URLs or payloads to the shared mechanical journal.
    internal static string FailureCode(Exception error, bool attempted)
    {
        string kind = error switch
        {
            TimeoutException => "timeout",
            System.Runtime.InteropServices.COMException => "com",
            System.Text.Json.JsonException => "json",
            _ => "other"
        };
        return (attempted ? "post_attempt_" : "pre_attempt_") + kind;
    }

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
            if (FailureCode(new TimeoutException("private text"), false) != "pre_attempt_timeout" ||
                FailureCode(new TimeoutException("private text"), true) != "post_attempt_timeout" ||
                FailureCode(new System.Runtime.InteropServices.COMException(), false) != "pre_attempt_com" ||
                FailureCode(new JsonException(), true) != "post_attempt_json" ||
                FailureCode(new InvalidOperationException("https://secret.example/abc"), false) != "pre_attempt_other")
                return 6;
            string failure = MakeLine("engine_failure", 28,
                result: FailureCode(new TimeoutException("secret"), false));
            if (!failure.Contains("pre_attempt_timeout") ||
                failure.Contains("secret")) return 7;
            string due = MakeLine("due");
            if (!due.Contains("\"receipt\":null")) return 2;
            try { MakeLine("attempt_reserved", receipt: "BAD-URL"); return 3; }
            catch (ArgumentException) { }
            try { MakeLine("deferred", result: "https://chatgpt.com/c/secret"); return 4; }
            catch (ArgumentException) { }
            var now = DateTimeOffset.Parse("2026-10-28T14:00:00Z");
            if (!ChatGptLiteForm.ShouldRestoreChatView("no_composer",false,
                    now, now.AddMinutes(-10)) ||
                !ChatGptLiteForm.ShouldRestoreChatView("response_error",false,
                    now, DateTimeOffset.MinValue) ||
                ChatGptLiteForm.ShouldRestoreChatView("generating",false,
                    now, DateTimeOffset.MinValue) ||
                ChatGptLiteForm.ShouldRestoreChatView("no_composer",true,
                    now, DateTimeOffset.MinValue) ||
                ChatGptLiteForm.ShouldRestoreChatView("no_composer",false,
                    now, now.AddMinutes(-2))) return 12;            if (ChatGptLiteForm.ShouldRestoreChatView(
                    "no_composer", false, now, now.AddSeconds(-44),
                    TimeSpan.FromSeconds(45)) ||
                !ChatGptLiteForm.ShouldRestoreChatView(
                    "no_composer", false, now, now.AddSeconds(-45),
                    TimeSpan.FromSeconds(45)) ||
                ChatGptLiteForm.ShouldRestoreChatView(
                    "response_error", true, now, now.AddMinutes(-10),
                    TimeSpan.FromSeconds(45))) return 17;
            if (LiteWakeEvidenceReconciler.SelfTest() != 0) return 11;
            string submitted = MakeLine("notes_submitted", 27,
                "0123456789abcdef0123456789abcdef", "click_invoked");
            if (!submitted.Contains("notes_submitted") ||
                submitted.Contains("receipt_confirmed")) return 13;
            // Regression: uncertain successor rearm is a first-class journal stage.
            // A mismatched allowlist silently loses durable evidence, because Write
            // intentionally catches errors to avoid interrupting the wake.
            string rearmed = MakeLine("rotation_retry_rearmed", 205,
                "0123456789abcdef0123456789abcdef", "no_receipt_proof_after_3min");
            using var rearmedDoc = JsonDocument.Parse(rearmed);
            if (rearmedDoc.RootElement.GetProperty("stage").GetString() !=
                    "rotation_retry_rearmed" ||
                rearmedDoc.RootElement.GetProperty("wakeSequence").GetInt64() != 205 ||
                rearmedDoc.RootElement.GetProperty("result").GetString() !=
                    "no_receipt_proof_after_3min") return 18;
            string label = ChatGptLiteForm.FormatWakeStatus(14,
                DateTimeOffset.Parse("2026-10-28T15:03:00+00:00"), true);
            if (!label.Contains("Poprz. #000014") ||
                !label.Contains("Nast.") ||
                !ChatGptLiteForm.FormatWakeStatus(0,
                    DateTimeOffset.UtcNow,true).Contains("Poprz. brak") ||
                !ChatGptLiteForm.FormatWakeStatus(14,
                    DateTimeOffset.UtcNow,false).Contains("Pauza")) return 14;
            if (LiteCheckpointRetention.SelfTest() != 0) return 15;
            if (ChatGptLiteForm.NotesDebounceMs != 3000 ||
                !ChatGptLiteForm.SavedNotesStatus("12:00:00")
                    .Contains("NIE do checkpointu")) return 16;
            var t = DateTimeOffset.UtcNow;
            if (!ChatGptLiteForm.IsUnusablePage(false,0,"complete",
                    t.AddSeconds(-40),t) ||
                ChatGptLiteForm.IsUnusablePage(false,0,"loading",
                    t.AddSeconds(-40),t) ||
                ChatGptLiteForm.IsUnusablePage(true,0,"complete",
                    t.AddSeconds(-40),t) ||
                ChatGptLiteForm.IsUnusablePage(false,1,"complete",
                    t.AddSeconds(-40),t) ||
                ChatGptLiteForm.IsUnusablePage(false,0,"complete",
                    t.AddSeconds(-10),t)) return 17;
            if (ChatGptLiteForm.MayQuarantineTerminal(false,"verification_mismatch") ||
                ChatGptLiteForm.MayQuarantineTerminal(true,"invalid_chat_url") ||
                !ChatGptLiteForm.MayQuarantineTerminal(true,"verified")) return 17;
            return 0;
        }
        catch { return 5; }
    }
}
