using System.Text.Json;
using System.Text.Json.Serialization;

namespace BridgeBrowserAlpha0.OpenBridgeHost.Commands;

public static class CommandExecutorOptionsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    public static CommandExecutorOptions? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CommandExecutorOptions>(json, JsonOptions);
    }

    public static CommandExecutorOptions LoadOrThrow(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CommandExecutorOptions>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize executor options from: " + path);
    }
}
