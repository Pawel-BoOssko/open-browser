using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Additional cross-source evidence for an already-uncertain wake. Chat
/// started/finished records never turn an engine receipt_uncertain into
/// receipt_confirmed and never cause a duplicate dispatch.
/// </summary>
internal static class LiteWakeEvidenceReconciler
{
    internal readonly record struct Entry(string Receipt, long Sequence,
        string Actor, string Stage, string Result);
    internal readonly record struct Finding(string Receipt, long Sequence, string Result);
    private static DateTimeOffset _nextScanUtc;
    private const int MaxTailBytes = 1024 * 1024;

    internal static IReadOnlyList<Finding> Reconcile(IReadOnlyList<Entry> rows)
    {
        var uncertain = rows.Where(x => x.Actor == "engine" &&
                x.Stage == "receipt_uncertain")
            .GroupBy(x => (x.Receipt, x.Sequence))
            .Select(g => g.Key);
        var results = new List<Finding>();
        foreach (var key in uncertain)
        {
            bool attempted = rows.Any(x => x.Receipt == key.Receipt &&
                x.Sequence == key.Sequence && x.Actor == "engine" &&
                x.Stage == "submission_attempted");
            if (!attempted) continue;
            foreach (string stage in new[] { "chat_started", "chat_finished" })
            {
                bool chatEvidence = rows.Any(x => x.Receipt == key.Receipt &&
                    x.Sequence == key.Sequence && x.Actor == "chat" &&
                    x.Stage == stage);
                bool observed = rows.Any(x => x.Receipt == key.Receipt &&
                    x.Sequence == key.Sequence && x.Actor == "engine" &&
                    x.Stage == "cross_source_evidence" && x.Result == stage);
                if (chatEvidence && !observed)
                    results.Add(new Finding(key.Receipt, key.Sequence, stage));
            }
        }
        return results;
    }

