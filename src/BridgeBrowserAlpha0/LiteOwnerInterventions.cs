using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Durable, append-only owner intervention outbox. Status text and recipient
/// are not taken from the model. Only one automated email hand-off is attempted
/// per immutable eventKey; crashes after dispatch_started remain UNCERTAIN,
/// never eligible for unattended replay.
/// </summary>
internal static class LiteOwnerInterventions
{
    private static readonly string OutboxPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "diagnostics", "owner-interventions.jsonl");
    private static readonly object Gate = new();

    internal static string EventKey(string conversationUrl, string status,
        string answerHash)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            conversationUrl + "\n" + status + "\n" + answerHash)));
    }

    internal static bool IsIntervention(string? status) =>
        status is "WAIT_USER" or "BLOCKED";

    private static bool Valid(string status, string id) =>
        IsIntervention(status) && id.Length == 64 && id.All(Uri.IsHexDigit);

    private static bool AppendIfNew(string path, string status, string id,
        string stage, string? project, bool requireQueued)
    {
        if (!Valid(status, id)) throw new ArgumentException("Invalid intervention");
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            bool present = false, dispatched = false, queued = false;
            using (var reader = new StreamReader(stream, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                while (reader.ReadLine() is { } line)
                {
                    try
                    {
                        using var j = JsonDocument.Parse(line);
                        var e = j.RootElement;
                        if (!e.TryGetProperty("eventKey", out var key) ||
                            key.GetString() != id) continue;
                        if (!e.TryGetProperty("stage", out var s)) continue;
                        string? kind = s.GetString();
                        if (kind == stage) present = true;
                        if (kind == "queued") queued = true;
                        if (kind is "delivery_started" or "outlook_submitted" or
                            "delivery_uncertain") dispatched = true;
                    }
                    catch (JsonException) { /* partial line does not authorize resending */ }
                }
            }
            // Never allow a second attempt once delivery_started was persisted.
            if (present || (requireQueued && (!queued || dispatched))) return false;
            stream.Position = stream.Length;
            string row = JsonSerializer.Serialize(new {
                schema = 1, utc = DateTimeOffset.UtcNow.ToString("O"),
                eventKey = id, status, stage,
                project = project ?? "",
                email = stage == "queued" ? "pending" :
                    stage == "delivery_started" ? "uncertain_until_confirmed" :
                    stage == "outlook_submitted" ? "submitted_not_delivered" :
                    "delivery_uncertain"
            }) + Environment.NewLine;
            byte[] bytes = new UTF8Encoding(false).GetBytes(row);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
            return true;
        }
    }

    internal static bool QueueForOwner(string status, string id, string project) =>
        AppendIfNew(OutboxPath, status, id, "queued", project, false);

    internal static void DispatchWithClassicOutlook(string status, string id,
        string project)
    {
        // Never interfere with the foreground ChatGPT WebView2 thread.
        var thread = new Thread(() => AttemptOutlook(status, id, project))
        { IsBackground = true, Name = "OB-Lite-Owner-Mail" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void AttemptOutlook(string status, string id, string project)
    {
        object? app = null, ns = null, accounts = null, account = null, mail = null;
        try
        {
            var type = Type.GetTypeFromProgID("Outlook.Application");
            if (type is null)
            {
                LiteEventLog.Write("owner_email_not_configured",
                    new { reason = "classic_outlook_unavailable" });
                return; // queued; can be resolved explicitly later
            }
            app = Activator.CreateInstance(type);
            if (app is null) return;
            dynamic client = app;
            ns = client.GetNamespace("MAPI");
            dynamic session = ns;
            accounts = session.Accounts;
            dynamic collection = accounts;
            // Multiple mailboxes are ambiguous; never guess the recipient.
            if ((int)collection.Count != 1)
            {
                LiteEventLog.Write("owner_email_not_configured",
                    new { reason = "ambiguous_account_count" });
                return;
            }
            account = collection.Item(1);
            dynamic ownAccount = account;
            string recipient = (string)ownAccount.SmtpAddress;
            if (string.IsNullOrWhiteSpace(recipient) || !recipient.Contains('@') ||
                recipient.Contains('\r') || recipient.Contains('\n'))
                return;
            // Commit the one-time delivery attempt BEFORE any Outlook Send.
            if (!AppendIfNew(OutboxPath, status, id,
                "delivery_started", project, true)) return;
            mail = client.CreateItem(0); // olMailItem, never open an inspector
            dynamic letter = mail;
            letter.SendUsingAccount = ownAccount;
            letter.To = recipient;
            string cleanProject = project.Replace("\r", " ").Replace("\n", " ");
            if (cleanProject.Length > 90) cleanProject = cleanProject[..90];
            letter.Subject = "Open Browser Lite: " + status + " — " + cleanProject;
            letter.Body = "Open Browser Lite wykrył nowe zdarzenie " + status +
                " w projekcie " + cleanProject + ".\n" +
                "Autopraca tego projektu została wstrzymana.\n" +
                "Otwórz Open Browser Lite → Projekty i sprawdź interwencję.\n" +
                "Id zdarzenia: " + id[..12] + "\n" +
                "To powiadomienie jest automatycznie generowane tylko raz.";
            letter.Send();
            AppendIfNew(OutboxPath, status, id, "outlook_submitted", project, false);
            LiteEventLog.Write("owner_email_handed_to_outlook",
                new { status, submissionOnly = true, delivered = false });
        }
        catch (Exception ex)
        {
            try
            {
                AppendIfNew(OutboxPath, status, id, "delivery_uncertain",
                    project, false);
            }
            catch { /* preserve original failure for diagnostics */ }
            LiteEventLog.Write("owner_email_delivery_uncertain",
                new { errorType = ex.GetType().Name, sent = false,
                    retryAllowed = false });
        }
        finally
        {
            foreach (var value in new[] { mail, account, accounts, ns, app })
                if (value is not null && Marshal.IsComObject(value))
                    try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }

    internal static int SelfTest()
    {
        const string url = "https://chatgpt.com/c/test";
        var key = EventKey(url, "BLOCKED", new string('A', 64));
        if (key.Length != 64 || !key.All(Uri.IsHexDigit) ||
            key != EventKey(url, "BLOCKED", new string('A', 64)) ||
            key == EventKey(url, "WAIT_USER", new string('A', 64)) ||
            !IsIntervention("BLOCKED") || !IsIntervention("WAIT_USER") ||
            IsIntervention("CONTINUE")) return 1;
        string path = Path.Combine(Path.GetTempPath(),
            "obl_owner_mail_test_" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            if (!AppendIfNew(path, "BLOCKED", key, "queued", "sample", false) ||
                AppendIfNew(path, "BLOCKED", key, "queued", "sample", false))
                return 2;
            if (!AppendIfNew(path, "BLOCKED", key, "delivery_started", "sample", true) ||
                AppendIfNew(path, "BLOCKED", key, "delivery_started", "sample", true))
                return 3;
            if (!AppendIfNew(path, "BLOCKED", key, "outlook_submitted",
                    "sample", false)) return 4;
            if (AppendIfNew(path, "BLOCKED", key, "delivery_started",
                    "sample", true)) return 5; // NEVER retry after handoff
            if (File.ReadAllLines(path).Length != 3) return 6;
            return 0;
        }
        catch { return 7; }
        finally { try { File.Delete(path); } catch { } }
    }
}
