using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Portable, readable UTF-8 copies of model answers. Never stores credentials
/// or browser profile data. Private local AppData ACLs protect this directory.
/// </summary>
internal sealed partial class LiteResponseMarkdownArchive
{
    private readonly string _root;
    private readonly object _recoveryGate = new();
    // Coordinated with scripts/obl-archive-retention-satanamd.ps1, including
    // the scheduled job running in a different Windows session. The mutex
    // fences Save, recovery and retention across processes, not just threads.
    private static readonly Mutex ArchiveTransactionMutex =
        new(false, @"Global\OBL_Response_Archive_Transaction_V1");
    private static bool EnterArchiveTransaction()
    {
        try { return ArchiveTransactionMutex.WaitOne(TimeSpan.FromSeconds(8)); }
        catch (AbandonedMutexException) { return true; } // We own the mutex.
    }
    // Cache the durable filesystem state, not a permanent per-process flag.
    // A later retention compaction or orphaned atomic Markdown rename must
    // trigger reconciliation without restarting the browser.
    private readonly Dictionary<string, (long IndexLength, long IndexTicks, long DirTicks)>
        _recoveredChats = new(StringComparer.Ordinal);
    private static (long IndexLength, long IndexTicks, long DirTicks)
        RecoverySnapshot(string directory)
    {
        if (!Directory.Exists(directory)) return (-1, -1, -1);
        var index = new FileInfo(Path.Combine(directory, "index.jsonl"));
        return (index.Exists ? index.Length : -1,
            index.Exists ? index.LastWriteTimeUtc.Ticks : -1,
            Directory.GetLastWriteTimeUtc(directory).Ticks);
    }
    // Only an explicit isolated crash-test child assigns this hook. It blocks
    // precisely after atomic rename, before durable JSONL append.
    internal static Action? CrashTestAfterAtomicRename;
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