    // User-visible outcome is CONFIRMED when the same receipt has an actual
    // chat_started record, even when the original DOM probe timed out.
    // Keep raw receipt_uncertain immutable for diagnostics.
    internal static IReadOnlyList<Finding> ResolvedByChat(IReadOnlyList<Entry> rows)
    {
        var results = new List<Finding>();
        foreach (var r in rows.Where(x => x.Actor == "engine" &&
            x.Stage == "receipt_uncertain"))
        {
            if (results.Any(x => x.Receipt == r.Receipt &&
                x.Sequence == r.Sequence)) continue;
            bool issued = rows.Any(x => x.Receipt == r.Receipt &&
                x.Sequence == r.Sequence && x.Actor == "engine" &&
                x.Stage == "submission_attempted");
            bool started = rows.Any(x => x.Receipt == r.Receipt &&
                x.Sequence == r.Sequence && x.Actor == "chat" &&
                x.Stage == "chat_started");
            bool finished = rows.Any(x => x.Receipt == r.Receipt &&
                x.Sequence == r.Sequence && x.Actor == "chat" &&
                x.Stage == "chat_finished");
            bool recorded = rows.Any(x => x.Receipt == r.Receipt &&
                x.Sequence == r.Sequence && x.Actor == "engine" &&
                x.Stage == "receipt_resolved" &&
                (x.Result == "confirmed_by_chat_started" ||
                 x.Result == "confirmed_by_chat_finished"));
            if (issued && (started || finished) && !recorded)
                results.Add(new Finding(r.Receipt, r.Sequence,
                    started ? "confirmed_by_chat_started" :
                              "confirmed_by_chat_finished"));
        }
        return results;
    }
    internal static void Observe()
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _nextScanUtc) return;
        _nextScanUtc = now.AddSeconds(30);
        try
        {
            var rows = ReadTail(LiteWakeJournal.JournalPath);
            foreach (var confirmed in ResolvedByChat(rows))
            {
                LiteWakeJournal.Write("receipt_resolved", confirmed.Sequence,
                    confirmed.Receipt, confirmed.Result);
                LiteEventLog.Write("multi_wake_delivery_resolved", new {
                    wakeSequence=confirmed.Sequence, receipt=confirmed.Receipt,
                    effectiveStatus="confirmed", evidence=confirmed.Result ==
                        "confirmed_by_chat_finished" ? "chat_finished" : "chat_started",
                    historicalDomProbe="uncertain", didResend=false
                });
            }
            foreach (var finding in Reconcile(rows))
            {
                // Distinct phase: evidence of the chat run, NOT a DOM ACK.
                LiteWakeJournal.Write("cross_source_evidence",
                    finding.Sequence, finding.Receipt, finding.Result);
                LiteEventLog.Write("multi_wake_chat_evidence_reconciled", new {
                    wakeSequence=finding.Sequence, receipt=finding.Receipt,
                    evidence=finding.Result, domAckChanged=false,
                    didResend=false
                });
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("multi_wake_evidence_scan_failed",
                new { errorType=ex.GetType().Name, hresult=ex.HResult });
        }
    }

    private static List<Entry> ReadTail(string file)
    {
        var rows = new List<Entry>();
        if (!File.Exists(file)) return rows;
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite);
        bool partial = fs.Length > MaxTailBytes;
        if (partial) fs.Seek(-MaxTailBytes, SeekOrigin.End);
        using var reader = new StreamReader(fs, new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks:true);
        if (partial) reader.ReadLine(); // discard incomplete first line
        while (reader.ReadLine() is { } line)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var node = document.RootElement;
                if (!node.TryGetProperty("receipt", out var receiptNode) ||
                    receiptNode.ValueKind != JsonValueKind.String) continue;
                string receipt = receiptNode.GetString() ?? "";
                if (receipt.Length != 32 || !receipt.All(c =>
                    c is >= '0' and <= '9' or >= 'a' and <= 'f')) continue;
                if (!node.TryGetProperty("wakeSequence", out var seqNode) ||
                    seqNode.ValueKind != JsonValueKind.Number ||
                    !seqNode.TryGetInt64(out long sequence)) continue;
                string actor = node.TryGetProperty("actor", out var a) ?
                    a.GetString() ?? "" : "";
                string stage = node.TryGetProperty("stage", out var st) ?
                    st.GetString() ?? "" : "";
                string result = node.TryGetProperty("result", out var res) &&
                    res.ValueKind == JsonValueKind.String ?
                    res.GetString() ?? "" : "";
                rows.Add(new Entry(receipt,sequence,actor,stage,result));
            }
            catch (JsonException) { /* partially appended line, retry later */ }
        }
        return rows;
    }

    internal static int SelfTest()
    {
        const string receipt="0123456789abcdef0123456789abcdef";
        const string other="abcdef0123456789abcdef0123456789";
        var rows = new List<Entry> {
            new(receipt,3,"engine","submission_attempted","click_invoked"),
            new(receipt,3,"engine","receipt_uncertain","click_invoked"),
            new(receipt,3,"chat","chat_started",""),
            new(receipt,3,"chat","chat_finished",""),
            new(other,4,"chat","chat_finished","")
        };
        var resolved = ResolvedByChat(rows);
        if (resolved.Count != 1 ||
            resolved[0].Result != "confirmed_by_chat_started") return 5;
        rows.Add(new Entry(receipt,3,"engine",
            "receipt_resolved","confirmed_by_chat_started"));
        if (ResolvedByChat(rows).Count != 0) return 6;
        rows.RemoveAt(rows.Count - 1);
        var foreign = new List<Entry> {
            new(receipt,3,"engine","submission_attempted","click_invoked"),
            new(receipt,3,"engine","receipt_uncertain","click_invoked"),
            new(other,3,"chat","chat_started","")
        };
        if (ResolvedByChat(foreign).Count != 0) return 7;
        var onlyT3 = new List<Entry> {
            new(receipt,3,"engine","submission_attempted","click_invoked"),
            new(receipt,3,"engine","receipt_uncertain","click_invoked"),
            new(receipt,3,"chat","chat_finished","")
        };
        var t3Result=ResolvedByChat(onlyT3);
        if (t3Result.Count != 1 ||
            t3Result[0].Result != "confirmed_by_chat_finished") return 8;
        onlyT3.Add(new Entry(receipt,3,"engine","receipt_resolved",
            "confirmed_by_chat_finished"));
        if (ResolvedByChat(onlyT3).Count != 0) return 9;
        onlyT3.RemoveAt(0);
        onlyT3.RemoveAll(x => x.Stage == "receipt_resolved");
        if (ResolvedByChat(onlyT3).Count != 0) return 10;
        var otherT3 = new List<Entry> {
            new(receipt,3,"engine","submission_attempted","click_invoked"),
            new(receipt,3,"engine","receipt_uncertain","click_invoked"),
            new(other,3,"chat","chat_finished","")
        };
        if (ResolvedByChat(otherT3).Count != 0) return 11;        var findings=Reconcile(rows);
        if (findings.Count != 2 ||
            !findings.Any(x => x.Result == "chat_started") ||
            !findings.Any(x => x.Result == "chat_finished") ||
            findings.Any(x => x.Result == "receipt_confirmed")) return 1;
        rows.Add(new Entry(receipt,3,"engine",
            "cross_source_evidence","chat_started"));
        if (Reconcile(rows).Count != 1 ||
            Reconcile(rows)[0].Result != "chat_finished") return 2;
        rows.Add(new Entry(receipt,3,"engine",
            "cross_source_evidence","chat_finished"));
        if (Reconcile(rows).Count != 0) return 3;
        rows.RemoveAll(x => x.Stage == "receipt_uncertain");
        if (Reconcile(rows).Count != 0) return 4;
        return 0;
    }
}
