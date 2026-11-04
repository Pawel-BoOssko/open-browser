using System.Text.Json;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // Check waiting server-archived chats against the complete normal list.
    // Archive-list presence is not required by the owner's acceptance policy.
    // No browser navigation or PATCH.
    // If a wake lands just before a server rate-limit cooldown ends, avoid
    // deferring a read-only audit for a whole 15-minute wake interval.
    // Longer cooldowns must not block the wake scheduler.
    internal static TimeSpan? NativeVisibilityShortCooldownDelay(
        DateTimeOffset nowUtc, DateTimeOffset nextAttemptUtc)
    {
        var remaining = nextAttemptUtc - nowUtc;
        return remaining > TimeSpan.Zero && remaining <= TimeSpan.FromSeconds(60)
            ? remaining + TimeSpan.FromSeconds(1) : null;
    }

    private async Task AuditNativeArchiveVisibilityAsync(string activeLeadUrl)
    {
        try
        {
            var registry = LiteProjectRegistry.OpenDefault();
            var project = registry.Projects.FirstOrDefault(p => p.Active &&
                p.Chats.Any(c => c.Role == "lead" && c.Url == activeLeadUrl));
            if (project is null) return;
            await AuditNativeArchiveSidebarAsync(activeLeadUrl);
            if (!project.Chats.Any(c => c.Role != "lead" &&
                c.NativeArchivedUtc.HasValue &&
                !c.NativeArchiveVisibilityVerifiedUtc.HasValue &&
                !c.NativeArchiveOwnerAccepted429Utc.HasValue))
            {
                LiteEventLog.Write("native_visibility_no_unresolved_archives", new {
                    project = project.Key, ownerPolicyComplete = true,
                    normalListRemovalVerified = project.Chats.Where(c => c.Role != "lead" &&
                        c.NativeArchivedUtc.HasValue).All(c =>
                            c.NativeArchiveVisibilityVerifiedUtc.HasValue)
                });
                return;
            }
            if (project.NativeVisibilityNextAttemptUtc is { } nextAttempt &&
                nextAttempt > DateTimeOffset.UtcNow)
            {
                var shortWait = NativeVisibilityShortCooldownDelay(
                    DateTimeOffset.UtcNow, nextAttempt);
                if (shortWait.HasValue)
                {
                    LiteEventLog.Write("native_visibility_cooldown_short_wait", new {
                        project = project.Key, waitMs = (int)shortWait.Value.TotalMilliseconds,
                        nextAttemptUtc = nextAttempt });
                    await Task.Delay(shortWait.Value);
                    // The active lead or cooldown might change during the wait.
                    registry = LiteProjectRegistry.OpenDefault();
                    project = registry.Projects.FirstOrDefault(p => p.Active &&
                        p.Chats.Any(c => c.Role == "lead" && c.Url == activeLeadUrl));
                    if (project is null) return;
            await AuditNativeArchiveSidebarAsync(activeLeadUrl);
                }
                if (project.NativeVisibilityNextAttemptUtc is { } stillBlocked &&
                    stillBlocked > DateTimeOffset.UtcNow)
                {
                    LiteEventLog.Write("native_visibility_skipped", new {
                        project = project.Key, reason = "rate_limit_cooldown",
                        nextAttemptUtc = stillBlocked });
                    return;
                }
            }
            var ids = new List<string>();
            var chatIdsByUuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var chat in project.Chats.Where(c => c.Role != "lead" &&
                         c.NativeArchivedUtc.HasValue &&
                         !c.NativeArchiveVisibilityVerifiedUtc.HasValue &&
                         !c.NativeArchiveOwnerAccepted429Utc.HasValue))
            {
                if (!Uri.TryCreate(chat.Url, UriKind.Absolute, out var uri) ||
                    uri.Scheme != "https" || uri.Host != "chatgpt.com" ||
                    !uri.AbsolutePath.StartsWith("/c/", StringComparison.Ordinal))
                    continue;
                var uuid = uri.AbsolutePath["/c/".Length..];
                if (!Guid.TryParseExact(uuid, "D", out _) || chatIdsByUuid.ContainsKey(uuid))
                    continue;
                ids.Add(uuid);
                chatIdsByUuid.Add(uuid, chat.Id);
            }
            if (ids.Count == 0) return;
            if (ids.Count > 32)
                ids = ids.Take(32).ToList();
            var foreground = _browser.CoreWebView2;
            var worker = _chatWakeWorker.CoreWebView2;
            var core = foreground is not null &&
                LiteDevWakeState.NormalizeUrl(foreground.Source) == activeLeadUrl
                ? foreground
                : worker is not null &&
                  LiteDevWakeState.NormalizeUrl(worker.Source) == activeLeadUrl
                    ? worker : null;
            if (core is null)
            {
                LiteEventLog.Write("native_visibility_unverified",
                    new { project = project.Key, candidateCount = ids.Count,
                          reason = "no_matching_webview" });
                return;
            }
            string js = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "LiteChatGptNativeArchiveVisibility.js"));
            await core.ExecuteScriptAsync(js).WaitAsync(TimeSpan.FromSeconds(5));
            string start = JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    "window.__OBL_NATIVE_VISIBILITY_V2__?.auditBatch(" +
                    JsonSerializer.Serialize(ids) + ")").WaitAsync(TimeSpan.FromSeconds(5)))
                    ?? "script_missing";
            if (start != "started")
            {
                LiteEventLog.Write("native_visibility_unverified",
                    new { project = project.Key, candidateCount = ids.Count, reason = start });
                return;
            }
            string result = "running";
            // Large accounts may require many paginated GETs. Never mark an
            // incomplete scan as proof that a chat vanished from the normal list.
            for (int i = 0; i < 160; i++)
            {
                await Task.Delay(750);
                result = JsonSerializer.Deserialize<string>(
                    await core.ExecuteScriptAsync(
                        "window.__OBL_NATIVE_VISIBILITY_V2__?.status()")
                        .WaitAsync(TimeSpan.FromSeconds(5))) ?? "script_lost";
                if (result != "running") break;
            }
            if (result == "running")
            {
                LiteEventLog.Write("native_visibility_unverified",
                    new { project = project.Key, candidateCount = ids.Count,
                          reason = "host_poll_timeout" });
                return;
            }
            using var parsed = JsonDocument.Parse(result);
            var root = parsed.RootElement;
            if (!root.TryGetProperty("archive", out var archive) ||
                !root.TryGetProperty("normal", out var normal))
            {
                string error = root.TryGetProperty("error", out var item)
                    ? item.GetString() ?? "unknown" : "invalid_result";
                bool sessionRateLimited = error == "session_http_429";
                DateTimeOffset? sessionCooldownUntil = sessionRateLimited
                    ? DateTimeOffset.UtcNow.AddHours(1) : null;
                bool sessionCooldownRecorded = sessionCooldownUntil.HasValue &&
                    LiteProjectRegistry.OpenDefault().SetNativeVisibilityCooldown(
                        project.Id, sessionCooldownUntil.Value);
                LiteEventLog.Write("native_visibility_unverified",
                    new { project = project.Key, candidateCount = ids.Count,
                          reason = error, sessionRateLimited,
                          sessionCooldownRecorded, sessionCooldownUntil });
                return;
            }
            var archiveFound = archive.GetProperty("found").EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var normalFound = normal.GetProperty("found").EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string archiveStatus = archive.GetProperty("status").GetString() ?? "unknown";
            string normalStatus = normal.GetProperty("status").GetString() ?? "unknown";
            int retries = root.TryGetProperty("retries", out var retryValue) &&
                retryValue.ValueKind == JsonValueKind.Number ? retryValue.GetInt32() : 0;
            int? firstTotal = normal.TryGetProperty("firstTotal", out var firstValue) &&
                firstValue.ValueKind == JsonValueKind.Number ? firstValue.GetInt32() : null;
            int? lastTotal = normal.TryGetProperty("lastTotal", out var lastValue) &&
                lastValue.ValueKind == JsonValueKind.Number ? lastValue.GetInt32() : null;
            bool offsetRelativeTotal = normal.TryGetProperty(
                "offsetRelativeTotal", out var totalMode) &&
                totalMode.ValueKind == JsonValueKind.True;            bool rateLimited = archiveStatus == "http_429" ||
                normalStatus == "http_429";
            DateTimeOffset? cooldownUntil = rateLimited
                ? DateTimeOffset.UtcNow.AddHours(1) : null;
            bool cooldownRecorded = cooldownUntil.HasValue &&
                LiteProjectRegistry.OpenDefault().SetNativeVisibilityCooldown(
                    project.Id, cooldownUntil.Value);
            bool normalComplete = normalStatus == "complete";
            if (normalFound.Count > 0)
                LiteEventLog.Write("native_archive_still_in_normal_list", new {
                    project = project.Key, count = normalFound.Count,
                    chatIds = normalFound.Select(id => chatIdsByUuid.GetValueOrDefault(id, id)).ToArray(),
                    normalStatus, provenVisible = true });
            // Owner policy: 429 on the ordinary list is accepted operationally
            // only after an independent per-chat archived=true readback.
            // Keep this status distinct from complete-list absence proof.
            int ownerAccepted429 = 0;
            if (normalStatus == "http_429")
            {
                var eligible = project.Chats.Where(c => c.Role != "lead" &&
                    c.NativeArchivedUtc.HasValue &&
                    !c.NativeArchiveVisibilityVerifiedUtc.HasValue &&
                    !c.NativeArchiveOwnerAccepted429Utc.HasValue)
                    .Where(c => Uri.TryCreate(c.Url, UriKind.Absolute, out var uri) &&
                        uri.Scheme == "https" && uri.Host == "chatgpt.com" &&
                        uri.AbsolutePath.StartsWith("/c/", StringComparison.Ordinal) &&
                        Guid.TryParseExact(uri.AbsolutePath["/c/".Length..], "D", out _) &&
                        !normalFound.Contains(uri.AbsolutePath["/c/".Length..]))
                    .Select(c => c.Id).ToArray();
                ownerAccepted429 = LiteProjectRegistry.OpenDefault()
                    .MarkNativeArchiveOwnerAccepted429Batch(project.Id, eligible);                LiteEventLog.Write("native_archive_owner_429_accepted", new {
                    project = project.Key, ownerAccepted429, reason = normalStatus,
                    priorServerArchiveRequired = true,
                    normalListRemovalVerified = false,
                    retryForAcceptedChats = false
                });
            }
            int savedCount = 0;
            if (normalComplete)
                foreach (var uuid in ids)
                    if (!normalFound.Contains(uuid) &&
                        LiteProjectRegistry.OpenDefault().MarkNativeArchiveVisibilityVerified(
                            project.Id, chatIdsByUuid[uuid]))
                        savedCount++;
            LiteEventLog.Write(savedCount > 0 ? "native_visibility_confirmed" :
                ownerAccepted429 > 0 ? "native_visibility_owner_accepted_429" :
                "native_visibility_unverified", new {
                    project = project.Key, candidateCount = ids.Count,
                    archiveStatus, normalStatus,
                    archivePages = archive.GetProperty("pages").GetInt32(),
                    normalPages = normal.GetProperty("pages").GetInt32(),
                    archiveMatches = archiveFound.Count,
                    normalMatches = normalFound.Count,
                    retries, firstTotal, lastTotal, offsetRelativeTotal,
                    rateLimited, cooldownRecorded, cooldownUntil,
                    savedCount, normalComplete, ownerAccepted429, ownerOperationalSuccess = ownerAccepted429 > 0
                });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("native_visibility_failed", new {
                errorType = ex.GetType().Name, error = ex.Message });
        }
    }
}
