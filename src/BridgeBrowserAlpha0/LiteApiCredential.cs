using System.Runtime.InteropServices;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Read the same Windows Credential Manager record already used by local
/// FileScannerAI. Values are not exported, logged, or copied into repo files.
/// </summary>
internal static class LiteApiCredential
{
    internal const string SharedTarget = "FileScannerAI/OpenAIAPIKey";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags,
        out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr credentialPtr);

    internal static bool IsSharedKeyAvailable()
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (!CredRead(SharedTarget, 1, 0, out var ptr)) return false;
        try
        {
            var cred = Marshal.PtrToStructure<Credential>(ptr);
            return cred.CredentialBlob != IntPtr.Zero && cred.CredentialBlobSize > 0;
        }
        finally { CredFree(ptr); }
    }

    internal static string? ReadSharedKey()
    {
        if (!OperatingSystem.IsWindows() ||
            !CredRead(SharedTarget, 1, 0, out var ptr)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<Credential>(ptr);
            if (cred.CredentialBlob == IntPtr.Zero ||
                cred.CredentialBlobSize is 0 or > 16384) return null;
            byte[] data = new byte[(int)cred.CredentialBlobSize];
            try
            {
                Marshal.Copy(cred.CredentialBlob, data, 0, data.Length);
                // Windows generic credential blobs may be UTF-16LE or UTF-8.
                bool unicode = data.Length > 4 && data[1] == 0 && data[3] == 0;
                string result = (unicode ?
                    System.Text.Encoding.Unicode : System.Text.Encoding.UTF8)
                    .GetString(data).TrimEnd('\0', '\r', '\n', ' ');
                return result.StartsWith("sk-", StringComparison.Ordinal) ? result : null;
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);
            }
        }
        finally { CredFree(ptr); }
    }
}
