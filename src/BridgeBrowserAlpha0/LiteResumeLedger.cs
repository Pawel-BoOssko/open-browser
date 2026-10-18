using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Per-chat durable once-only continuation receipts. Unknown delivery is terminal
/// until manually reconciled: a restart MUST NOT repeat the same prompt.
/// </summary>
internal sealed class LiteResumeLedger
{
    internal enum Phase { Reviewed, SendingUncertain, Confirmed }
    internal sealed class Receipt
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ResponseHash { get; set; } = "";
        public Phase Status { get; set; }
        public string Decision { get; set; } = "";
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
    private sealed class Store
    {
        public int Schema { get; set; } = 1;
        public List<Receipt> Receipts { get; set; } = new();
    }

    private readonly string _folder;

    internal LiteResumeLedger(string? folder = null)
    {
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "resume-ledger");
    }

    private string PathFor(string key)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_folder, hash + ".dpapi");
    }

    private Store Read(string key)
    {
        string path = PathFor(key);
        if (!File.Exists(path)) return new Store();
        byte[] clear = LiteSecureArchive.Unprotect(File.ReadAllBytes(path));
        try
        {
            var value = JsonSerializer.Deserialize<Store>(clear);
            if (value is null || value.Schema != 1 || value.Receipts is null)
                throw new InvalidDataException("Unsupported resume ledger");
            return value;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    private void Write(string key, Store store)
    {
        Directory.CreateDirectory(_folder);
        string path = PathFor(key);
        byte[] clear = JsonSerializer.SerializeToUtf8Bytes(store);
        byte[] encrypted;
        try { encrypted = LiteSecureArchive.Protect(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(staging, encrypted);
            if (File.Exists(path)) File.Replace(staging, path, path + ".backup");
            else File.Move(staging, path);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    internal bool WasReviewed(string key, string responseHash)
    {
        return Read(key).Receipts.Any(r => r.ResponseHash == responseHash);
    }

    internal Receipt? Register(string key, string responseHash, string decision,
        DateTimeOffset? now = null)
    {
        if (responseHash.Length != 64 ||
            !responseHash.All(Uri.IsHexDigit) ||
            decision is not ("continue" or "wait" or "ask_user" or "stop"))
            throw new ArgumentException("Invalid continuation receipt");
        Store store = Read(key);
        if (store.Receipts.Any(r => r.ResponseHash == responseHash)) return null;
        var at = now ?? DateTimeOffset.UtcNow;
        if (store.Receipts.Count(r => r.CreatedUtc > at.AddHours(-1)) >= 2)
            return null;
        if (store.Receipts.Count(r => r.CreatedUtc > at.AddDays(-1)) >= 8)
            return null;
        var receipt = new Receipt
        {
            ResponseHash = responseHash, Decision = decision, CreatedUtc = at
        };
        store.Receipts.Add(receipt);
        if (store.Receipts.Count > 400)
            store.Receipts.RemoveRange(0, store.Receipts.Count - 400);
        Write(key, store);
        return receipt;
    }

    internal bool MarkSendingUncertain(string key, string receiptId)
    {
        Store store = Read(key);
        var receipt = store.Receipts.FirstOrDefault(r =>
            r.Id == receiptId && r.Status == Phase.Reviewed &&
            r.Decision == "continue");
        if (receipt is null) return false;
        receipt.Status = Phase.SendingUncertain;
        Write(key, store);
        return true;
    }

    internal bool MarkConfirmed(string key, string receiptId)
    {
        Store store = Read(key);
        var receipt = store.Receipts.FirstOrDefault(r =>
            r.Id == receiptId && r.Status == Phase.SendingUncertain);
        if (receipt is null) return false;
        receipt.Status = Phase.Confirmed;
        Write(key, store);
        return true;
    }
}
