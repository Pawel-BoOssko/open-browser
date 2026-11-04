namespace BridgeBrowserAlpha0;

// A local-chatgpt%3A route is an optimistic client-side placeholder.
// Even if the seed bubble was visible, it is NOT a durable successor ID.
internal static class LiteHandoffPersistedRoute
{
    internal static bool IsServerConversation(string? url)
    {
        if (!LiteDevWakeState.IsChatUrl(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        string path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (!path.StartsWith("/c/", StringComparison.Ordinal)) return false;
        string id = path[3..];
        return Guid.TryParseExact(id, "D", out _);
    }
}