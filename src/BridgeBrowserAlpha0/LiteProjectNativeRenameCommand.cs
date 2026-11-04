using System.Text.Json;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // Change the real ChatGPT conversation title, not only the local project label.
    // No navigation, no Delete, and no updates to the wake schedule.
    private async Task<string> RenameNativeProjectChatAsync(
        string projectId, string chatId, string newTitle)
    {
        newTitle = (newTitle ?? "").Trim();
        if (newTitle.Length is < 1 or > 120)
            throw new ArgumentException("Tytul rozmowy musi miec 1-120 znakow.");

        var project = LiteProjectRegistry.OpenDefault().Projects
            .SingleOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Projekt nie istnieje.");
        var chat = project.Chats.SingleOrDefault(c => c.Id == chatId)
            ?? throw new InvalidOperationException("Rozmowa nie nalezy do projektu.");
        var lead = project.Chats.SingleOrDefault(c => c.Role == "lead")
            ?? throw new InvalidOperationException("Projekt nie ma jednego leada.");
        if (!Uri.TryCreate(chat.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "chatgpt.com" ||
            !uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(uri.AbsolutePath["/c/".Length..], "D", out _))
            throw new InvalidOperationException("Nieprawidlowy adres rozmowy ChatGPT.");
        string conversationId = uri.AbsolutePath["/c/".Length..];

        var fg = _browser.CoreWebView2;
        var wk = _chatWakeWorker.CoreWebView2;
        bool Matches(string? source) =>
            source is not null && (LiteDevWakeState.NormalizeUrl(source) == lead.Url ||
            LiteDevWakeState.NormalizeUrl(source) == chat.Url);
        var core = fg != null && Matches(fg.Source) ? fg :
            wk != null && Matches(wk.Source) ? wk : null;
        if (core is null || !Uri.TryCreate(core.Source, UriKind.Absolute, out var source) ||
            source.Scheme != "https" || source.Host != "chatgpt.com")
            throw new InvalidOperationException(
                "Otworz czat glowny projektu lub wybrana rozmowe w Open Browser Lite.");

        LiteEventLog.Write("native_chat_rename_requested", new {
            projectKey = project.Key, isLead = chat.Role == "lead", chatId,
            previousLength = chat.Title.Length, targetLength = newTitle.Length
        });
        try
        {
            string js = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "LiteChatGptNativeRename.js"));
            await core.ExecuteScriptAsync(js).WaitAsync(TimeSpan.FromSeconds(5));
            string? started = JsonSerializer.Deserialize<string>(
                await core.ExecuteScriptAsync(
                    "window.__OBL_NATIVE_RENAME__?.start(" +
                    JsonSerializer.Serialize(conversationId) + "," +
                    JsonSerializer.Serialize(chat.Title) + "," +
                    JsonSerializer.Serialize(newTitle) + ")")
                    .WaitAsync(TimeSpan.FromSeconds(5)));
            if (started != "started")
                throw new InvalidOperationException(
                    "Nie uruchomiono zmiany tytulu na serwerze: " + (started ?? "script_unavailable"));

            string result = "running";
            for (int i = 0; i < 64; i++)
            {
                await Task.Delay(750);
                result = JsonSerializer.Deserialize<string>(
                    await core.ExecuteScriptAsync(
                        "window.__OBL_NATIVE_RENAME__?.status()")
                        .WaitAsync(TimeSpan.FromSeconds(5))) ?? "script_lost";
                if (result != "running") break;
            }
            if (result is not ("renamed" or "already_named"))
            {
                LiteEventLog.Write("native_chat_rename_unconfirmed", new {
                    projectKey = project.Key, chatId, reason = result
                });
                throw new InvalidOperationException(
                    "Brak potwierdzenia zmiany tytulu ChatGPT: " + result);
            }

            // Only a verified server readback authorizes a local label update.
            LiteProjectRegistry.OpenDefault().RenameChatTitle(
                projectId, chatId, chat.Url, chat.Title, newTitle);
            LiteEventLog.Write("native_chat_rename_confirmed", new {
                projectKey = project.Key, chatId, serverReadback = true, result
            });
            return newTitle;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("native_chat_rename_failed", new {
                projectKey = project.Key, chatId,
                errorType = ex.GetType().Name, error = ex.Message
            });
            throw;
        }
    }
}
