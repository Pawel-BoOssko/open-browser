using System.Text.Json;
using System.Text.Json.Nodes;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Canonical project-owned wake schedule in projects.json, with disabled
/// historical records retained for lossless migration. A live wake has no
/// stored URL; it always resolves to the project's single current lead.
/// The legacy DPAPI file remains a write-through rollback copy temporarily.
/// </summary>
internal static class LiteProjectWakeStore
{
    internal static List<LiteChatWakeRegistry.Chat>? Read()
    {
        var db = LiteProjectRegistry.OpenDefault();
        if (!db.WakeMigrated) return null;
        return Expand(db.Projects, db.RetiredWakes);
    }

    private static LiteChatWakeRegistry.Chat ExpandOne(
        LiteProjectRegistry.Project project)
    {
        var leads = project.Chats.Where(c => c.Role == "lead").ToArray();
        if (leads.Length != 1 || project.Wake is null)
            throw new InvalidDataException("Missing unique project wake lead");
        var wake = JsonSerializer.Deserialize<LiteChatWakeRegistry.Chat>(
            project.Wake.ToJsonString())
            ?? throw new InvalidDataException("Project wake is empty");
        if (project.Wake.ContainsKey("Url"))
            throw new InvalidDataException("Project wake redundantly stores URL");
        wake.Url = leads[0].Url;
        return wake;
    }

    internal static List<LiteChatWakeRegistry.Chat> Expand(
        IReadOnlyList<LiteProjectRegistry.Project> projects,
        IReadOnlyList<LiteChatWakeRegistry.Chat> retired)
    {
        var result = new List<LiteChatWakeRegistry.Chat>();
        foreach (var p in projects.Where(p => p.Wake is not null))
            result.Add(ExpandOne(p));
        foreach (var c in retired)
        {
            if (c.Enabled)
                throw new InvalidDataException("Retired wake must be disabled");
            // A successor can already have a disabled historical record. Keep
            // its history on disk, but expose only the active project schedule.
            if (!result.Any(active => active.Url == c.Url)) result.Add(c);
        }
        if (result.Count > 48 || result.Select(x => x.Url)
            .Distinct(StringComparer.Ordinal).Count() != result.Count ||
            !LiteProjectRegistry.CanScheduleProjectLeads(projects, result))
            throw new InvalidDataException("Invalid project-owned wake registry");
        return result;
    }

    internal static void Write(IReadOnlyList<LiteChatWakeRegistry.Chat> chats)
    {
        var db = LiteProjectRegistry.OpenDefault(); // fresh, never cached UI data
        WriteInto(db, chats);
    }

