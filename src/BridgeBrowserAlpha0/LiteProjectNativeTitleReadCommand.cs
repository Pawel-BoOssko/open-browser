using System.Text.Json;

namespace BridgeBrowserAlpha0;

// Explicit server GET to reconcile an OBL chat bookmark with ChatGPT's title.
// Never sends a PATCH, changes the lead, or edits any schedule.
internal sealed partial class ChatGptLiteForm
{
    private async Task<string> ReadNativeProjectChatTitleAsync(string projectId, string chatId)
    {
        var project = LiteProjectRegistry.OpenDefault().Projects.SingleOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Projekt nie istnieje.");
        var chat = project.Chats.SingleOrDefault(c => c.Id == chatId)
            ?? throw new InvalidOperationException("Rozmowa nie nalezy do projektu.");
        var lead = project.Chats.SingleOrDefault(c => c.Role == "lead")
            ?? throw new InvalidOperationException("Brak jednego czatu glownego.");
        if (!Uri.TryCreate(chat.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "chatgpt.com" ||
            !uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(uri.AbsolutePath["/c/".Length..], "D", out _))
            throw new InvalidOperationException("Nieprawidlowy adres rozmowy ChatGPT.");
        string nativeId = uri.AbsolutePath["/c/".Length..];
        var fg = _browser.CoreWebView2;
        var worker = _chatWakeWorker.CoreWebView2;
        bool Matches(string? source) => source is not null &&
            (LiteDevWakeState.NormalizeUrl(source) == lead.Url ||
             LiteDevWakeState.NormalizeUrl(source) == chat.Url);
        var core = fg is not null && Matches(fg.Source) ? fg :
            worker is not null && Matches(worker.Source) ? worker : null;
        if (core is null || !Uri.TryCreate(core.Source, UriKind.Absolute, out var source) ||
            source.Scheme != Uri.UriSchemeHttps || source.Host != "chatgpt.com")
            throw new InvalidOperationException("Otworz czat glowny lub wybrana rozmowe tego projektu.");

        LiteEventLog.Write("native_chat_title_read_requested", new { projectKey = project.Key, chatId });
        try
        {
            string js = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "LiteChatGptNativeTitleRead.js"));
            await core.ExecuteScriptAsync(js).WaitAsync(TimeSpan.FromSeconds(5));
            string started = JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    "window.__OBL_NATIVE_TITLE_READ__?.start(" +
                    JsonSerializer.Serialize(nativeId) + ")").WaitAsync(TimeSpan.FromSeconds(5)))
                ?? "script_missing";
            if (started != "started")
                throw new InvalidOperationException("Nie uruchomiono odczytu ChatGPT: " + started);

            string result = "running";
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(750);
                result = JsonSerializer.Deserialize<string>(
                    await core.ExecuteScriptAsync("window.__OBL_NATIVE_TITLE_READ__?.status()")
                        .WaitAsync(TimeSpan.FromSeconds(5))) ?? "script_lost";
                if (result != "running") break;
            }
            if (result == "running")
                throw new TimeoutException("Serwer ChatGPT nie zakonczyl odczytu tytulu.");
            using var response = JsonDocument.Parse(result);
            var root = response.RootElement;
            string status = root.GetProperty("state").GetString() ?? "invalid_status";
            if (status != "ok")
            {
                string reason = root.TryGetProperty("code", out var code)
                    ? code.GetString() ?? "unknown" : "missing_error_code";
                throw new InvalidOperationException("Odczyt tytulu ChatGPT: " + reason);
            }
            string title = root.GetProperty("title").GetString()
                ?? throw new InvalidDataException("Serwer zwrocil pusty tytul.");
            if (title.Trim().Length is < 1 or > 120)
                throw new InvalidDataException("Tytul serwera ma dlugosc poza zakresem 1-120.");
            // Optimistic concurrency: do not overwrite newer edits in projects.json.
            LiteProjectRegistry.OpenDefault().RenameChatTitle(
                projectId, chatId, chat.Url, chat.Title, title);
            LiteEventLog.Write("native_chat_title_read_confirmed", new {
                projectKey = project.Key, chatId,
                updatedLocal = title != chat.Title, titleLength = title.Length,
                serverReadback = true
            });
            return title;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("native_chat_title_read_failed", new {
                projectKey = project.Key, chatId, errorType = ex.GetType().Name,
                error = ex.Message
            });
            throw;
        }
    }
}
