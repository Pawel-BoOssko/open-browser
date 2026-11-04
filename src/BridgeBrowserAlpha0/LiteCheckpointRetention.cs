using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

// OBL-owned checkpoint retention: plain Markdown in a separate COLD root.
// Never ZIP, delete history, or create an extra index. Preserve source
// atomically as the File.Replace backup before installing the short HOT file.
internal static class LiteCheckpointRetention
{
    internal const int TriggerBytes = 24576;
    internal const int MaxBytes = 32768;
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private static readonly string Checkpoint = @"D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md";
    internal static readonly string ColdRoot = @"D:\OpenBrowserLiteArchive\checkpoints\obl";
    private static DateTimeOffset _lastChecked;
    private static readonly object Gate = new();

    internal static void Observe()
    {
        lock (Gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastChecked < TimeSpan.FromMinutes(5)) return;
            _lastChecked = now;
        }
        try
        {
            if (CompactIfNeeded(Checkpoint, ColdRoot))
                LiteEventLog.Write("obl_checkpoint_compacted", new {
                    checkpoint = "OPENBROWSER_LITE_CHECKPOINT.md",
                    archiveFormat = "plain_markdown",
                    archiveExternalToProject = true, preserved = true
                });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("obl_checkpoint_retention_error", new {
                errorType = ex.GetType().Name, errorCode = ex.HResult
            });
        }
    }

    internal static bool CompactIfNeeded(string path, string coldRoot,
        int triggerBytes = TriggerBytes)
    {
        if (!File.Exists(path)) return false;
        var fi = new FileInfo(path);
        if (fi.Length <= triggerBytes) return false;
        if (Path.GetFullPath(coldRoot).TrimEnd('\\')
            .StartsWith(Path.GetFullPath(Path.GetDirectoryName(path)!)
                .TrimEnd('\\') + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ARCHIVE_INSIDE_HOT_ROOT");

        string original = File.ReadAllText(path, Utf8);
        long oldLength = fi.Length;
        DateTime oldModified = fi.LastWriteTimeUtc;
        string shortText = BuildShort(original, coldRoot);
        if (Utf8.GetByteCount(shortText) >= MaxBytes)
            throw new InvalidOperationException("SHORT_CHECKPOINT_TOO_LONG");

        Directory.CreateDirectory(coldRoot);
        string fileName = "OBL_checkpoint_" +
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HHmmss_fffffff") + ".md";
        string archive = Path.Combine(coldRoot, fileName);
        if (File.Exists(archive)) throw new IOException("ARCHIVE_ALREADY_EXISTS");
        // Retain original bytes via File.Replace's *atomic backup*.
        // If another writer changes the file, avoid replacing that snapshot.
        fi.Refresh();
        if (fi.Length != oldLength || fi.LastWriteTimeUtc != oldModified)
            return false;
        string temp = path + ".obl-short-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp,
                shortText.Replace("ARCHIVE_PATH_TO_BE_REPLACED", archive), Utf8);
            File.Replace(temp, path, archive, ignoreMetadataErrors: true);
            if (!File.Exists(archive))
                throw new IOException("ARCHIVE_MISSING_AFTER_REPLACE");
            if (new FileInfo(path).Length >= MaxBytes)
                throw new IOException("ACTIVE_CHECKPOINT_TOO_BIG");
            return true;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    internal static string BuildShort(string original, string coldRoot)
    {
        const string title = "# OPEN BROWSER LITE — ACTIVE CHECKPOINT (OBL AUTORETENTION)\n";
        string header = title +
            "Historical checkpoint: ARCHIVE_PATH_TO_BE_REPLACED\n" +
            "Archive: original plain Markdown, no ZIP or compression.\n" +
            "All 34 project tasks: D:\\projects\\open-browser\\docs\\OBL_BACKLOG.md\n" +
            "Operational rules: AGENTS.md and docs/OPENBROWSER_LITE_OPERATING_RULES_2026-10-25.md\n" +
            "Recent chronological checkpoint entries follow. Older entries remain in archive.\n\n";
        var lines = original.Replace("\r\n", "\n").Split('\n');
        var newest = new List<string>();
        int size = 0;
        for (int i = lines.Length - 1; i >= 0 && newest.Count < 85; i--)
        {
            string item = lines[i];
            int bytes = Utf8.GetByteCount(item) + 1;
            if (size + bytes > 10000) break;
            newest.Add(item); size += bytes;
        }
        newest.Reverse();
        return header + string.Join("\n", newest).TrimStart('\n') + "\n";
    }

    internal static int SelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "obl-checkpoint-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            string hot = Path.Combine(root, "hot");
            string cold = Path.Combine(root, "cold");
            Directory.CreateDirectory(hot);
            string file = Path.Combine(hot, "checkpoint.md");
            string source = string.Concat(Enumerable.Range(0, 300)
                .Select(i => "2026-10-28 CHECKPOINT " + i + " " +
                    new string('x', 120) + "\n")) +
                "LATEST_RELEVANT_CHECKPOINT\n";
            File.WriteAllText(file, source, Utf8);
            if (!CompactIfNeeded(file, cold, 1000)) return 1;
            string[] archives = Directory.GetFiles(cold, "*.md");
            if (archives.Length != 1) return 2;
            if (File.ReadAllText(archives[0], Utf8) != source) return 3;
            string current = File.ReadAllText(file, Utf8);
            if (!current.Contains("LATEST_RELEVANT_CHECKPOINT") ||
                !current.Contains(archives[0]) ||
                new FileInfo(file).Length >= MaxBytes) return 4;
            if (CompactIfNeeded(file, cold)) return 5;
            if (Directory.GetFiles(cold, "*.zip").Length != 0) return 6;
            return 0;
        }
        catch { return 7; }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { }
        }
    }
}
