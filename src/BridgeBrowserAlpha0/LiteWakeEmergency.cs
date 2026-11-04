using System.Globalization;
using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>Simple per-project emergency ID and current-file freshness checks.</summary>
internal static class LiteWakeEmergency
{
    // OBL already has a durable engine-side checkpoint and handoff snapshot.
    // Waiting for an uncertain old conversation to write another handoff
    // cannot block a new lead. Non-OBL projects retain the legacy policy.
    internal static bool SkipOldChatHandoffWait(string projectKey, string phase) =>
        string.Equals(projectKey, "obl", StringComparison.OrdinalIgnoreCase) &&
        (phase is "request_pending" or "waiting_handoff");

    internal static string MessageId(string projectKey, long wake,
        int ordinal, DateTimeOffset sentUtc)
    {
        if (!LiteProjectWakeCli.ValidKey(projectKey) || wake < 0 ||
            ordinal < 1 || ordinal > 999) throw new ArgumentException("Invalid emergency identifier");
        var warsaw = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var local = TimeZoneInfo.ConvertTime(sentUtc, warsaw);
        return projectKey.ToUpperInvariant() + "-" +
            wake.ToString("D6", CultureInfo.InvariantCulture) + "." +
            ordinal.ToString(CultureInfo.InvariantCulture) + "-A01-" +
            local.ToString("HHmmss", CultureInfo.InvariantCulture);
    }

    internal static string CurrentHandoffPath(string projectKey) =>
        projectKey == "obl"
            ? @"D:\projects\open-browser\docs\OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md"
            : Path.Combine(@"D:\OpenBrowserLiteHandoffs", projectKey, "HANDOFF_CURRENT.md");

    // The 30-minute limit belongs to the durable request reservation, not
    // to successful WebView2 navigation/click. Invalid persisted timestamps
    // must never extend the wait indefinitely.
    internal static bool WaitForHandoffUntilDeadline(
        DateTimeOffset now, DateTimeOffset reservedUtc,
        DateTimeOffset deadlineUtc)
    {
        return reservedUtc != default && deadlineUtc != default &&
            deadlineUtc > reservedUtc &&
            deadlineUtc <= reservedUtc.AddMinutes(30) &&
            now < deadlineUtc;
    }

    internal static bool FreshHandoff(string path, DateTimeOffset sentUtc,
        string messageId, string projectKey, string oldChatUrl)
    {
        if (sentUtc == default || !File.Exists(path) ||
            !LiteDevWakeState.IsChatUrl(oldChatUrl) ||
            messageId.Length < 14) return false;
        try
        {
            var file = new FileInfo(path);
            file.Refresh();
            if (!file.Exists || file.Length < 160 || file.Length > 1024 * 1024 ||
                file.LastWriteTimeUtc < sentUtc.UtcDateTime)
                return false;
            string doc = File.ReadAllText(path, Encoding.UTF8);
            // Only three identities: exact emitted command, originating
            // conversation, and project. No footer sentinel or archive scan.
            return doc.Contains(messageId, StringComparison.OrdinalIgnoreCase) &&
                doc.Contains(oldChatUrl, StringComparison.OrdinalIgnoreCase) &&
                doc.Contains(projectKey, StringComparison.OrdinalIgnoreCase) &&
                doc.Contains("SATANAMD", StringComparison.OrdinalIgnoreCase) &&
                doc.Contains("HANDOFF", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static int SelfTest()
    {
        if (!SkipOldChatHandoffWait("obl", "request_pending") ||
            !SkipOldChatHandoffWait("OBL", "waiting_handoff") ||
            SkipOldChatHandoffWait("obl", "rotate") ||
            SkipOldChatHandoffWait("other", "request_pending") ||
            SkipOldChatHandoffWait("other", "waiting_handoff")) return 16;
        string path = Path.Combine(Path.GetTempPath(), "obl-emergency-" +
            Guid.NewGuid().ToString("N") + ".md");
        const string old = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
        try
        {
            var sent = DateTimeOffset.UtcNow.AddMinutes(-1);
            var id = MessageId("obl", 43, 1, sent);
            if (!id.StartsWith("OBL-000043.1-A01-", StringComparison.Ordinal)) return 1;
            if (FreshHandoff(path, sent, id, "obl", old)) return 2;
            File.WriteAllText(path, "# HANDOFF\nProjekt: obl\n" +
                "Pełne ID: " + id + "\nStary czat: " + old +
                "\nHost: SATANAMD\n" + new string('x', 250));
            if (!FreshHandoff(path, sent, id, "obl", old)) return 3;
            if (FreshHandoff(path, sent, id.Replace(".1-", ".2-"),
                "obl", old)) return 4;
            if (FreshHandoff(path, sent, id, "nr", old)) return 5;
            if (FreshHandoff(path, sent, id, "obl",
                old.Replace("1111", "2222"))) return 6;
            if (FreshHandoff(path, DateTimeOffset.UtcNow.AddMinutes(1),
                id, "obl", old)) return 7;
            var wake = new LiteChatWakeRegistry.Chat {
                Url=old, WakeSequence=43, Enabled=true, EmergencyPhase="waiting_handoff",
                EmergencyMessageId=id, EmergencySentUtc=sent,
                EmergencyDeadlineUtc=sent.AddMinutes(30),
                EmergencyReason="conversation_length_exceeded"
            };
            var json = System.Text.Json.JsonSerializer.Serialize(wake);
            var restarted = System.Text.Json.JsonSerializer.Deserialize<
                LiteChatWakeRegistry.Chat>(json);
            if (restarted is null || restarted.EmergencyPhase != "waiting_handoff" ||
                restarted.EmergencyMessageId != id ||
                restarted.EmergencyDeadlineUtc != wake.EmergencyDeadlineUtc ||
                restarted.WakeSequence != 43) return 8;
            // Restart 12 minutes after request: only 18 minutes remain.
            if ((restarted.EmergencyDeadlineUtc -
                restarted.EmergencySentUtc.AddMinutes(12)).TotalMinutes != 18)
                return 9;
            // Regression: even if old-chat WebView2 could NOT navigate,
            // the persisted one-shot reservation still expires at minute 30.
            if (!WaitForHandoffUntilDeadline(sent.AddMinutes(29),
                    restarted.EmergencySentUtc, restarted.EmergencyDeadlineUtc))
                return 10;
            if (WaitForHandoffUntilDeadline(sent.AddMinutes(30),
                    restarted.EmergencySentUtc, restarted.EmergencyDeadlineUtc))
                return 11;
            if (WaitForHandoffUntilDeadline(sent.AddMinutes(45),
                    restarted.EmergencySentUtc, restarted.EmergencyDeadlineUtc))
                return 12;
            if (WaitForHandoffUntilDeadline(sent, default,
                    restarted.EmergencyDeadlineUtc))
                return 13;
            if (WaitForHandoffUntilDeadline(sent,
                    restarted.EmergencySentUtc, sent.AddDays(1)))
                return 14;
            if (!WaitForHandoffUntilDeadline(sent.AddMinutes(12),
                    restarted.EmergencySentUtc, restarted.EmergencyDeadlineUtc))
                return 15;
            return 0;
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }
}
