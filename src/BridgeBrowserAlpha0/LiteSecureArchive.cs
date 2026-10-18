using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Append-only, current-Windows-user-encrypted copies of visible assistant text.
/// No plaintext is written to disk, and the local archive is not part of Autosync.
/// </summary>
internal sealed class LiteSecureArchive
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref Blob input, string description, IntPtr entropy, IntPtr reserved,
        IntPtr prompt, int flags, out Blob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved,
        IntPtr prompt, int flags, out Blob output);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    private readonly string _directory;
    private readonly Dictionary<string, string> _lastHashes = new();
    private DateTime _lastPruneUtc = DateTime.MinValue;

    internal LiteSecureArchive(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "archive");
    }

    internal bool Save(string conversationId, string turnId, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200_000)
            return false;
        string id = Hash(conversationId + "|" + turnId);
        string digest = Hash(text);
        if (_lastHashes.TryGetValue(id, out string? last) && last == digest)
            return false;

        byte[] plaintext = Encoding.UTF8.GetBytes(text);
        byte[] ciphertext;
        try { ciphertext = Protect(plaintext); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }

        Directory.CreateDirectory(_directory);
        string filename = id + "_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                          "_" + digest.Substring(0, 12) + ".dpapi";
        string path = Path.Combine(_directory, filename);
        string temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create,
                FileAccess.Write, FileShare.None))
            {
                stream.Write(ciphertext);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path);
            _lastHashes[id] = digest;
            LiteEventLog.Write("assistant_snapshot_saved", new
            {
                characters = text.Length,
                sha256Prefix = digest.Substring(0, 12),
                fileBytes = ciphertext.Length
            });
            if ((DateTime.UtcNow - _lastPruneUtc).TotalHours > 24)
                Prune();
            return true;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal void Prune()
    {
        if (!Directory.Exists(_directory)) return;
        DateTime cutoff = DateTime.UtcNow.AddDays(-30);
        foreach (var file in new DirectoryInfo(_directory).GetFiles("*.dpapi"))
        {
            if (file.LastWriteTimeUtc < cutoff) file.Delete();
        }
        _lastPruneUtc = DateTime.UtcNow;
    }

    internal static byte[] Protect(byte[] bytes) => Transform(bytes, false);
    internal static byte[] Unprotect(byte[] bytes) => Transform(bytes, true);

    private static byte[] Transform(byte[] bytes, bool decrypt)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows DPAPI required");
        Blob input = new();
        Blob output = new();
        input.Length = bytes.Length;
        input.Data = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            bool ok = decrypt
                ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptProtectData(ref input, "OpenBrowser Lite archived answer",
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            byte[] result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (input.Data != IntPtr.Zero)
            {
                for (int i = 0; i < bytes.Length; i++)
                    Marshal.WriteByte(input.Data, i, 0);
                Marshal.FreeHGlobal(input.Data);
            }
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }

    private static string Hash(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        try { return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
