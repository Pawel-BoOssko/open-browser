using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

public static class RedactedExport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static readonly HashSet<string> SafeRunEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "run_start",
        "webview_ready",
        "navigation_start",
        "navigation_completed",
        "extraction_update",
        "extraction_final",
        "parser_warning",
        "redacted_export",
        "error"
    };

    public static string ExportRun(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId) || runId == "no-run")
            throw new InvalidOperationException("No active run id is available.");

        AppPaths.EnsureAll();
        var exportRoot = Path.Combine(AppPaths.GithubExport, $"run_{runId}_github_redacted");
        Directory.CreateDirectory(exportRoot);

        var manifestItems = new List<object>();
        var errors = new List<object>();

        TryStep("copy_answer", errors, () => CopyIfExists(Path.Combine(AppPaths.Extracted, $"run_{runId}_answer.txt"), Path.Combine(exportRoot, "answer.txt"), manifestItems, "extracted_answer"));
        TryStep("copy_messages", errors, () => CopyIfExists(Path.Combine(AppPaths.Extracted, $"run_{runId}_messages.ndjson"), Path.Combine(exportRoot, "messages.ndjson"), manifestItems, "message_frames"));
        TryStep("write_redacted_run_log", errors, () => WriteRedactedRunLog(runId, Path.Combine(exportRoot, "run.redacted.ndjson"), manifestItems));
        TryStep("write_export_readme", errors, () => WriteSafetyReadme(exportRoot, runId));
        TryStep("write_synthetic_sample", errors, () => WriteSyntheticSample(exportRoot));

        var runLogPath = Path.Combine(AppPaths.Logs, $"run_{runId}.ndjson");
        var rawPath = Path.Combine(AppPaths.Extracted, $"run_{runId}_raw.ndjson");
        var answerPath = Path.Combine(AppPaths.Extracted, $"run_{runId}_answer.txt");
        var messagesPath = Path.Combine(AppPaths.Extracted, $"run_{runId}_messages.ndjson");

        var manifest = new
        {
            exportedAtUtc = DateTime.UtcNow.ToString("O"),
            runId,
            exportVersion = "v0.01.0-alpha.10",
            policy = "github_safe_redacted_export_v3",
            runLogStatus = File.Exists(runLogPath) ? (new FileInfo(runLogPath).Length == 0 ? "empty" : "present") : "missing",
            status = errors.Count == 0 ? "ok" : "partial",
            sourceFiles = new
            {
                runLog = FileStatus(runLogPath),
                raw = FileStatus(rawPath),
                answer = FileStatus(answerPath),
                messages = FileStatus(messagesPath)
            },
            included = manifestItems,
            errors,
            excludedByDesign = new[]
            {
                "run_*_raw.ndjson",
                "full CDP response bodies",
                "page_fetch_chunk raw payloads",
                "cookies",
                "authorization headers",
                "query tokens",
                "sentinel/proof/verify/session-like values"
            }
        };
        File.WriteAllText(Path.Combine(exportRoot, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonOptions){ WriteIndented = true }), new UTF8Encoding(false));
        return exportRoot;
    }

    private static void TryStep(string step, List<object> errors, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            errors.Add(new { step, error = ex.GetType().Name, ex.Message });
        }
    }

    private static object FileStatus(string path)
    {
        if (!File.Exists(path)) return new { path, exists = false, bytes = 0L, status = "missing" };
        var info = new FileInfo(path);
        return new { path, exists = true, bytes = info.Length, status = info.Length == 0 ? "empty" : "ok", lastWriteTimeUtc = info.LastWriteTimeUtc.ToString("O") };
    }

    private static void CopyIfExists(string source, string target, List<object> manifestItems, string kind)
    {
        if (!File.Exists(source))
        {
            manifestItems.Add(new { kind, file = Path.GetFileName(target), copied = false, sourceStatus = "missing" });
            return;
        }

        File.Copy(source, target, true);
        manifestItems.Add(new { kind, file = Path.GetFileName(target), copied = true, bytes = new FileInfo(target).Length });
    }

    private static void WriteRedactedRunLog(string runId, string target, List<object> manifestItems)
    {
        var source = Path.Combine(AppPaths.Logs, $"run_{runId}.ndjson");
        using var writer = new StreamWriter(new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };

        if (!File.Exists(source))
        {
            writer.WriteLine(JsonSerializer.Serialize(new { tsUtc = DateTime.UtcNow.ToString("O"), runId, source = "export", eventType = "export_notice", status = "missing_source_run_log", message = "Source run log was not found during export." }, JsonOptions));
            manifestItems.Add(new { kind = "redacted_run_log", file = Path.GetFileName(target), sourceStatus = "missing", bytes = new FileInfo(target).Length });
            return;
        }

        var info = new FileInfo(source);
        if (info.Length == 0)
        {
            writer.WriteLine(JsonSerializer.Serialize(new { tsUtc = DateTime.UtcNow.ToString("O"), runId, source = "export", eventType = "export_notice", status = "empty_source_run_log", message = "Source run log existed but had zero bytes during export." }, JsonOptions));
            manifestItems.Add(new { kind = "redacted_run_log", file = Path.GetFileName(target), sourceStatus = "empty", bytes = new FileInfo(target).Length });
            return;
        }

        var kept = 0;
        var skipped = 0;
        foreach (var line in File.ReadLines(source, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var eventType = root.TryGetProperty("eventType", out var et) && et.ValueKind == JsonValueKind.String ? et.GetString() ?? "" : "";
                if (!SafeRunEventTypes.Contains(eventType))
                {
                    skipped++;
                    continue;
                }

                var safe = MakeSafeRecord(root);
                writer.WriteLine(JsonSerializer.Serialize(safe, JsonOptions));
                kept++;
            }
            catch
            {
                skipped++;
            }
        }

        writer.WriteLine(JsonSerializer.Serialize(new
        {
            tsUtc = DateTime.UtcNow.ToString("O"),
            runId,
            source = "export",
            eventType = "export_summary",
            status = "ok",
            data = new { kept, skipped, policy = "safe-event allowlist; raw chunks and CDP bodies excluded" }
        }, JsonOptions));
        manifestItems.Add(new { kind = "redacted_run_log", file = Path.GetFileName(target), sourceStatus = "ok", kept, skipped, bytes = new FileInfo(target).Length });
    }

    private static object MakeSafeRecord(JsonElement root)
    {
        string? GetStr(string name) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? Redactor.RedactString(p.GetString() ?? "") : null;
        object? data = null;
        if (root.TryGetProperty("data", out var dataEl))
            data = PruneAndRedact(dataEl, 0);

        return new
        {
            tsUtc = GetStr("tsUtc"),
            seq = root.TryGetProperty("seq", out var seq) && seq.TryGetInt32(out var i) ? i : 0,
            runId = GetStr("runId"),
            tabInternalId = GetStr("tabInternalId"),
            source = GetStr("source"),
            eventType = GetStr("eventType"),
            status = GetStr("status"),
            message = GetStr("message"),
            data
        };
    }

    private static object? PruneAndRedact(JsonElement e, int depth)
    {
        if (depth > 8) return "[MAX_DEPTH]";
        return e.ValueKind switch
        {
            JsonValueKind.Object => PruneObject(e, depth),
            JsonValueKind.Array => e.EnumerateArray().Take(100).Select(x => PruneAndRedact(x, depth + 1)).ToArray(),
            JsonValueKind.String => Redactor.RedactString(e.GetString() ?? ""),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => Redactor.RedactString(e.ToString())
        };
    }

    private static Dictionary<string, object?> PruneObject(JsonElement e, int depth)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in e.EnumerateObject())
        {
            if (IsDangerousExportField(p.Name))
            {
                result[p.Name] = "[EXCLUDED_FROM_GITHUB_EXPORT]";
                continue;
            }
            result[p.Name] = PruneAndRedact(p.Value, depth + 1);
        }
        return result;
    }

    private static bool IsDangerousExportField(string name)
    {
        var n = name.ToLowerInvariant();
        return n is "raw" or "body" or "chunk" or "responsetext" or "headers" or "requestheaders" or "responseheaders"
               || n.Contains("cookie") || n.Contains("authorization") || n.Contains("token") || n.Contains("session")
               || n.Contains("sentinel") || n.Contains("proof") || n.Contains("verify");
    }

    private static void WriteSafetyReadme(string exportRoot, string runId)
    {
        var lines = new List<string>
        {
            "# Redacted GitHub export",
            "",
            "Run: " + runId,
            "",
            "This folder is intended for a future public case study. It intentionally excludes raw stream files and full network bodies.",
            "",
            "Included files:",
            "",
            "1. answer.txt: reconstructed assistant responses;",
            "2. messages.ndjson: message-frame diagnostics;",
            "3. run.redacted.ndjson: allowlisted operational events only or an explicit export_notice;",
            "4. synthetic_messages.ndjson: synthetic example format;",
            "5. manifest.json: source and export status.",
            "",
            "Excluded by design:",
            "",
            "1. run_*_raw.ndjson;",
            "2. raw page_fetch_chunk payloads;",
            "3. full cdp_response_body data;",
            "4. cookies, authorization headers, query tokens, sentinel/proof/verify/session-like values;",
            "5. WebView2 profile files.",
            "",
            "If run.redacted.ndjson contains only an export_notice, inspect manifest.json: the source run log may have been missing or empty.",
            "Before publication, inspect every file manually.",
            ""
        };
        File.WriteAllLines(Path.Combine(exportRoot, "README_EXPORT.md"), lines, new UTF8Encoding(false));
    }

    private static void WriteSyntheticSample(string exportRoot)
    {
        var lines = new[]
        {
            JsonSerializer.Serialize(new { messageId = "synthetic-message-1", startedAt = "2026-05-09T12:00:00.0000000Z", endedAt = "2026-05-09T12:00:01.0000000Z", role = "assistant", channel = "final", chars = 31, deltaCount = 3, status = "complete", endReason = "last_token" }, JsonOptions),
            JsonSerializer.Serialize(new { messageId = "synthetic-message-2", startedAt = "2026-05-09T12:00:10.0000000Z", endedAt = "2026-05-09T12:00:11.0000000Z", role = "assistant", channel = "final", chars = 47, deltaCount = 3, status = "complete", endReason = "last_token" }, JsonOptions)
        };
        File.WriteAllLines(Path.Combine(exportRoot, "synthetic_messages.ndjson"), lines, new UTF8Encoding(false));
    }
}
