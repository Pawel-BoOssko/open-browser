using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Headless DPAPI and archive smoke tests. Uses only disposable synthetic text
/// and a random temporary directory, never the user's conversation profile.
/// </summary>
internal static class LiteArchiveSelfTest
{
    internal static int Run()
    {
        string folder = Path.Combine(Path.GetTempPath(),
            "ob-lite-archive-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] original = Encoding.UTF8.GetBytes("Zażółć gęślą jaźń 🔒 2026");
            byte[] encrypted = LiteSecureArchive.Protect(original);
            Require(!encrypted.AsSpan().SequenceEqual(original), "ciphertext differs");
            byte[] restored = LiteSecureArchive.Unprotect(encrypted);
            Require(restored.AsSpan().SequenceEqual(original), "DPAPI roundtrip");
            CryptographicOperations.ZeroMemory(original);
            CryptographicOperations.ZeroMemory(restored);

            var archive = new LiteSecureArchive(folder);
            Require(archive.Save("https://chatgpt.com/c/synthetic", "turn-1", "Synthetic version A"), "first snapshot");
            Require(!archive.Save("https://chatgpt.com/c/synthetic", "turn-1", "Synthetic version A"), "deduplicate");
            Require(archive.Save("https://chatgpt.com/c/synthetic", "turn-1", "Synthetic version B"), "changed snapshot");
            var files = Directory.GetFiles(folder, "*.dpapi");
            Require(files.Length == 2, "two encrypted versions");
            var texts = files.Select(path => {
                byte[] clear = LiteSecureArchive.Unprotect(File.ReadAllBytes(path));
                try { return Encoding.UTF8.GetString(clear); }
                finally { CryptographicOperations.ZeroMemory(clear); }
            }).OrderBy(x => x).ToArray();
            Require(texts.SequenceEqual(new[] { "Synthetic version A", "Synthetic version B" }), "read stored versions");
            bool corruptRejected = false;
            try { LiteSecureArchive.Unprotect(new byte[] { 1, 2, 3, 4 }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or CryptographicException)
            { corruptRejected = true; }
            Require(corruptRejected, "reject damaged ciphertext");
            LiteEventLog.Write("lite_archive_selftest", new { passed = true, checks = 7 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("lite_archive_selftest", new {
                passed = false, errorType = ex.GetType().Name, errorCode = ex.HResult
            });
            return 2;
        }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
            catch { }
        }
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException("Archive selftest: " + check);
    }
}
