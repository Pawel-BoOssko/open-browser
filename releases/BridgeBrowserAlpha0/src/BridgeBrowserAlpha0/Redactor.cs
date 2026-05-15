using System.Text.Json;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

public static partial class Redactor
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cookie", "set-cookie", "authorization", "proxy-authorization", "x-csrf-token", "csrf-token", "x-xsrf-token",
        "openai-sentinel-chat-requirements-token", "openai-sentinel-proof-token", "openai-sentinel-turnstile-token",
        "x-oai-is", "oai-session-id", "token", "session", "jwt", "secret", "verify"
    };

    public static object? RedactObject(object? value)
    {
        if (value == null) return null;
        if (value is string s) return RedactString(s);
        try
        {
            var json = JsonSerializer.Serialize(value);
            using var doc = JsonDocument.Parse(json);
            return RedactElement(doc.RootElement);
        }
        catch
        {
            return RedactString(value.ToString() ?? "");
        }
    }

    public static string RedactString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var s = RedactUrlQueryValues(input);
        s = BearerRegex().Replace(s, "$1[REDACTED]");
        s = JwtRegex().Replace(s, "[REDACTED_JWT]");
        s = JsonSensitiveStringRegex().Replace(s, "$1\"[REDACTED]\"");
        s = HeaderSensitiveRegex().Replace(s, "$1[REDACTED]");
        s = TokenLikeRegex().Replace(s, "$1[REDACTED]");
        return s;
    }

    private static object? RedactElement(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.Object => RedactObjectElement(e),
            JsonValueKind.Array => e.EnumerateArray().Select(RedactElement).ToArray(),
            JsonValueKind.String => RedactString(e.GetString() ?? ""),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => e.ToString()
        };
    }

    private static Dictionary<string, object?> RedactObjectElement(JsonElement e)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in e.EnumerateObject())
        {
            result[p.Name] = SensitiveNames.Contains(p.Name) || LooksSensitive(p.Name)
                ? "[REDACTED]"
                : RedactElement(p.Value);
        }
        return result;
    }

    private static bool LooksSensitive(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("token") || n.Contains("session") || n.Contains("jwt") || n.Contains("auth") ||
               n.Contains("secret") || n.Contains("cookie") || n.Contains("proof") || n.Contains("sentinel") ||
               n.Equals("verify") || n.Equals("k") || n.Equals("sid");
    }

    private static string RedactUrlQueryValues(string input)
    {
        return UrlRegex().Replace(input, m =>
        {
            var url = m.Value;
            var q = url.IndexOf('?');
            if (q < 0) return url;
            var hash = url.IndexOf('#', q + 1);
            var prefix = url[..(q + 1)];
            var query = hash >= 0 ? url[(q + 1)..hash] : url[(q + 1)..];
            var suffix = hash >= 0 ? url[hash..] : "";
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(part =>
            {
                var eq = part.IndexOf('=');
                if (eq < 0) return part;
                return part[..(eq + 1)] + "[REDACTED]";
            });
            return prefix + string.Join("&", parts) + suffix;
        });
    }

    [GeneratedRegex("https?://[^\\s\\\"'<>]+|wss?://[^\\s\\\"'<>]+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex("(?i)(bearer\\s+)[A-Za-z0-9._\\-+/=]+")]
    private static partial Regex BearerRegex();

    [GeneratedRegex("eyJ[A-Za-z0-9_\\-]+\\.[A-Za-z0-9_\\-]+\\.[A-Za-z0-9_\\-]+")]
    private static partial Regex JwtRegex();

    [GeneratedRegex("(?i)(\\\"[^\\\"]*(?:token|session|jwt|auth|secret|cookie|proof|sentinel|verify)[^\\\"]*\\\"\\s*:\\s*)\\\"[^\\\"]*\\\"")]
    private static partial Regex JsonSensitiveStringRegex();

    [GeneratedRegex("(?i)((?:OpenAI-Sentinel-[A-Za-z0-9-]+|X-OAI-IS|OAI-Session-Id|Authorization|Cookie|Set-Cookie|x-csrf-token)[\\\"']?\\s*[:=]\\s*[\\\"']?)[A-Za-z0-9._\\-+/=%]+")]
    private static partial Regex HeaderSensitiveRegex();

    [GeneratedRegex("(?i)((?:session|token|jwt|auth|csrf|proof|sentinel|verify)[A-Za-z0-9_\\-]*[=:]\\s*)[A-Za-z0-9._\\-+/=%]{8,}")]
    private static partial Regex TokenLikeRegex();
}
