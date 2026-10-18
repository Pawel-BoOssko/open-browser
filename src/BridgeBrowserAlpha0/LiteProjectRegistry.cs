using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Local user-managed project/chat bookmarks. Does not store conversation text.
/// Project count is unbounded; separate chat identities can share a project.
/// </summary>
internal sealed class LiteProjectRegistry
{
    internal sealed class Chat
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Role { get; set; } = "worker";
        public DateTimeOffset AddedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    internal sealed class Project
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";
        public bool Active { get; set; } = true;
        public List<Chat> Chats { get; set; } = new();
    }

    private sealed class Store
    {
        public int Version { get; set; } = 1;
        public List<Project> Projects { get; set; } = new();
    }

    private static readonly string[] StarterProjects =
    {
        "Open Browser Lite", "Lupus Commander", "OpenFill",
        "Networking Radar", "Transkrypcja AI",
        "Lupus Commander MultiHost", "CourtControl"
    };

    private readonly string _file;
    private readonly Store _store;

    internal IReadOnlyList<Project> Projects => _store.Projects;

    private LiteProjectRegistry(string file, Store store)
    {
        _file = file;
        _store = store;
    }

    internal static LiteProjectRegistry OpenDefault()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite");
        return Open(Path.Combine(folder, "projects.json"));
    }

    internal static LiteProjectRegistry Open(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                var value = JsonSerializer.Deserialize<Store>(File.ReadAllText(path));
                if (value?.Version == 1 && value.Projects != null &&
                    value.Projects.All(p => p != null && p.Id.Length > 0 && p.Chats != null))
                    return new LiteProjectRegistry(path, value);
                throw new InvalidDataException("Unsupported project registry schema");
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("projects_load_failed", new { errorType = ex.GetType().Name });
                // Never overwrite a corrupt file, so recovery is possible.
                throw;
            }
        }
        var initial = new Store();
        foreach (string title in StarterProjects)
            initial.Projects.Add(new Project { Title = title });
        var registry = new LiteProjectRegistry(path, initial);
        registry.Save();
        return registry;
    }

    internal Project AddProject(string title)
    {
        title = (title ?? "").Trim();
        if (title.Length is < 1 or > 120)
            throw new ArgumentException("Project title must contain 1-120 characters");
        var item = new Project { Title = title };
        _store.Projects.Add(item);
        Save();
        return item;
    }

    internal Chat AddChat(string projectId, string title, string url)
    {
        if (!_store.Projects.Any(p => p.Id == projectId))
            throw new ArgumentException("Unknown project");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase) ||
            !(uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
              uri.AbsolutePath.StartsWith("/g/", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Expected ChatGPT conversation URL");
        var project = _store.Projects.First(p => p.Id == projectId);
        if (project.Chats.Any(c => c.Url == uri.GetLeftPart(UriPartial.Path)))
            throw new InvalidOperationException("Conversation already assigned to this project");
        var chat = new Chat
        {
            Url = uri.GetLeftPart(UriPartial.Path),
            Title = string.IsNullOrWhiteSpace(title) ? "Czat " + (project.Chats.Count + 1) : title.Trim()
        };
        project.Chats.Add(chat);
        Save();
        return chat;
    }

    internal void Save()
    {
        string? folder = Path.GetDirectoryName(_file);
        if (folder is null) throw new InvalidOperationException("Project path has no parent");
        Directory.CreateDirectory(folder);
        string staging = _file + ".tmp";
        string json = JsonSerializer.Serialize(_store,
            new JsonSerializerOptions { WriteIndented = true });
        try
        {
            File.WriteAllText(staging, json, new UTF8Encoding(false));
            File.Move(staging, _file, true);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }
}
