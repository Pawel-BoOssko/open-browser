using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

public static class Humanizer
{
    private static string[] _slot1 = Array.Empty<string>();
    private static string[] _slot2 = Array.Empty<string>();
    private static string[] _slot3 = Array.Empty<string>();
    private static string[] _slot4 = Array.Empty<string>();
    private static string? _lastPrefix;
    private static bool _loaded;

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var paths = new[]
        {
            // Local override (git-ignored, for custom language)
            Path.Combine(baseDir, "config", "local", "humanizer.json"),
            // Shipped with the app (copied to output by build)
            Path.Combine(baseDir, "config", "humanizer.json"),
            // Project root for dotnet run
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "config", "humanizer.json")),
        };

        foreach (var path in paths)
        {
            try
            {
                var resolved = Path.GetFullPath(path);
                if (!File.Exists(resolved)) continue;

                using var doc = JsonDocument.Parse(File.ReadAllText(resolved, System.Text.Encoding.UTF8));
                var root = doc.RootElement;

                _slot1 = ReadArray(root, "slot1");
                _slot2 = ReadArray(root, "slot2");
                _slot3 = ReadArray(root, "slot3");
                _slot4 = ReadArray(root, "slot4");

                if (_slot1.Length > 0 && _slot2.Length > 0 && _slot3.Length > 0 && _slot4.Length > 0)
                    return;
            }
            catch { }
        }
    }

    private static string[] ReadArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var list = new System.Collections.Generic.List<string>();
        foreach (var item in prop.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String)
                list.Add(item.GetString() ?? "");
        return list.ToArray();
    }

    public static string Wrap(string content)
    {
        Load();
        if (_slot1.Length == 0)
            return content; // no humanizer config — pass through unchanged

        string prefix;
        do
        {
            var s1 = _slot1[RandomIndex(_slot1.Length)];
            var s2 = _slot2[RandomIndex(_slot2.Length)];
            var s3 = _slot3[RandomIndex(_slot3.Length)];
            var s4 = _slot4[RandomIndex(_slot4.Length)];
            prefix = $"{s1} {s2}, {s3} {s4}:";
        } while (prefix == _lastPrefix);

        _lastPrefix = prefix;
        return $"{prefix}\n\n{content}";
    }

    private static int RandomIndex(int max)
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        return (int)(BitConverter.ToUInt32(bytes) & 0x7FFFFFFF) % max;
    }
}
