using System.Text.Json;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // Bounded catch-up of old project chats. Every successor is selected from
    // the freshly reloaded registry and requires an independent server GET.
    // Stop on any uncertain result; never continue a batch after a failed proof.
    private async Task ArchiveOlderNativeChatBatchAsync(string activeLeadUrl)
    {
        const int maxPerWake = 4;
        int confirmed = 0;
        for (int i = 0; i < maxPerWake; i++)
        {
            var before = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(
                p => p.Active && p.Chats.Any(c => c.Role == "lead" && c.Url == activeLeadUrl));
            if (before is null) break;
            int archivedBefore = before.Chats.Count(c => c.NativeArchivedUtc.HasValue);
            await ArchiveOneOlderNativeChatAsync(activeLeadUrl);
            var after = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(
                p => p.Id == before.Id && p.Active &&
                     p.Chats.Any(c => c.Role == "lead" && c.Url == activeLeadUrl));
            if (after is null || after.Chats.Count(c => c.NativeArchivedUtc.HasValue) <= archivedBefore)
                break;
            confirmed++;
            if (i + 1 < maxPerWake) await Task.Delay(350);
        }
        LiteEventLog.Write("native_archive_backend_batch_completed", new {
            maxPerWake, backendConfirmed = confirmed, visibleListVerified = false, stoppedBeforeLimit = confirmed < maxPerWake });
    }

    // Process one older bookmarked conversation after a confirmed wake.
    // No navigation, no Delete endpoint and no second browser/process.
    private async Task ArchiveOneOlderNativeChatAsync(string activeLeadUrl)
    {
        string? attemptedProject = null;
        string? attemptedChatId = null;
        try
        {
            var registry = LiteProjectRegistry.OpenDefault();
            var project = registry.Projects.FirstOrDefault(p => p.Active &&
                p.Chats.Any(c => c.Role == "lead" && c.Url == activeLeadUrl));
            if (project is null)
            {
                LiteEventLog.Write("native_archive_skipped", new {
                    reason = "no_active_project_lead" });
                return;
            }
            attemptedProject = project.Key;
            var old = project.Chats.Where(c => c.Role != "lead")
                .OrderByDescending(c => c.AddedUtc)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .Skip(2)
                // Confirm previously attempted PATCH before submitting new ones.
                .OrderByDescending(c => c.NativeArchiveVerifyPendingUtc.HasValue)
                .FirstOrDefault(c => !c.NativeArchivedUtc.HasValue &&
                    Uri.TryCreate(c.Url, UriKind.Absolute, out var uri) &&
                    uri.Host == "chatgpt.com" &&
                    uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase));
            if (old is null)
            {
                LiteEventLog.Write("native_archive_skipped", new {
                    project = project.Key, reason = "no_eligible_old_chat" });
                return;
            }

            attemptedChatId = old.Id;
            // Most OBL wakes are delivered in the foreground WebView2.
            // A background worker may not have been initialized at all.
            var foreground = _browser.CoreWebView2;
            var worker = _chatWakeWorker.CoreWebView2;
            var core = foreground is not null &&
                LiteDevWakeState.NormalizeUrl(foreground.Source) == activeLeadUrl
                ? foreground
                : worker is not null &&
                  LiteDevWakeState.NormalizeUrl(worker.Source) == activeLeadUrl
                    ? worker : null;
            if (core is null || !Uri.TryCreate(core.Source, UriKind.Absolute, out var source) ||
                source.Scheme != "https" || source.Host != "chatgpt.com")
            {
                LiteEventLog.Write("native_archive_skipped", new {
                    project = project.Key, reason = "no_matching_chat_webview",
                    foregroundAvailable = foreground is not null,
                    workerAvailable = worker is not null });
                return;
            }
            var id = new Uri(old.Url).AbsolutePath["/c/".Length..];
            if (!Guid.TryParseExact(id, "D", out _)) return;
            var js = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "LiteChatGptNativeArchive.js"));
            await core.ExecuteScriptAsync(js).WaitAsync(TimeSpan.FromSeconds(5));
            string? initial = JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    "window.__OBL_NATIVE_ARCHIVE__?.start(" +
                    JsonSerializer.Serialize(id) + "," +
                    (old.NativeArchiveVerifyPendingUtc.HasValue ? "true" : "false") + ")")
                    .WaitAsync(TimeSpan.FromSeconds(5)));
            if (initial != "started")
            {
                LiteEventLog.Write("native_archive_skipped", new {
                    project = project.Key, reason = initial ?? "script_unavailable" });
                return;
            }
            string result = "running";
            // Up to 5s session + 12s initial GET + 5s PATCH + 12s
            // independent GET readback, with room for WebView2 scheduling.
            for (int i = 0; i < 64; i++)
            {
                await Task.Delay(750);
                result = JsonSerializer.Deserialize<string>(
                    await core.ExecuteScriptAsync(
                        "window.__OBL_NATIVE_ARCHIVE__?.status()")
                        .WaitAsync(TimeSpan.FromSeconds(5))) ?? "script_lost";
                if (result != "running") break;
            }
            if (result is "archived" or "already_archived")
            {
                var latest = LiteProjectRegistry.OpenDefault();
                bool recorded = latest.MarkNativeArchived(project.Id, old.Id);
                LiteEventLog.Write("native_archive_backend_confirmed", new {
                    project = project.Key, recorded, result, chatId = old.Id,
                    serverReadback = true, normalListRemovalVerified = false });
            }
            else
            {
                bool verificationOnly = old.NativeArchiveVerifyPendingUtc.HasValue;
                bool verifiedStillUnarchived = verificationOnly &&
                    result == "verify_unarchived";
                bool clearedPending = verifiedStillUnarchived &&
                    LiteProjectRegistry.OpenDefault().ClearNativeArchiveVerifyPending(
                        project.Id, old.Id);
                bool verificationPending = !verifiedStillUnarchived &&
                    (result is "running" or "timeout" or "request_error" or
                        "verify_timeout" or "verify_request_error" or
                        "verify_unarchived" ||
                     result.StartsWith("verify_http_", StringComparison.Ordinal));
                bool pendingRecorded = verificationPending &&
                    LiteProjectRegistry.OpenDefault().MarkNativeArchiveVerifyPending(
                        project.Id, old.Id);
                LiteEventLog.Write("native_archive_unconfirmed", new {
                    project = project.Key, reason = result, chatId = old.Id,
                    verificationOnly, pendingRecorded, clearedPending });
            }
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("native_archive_failed", new {
                project = attemptedProject, chatId = attemptedChatId,
                errorType = ex.GetType().Name, error = ex.Message });
        }
    }
}
