using System.Text.Json;
using System.Text.Json.Serialization;

namespace BridgeBrowserAlpha0.OpenBridgeHost.ClaudeCode;

public static class ClaudeCodeExecutorOptionsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ClaudeCodeExecutorOptions? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ClaudeCodeExecutorOptions>(json, JsonOptions);
    }

    public static ClaudeCodeExecutorOptions LoadOrThrow(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ClaudeCodeExecutorOptions>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize executor options from: " + path);
    }
}
