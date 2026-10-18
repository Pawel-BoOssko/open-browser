namespace BridgeBrowserAlpha0;

internal static class LiteResumeLedgerSelfTest
{
    internal static int Run()
    {
        string folder = Path.Combine(Path.GetTempPath(),
            "ob-resume-ledger-" + Guid.NewGuid().ToString("N"));
        string key = "chatgpt.com/c/synthetic";
        string hash1 = new string('A', 64);
        string hash2 = new string('B', 64);
        try
        {
            var ledger = new LiteResumeLedger(folder);
            Check(!ledger.WasReviewed(key, hash1), "empty");
            var receipt = ledger.Register(key, hash1, "continue");
            Check(receipt is not null, "registered");
            Check(ledger.WasReviewed(key, hash1), "receipt persisted");
            Check(ledger.Register(key, hash1, "continue") is null, "no duplicate");
            var revived = new LiteResumeLedger(folder);
            Check(revived.WasReviewed(key, hash1), "survives restart");
            Check(revived.MarkSendingUncertain(key, receipt!.Id), "unknown persisted first");
            Check(!revived.MarkSendingUncertain(key, receipt.Id), "cannot resend");
            Check(revived.MarkConfirmed(key, receipt.Id), "confirm");
            Check(!revived.MarkConfirmed(key, receipt.Id), "no second confirmation");
            var later = revived.Register(key, hash2, "wait");
            Check(later is not null, "second response");
            Check(revived.Register(key, new string('C', 64), "continue") is null,
                "hourly cap");
            var data = File.ReadAllBytes(Directory.GetFiles(folder, "*.dpapi")[0]);
            Check(!System.Text.Encoding.UTF8.GetString(data).Contains(hash1),
                "ledger file encrypted");
            LiteEventLog.Write("resume_ledger_selftest",
                new { passed = true, checks = 11 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("resume_ledger_selftest",
                new { passed = false, errorType = ex.GetType().Name });
            return 2;
        }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            catch { }
        }
    }
    private static void Check(bool value, string why)
    {
        if (!value) throw new InvalidOperationException(why);
    }
}