    // An interrupted save can leave the atomic Markdown file on disk without
    // its later index append. Replaying the same answer must repair the index.
    private static bool HasIndexEntry(string path, string fileName,
        string digest, bool partial)
    {
        if (!File.Exists(path)) return false;
        string expectedStatus = partial ? "PARTIAL" : "FINAL";
        foreach (string line in File.ReadLines(path))
        {
            try
            {
                using var json = JsonDocument.Parse(line);
                var record = json.RootElement;
                if (record.ValueKind == JsonValueKind.Object &&
                    record.TryGetProperty("fileName", out var file) &&
                    record.TryGetProperty("sha256", out var sha) &&
                    record.TryGetProperty("status", out var status) &&
                    record.TryGetProperty("turnKey", out var turn) &&
                    record.TryGetProperty("chatKey", out var chat) &&
                    file.ValueKind == JsonValueKind.String &&
                    sha.ValueKind == JsonValueKind.String &&
                    status.ValueKind == JsonValueKind.String &&
                    turn.ValueKind == JsonValueKind.String &&
                    chat.ValueKind == JsonValueKind.String &&
                    file.GetString() == fileName &&
                    sha.GetString() == digest &&
                    status.GetString() == expectedStatus &&
                    fileName.Length >= 16 &&
                    string.Equals(turn.GetString(), fileName[..16],
                        StringComparison.Ordinal) &&
                    string.Equals(chat.GetString(),
                        Path.GetFileName(Path.GetDirectoryName(path)),
                        StringComparison.Ordinal))
                    return true;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            { /* Ignore interrupted or invalid index lines. */ }
        }
        return false;
    }

    // A crash can leave a JSONL tail without its trailing newline. Preserve
    // invalid bytes separately before removing them from the active index.
    private static void RepairIndexTail(FileStream stream, string path)
    {
        // Multiple consecutive malformed rows can accumulate after crashes.
        // Quarantine each trailing row until a valid durable record is found.
        while (stream.Length > 0)
        {
        stream.Position = stream.Length - 1;
        bool terminated = stream.ReadByte() == (byte)'\n';
        // Validate the last record even when it ends with LF: a crash or
        // interrupted writer can leave a complete but malformed JSONL line.
        const int maxTailBytes = 1_048_576;
        long start = stream.Length - (terminated ? 1 : 0);
        while (start > 0 && stream.Length - start < maxTailBytes)
        {
            stream.Position = --start;
            if (stream.ReadByte() == (byte)'\n') { start++; break; }
        }
        if (start > 0 && stream.Length - start >= maxTailBytes)
            throw new InvalidDataException("Index tail exceeds bounded recovery size");
        var tail = new byte[checked((int)(stream.Length - start))];
        stream.Position = start;
        stream.ReadExactly(tail);
        bool complete = false;
        try
        {
            using var json = JsonDocument.Parse(tail);
            var row = json.RootElement;
            complete = row.ValueKind == JsonValueKind.Object &&
                row.TryGetProperty("fileName", out var file) &&
                file.ValueKind == JsonValueKind.String &&
                Regex.IsMatch(file.GetString() ?? "",
                    @"^[a-f0-9]{16}_(?:partial|[a-f0-9]{16}(?:_partial)?)\.md$") &&
                row.TryGetProperty("sha256", out var sha) &&
                sha.ValueKind == JsonValueKind.String &&
                Regex.IsMatch(sha.GetString() ?? "", @"^[a-f0-9]{64}$") &&
                row.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.String &&
                ((status.GetString() == "PARTIAL" &&
                  file.GetString()!.EndsWith("_partial.md", StringComparison.Ordinal)) ||
                 (status.GetString() == "FINAL" &&
                  !file.GetString()!.EndsWith("_partial.md", StringComparison.Ordinal)));
            if (complete)
            {
                // Immutable file names embed the first 16 SHA256 digits.
                // A schema-shaped index row with a different prefix is corrupt.
                string fileName = row.GetProperty("fileName").GetString()!;
                string digest = row.GetProperty("sha256").GetString()!;
                var immutable = Regex.Match(fileName,
                    @"^[a-f0-9]{16}_(?<digest>[a-f0-9]{16})(?:_partial)?\.md$");
                if (immutable.Success &&
                    !digest.StartsWith(immutable.Groups["digest"].Value,
                        StringComparison.Ordinal))
                    complete = false;
                // Reject well-formed rows that falsely identify the chat or
                // turn: a digest alone is not sufficient for a pruning ACK.
                complete = complete &&
                    row.TryGetProperty("turnKey", out var turn) &&
                    turn.ValueKind == JsonValueKind.String &&
                    string.Equals(turn.GetString(), fileName[..16],
                        StringComparison.Ordinal) &&
                    row.TryGetProperty("chatKey", out var chat) &&
                    chat.ValueKind == JsonValueKind.String &&
                    string.Equals(chat.GetString(),
                        Path.GetFileName(Path.GetDirectoryName(path)),
                        StringComparison.Ordinal);
            }
        }
        catch (JsonException) { /* A torn JSON record cannot stay in JSONL. */ }
        if (complete)
        {
            if (!terminated)
            {
                stream.Position = stream.Length;
                stream.WriteByte((byte)'\n');
            }
            return;
        }
        string backup = path + ".torn-" + Guid.NewGuid().ToString("N") + ".txt";
        using (var copy = new FileStream(backup, FileMode.CreateNew,
            FileAccess.Write, FileShare.None))
        {
            copy.Write(tail);
            copy.Flush(flushToDisk: true);
        }
        stream.SetLength(start);
        LiteEventLog.Write("assistant_index_tail_quarantined", new {
            bytes = tail.Length, backupName = Path.GetFileName(backup) });
        } // Continue until the final surviving index record is valid.
    }

    // An ACK must not precede durable index metadata. Complete every JSONL
    // append with an OS flush to disk; recovery handles torn final lines.
    private static void AppendIndexRecord(string path, object record)
    {
        using var stream = new FileStream(path, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.Read);
        RepairIndexTail(stream, path);
        stream.Seek(0, SeekOrigin.End);
        byte[] jsonl = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record) + "\n");
        stream.Write(jsonl);
        stream.Flush(flushToDisk: true);
    }

    // Reconcile durable Markdown after restart AND after any subsequent
    // on-disk mutation (external retention, orphan rename, index rewrite).
    // No new model response or network request is required.
    internal int RecoverMissingIndex(string conversationUrl)
    {
        if (string.IsNullOrWhiteSpace(conversationUrl)) return 0;
        string chatKey = Hash(conversationUrl);
        lock (_recoveryGate)
        {
            if (!EnterArchiveTransaction())
                throw new TimeoutException("ARCHIVE_TRANSACTION_BUSY");
            try
            {
            string directory = Path.Combine(_root, chatKey);
            var current = RecoverySnapshot(directory);
            if (_recoveredChats.TryGetValue(chatKey, out var previous) &&
                previous == current) return 0;
            int repaired = 0;
            bool transientReadFailure = false;
            try
            {
                if (Directory.Exists(directory))
                {
                    string index = Path.Combine(directory, "index.jsonl");
                    // Repair an interrupted JSONL tail on restart even when
                    // no Markdown file needs a new index record. Otherwise a
                    // corrupt tail can remain indefinitely until a new answer.
                    if (File.Exists(index))
                    {
                        using var stream = new FileStream(index, FileMode.Open,
                            FileAccess.ReadWrite, FileShare.Read);
                        RepairIndexTail(stream, index);
                        stream.Flush(flushToDisk: true);
                    }
                    foreach (string file in Directory.EnumerateFiles(directory, "*.md"))
                    {
                        string name = Path.GetFileName(file);
                        var match = Regex.Match(name,
                            @"^(?<turn>[a-f0-9]{16})_(?<suffix>partial|[a-f0-9]{16}(?:_partial)?)\.md$");
                        if (!match.Success) continue;
                        string content;
                        try { content = Clean(File.ReadAllText(file, Encoding.UTF8)); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            // A temporarily locked file must not block recovery
                            // of unrelated answers. Try it again next poll.
                            transientReadFailure = true;
                            LiteEventLog.Write("archive_index_file_unreadable", new {
                                fileName = name, errorType = ex.GetType().Name });
                            continue;
                        }
                        if (content.Length < 5 || content.Length > 200_000) continue;
                        string digest = Hash(content);
                        string suffix = match.Groups["suffix"].Value;
                        bool partial = suffix == "partial" ||
                            suffix.EndsWith("_partial", StringComparison.Ordinal);
                        if (suffix != "partial" && suffix[..16] != digest[..16])
                            continue; // Reject tampered immutable FINAL or PARTIAL filenames.
                        if (HasIndexEntry(index, name, digest, partial)) continue;
                        var record = new
                        {
                            utc = DateTimeOffset.UtcNow.ToString("O"),
                            chatKey, turnKey = match.Groups["turn"].Value,
                            sha256 = digest, status = partial ? "PARTIAL" : "FINAL",
                            fileName = name, characters = content.Length, sanitizer = "v1"
                        };
                        AppendIndexRecord(index, record);
                        repaired++;
                    }
                }
                // Cache the POST-repair state. Any later external rewrite or
                // orphan creation invalidates it and triggers another scan.
                // Transient read errors must always be retried next poll.
                if (!transientReadFailure)
                    _recoveredChats[chatKey] = RecoverySnapshot(directory);
                else
                    _recoveredChats.Remove(chatKey);
                return repaired;
            }
            catch
            {
                _recoveredChats.Remove(chatKey);
                throw;
            }
            }
            finally { ArchiveTransactionMutex.ReleaseMutex(); }
        }
    }

    internal bool Save(string conversationUrl, string turnId,
        string displayedText, bool partial)
    {
        // Foreground and worker can complete captures concurrently. Use the
        // same gate as recovery so a JSONL append is one indivisible action.
        lock (_recoveryGate)
        {
            if (!EnterArchiveTransaction())
                throw new TimeoutException("ARCHIVE_TRANSACTION_BUSY");
            try { return SaveCore(conversationUrl, turnId, displayedText, partial); }
            finally { ArchiveTransactionMutex.ReleaseMutex(); }
        }
    }

    private bool SaveCore(string conversationUrl, string turnId,
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
        // Every index row must point to immutable bytes. The former shared
        // turnKey_partial.md was overwritten as streaming grew, leaving old
        // PARTIAL hashes dangling in index.jsonl.
        string fileName = partial
            ? turnKey + "_" + digest[..16] + "_partial.md"
            : turnKey + "_" + digest[..16] + ".md";
        string file = Path.Combine(directory, fileName);
        byte[] utf8 = Encoding.UTF8.GetBytes(cleaned + "\n");
        bool unchanged = File.Exists(file) &&
            File.ReadAllBytes(file).AsSpan().SequenceEqual(utf8);
        if (!unchanged)
        {
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
                CrashTestAfterAtomicRename?.Invoke();
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        string index = Path.Combine(directory, "index.jsonl");
        // Content can survive a crash after rename, before index append.
        // Never skip that append merely because the answer file is identical.
        if (HasIndexEntry(index, fileName, digest, partial))
            return !unchanged;

        // Index does not contain response content or private conversation URL.
        var record = new {
            utc = DateTimeOffset.UtcNow.ToString("O"),
            chatKey, turnKey, sha256 = digest,
            status = partial ? "PARTIAL" : "FINAL",
            fileName, characters = cleaned.Length, sanitizer = "v1"
        };
        AppendIndexRecord(index, record);
        LiteEventLog.Write("assistant_markdown_saved", new {
            status = partial ? "PARTIAL" : "FINAL",
            characters = cleaned.Length, digestPrefix = digest[..12]
        });
        return true;
    }

    internal bool IsPersisted(string conversationUrl, string turnId,
        string displayedText)
    {
        lock (_recoveryGate)
        {
            if (!EnterArchiveTransaction()) return false; // No pruning ACK.
            try { return IsPersistedCore(conversationUrl, turnId, displayedText); }
            finally { ArchiveTransactionMutex.ReleaseMutex(); }
        }
    }

    private bool IsPersistedCore(string conversationUrl, string turnId,
        string displayedText)
    {
        if (string.IsNullOrWhiteSpace(conversationUrl) ||
            string.IsNullOrWhiteSpace(turnId)) return false;
        string cleaned = Clean(displayedText);
        if (cleaned.Length < 5 || cleaned.Length > 200_000) return false;
        string directory = Path.Combine(_root, Hash(conversationUrl));
        string digest = Hash(cleaned);
        string fileName = Hash(turnId)[..16] + "_" + digest[..16] + ".md";
        string path = Path.Combine(directory, fileName);
        // The hard-DOM prune ACK requires both the durable content AND
        // its durable index record. A crash between those writes must not
        // remove the only renderer copy before the index is repaired.
        return File.Exists(path) &&
            string.Equals(File.ReadAllText(path, Encoding.UTF8),
                cleaned + "\n", StringComparison.Ordinal) &&
            HasIndexEntry(Path.Combine(directory, "index.jsonl"),
                fileName, digest, partial: false);
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
            if (!store.IsPersisted(url, "turn1", "Zakończona odpowiedź")) return 8;
            if (store.IsPersisted(url, "turn1", "Nieistniejąca odpowiedź")) return 9;
            string[] files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
            if (files.Length != 2 ||
                !files.Any(x => File.ReadAllText(x).Contains("Zakończona odpowiedź")))
                return 5;
            string[] indexes = Directory.GetFiles(dir, "index.jsonl", SearchOption.AllDirectories);
            if (indexes.Length != 1 || File.ReadAllLines(indexes[0]).Length != 2)
                return 6;
            // Simulate a crash after the FINAL file is atomically renamed,
            // but before its index line can be appended. Identical retry
            // must repair the missing entry, without rewriting the answer.
            string[] originalIndex = File.ReadAllLines(indexes[0]);
            File.WriteAllLines(indexes[0], originalIndex.Take(1),
                new UTF8Encoding(false));
            if (store.IsPersisted(url, "turn1", "Zakończona odpowiedź"))
                return 16; // Never ACK DOM pruning with a missing index.
            if (!store.Save(url, "turn1", "Zakończona odpowiedź", false))
                return 10;
            if (File.ReadAllLines(indexes[0]).Length != 2) return 11;
            if (!store.IsPersisted(url, "turn1", "Zakończona odpowiedź"))
                return 17;
            if (store.Save(url, "turn1", "Zakończona odpowiedź", false))
                return 12;
            // The same recovery must work for a PARTIAL response.
            File.WriteAllLines(indexes[0], originalIndex.Skip(1),
                new UTF8Encoding(false));
            if (!store.Save(url, "turn1", "Pierwszy fragment", true))
                return 13;
            if (File.ReadAllLines(indexes[0]).Length != 2) return 14;
            if (store.Save(url, "turn1", "Pierwszy fragment", true))
                return 15;
            // Simulate a torn JSONL append. The next record must be on its
            // own parseable line, not concatenated to the unfinished fragment.
            File.AppendAllText(indexes[0], "{\"truncated\":",
                new UTF8Encoding(false));
            if (!store.Save(url, "turn2", "Nowa odpowiedź po awarii", false))
                return 18;
            if (!store.IsPersisted(url, "turn2", "Nowa odpowiedź po awarii"))
                return 19;
            string[] repaired = File.ReadAllLines(indexes[0]);
            if (repaired.Length != 3) return 20;
            foreach (string row in repaired)
                using (JsonDocument.Parse(row)) { }
            var tornCopies = Directory.GetFiles(Path.GetDirectoryName(indexes[0])!,
                "index.jsonl.torn-*.txt");
            if (tornCopies.Length != 1 ||
                File.ReadAllText(tornCopies[0]) != "{\"truncated\":") return 36;
            // A fully written JSON record with a missing newline is valid.
            File.WriteAllText(indexes[0], File.ReadAllText(indexes[0]).TrimEnd('\n'),
                new UTF8Encoding(false));
            if (!store.Save(url, "turn3", "Kolejny pełny zapis", false)) return 37;
            var completeRows = File.ReadAllLines(indexes[0]);
            if (completeRows.Length != 4) return 38;
            foreach (string row in completeRows)
                using (JsonDocument.Parse(row)) { }
            if (Directory.GetFiles(Path.GetDirectoryName(indexes[0])!,
                "index.jsonl.torn-*.txt").Length != 1) return 39;

            // A restart with no orphaned Markdown must still repair a torn
            // index tail immediately, without awaiting a future answer.
            const string passiveUrl = "https://chatgpt.com/c/77777777-7777-7777-7777-777777777777";
            if (!store.Save(passiveUrl, "passive", "Durable passive answer", false))
                return 49;
            string passiveDir = Path.Combine(dir, Hash(passiveUrl));
            string passiveIndex = Path.Combine(passiveDir, "index.jsonl");
            File.AppendAllText(passiveIndex, "{\"interrupted\":", new UTF8Encoding(false));
            var passiveRestart = new LiteResponseMarkdownArchive(dir);
            if (passiveRestart.RecoverMissingIndex(passiveUrl) != 0) return 50;
            string[] passiveLines = File.ReadAllLines(passiveIndex);
            if (passiveLines.Length != 1) return 51;
            using (JsonDocument.Parse(passiveLines[0])) { }
            string[] passiveQuarantine = Directory.GetFiles(passiveDir,
                "index.jsonl.torn-*.txt");
            if (passiveQuarantine.Length != 1 ||
                File.ReadAllText(passiveQuarantine[0]) != "{\"interrupted\":")
                return 52;
            if (!passiveRestart.IsPersisted(passiveUrl, "passive",
                "Durable passive answer")) return 53;

            // A complete newline-free last record is normalized on restart
            // even if the chat produces no further messages.
            const string newlineUrl = "https://chatgpt.com/c/88888888-8888-8888-8888-888888888888";
            if (!store.Save(newlineUrl, "old", "Valid newline-free record", false))
                return 54;
            string newlineDir = Path.Combine(dir, Hash(newlineUrl));
            string newlineIndex = Path.Combine(newlineDir, "index.jsonl");
            File.WriteAllText(newlineIndex,
                File.ReadAllText(newlineIndex).TrimEnd('\n'), new UTF8Encoding(false));
            if (passiveRestart.RecoverMissingIndex(newlineUrl) != 0) return 55;
            if (!File.ReadAllText(newlineIndex).EndsWith('\n')) return 56;
            if (Directory.GetFiles(newlineDir, "index.jsonl.torn-*.txt").Length != 0)
                return 57;

            // A syntactically complete JSON object without the required
            // index fields is NOT a valid durable index record.
            const string invalidSchemaUrl =
                "https://chatgpt.com/c/99999999-9999-9999-9999-999999999999";
            if (!store.Save(invalidSchemaUrl, "schema", "Schema valid answer", false))
                return 58;
            string schemaDir = Path.Combine(dir, Hash(invalidSchemaUrl));
            string schemaIndex = Path.Combine(schemaDir, "index.jsonl");
            File.AppendAllText(schemaIndex, "{\"ignored\":true}", new UTF8Encoding(false));
            var schemaRestart = new LiteResponseMarkdownArchive(dir);
            if (schemaRestart.RecoverMissingIndex(invalidSchemaUrl) != 0) return 59;
            if (File.ReadAllLines(schemaIndex).Length != 1) return 60;
            string[] schemaQuarantine = Directory.GetFiles(schemaDir,
                "index.jsonl.torn-*.txt");
            if (schemaQuarantine.Length != 1 ||
                File.ReadAllText(schemaQuarantine[0]) != "{\"ignored\":true}")
                return 61;
            if (!schemaRestart.IsPersisted(invalidSchemaUrl, "schema",
                "Schema valid answer")) return 62;

            // A newline-terminated invalid row must be quarantined as well.
            // Previously RepairIndexTail returned immediately on final LF.
            const string terminatedUrl =
                "https://chatgpt.com/c/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            if (!store.Save(terminatedUrl, "terminated", "Valid answer before bad row", false))
                return 63;
            string terminatedDir = Path.Combine(dir, Hash(terminatedUrl));
            string terminatedIndex = Path.Combine(terminatedDir, "index.jsonl");
            const string invalidTerminated = "{\"ignored\":true}\n";
            File.AppendAllText(terminatedIndex, invalidTerminated, new UTF8Encoding(false));
            var terminatedRestart = new LiteResponseMarkdownArchive(dir);
            if (terminatedRestart.RecoverMissingIndex(terminatedUrl) != 0) return 64;
            if (File.ReadAllLines(terminatedIndex).Length != 1) return 65;
            string[] terminatedQuarantine = Directory.GetFiles(terminatedDir,
                "index.jsonl.torn-*.txt");
            if (terminatedQuarantine.Length != 1 ||
                File.ReadAllText(terminatedQuarantine[0]) != invalidTerminated) return 66;
            if (!terminatedRestart.IsPersisted(terminatedUrl, "terminated",
                "Valid answer before bad row")) return 67;

            // A valid-looking row with SHA256 metadata that disagrees with
            // the immutable filename's digest prefix must not remain indexed.
            const string mismatchUrl =
                "https://chatgpt.com/c/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
            if (!store.Save(mismatchUrl, "mismatch", "Original immutable answer", false))
                return 68;
            string mismatchDir = Path.Combine(dir, Hash(mismatchUrl));
            string mismatchIndex = Path.Combine(mismatchDir, "index.jsonl");
            string validRow = File.ReadAllLines(mismatchIndex).Single();
            using var parsedValidRow = JsonDocument.Parse(validRow);
            string validDigest = parsedValidRow.RootElement.GetProperty("sha256").GetString()!;
            string forgedPrefix = (validDigest[0] == 'a' ? "b" : "a") + validDigest[1..16];
            string forgedRow = validRow.Replace("_" + validDigest[..16] + ".md",
                "_" + forgedPrefix + ".md", StringComparison.Ordinal);
            if (forgedRow == validRow) return 69;
            File.AppendAllText(mismatchIndex, forgedRow + "\n", new UTF8Encoding(false));
            var mismatchRestart = new LiteResponseMarkdownArchive(dir);
            if (mismatchRestart.RecoverMissingIndex(mismatchUrl) != 0) return 70;
            if (File.ReadAllLines(mismatchIndex).Length != 1) return 71;
            string[] mismatchQuarantine = Directory.GetFiles(mismatchDir,
                "index.jsonl.torn-*.txt");
            if (mismatchQuarantine.Length != 1 ||
                File.ReadAllText(mismatchQuarantine[0]) != forgedRow + "\n") return 72;
            if (!mismatchRestart.IsPersisted(mismatchUrl, "mismatch",
                "Original immutable answer")) return 73;

            // Valid file/digest/status but forged turn identity must never
            // count as a durable response or allow hard-DOM pruning ACK.
            string actualTurn = parsedValidRow.RootElement.GetProperty("turnKey").GetString()!;
            string wrongTurn = (actualTurn[0] == 'a' ? "b" : "a") + actualTurn[1..];
            string forgedTurn = validRow.Replace("\"turnKey\":\"" + actualTurn + "\"",
                "\"turnKey\":\"" + wrongTurn + "\"", StringComparison.Ordinal);
            if (forgedTurn == validRow) return 74;
            File.WriteAllText(mismatchIndex, forgedTurn + "\n", new UTF8Encoding(false));
            var turnRestart = new LiteResponseMarkdownArchive(dir);
            if (turnRestart.IsPersisted(mismatchUrl, "mismatch",
                "Original immutable answer")) return 75;
            if (turnRestart.RecoverMissingIndex(mismatchUrl) != 1) return 76;
            if (File.ReadAllLines(mismatchIndex).Length != 1) return 77;
            if (!Directory.GetFiles(mismatchDir, "index.jsonl.torn-*.txt")
                .Any(x => File.ReadAllText(x) == forgedTurn + "\n")) return 78;
            if (!turnRestart.IsPersisted(mismatchUrl, "mismatch",
                "Original immutable answer")) return 79;

            // The chat identity in a row must also match its directory.
            string actualChat = parsedValidRow.RootElement.GetProperty("chatKey").GetString()!;
            string wrongChat = (actualChat[0] == 'a' ? "b" : "a") + actualChat[1..];
            string forgedChat = validRow.Replace("\"chatKey\":\"" + actualChat + "\"",
                "\"chatKey\":\"" + wrongChat + "\"", StringComparison.Ordinal);
            if (forgedChat == validRow) return 80;
            File.WriteAllText(mismatchIndex, forgedChat + "\n", new UTF8Encoding(false));
            var chatRestart = new LiteResponseMarkdownArchive(dir);
            if (chatRestart.IsPersisted(mismatchUrl, "mismatch",
                "Original immutable answer")) return 81;
            if (chatRestart.RecoverMissingIndex(mismatchUrl) != 1) return 82;
            if (File.ReadAllLines(mismatchIndex).Length != 1) return 83;
            if (!Directory.GetFiles(mismatchDir, "index.jsonl.torn-*.txt")
                .Any(x => File.ReadAllText(x) == forgedChat + "\n")) return 84;
            if (!chatRestart.IsPersisted(mismatchUrl, "mismatch",
                "Original immutable answer")) return 85;

            // A long-running instance must notice an EXTERNAL index compaction
            // and restore an orphan without requiring a browser restart.
            const string reactiveUrl =
                "https://chatgpt.com/c/12121212-1212-1212-1212-121212121212";
            if (!store.Save(reactiveUrl, "reactive", "Durable response for rescan", false))
                return 96;
            var reactive = new LiteResponseMarkdownArchive(dir);
            if (reactive.RecoverMissingIndex(reactiveUrl) != 0) return 97;
            string reactiveDir = Path.Combine(dir, Hash(reactiveUrl));
            string reactiveIndex = Path.Combine(reactiveDir, "index.jsonl");
            File.WriteAllText(reactiveIndex, "", new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(reactiveIndex, DateTime.UtcNow.AddMinutes(1));
            if (reactive.RecoverMissingIndex(reactiveUrl) != 1) return 98;
            if (!reactive.IsPersisted(reactiveUrl, "reactive",
                "Durable response for rescan")) return 99;
            if (reactive.RecoverMissingIndex(reactiveUrl) != 0) return 100;
            if (File.ReadAllLines(reactiveIndex).Length != 1) return 101;

            // A newly renamed immutable orphan must likewise invalidate the
            // cached snapshot even when the index itself was NOT modified.
            const string lateContent = "New orphan after initial recovery";
            string lateDigest = Hash(lateContent);
            string lateName = Hash("late-turn")[..16] + "_" +
                lateDigest[..16] + ".md";
            File.WriteAllText(Path.Combine(reactiveDir, lateName),
                lateContent + "\n", new UTF8Encoding(false));
            Directory.SetLastWriteTimeUtc(reactiveDir, DateTime.UtcNow.AddMinutes(2));
            if (reactive.RecoverMissingIndex(reactiveUrl) != 1) return 102;
            if (!reactive.IsPersisted(reactiveUrl, "late-turn", lateContent))
                return 103;
            if (File.ReadAllLines(reactiveIndex).Length != 2) return 104;

            // Two consecutive bad records must BOTH be quarantined, even if
            // the first ended in LF. A single-row repair leaves a corrupt
            // interior index entry after the next append.
            const string multiUrl =
                "https://chatgpt.com/c/dddddddd-dddd-dddd-dddd-dddddddddddd";
            const string multiBadA = "{\"ignored\":true}\n";
            const string multiBadB = "{\"truncated\":";
            if (!store.Save(multiUrl, "original", "Healthy original answer", false))
                return 86;
            string multiDir = Path.Combine(dir, Hash(multiUrl));
            string multiIndex = Path.Combine(multiDir, "index.jsonl");
            File.AppendAllText(multiIndex, multiBadA + multiBadB,
                new UTF8Encoding(false));
            if (!store.Save(multiUrl, "later", "Answer after both broken rows", false))
                return 87;
            if (File.ReadAllLines(multiIndex).Length != 2) return 88;
            string[] multiCopies = Directory.GetFiles(multiDir,
                "index.jsonl.torn-*.txt");
            if (multiCopies.Length != 2 ||
                !multiCopies.Any(x => File.ReadAllText(x) == multiBadA) ||
                !multiCopies.Any(x => File.ReadAllText(x) == multiBadB)) return 89;
            if (!store.IsPersisted(multiUrl, "original", "Healthy original answer") ||
                !store.IsPersisted(multiUrl, "later", "Answer after both broken rows"))
                return 90;

            // Also recover both rows on a restarted but idle conversation.
            const string multiIdleUrl =
                "https://chatgpt.com/c/eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";
            if (!store.Save(multiIdleUrl, "idle", "Healthy idle answer", false))
                return 91;
            string idleDir = Path.Combine(dir, Hash(multiIdleUrl));
            string idleIndex = Path.Combine(idleDir, "index.jsonl");
            File.AppendAllText(idleIndex, multiBadA + multiBadB,
                new UTF8Encoding(false));
            var idleRestart = new LiteResponseMarkdownArchive(dir);
            if (idleRestart.RecoverMissingIndex(multiIdleUrl) != 0) return 92;
            if (File.ReadAllLines(idleIndex).Length != 1) return 93;
            string[] idleCopies = Directory.GetFiles(idleDir,
                "index.jsonl.torn-*.txt");
            if (idleCopies.Length != 2 ||
                !idleCopies.Any(x => File.ReadAllText(x) == multiBadA) ||
                !idleCopies.Any(x => File.ReadAllText(x) == multiBadB)) return 94;
            if (!idleRestart.IsPersisted(multiIdleUrl, "idle",
                "Healthy idle answer")) return 95;

            // A separate app instance must recover orphaned files on its first
            // archive poll, even if the same response is never rendered again.
            const string otherUrl = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            if (!store.Save(otherUrl, "orphan-final", "Recovered final", false)) return 21;
            if (!store.Save(otherUrl, "orphan-partial", "Recovered partial", true)) return 22;
            string otherDir = Path.Combine(dir, Hash(otherUrl));
            string otherIndex = Path.Combine(otherDir, "index.jsonl");
            File.Delete(otherIndex); // Crash before the index persisted.
            string tampered = Hash("fake")[..16] + "_" + new string('0', 16) + ".md";
            File.WriteAllText(Path.Combine(otherDir, tampered), "Tampered data\n");
            var restarted = new LiteResponseMarkdownArchive(dir);
            if (restarted.RecoverMissingIndex(otherUrl) != 2) return 23;
            if (!restarted.IsPersisted(otherUrl, "orphan-final", "Recovered final"))
                return 24;
            if (restarted.RecoverMissingIndex(otherUrl) != 0) return 25;
            if (File.ReadAllLines(otherIndex).Length != 2) return 26;
            if (!File.ReadAllLines(otherIndex).All(x =>
                !x.Contains(tampered, StringComparison.Ordinal))) return 27;

            // A sharing violation on one Markdown file must not prevent
            // recovery of other replies, and must not mark the chat done.
            const string lockedUrl = "https://chatgpt.com/c/44444444-4444-4444-4444-444444444444";
            if (!store.Save(lockedUrl, "locked", "Locked partial answer", true)) return 31;
            if (!store.Save(lockedUrl, "available", "Available final answer", false)) return 32;
            string lockedDir = Path.Combine(dir, Hash(lockedUrl));
            string lockedIndex = Path.Combine(lockedDir, "index.jsonl");
            File.Delete(lockedIndex);
            string lockedFile = Path.Combine(lockedDir,
                Hash("locked")[..16] + "_" +
                Hash("Locked partial answer")[..16] + "_partial.md");
            using (var held = new FileStream(lockedFile, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None))
            {
                if (restarted.RecoverMissingIndex(lockedUrl) != 1) return 33;
            }
            if (restarted.RecoverMissingIndex(lockedUrl) != 1) return 34;
            if (restarted.RecoverMissingIndex(lockedUrl) != 0 ||
                File.ReadAllLines(lockedIndex).Length != 2) return 35;

            // Evolving PARTIAL snapshots must remain immutable and must not
            // produce dangling index digests after another streaming update.
            const string revisionUrl = "https://chatgpt.com/c/55555555-5555-5555-5555-555555555555";
            if (!store.Save(revisionUrl, "stream", "First intermediate answer", true)) return 40;
            if (!store.Save(revisionUrl, "stream", "Second intermediate answer", true)) return 41;
            if (store.Save(revisionUrl, "stream", "Second intermediate answer", true)) return 42;
            string revisionDir = Path.Combine(dir, Hash(revisionUrl));
            string revisionIndex = Path.Combine(revisionDir, "index.jsonl");
            string[] revisionFiles = Directory.GetFiles(revisionDir, "*_partial.md");
            if (revisionFiles.Length != 2 || File.ReadAllLines(revisionIndex).Length != 2)
                return 43;
            foreach (var line in File.ReadAllLines(revisionIndex))
            {
                using var json = JsonDocument.Parse(line);
                var row = json.RootElement;
                string candidate = Path.Combine(revisionDir,
                    row.GetProperty("fileName").GetString()!);
                if (!File.Exists(candidate) ||
                    row.GetProperty("status").GetString() != "PARTIAL" ||
                    Hash(Clean(File.ReadAllText(candidate))) !=
                        row.GetProperty("sha256").GetString()) return 44;
            }
            File.Delete(revisionIndex);
            if (restarted.RecoverMissingIndex(revisionUrl) != 2 ||
                File.ReadAllLines(revisionIndex).Length != 2) return 45;

            // Existing pre-upgrade turn_partial.md files stay recoverable.
            const string legacyUrl = "https://chatgpt.com/c/66666666-6666-6666-6666-666666666666";
            if (!store.Save(legacyUrl, "old-turn", "Legacy streaming content", true))
                return 46;
            string legacyDir = Path.Combine(dir, Hash(legacyUrl));
            string legacyIndex = Path.Combine(legacyDir, "index.jsonl");
            string legacyName = Hash("old-turn")[..16] + "_partial.md";
            File.Move(Directory.GetFiles(legacyDir, "*_partial.md").Single(),
                Path.Combine(legacyDir, legacyName));
            File.Delete(legacyIndex);
            if (restarted.RecoverMissingIndex(legacyUrl) != 1) return 47;
            using (var legacy = JsonDocument.Parse(File.ReadAllText(legacyIndex)))
                if (legacy.RootElement.GetProperty("fileName").GetString() != legacyName ||
                    legacy.RootElement.GetProperty("status").GetString() != "PARTIAL")
                    return 48;

            // Foreground/worker overlap: concurrent writes to a single index
            // must produce valid complete lines, without duplicate records.
            const string sharedUrl = "https://chatgpt.com/c/33333333-3333-3333-3333-333333333333";
            var results = new bool[24];
            Parallel.For(0, results.Length, i => results[i] =
                restarted.Save(sharedUrl, "turn-" + i, "Answer " + i, false));
            if (results.Any(x => !x)) return 28;
            var duplicateResults = new bool[12];
            Parallel.For(0, duplicateResults.Length, i =>
                duplicateResults[i] = restarted.Save(sharedUrl,
                    "duplicate", "Same final answer", false));
            if (duplicateResults.Count(x => x) != 1) return 29;
            string sharedIndex = Path.Combine(dir, Hash(sharedUrl), "index.jsonl");
            string[] sharedLines = File.ReadAllLines(sharedIndex);
            if (sharedLines.Length != 25) return 30;
            foreach (string line in sharedLines)
                using (JsonDocument.Parse(line)) { }
            return 0;
        }
        catch { return 7; }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
