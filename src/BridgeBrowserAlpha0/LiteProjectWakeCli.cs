using System.Globalization;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Exact stable project-key wake CLI. Keys are selectors, not credentials.
/// The project must own exactly one lead before enabling or dispatching.
/// </summary>
internal static class LiteProjectWakeCli
{
    internal static int Run(string[] args)
    {
        try
        {
            if (args.Length < 3 || args[0] != "--lite-project-wake")
                return Fail("syntax",2);
            string action = args[1].ToLowerInvariant();
            if (action is not ("status" or "off" or "on" or "now" or "every"))
                return Fail("unknown_action",2);
            if (args.Length != (action == "every" ? 4 : 3))
                return Fail("argument_count",2);
            string key = args[^1];
            if (!ValidKey(key))
                return Fail("invalid_exact_project_key",3);
            var db = LiteProjectRegistry.OpenDefault();
            var project = FindProject(db.Projects,key);
            if (project is null)
                return Fail("unknown_exact_project_key",4);
            var leads = project.Chats.Where(c => c.Role == "lead").ToArray();
            var records = LiteChatWakeRegistry.Read();
            var record = leads.Length == 1
                ? records.FirstOrDefault(c => c.Url == leads[0].Url) : null;
            if (action == "status")
            {
                Report(new { project = key, enabled = record?.Enabled ?? false,
                    hasLead = leads.Length == 1, minutes = record?.EffectiveMinutes,
                    dueUtc = record?.DueUtc, slotAnchorUtc = record?.SlotAnchorUtc,
                    sequence = record?.WakeSequence,
                    rotationPending = record is not null && LiteWakeRotation.BeforeNewLeadWake(record!.WakeSequence),
                    rotationPhase = record?.RotationPhase });
                return 0;
            }
            if (leads.Length != 1)
                return Fail("missing_unique_project_lead",5);
            // An authorized project chat may create its own independent
            // schedule using "every 15m <exact-key>". Other operations
            // still require an existing schedule.
            if (record is null && action != "every")
                return Fail("project_wake_not_configured",6);
            if (action == "off")
            {
                record!.Enabled = false;
                record!.LastState = "project_cli_off";
            }
            else if (action == "now")
            {
                if (!record!.Enabled) return Fail("schedule_disabled",7);
                record!.DueUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                record!.LastState = "project_cli_now_due";
            }
            else if (action == "on")
            {
                record!.Enabled = true;
                record!.DueUtc = DateTimeOffset.UtcNow.AddMinutes(record!.EffectiveMinutes);
                record!.SlotAnchorUtc = record!.DueUtc;
                record!.LastState = "project_cli_on";
            }
            else if (action == "every")
            {
                var field = args[2];
                if (!field.EndsWith('m') ||
                    !int.TryParse(field[..^1],NumberStyles.None,
                        CultureInfo.InvariantCulture,out int minutes) ||
                    minutes is < 15 or > 1440)
                    return Fail("invalid_interval",8);
                // Explicit "every" is the project's self-service activation.
                // Only its unique registered lead URL may be configured.
                record ??= LiteChatWakeRegistry.Configure(records,
                    leads[0].Url, minutes,
                    "Kontynuuj prace w projekcie zgodnie z jego planem.",
                    DateTimeOffset.UtcNow);
                record!.Minutes = Math.Max(20,minutes);
                record!.FastMinutes = minutes < 20 ? minutes : 0;
                record!.Enabled = true;
                record!.DueUtc = DateTimeOffset.UtcNow.AddMinutes(minutes);
                record!.SlotAnchorUtc = record!.DueUtc;
                record!.LastState = "project_cli_every";
            }
            LiteChatWakeRegistry.Write(records);
            Report(new { project=key, action, enabled=record!.Enabled,
                minutes=record!.EffectiveMinutes, dueUtc=record!.DueUtc,
                slotAnchorUtc=record!.SlotAnchorUtc });
            return 0;
        }
        catch (Exception e)
        {
            LiteEventLog.Write("project_wake_cli_failed",
                new { errorType=e.GetType().Name, hresult=e.HResult,
                    errorCode="operation_failed" });
            return 9;
        }
    }

    private static void Report(object result)
    {
        var json = JsonSerializer.Serialize(result);
        Console.WriteLine(json);
        LiteEventLog.Write("project_wake_cli",result);
    }

    private static int Fail(string reason,int exit)
    {
        Console.Error.WriteLine(reason);
        LiteEventLog.Write("project_wake_cli_refused",
            new { reason, exit });
        return exit;
    }

    internal static bool ValidKey(string code) =>
        code.Length is >= 2 and <= 16 &&
        code.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9');

    internal static LiteProjectRegistry.Project? FindProject(
        IReadOnlyList<LiteProjectRegistry.Project> projects, string key)
    {
        if (!ValidKey(key)) return null;
        var matches=projects.Where(p => p.Key == key).Take(2).ToArray();
        if (matches.Length > 1)
            throw new InvalidDataException("Duplicate stable project key");
        return matches.SingleOrDefault();
    }

    internal static int SelfTest()
    {
        var p1=new LiteProjectRegistry.Project { Title="Open Browser Lite", Key="obl" };
        var p2=new LiteProjectRegistry.Project { Title="Networking Radar", Key="nr" };
        LiteProjectRegistry.Project[] projects={p1,p2};
        if (!ValidKey("obl") || !ValidKey("nr") ||
            ValidKey("OBL") || ValidKey("obl-000007.1.132238") ||
            ValidKey("ol!") || ValidKey("o") ||
            FindProject(projects,"obl") != p1 ||
            FindProject(projects,"nr") != p2 ||
            FindProject(projects,"ob") is not null ||
            FindProject(projects,"OBL") is not null) return 1;
        if (LiteProjectRegistry.DefaultProjectKey("Open Browser Lite",p1.Id)
            != "obl" ||
            LiteProjectRegistry.DefaultProjectKey("Networking Radar",p2.Id)
            != "nr") return 2;
        return 0;
    }
}