    internal static void WriteInto(LiteProjectRegistry db,
        IReadOnlyList<LiteChatWakeRegistry.Chat> chats)
    {
        if (!LiteProjectRegistry.CanScheduleProjectLeads(db.Projects, chats))
            throw new InvalidDataException("Project wake lead check failed");
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var wakeMap = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var p in db.Projects)
        {
            var lead = p.Chats.Where(c => c.Role == "lead").ToArray();
            if (lead.Length > 1)
                throw new InvalidDataException("Multiple leads for a project");
            if (lead.Length != 1) continue;
            var w = chats.SingleOrDefault(c => c.Url == lead[0].Url);
            if (w is null) continue;
            assigned.Add(w.Url);
            var node = JsonSerializer.SerializeToNode(w) as JsonObject
                ?? throw new InvalidDataException("Invalid project wake");
            node.Remove("Url");
            wakeMap.Add(p.Id, node);
        }
        var retired = chats.Where(c => !assigned.Contains(c.Url)).ToList();
        // Preserve archived records of a newly promoted lead across writes.
        foreach (var historical in db.RetiredWakes.Where(c => assigned.Contains(c.Url)))
            if (!retired.Any(c => c.Url == historical.Url)) retired.Add(historical);
        if (retired.Any(c => c.Enabled))
            throw new InvalidDataException("Enabled wake lacks project lead");
        // Atomic projects.json Save; old DPAPI file remains available on rollback.
        db.ReplaceProjectWakes(wakeMap, retired);
    }

    internal static int SelfTest()
    {
        try
        {
            const string a = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            const string b = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            var project = new LiteProjectRegistry.Project
            {
                Title = "Test Project",
                Chats = new List<LiteProjectRegistry.Chat> {
                    new() { Url = a, Role = "lead" },
                    new() { Url = b, Role = "archive" }
                }
            };
            var live = new LiteChatWakeRegistry.Chat
            {
                Url = a, Enabled = true, Minutes = 20, FastMinutes = 15,
                WakeSequence = 15,
                SlotAnchorUtc = DateTimeOffset.Parse("2026-10-28T12:00:00Z"),
                Prompt = "Test", LastState = "armed"
            };
            project.Wake = JsonSerializer.SerializeToNode(live)!.AsObject();
            project.Wake.Remove("Url");
            var old = new LiteChatWakeRegistry.Chat { Url = b, Enabled = false,
                WakeSequence = 100, Prompt = "old" };
            var rows = Expand(new[] { project }, new[] { old });
            if (rows.Count != 2 || rows[0].Url != a || rows[1].WakeSequence != 100 ||
                rows[0].EffectiveMinutes != 15 ||
                rows[0].SlotAnchorUtc != live.SlotAnchorUtc) return 1;
            // The store must preserve project-owned schedule data during the
            // intermediate lead change; the rotation controller subsequently
            // resets DueUtc and SlotAnchorUtc from the first new wake timestamp.
            project.Chats[0].Role = "archive";
            project.Chats[1].Role = "lead";
            rows = Expand(new[] { project }, Array.Empty<LiteChatWakeRegistry.Chat>());
            if (rows.Count != 1 || rows[0].Url != b || rows[0].WakeSequence != 15 ||
                rows[0].SlotAnchorUtc != live.SlotAnchorUtc) return 2;
            if (project.Wake.ContainsKey("Url")) return 3;
            // Full isolated write/read migration: preserve one live + eight
            // disabled records; stale UI project save must not erase schedules.
            string fixture = Path.Combine(Path.GetTempPath(),
                "obl-wake-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = LiteProjectRegistry.Open(fixture);
                var staleUi = LiteProjectRegistry.Open(fixture);
                var projectEntry = store.Projects.First(p => p.Title == "Open Browser Lite");
                var assigned = store.AddChat(projectEntry.Id, "lead", a);
                store.PromoteLead(projectEntry.Id, assigned.Id);
                var fixtureRows = new List<LiteChatWakeRegistry.Chat> { live };
                for (int i = 0; i < 8; i++)
                    fixtureRows.Add(new LiteChatWakeRegistry.Chat {
                        Url = "https://chatgpt.com/c/" + (i + 1).ToString("D8") +
                              "-2222-2222-2222-222222222222",
                        Enabled = false, WakeSequence = 80 + i,
                        Prompt = "archived " + i
                    });
                WriteInto(store, fixtureRows);
                var reread = LiteProjectRegistry.Open(fixture);
                var after = Expand(reread.Projects, reread.RetiredWakes);
                if (!reread.WakeMigrated || after.Count != 9 ||
                    after.Count(x => x.Enabled) != 1 ||
                    reread.RetiredWakes.Count != 8 ||
                    after.Single(x => x.Enabled).WakeSequence != 15)
                    return 6;
                staleUi.AddProject("UI saved after wake migration");
                var afterUi = LiteProjectRegistry.Open(fixture);
                if (!afterUi.WakeMigrated || afterUi.RetiredWakes.Count != 8 ||
                    Expand(afterUi.Projects, afterUi.RetiredWakes).Count != 9)
                    return 7;
            }
            finally { if (File.Exists(fixture)) File.Delete(fixture); }
            try {
                Expand(new[] { project },
                    new[] { new LiteChatWakeRegistry.Chat { Url = a, Enabled = true } });
                return 4;
            } catch (InvalidDataException) { }
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("project_wake_selftest_failure", new { errorType = ex.GetType().Name, error = ex.Message });
            return 5;
        }
    }
}
