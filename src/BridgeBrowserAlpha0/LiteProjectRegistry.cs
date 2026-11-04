using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        // Only lead is permitted to execute. All other chats are archives.
        public string Role { get; set; } = "archive";
        public DateTimeOffset AddedUtc { get; set; } = DateTimeOffset.UtcNow;
        // Non-null only after ChatGPT returned explicit archive readback.
        public DateTimeOffset? NativeArchivedUtc { get; set; }
        // PATCH was accepted, but readback timed out. Verify via GET only.
        public DateTimeOffset? NativeArchiveVerifyPendingUtc { get; set; }
        // Both filtered server lists independently confirmed archive membership.
        public DateTimeOffset? NativeArchiveVisibilityVerifiedUtc { get; set; }
        // Separate owner-accepted outcome after archived=true and normal-list HTTP429.
        // This is NOT a full-list absence verification.
        public DateTimeOffset? NativeArchiveOwnerAccepted429Utc { get; set; }
    }

    internal sealed class Project
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";
        public string Key { get; set; } = ""; // stable exact CLI project selector
        public bool Active { get; set; } = true;
        public string? WorkDocumentPath { get; set; }
        public List<Chat> Chats { get; set; } = new();
        // Project-owned wake; never embeds a chat URL: the current lead is its recipient.
        // Persistent cooldown only for read-only native archive list auditing.
        public DateTimeOffset? NativeVisibilityNextAttemptUtc { get; set; }
        public JsonObject? Wake { get; set; }
    }

    private sealed class Store
    {
        public int Version { get; set; } = 1;
        public List<Project> Projects { get; set; } = new();
        public bool WakeMigrated { get; set; }
        public List<LiteChatWakeRegistry.Chat> RetiredWakes { get; set; } = new();
    }

    private static readonly string[] StarterProjects =
    {
        "Open Browser Lite", "Lupus Commander", "OpenFill",
        "Networking Radar", "Transkrypcja AI",
        "Lupus Commander MultiHost", "CourtControl"
    };

    private readonly string _file;
    private Store _store;

    internal IReadOnlyList<Project> Projects => _store.Projects;
    internal bool WakeMigrated => _store.WakeMigrated;
    internal IReadOnlyList<LiteChatWakeRegistry.Chat> RetiredWakes => _store.RetiredWakes;

    private LiteProjectRegistry(string file, Store store)
    {
        _file = file;
        _store = store;
    }

    internal static string DefaultProjectKey(string title, string id) =>
        title switch {
            "Open Browser Lite" => "obl",
            "Lupus Commander" => "lc",
            "OpenFill" => "of",
            "Networking Radar" => "nr",
            "Transkrypcja AI" => "ta",
            "Lupus Commander MultiHost" => "lcm",
            "CourtControl" => "cc",
            _ => "p" + id[..Math.Min(8,id.Length)].ToLowerInvariant()
        };

    private void EnsureProjectKeys()
    {
        bool changed = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in _store.Projects)
        {
            if (string.IsNullOrWhiteSpace(p.Key))
            {
                p.Key = DefaultProjectKey(p.Title,p.Id);
                changed = true;
            }
            if (p.Key.Length is < 2 or > 16 ||
                p.Key.Any(c => c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9')) ||
                !seen.Add(p.Key))
                throw new InvalidDataException("Duplicate or invalid stable project key");
        }
        if (changed) Save();
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
                { var loaded = new LiteProjectRegistry(path,value); loaded.EnsureProjectKeys(); return loaded; }
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
            initial.Projects.Add(new Project { Title = title, Key = DefaultProjectKey(title, Guid.NewGuid().ToString("N")) });
        var registry = new LiteProjectRegistry(path, initial);
        registry.EnsureProjectKeys();
        registry.Save();
        return registry;
    }

    // Refresh cached project panels before a mutation; otherwise an old UI
    // snapshot could erase a promoted lead or a newly migrated wake.
    private void ReloadBeforeMutation()
    {
        if (!File.Exists(_file)) return;
        var fresh = JsonSerializer.Deserialize<Store>(File.ReadAllText(_file));
        if (fresh?.Version != 1 || fresh.Projects is null)
            throw new InvalidDataException("Project registry changed or invalid");
        _store = fresh;
    }
    // Optional project-specific working document. No browser navigation or
    // cross-project association; modifications reload the latest registry.
    internal void SetWorkDocumentPath(string projectId, string file)
    {
        ReloadBeforeMutation();
        var project = _store.Projects.FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Project no longer exists");
        string path = Path.GetFullPath(file);
        if (!File.Exists(path) ||
            !(path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
              path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Select an existing Markdown or TXT document");
        project.WorkDocumentPath = path;
        Save();
    }
    // Rename only the visible project title. Its stable CLI key, project ID,
    // lead, archive history and project-owned wake must remain unchanged.
    internal void RenameProject(string projectId, string title)
    {
        ReloadBeforeMutation();
        var project = _store.Projects.FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Project no longer exists");
        title = (title ?? "").Trim();
        if (title.Length is < 1 or > 120)
            throw new ArgumentException("Project title must contain 1-120 characters");
        if (project.Title == title) return;
        project.Title = title;
        Save();
    }

    // Synchronize a bookmark only after native ChatGPT GET confirmed the title.
    internal void RenameChatTitle(string projectId, string chatId,
        string expectedUrl, string expectedTitle, string confirmedTitle)
    {
        ReloadBeforeMutation();
        var project = _store.Projects.SingleOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Projekt juz nie istnieje");
        var chat = project.Chats.SingleOrDefault(c => c.Id == chatId)
            ?? throw new InvalidOperationException("Rozmowa zostala przeniesiona lub usunieta");
        if (chat.Url != expectedUrl || chat.Title != expectedTitle)
            throw new InvalidOperationException("Dane rozmowy zmienily sie w trakcie operacji");
        confirmedTitle = (confirmedTitle ?? "").Trim();
        if (confirmedTitle.Length is < 1 or > 120)
            throw new ArgumentException("Nieprawidlowy potwierdzony tytul");
        if (chat.Title == confirmedTitle) return;
        chat.Title = confirmedTitle;
        Save();
    }
    internal Project AddProject(string title)
    {
        ReloadBeforeMutation();
        title = (title ?? "").Trim();
        if (title.Length is < 1 or > 120)
            throw new ArgumentException("Project title must contain 1-120 characters");
        var item = new Project { Title = title };
        item.Key = DefaultProjectKey(title,item.Id);
        if (_store.Projects.Any(p => p.Key == item.Key)) throw new InvalidOperationException("Project key collision");
        _store.Projects.Add(item);
        Save();
        return item;
    }

    // One-step onboarding. The open chat becomes the project's single lead
    // when it is a valid, still-unassigned conversation. Otherwise create an
    // empty project, without moving a chat from a different project.
    // Crucially, both objects are saved in ONE registry transaction.
    internal Project AddProjectWithCurrentChat(
        string title, string? chatTitle, string? currentUrl,
        bool requireCurrentChat = false)
    {
        ReloadBeforeMutation();
        title = (title ?? "").Trim();
        // A project can be onboarded in one click from an open, unassigned
        // conversation. Do not silently name an empty or already-owned chat.
        Uri? newLeadUri = null;
        if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var candidate) &&
            candidate.Scheme == Uri.UriSchemeHttps &&
            string.Equals(candidate.Host, "chatgpt.com",
                StringComparison.OrdinalIgnoreCase) &&
            (candidate.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
             candidate.AbsolutePath.StartsWith("/g/", StringComparison.OrdinalIgnoreCase)) &&
            !_store.Projects.Any(p => p.Chats.Any(c =>
                string.Equals(c.Url, candidate.GetLeftPart(UriPartial.Path),
                    StringComparison.OrdinalIgnoreCase))))
            newLeadUri = candidate;
        if (requireCurrentChat && newLeadUri is null)
            throw new InvalidOperationException(
                "Otwarty czat jest nieprawidlowy lub przypisany do innego projektu.");
        if (title.Length == 0 && newLeadUri is not null)
        {
            title = (chatTitle ?? "").Trim();
            const string suffix = " | ChatGPT";
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                title = title[..^suffix.Length].Trim();
            if (title.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase))
                title = "";
            if (title.Length == 0)
            {
                string chatId = newLeadUri.Segments.Last().Trim('/');
                title = "Czat " + chatId[..Math.Min(chatId.Length, 8)];
            }
            if (title.Length > 120) title = title[..120];
        }
        if (title.Length is < 1 or > 120)
            throw new ArgumentException("Podaj nazwę lub otwórz nieprzypisany czat ChatGPT");

        var item = new Project { Title = title };
        item.Key = DefaultProjectKey(title, item.Id);
        if (_store.Projects.Any(p => p.Key == item.Key))
            throw new InvalidOperationException("Project key collision");

        if (newLeadUri is not null)
        {
            string normalizedTitle = string.IsNullOrWhiteSpace(chatTitle)
                ? title : chatTitle.Trim();
            if (normalizedTitle.Length > 100)
                normalizedTitle = normalizedTitle[..100];
            item.Chats.Add(new Chat {
                Title = normalizedTitle,
                Url = newLeadUri.GetLeftPart(UriPartial.Path),
                Role = "lead"
            });
        }
        _store.Projects.Add(item);
        Save();
        return item;
    }

    internal Chat AddChat(string projectId, string title, string url)
    {
        ReloadBeforeMutation();
        if (!_store.Projects.Any(p => p.Id == projectId))
            throw new ArgumentException("Unknown project");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase) ||
            !(uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
              uri.AbsolutePath.StartsWith("/g/", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Expected ChatGPT conversation URL");
        var project = _store.Projects.First(p => p.Id == projectId);
        if (_store.Projects.Any(p => p.Chats.Any(c =>
            c.Url == uri.GetLeftPart(UriPartial.Path))))
            throw new InvalidOperationException(
                "Conversation already assigned to a project");
        var chat = new Chat
        {
            Url = uri.GetLeftPart(UriPartial.Path),
            Title = string.IsNullOrWhiteSpace(title) ? "Czat " + (project.Chats.Count + 1) : title.Trim()
        };
        project.Chats.Add(chat);
        Save();
        return chat;
    }

    // Persist positive ChatGPT archive evidence without overwriting
    // concurrently changed lead and wake state.
    internal bool MarkNativeArchiveVerifyPending(string projectId, string chatId)
    {
        ReloadBeforeMutation();
        var chat = _store.Projects.FirstOrDefault(p => p.Id == projectId)?
            .Chats.FirstOrDefault(c => c.Id == chatId);
        if (chat is null || chat.Role == "lead" || chat.NativeArchivedUtc.HasValue)
            return false;
        if (chat.NativeArchiveVerifyPendingUtc.HasValue) return true;
        chat.NativeArchiveVerifyPendingUtc = DateTimeOffset.UtcNow;
        Save();
        return true;
    }

    // A verified GET returning is_archived=false clears a previously
    // uncertain PATCH, so a later wake may safely try it again.
    internal bool ClearNativeArchiveVerifyPending(string projectId, string chatId)
    {
        ReloadBeforeMutation();
        var chat = _store.Projects.FirstOrDefault(p => p.Id == projectId)?
            .Chats.FirstOrDefault(c => c.Id == chatId);
        if (chat is null || chat.Role == "lead" || chat.NativeArchivedUtc.HasValue)
            return false;
        if (!chat.NativeArchiveVerifyPendingUtc.HasValue) return true;
        chat.NativeArchiveVerifyPendingUtc = null;
        Save();
        return true;
    }

    internal bool SetNativeVisibilityCooldown(string projectId, DateTimeOffset nextAttemptUtc)
    {
        ReloadBeforeMutation();
        var project = _store.Projects.FirstOrDefault(p => p.Id == projectId && p.Active);
        if (project is null) return false;
        if (project.NativeVisibilityNextAttemptUtc >= nextAttemptUtc) return true;
        project.NativeVisibilityNextAttemptUtc = nextAttemptUtc;
        Save();
        return true;
    }

    internal bool MarkNativeArchiveVisibilityVerified(string projectId, string chatId)
    {
        ReloadBeforeMutation();
        var chat = _store.Projects.FirstOrDefault(p => p.Id == projectId)?
            .Chats.FirstOrDefault(c => c.Id == chatId);
        if (chat is null || chat.Role == "lead" || !chat.NativeArchivedUtc.HasValue)
            return false;
        if (chat.NativeArchiveVisibilityVerifiedUtc.HasValue) return true;
        chat.NativeArchiveVisibilityVerifiedUtc = DateTimeOffset.UtcNow;
        Save();
        return true;
    }

    // Single registry save for an owner-accepted HTTP429 archive batch.
    // Never touches current lead or the two most recent preceding chats.
    internal int MarkNativeArchiveOwnerAccepted429Batch(string projectId, IEnumerable<string> chatIds)
    {
        ReloadBeforeMutation();
        var project = _store.Projects.FirstOrDefault(p => p.Id == projectId && p.Active);
        if (project is null) return 0;
        var requested = chatIds.ToHashSet(StringComparer.Ordinal);
        var protectedIds = project.Chats.Where(c => c.Role != "lead")
            .OrderByDescending(c => c.AddedUtc)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .Take(2).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        int count = 0;
        foreach (var chat in project.Chats)
        {
            if (!requested.Contains(chat.Id) || protectedIds.Contains(chat.Id) ||
                chat.Role == "lead" || !chat.NativeArchivedUtc.HasValue ||
                chat.NativeArchiveVisibilityVerifiedUtc.HasValue ||
                chat.NativeArchiveOwnerAccepted429Utc.HasValue)
                continue;
            chat.NativeArchiveOwnerAccepted429Utc = now;
            count++;
        }
        if (count > 0) Save();
        return count;
    }
    internal bool MarkNativeArchived(string projectId, string chatId)
    {
        ReloadBeforeMutation();
        var project = _store.Projects.FirstOrDefault(p => p.Id == projectId);
        var chat = project?.Chats.FirstOrDefault(c => c.Id == chatId);
        if (chat is null || chat.Role == "lead") return false;
        if (chat.NativeArchivedUtc.HasValue) return true;
        chat.NativeArchivedUtc = DateTimeOffset.UtcNow;
        chat.NativeArchiveVerifyPendingUtc = null;
        Save();
        return true;
    }

    // Owner-confirmed change of the active executor. The project's wake is
    // paused in the SAME atomic projects.json save as the lead role change.
    // All wake sequence, slot and receipt history remain with the project.
    internal void PromoteLeadPaused(string projectId, string chatId)
    {
        ReloadBeforeMutation();
        var project = _store.Projects.SingleOrDefault(p => p.Id == projectId)
            ?? throw new ArgumentException("Unknown project");
        var chosen = project.Chats.SingleOrDefault(c => c.Id == chatId)
            ?? throw new ArgumentException("Unknown chat");
        var existing = project.Chats.SingleOrDefault(c => c.Role == "lead");
        if (existing?.Id == chosen.Id) return;
        if (project.Wake is not null)
        {
            project.Wake["Enabled"] = false;
            project.Wake["LastState"] = "paused_manual_project_lead_change";
        }
        foreach (var chat in project.Chats)
            chat.Role = chat.Id == chosen.Id ? "lead" : "archive";
        Save();
    }

    internal void PromoteLead(string projectId, string chatId)
    {
        var displayed = _store.Projects.FirstOrDefault(p => p.Id == projectId)?.Chats.ToArray() ?? Array.Empty<Chat>();
        ReloadBeforeMutation();
        var project = _store.Projects.FirstOrDefault(p => p.Id == projectId)
            ?? throw new ArgumentException("Unknown project");
        if (!project.Chats.Any(c => c.Id == chatId))
            throw new ArgumentException("Unknown chat");
        foreach (var chat in project.Chats)
            chat.Role = chat.Id == chatId ? "lead" : "archive";
        foreach (var chat in displayed)
            chat.Role = chat.Id == chatId ? "lead" : "archive";
        Save();
    }

    /// <summary>Fail closed on multiple active executors of one project.</summary>
    internal static bool CanScheduleProjectLeads(
        IReadOnlyList<Project> projects, IReadOnlyList<LiteChatWakeRegistry.Chat> wakes)
    {
        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            var leads = project.Chats.Where(c => c.Role == "lead").ToList();
            if (leads.Count > 1) return false;
            foreach (var chat in project.Chats)
            {
                if (mapped.TryGetValue(chat.Url, out var owner) && owner != project.Id)
                    return false;
                mapped[chat.Url] = project.Id;
            }
            if (wakes.Any(w => w.Enabled && project.Chats.Any(c =>
                c.Url == w.Url && (leads.Count != 1 || leads[0].Url != c.Url))))
                return false;
        }
        return true;
    }

    internal static int WakePolicySelfTest()
    {
        const string a = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
        const string b = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
        const string c = "https://chatgpt.com/c/33333333-3333-3333-3333-333333333333";
        var p1 = new Project { Chats = new List<Chat> {
            new() { Url=a, Role="lead" }, new() { Url=b, Role="archive" }
        }};
        var p2 = new Project { Chats = new List<Chat> {
            new() { Url=c, Role="lead" }
        }};
        var w = new List<LiteChatWakeRegistry.Chat> {
            new(){ Url=a, Enabled=true }, new(){ Url=c, Enabled=true }
        };
        if (!CanScheduleProjectLeads(new[]{p1,p2},w)) return 1;
        w.Add(new LiteChatWakeRegistry.Chat { Url=b, Enabled=true });
        if (CanScheduleProjectLeads(new[]{p1,p2},w)) return 2;
        w.RemoveAt(2);
        p1.Chats[0].Role="archive";
        if (CanScheduleProjectLeads(new[]{p1,p2},w)) return 3;
        p1.Chats[0].Role="lead";
        p1.Chats[1].Role="lead";
        if (CanScheduleProjectLeads(new[]{p1,p2},w)) return 4;
        p1.Chats[1].Role="archive";
        p2.Chats[0].Url=a;
        if (CanScheduleProjectLeads(new[]{p1,p2},w)) return 5;
        return 0;
    }

    internal void ReplaceProjectWakes(
        Dictionary<string, JsonObject> schedules,
        List<LiteChatWakeRegistry.Chat> retired)
    {
        foreach (var project in _store.Projects)
            project.Wake = schedules.TryGetValue(project.Id, out var wake)
                ? (JsonObject)wake.DeepClone() : null;
        _store.RetiredWakes = retired;
        _store.WakeMigrated = true;
        Save(preserveWake: false);
    }

    internal void Save(bool preserveWake = true)
    {
        string? folder = Path.GetDirectoryName(_file);
        if (folder is null) throw new InvalidOperationException("Project path has no parent");
        Directory.CreateDirectory(folder);
        // UI may have cached this registry before a new wake. Never overwrite
        // its canonical project schedule from a stale projects-panel save.
        if (preserveWake && File.Exists(_file))
        {
            var disk = JsonSerializer.Deserialize<Store>(
                File.ReadAllText(_file));
            if (disk?.WakeMigrated == true)
            {
                _store.WakeMigrated = true;
                _store.RetiredWakes = disk.RetiredWakes;
                foreach (var project in _store.Projects)
                {
                    var previous = disk.Projects.FirstOrDefault(
                        x => x.Id == project.Id);
                    if (previous is not null)
                        project.Wake = previous.Wake is null
                            ? null : (JsonObject)previous.Wake.DeepClone();
                }
            }
        }
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
