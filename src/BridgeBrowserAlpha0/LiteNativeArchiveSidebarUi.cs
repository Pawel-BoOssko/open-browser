using System.Text.Json;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // A positive sidebar match is direct evidence that an old chat remains
    // in the normal view. A non-match is NEVER evidence of disappearance,
    // because ChatGPT may virtualize or lazy-load the conversation list.
    private async Task AuditNativeArchiveSidebarAsync(string activeLeadUrl)
    {
        try
        {
            var project = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(
                p => p.Active && p.Chats.Any(c => c.Role == "lead" && c.Url == activeLeadUrl));
            if (project is null) return;
            var ids = new List<string>();
            foreach (var chat in project.Chats.Where(c =>
                c.Role != "lead" && c.NativeArchivedUtc.HasValue))
            {
                if (!Uri.TryCreate(chat.Url, UriKind.Absolute, out var uri) ||
                    uri.Scheme != "https" || uri.Host != "chatgpt.com" ||
                    !uri.AbsolutePath.StartsWith("/c/", StringComparison.Ordinal)) continue;
                string id = uri.AbsolutePath["/c/".Length..];
                if (Guid.TryParseExact(id, "D", out _) && !ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                    ids.Add(id);
            }
            if (ids.Count == 0) return;
            ids = ids.Take(100).ToList();
            var fg = _browser.CoreWebView2;
            var wk = _chatWakeWorker.CoreWebView2;
            var core = fg is not null && LiteDevWakeState.NormalizeUrl(fg.Source) == activeLeadUrl
                ? fg : wk is not null && LiteDevWakeState.NormalizeUrl(wk.Source) == activeLeadUrl
                ? wk : null;
            if (core is null) return;

            string js = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "LiteChatGptSidebarArchiveAudit.js"));
            await core.ExecuteScriptAsync(js).WaitAsync(TimeSpan.FromSeconds(5));
            string result = JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    "window.__OBL_SIDEBAR_ARCHIVE_AUDIT__?.inspect(" +
                    JsonSerializer.Serialize(ids) + ")").WaitAsync(TimeSpan.FromSeconds(5)))
                ?? "{}";
            using var report = JsonDocument.Parse(result);
            var root = report.RootElement;
            string status = root.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? "unknown" : "unknown";
            if (status != "observed")
                throw new InvalidDataException("sidebar_audit_" + status);
            int links = root.GetProperty("sidebarLinks").GetInt32();
            int renderedLinks = root.GetProperty("renderedSidebarLinks").GetInt32();
            string[] visible = root.GetProperty("visibleArchivedIds").EnumerateArray()
                .Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray();
            string[] inDom = root.GetProperty("sidebarDomArchivedIds").EnumerateArray()
                .Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray();
            LiteEventLog.Write(visible.Length > 0
                    ? "native_archive_still_in_normal_sidebar"
                    : inDom.Length > 0 ? "native_archive_sidebar_dom_only"
                    : "native_archive_sidebar_presence_unconfirmed", new {
                project = project.Key, serverArchivedCount = ids.Count,
                sidebarDomLinks = links, renderedSidebarLinks = renderedLinks,
                archivedChatsStillVisible = visible, archivedChatsInSidebarDom = inDom,
                countStillVisible = visible.Length, countInDom = inDom.Length,
                absenceProven = false,
                reason = links == 0 ? "no_sidebar_dom_links" :
                    visible.Length > 0 ? "css_rendered_sidebar_link" :
                    inDom.Length > 0 ? "hidden_sidebar_dom_only" :
                    "partial_sidebar_not_complete_list"
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("native_archive_sidebar_audit_failed", new {
                errorType = ex.GetType().Name, error = ex.Message
            });
        }
    }
}
