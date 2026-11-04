namespace BridgeBrowserAlpha0;

/// <summary>
/// Removes only an obsolete local project bookmark after the owner has already
/// deleted or detached that conversation in ChatGPT. No ChatGPT API calls.
/// </summary>
internal static class LiteProjectChatLocalDetach
{
    internal static void Remove(
        LiteProjectRegistry registry, string projectId, string chatId, string expectedUrl)
    {
        var project = registry.Projects.SingleOrDefault(p => p.Id == projectId)
            ?? throw new InvalidDataException("Projekt nie istnieje.");
        var chat = project.Chats.SingleOrDefault(c => c.Id == chatId)
            ?? throw new InvalidDataException("Rozmowa nie istnieje w projekcie.");
        if (chat.Role == "lead")
            throw new InvalidOperationException("Nie wolno odlaczyc aktywnego leada.");
        if (chat.Url != expectedUrl)
            throw new InvalidOperationException("Adres rozmowy zostal zmieniony.");
        var currentLead = project.Chats.SingleOrDefault(c => c.Role == "lead")
            ?? throw new InvalidDataException("Brak jednego aktywnego leada.");
        if (string.IsNullOrWhiteSpace(currentLead.Url))
            throw new InvalidDataException("Lead nie ma adresu.");
        project.Chats.Remove(chat);
        registry.Save();  // Preserve canonical project wake and retired wake history.
    }
}
