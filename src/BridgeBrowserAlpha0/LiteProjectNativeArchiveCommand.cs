namespace BridgeBrowserAlpha0;

internal sealed record LiteNativeArchiveManualResult(
    int NewlyArchivedOnServer, int NewlyAbsentFromNormalList,
    int ConfirmedAbsentFromNormalList, int OwnerAccepted429, int StillUnverified);

// Manual action now checks both stages: server flag and disappearance from the
// complete ordinary conversation list. A backend archive flag is not success.
internal sealed partial class ChatGptLiteForm
{
    private async Task<LiteNativeArchiveManualResult> ArchiveOlderChatsOfProjectAsync(string projectId)
    {
        var project = LiteProjectRegistry.OpenDefault().Projects
            .SingleOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Projekt nie istnieje.");
        if (!project.Active)
            throw new InvalidOperationException("Projekt jest nieaktywny.");
        var lead = project.Chats.SingleOrDefault(c => c.Role == "lead")
            ?? throw new InvalidOperationException("Brak jednego czatu glownego.");
        var fg = _browser.CoreWebView2;
        var wk = _chatWakeWorker.CoreWebView2;
        bool matches = fg is not null &&
            LiteDevWakeState.NormalizeUrl(fg.Source) == lead.Url;
        bool workerMatches = wk is not null &&
            LiteDevWakeState.NormalizeUrl(wk.Source) == lead.Url;
        if (!matches && !workerMatches)
        {
            LiteEventLog.Write("native_archive_manual_blocked", new
            {
                projectKey = project.Key, reason = "no_matching_lead_webview"
            });
            throw new InvalidOperationException(
                "Otworz czat glowny projektu, aby sprawdzic archiwizacje ChatGPT.");
        }

        int backendBefore = project.Chats.Count(c => c.Role != "lead" &&
            c.NativeArchivedUtc.HasValue);
        int visibleVerifiedBefore = project.Chats.Count(c => c.Role != "lead" &&
            c.NativeArchiveVisibilityVerifiedUtc.HasValue);
        bool hasUnarchived = project.Chats.Where(c => c.Role != "lead")
            .OrderByDescending(c => c.AddedUtc)
            .ThenBy(c => c.Id, StringComparer.Ordinal).Skip(2)
            .Any(c => !c.NativeArchivedUtc.HasValue);
        LiteEventLog.Write("native_archive_manual_requested", new
        {
            projectKey = project.Key, backendBefore,
            visibleVerifiedBefore, hasUnarchived
        });
        if (hasUnarchived)
            await ArchiveOlderNativeChatBatchAsync(lead.Url);

        // This method checks the rendered sidebar even during API cooldown;
        // only complete normal-list pagination can certify absence.
        await AuditNativeArchiveVisibilityAsync(lead.Url);

        var updated = LiteProjectRegistry.OpenDefault().Projects
            .SingleOrDefault(p => p.Id == projectId)
            ?? throw new InvalidDataException("Projekt zniknal po weryfikacji.");
        int backendAfter = updated.Chats.Count(c => c.Role != "lead" &&
            c.NativeArchivedUtc.HasValue);
        int visibleVerifiedAfter = updated.Chats.Count(c => c.Role != "lead" &&
            c.NativeArchiveVisibilityVerifiedUtc.HasValue);
        var outcome = new LiteNativeArchiveManualResult(
            Math.Max(0, backendAfter - backendBefore),
            Math.Max(0, visibleVerifiedAfter - visibleVerifiedBefore),
            visibleVerifiedAfter,
            updated.Chats.Count(c => c.Role != "lead" &&
                c.NativeArchivedUtc.HasValue &&
                c.NativeArchiveOwnerAccepted429Utc.HasValue &&
                !c.NativeArchiveVisibilityVerifiedUtc.HasValue),
            updated.Chats.Count(c => c.Role != "lead" &&
                c.NativeArchivedUtc.HasValue &&
                !c.NativeArchiveVisibilityVerifiedUtc.HasValue &&
                !c.NativeArchiveOwnerAccepted429Utc.HasValue));
        LiteEventLog.Write("native_archive_manual_result", new
        {
            projectKey = project.Key, outcome.NewlyArchivedOnServer,
            outcome.NewlyAbsentFromNormalList,
            outcome.ConfirmedAbsentFromNormalList, outcome.OwnerAccepted429,
            outcome.StillUnverified,
            allNormalListAbsencesVerified = outcome.StillUnverified == 0 && outcome.OwnerAccepted429 == 0,
            allOwnerPolicyStatusesSatisfied = outcome.StillUnverified == 0 && outcome.OwnerAccepted429 == 0, allOperationallyAccepted = outcome.StillUnverified == 0
        });
        return outcome;
    }
}
