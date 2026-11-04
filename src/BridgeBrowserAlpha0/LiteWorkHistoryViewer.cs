using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

/// <summary>
/// On-demand project history. Reads only the selected project's registered
/// registered chats and at most 12 recent archived response turns. Never touches DOM.
/// Project work documents are explicitly associated and read only when asked.
/// </summary>
internal sealed class LiteWorkHistoryViewer : Form
{
    private sealed record Answer(DateTimeOffset At, string Turn, string Status,
        string FilePath, int Characters, string ChatTitle)
    {
        public override string ToString() =>
            $"{At.ToLocalTime():MM-dd HH:mm}  {Status,-7}  {Characters,6} zn.  {ChatTitle[..Math.Min(ChatTitle.Length, 24)]}";
    }

    // Work files appear only after an explicit click. File selection stays
    // inside the currently selected project's registered documents.
    private sealed record WorkFile(string Path, DateTime Modified, long Bytes)
    {
        public override string ToString() =>
            $"{Modified:MM-dd HH:mm}  {Bytes,8} B  {System.IO.Path.GetFileName(Path)}";
    }
    private readonly string _projectId;
    private readonly ListBox _answers = new() { Dock = DockStyle.Fill };
    private readonly TextBox _searchQuery = new()
    {
        Width = 194, PlaceholderText = "Szukaj w zapisanych odpowiedziach"
    };
    private List<Answer> _allAnswers = new();
    private readonly TextBox _preview = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Both, WordWrap = true,
        Font = new Font("Consolas", 10f)
    };
    private readonly Label _summary = new()
    {
        Dock = DockStyle.Top, Height = 44, AutoEllipsis = true
    };
    private readonly string _archiveRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "responses");
    private LiteProjectRegistry.Project? _project;

    internal LiteWorkHistoryViewer(string projectId)
    {
        _projectId = projectId;
        Text = "Open Browser Lite: historia pracy projektu";
        Width = 1080;
        Height = 700;
        MinimumSize = new Size(650, 450);
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterDistance = 350, Panel1MinSize = 230, Panel2MinSize = 280
        };
        var searchBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false
        };
        var searchButton = new Button { Text = "Szukaj", Width = 75 };
        searchBar.Controls.AddRange(new Control[] { _searchQuery, searchButton });
        searchButton.Click += (_, _) => SearchHistory();
        _searchQuery.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SearchHistory();
        };
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(searchBar, 0, 0);
        left.Controls.Add(_answers, 0, 1);
        split.Panel1.Controls.Add(left);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 43, WrapContents = false, AutoScroll = true
        };
        var refresh = new Button { Text = "Odśwież historię", Width = 125 };
        var document = new Button { Text = "Dokument projektu", Width = 135 };
        var choose = new Button { Text = "Wskaż dokument", Width = 125 };
        var files = new Button { Text = "Pliki projektu", Width = 115 };
        var searchFiles = new Button { Text = "Szukaj pliki", Width = 103 };
        var archive = new Button { Text = "Archiwum", Width = 105 };
        refresh.Click += (_, _) => RefreshHistory();
        document.Click += (_, _) => OpenDocument();
        choose.Click += (_, _) => ChooseDocument();
        files.Click += (_, _) => ShowProjectFiles();
        archive.Click += (_, _) => ShowCheckpointArchive();
        searchFiles.Click += (_, _) => SearchProjectDocuments();
        toolbar.Controls.AddRange(new Control[] { refresh, document, files, archive, choose, searchFiles });
        split.Panel2.Controls.Add(_preview);
        split.Panel2.Controls.Add(toolbar);
        split.Panel2.Controls.Add(_summary);
        Controls.Add(split);
        _answers.SelectedIndexChanged += (_, _) => OpenAnswer();
        Load += (_, _) => RefreshHistory();
    }

    private static string ChatKey(string url) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))
            .ToLowerInvariant();

    private void RefreshHistory()
    {
        _answers.Items.Clear();
        _preview.Clear();
        _allAnswers.Clear();
        try
        {
            _project = LiteProjectRegistry.OpenDefault().Projects
                .FirstOrDefault(p => p.Id == _projectId);
            if (_project is null)
            {
                _summary.Text = "Projekt usunięty lub niedostępny.";
                return;
            }
            // Include every registered chat belonging to THIS project. Distinct
            // URLs avoid double-counting a lead saved under multiple bookmarks.
            var chats = _project.Chats
                .Where(c => !string.IsNullOrWhiteSpace(c.Url))
                .GroupBy(c => c.Url, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToArray();
            if (chats.Length == 0)
            {
                _summary.Text = $"{_project.Title}: brak przypisanych czatów.";
                return;
            }
            var byTurn = new Dictionary<string, Answer>(StringComparer.Ordinal);
            foreach (var chat in chats)
            {
                string chatKey = ChatKey(chat.Url);
                string directory = Path.Combine(_archiveRoot, chatKey);
                string index = Path.Combine(directory, "index.jsonl");
                if (!File.Exists(index)) continue;
                string chatLabel = (chat.Role == "lead" ? "Lead: " : "Archiwum: ") +
                    chat.Title;
                foreach (string line in File.ReadLines(index))
                {
                    try
                    {
                        using var json = JsonDocument.Parse(line);
                        var e = json.RootElement;
                        string name = e.GetProperty("fileName").GetString() ?? "";
                        string turn = e.GetProperty("turnKey").GetString() ?? "";
                        string status = e.GetProperty("status").GetString() ?? "";
                        string digest = e.GetProperty("sha256").GetString() ?? "";
                        if (!Regex.IsMatch(name, @"^[a-f0-9]{16}_[a-f0-9]{16}(_partial)?\.md$") ||
                            !Regex.IsMatch(turn, @"^[a-f0-9]{16}$") ||
                            !name.StartsWith(turn + "_", StringComparison.Ordinal) ||
                            !Regex.IsMatch(digest, @"^[a-f0-9]{64}$") ||
                            !string.Equals(name.Substring(17, 16), digest[..16],
                                StringComparison.Ordinal) ||
                            (status != "FINAL" && status != "PARTIAL") ||
                            !string.Equals(e.GetProperty("chatKey").GetString(), chatKey,
                                StringComparison.Ordinal))
                            continue;
                        DateTimeOffset at = DateTimeOffset.Parse(
                            e.GetProperty("utc").GetString() ?? "");
                        string path = Path.Combine(directory, name);
                        if (!File.Exists(path)) continue;
                        int chars = e.TryGetProperty("characters", out var c)
                            && c.TryGetInt32(out int value) ? value : 0;
                        var next = new Answer(at, turn, status, path, chars, chatLabel);
                        // Turn keys are not globally unique; scope them to the
                        // archived conversation hash before deduplication.
                        string key = chatKey + ":" + turn;
                        if (!byTurn.TryGetValue(key, out var prior) ||
                            (prior.Status != "FINAL" &&
                             (status == "FINAL" || at > prior.At)) ||
                            (status == "FINAL" && prior.Status == "FINAL" &&
                             at > prior.At))
                            byTurn[key] = next;
                    }
                    catch (Exception ex) when (ex is JsonException or
                        InvalidOperationException or FormatException or
                        KeyNotFoundException or ArgumentOutOfRangeException)
                    {
                        // Ignore malformed index rows; read-only historical view.
                    }
                }
            }
            _allAnswers = byTurn.Values.OrderByDescending(x => x.At).ToList();
            foreach (var entry in _allAnswers.Take(12))
                _answers.Items.Add(entry);
            _summary.Text = $"{_project.Title} | {chats.Length} czatów | {_answers.Items.Count} ostatnich odpowiedzi";
        }
        catch (Exception ex)
        {
            _summary.Text = "Błąd odczytu historii: " + ex.GetType().Name;
            LiteEventLog.Write("work_history_load_failed", new { errorType = ex.GetType().Name });
        }
    }

    // Search is requested explicitly and stays inside the selected project's
    // already indexed responses, including its archived chats. No DOM reads.
    private void SearchHistory()
    {
        string query = _searchQuery.Text.Trim();
        _answers.Items.Clear();
        _preview.Clear();
        if (query.Length == 0)
        {
            foreach (var item in _allAnswers.Take(12)) _answers.Items.Add(item);
            _summary.Text = "Ostatnie odpowiedzi: " + _answers.Items.Count;
            return;
        }
        if (query.Length < 2)
        {
            _summary.Text = "Wpisz co najmniej 2 znaki.";
            return;
        }
        int unreadable = 0;
        foreach (var item in _allAnswers)
        {
            try
            {
                var file = new FileInfo(item.FilePath);
                if (!file.Exists || file.Length > 1_048_576) continue;
                if (!File.ReadAllText(item.FilePath, Encoding.UTF8)
                    .Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                _answers.Items.Add(item);
                if (_answers.Items.Count == 12) break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                unreadable++;
            }
        }
        _summary.Text = "Wyniki wyszukiwania: " + _answers.Items.Count
            + " (maks. 12)" + (unreadable > 0 ? " | Nieodczytane: " + unreadable : "");
    }

    private void ShowTextFile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 1_048_576)
            throw new InvalidDataException("Brak pliku lub przekroczono limit 1 MiB");
        _preview.Text = File.ReadAllText(path, Encoding.UTF8);
        _summary.Text = Path.GetFileName(path) + " | " + file.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
    }

    // Search locally owned project documentation and plaintext checkpoint
    // archive only after an explicit click. Other projects see only their
    // owner-associated document; no unrelated directories are traversed.
    private void SearchProjectDocuments()
    {
        _answers.Items.Clear();
        _preview.Clear();
        string query = _searchQuery.Text.Trim();
        if (query.Length < 2)
        {
            _summary.Text = "Wpisz minimum 2 znaki w polu wyszukiwania.";
            return;
        }
        try
        {
            if (_project is null)
            {
                _summary.Text = "Nie znaleziono wybranego projektu.";
                return;
            }
            var candidates = new List<FileInfo>();
            string? assigned = WorkDocument();
            if (assigned is null)
            {
                _summary.Text = "Brak dokumentu projektu. Uzyj: Wskaz dokument.";
                return;
            }
            if (_project.Key == "obl" &&
                string.IsNullOrWhiteSpace(_project.WorkDocumentPath))
            {
                const string docs = @"D:\projects\open-browser\docs";
                const string cold = @"D:\OpenBrowserLiteArchive\checkpoints\obl";
                if (Directory.Exists(docs))
                    candidates.AddRange(new DirectoryInfo(docs)
                        .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                        .Where(f => f.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
                            || f.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)));
                if (Directory.Exists(cold))
                    candidates.AddRange(new DirectoryInfo(cold)
                        .EnumerateFiles("OBL_checkpoint_*.md", SearchOption.TopDirectoryOnly));
            }
            else
            {
                candidates.Add(new FileInfo(assigned));
            }
            int inspected = 0;
            int unreadable = 0;
            foreach (var file in candidates.OrderByDescending(f => f.LastWriteTimeUtc).Take(200))
            {
                if (!file.Exists || file.Length > 1_048_576) continue;
                inspected++;
                try
                {
                    if (!File.ReadAllText(file.FullName, Encoding.UTF8)
                        .Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                    _answers.Items.Add(new WorkFile(file.FullName, file.LastWriteTime, file.Length));
                    if (_answers.Items.Count == 20) break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable++;
                }
            }
            _summary.Text = "Dokumenty: " + _answers.Items.Count
                + " wynikow (maks. 20), przeszukano: " + inspected
                + (unreadable > 0 ? ", bledy odczytu: " + unreadable : "");
        }
        catch (Exception ex)
        {
            _summary.Text = "Blad wyszukiwania dokumentow: " + ex.GetType().Name
                + ": " + ex.Message;
            LiteEventLog.Write("project_document_search_failed",
                new { errorType = ex.GetType().Name, message = ex.Message });
        }
    }

    // Local on-demand file list. OBL has a canonical docs folder; other
    // projects expose ONLY the file the owner explicitly associated.
    // No directory crawling across unrelated projects, no DOM interaction.
    private void ShowProjectFiles()
    {
        _answers.Items.Clear();
        _preview.Clear();
        try
        {
            string? document = WorkDocument();
            if (document is null)
            {
                _summary.Text = "Brak przypisanego dokumentu. Użyj: Wskaż dokument.";
                return;
            }
            if (_project?.Key == "obl" &&
                string.IsNullOrWhiteSpace(_project.WorkDocumentPath))
            {
                string? folder = System.IO.Path.GetDirectoryName(document);
                if (folder is null || !Directory.Exists(folder))
                {
                    _summary.Text = "Katalog dokumentów projektu niedostępny.";
                    return;
                }
                foreach (var file in new DirectoryInfo(folder)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                                f.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(20))
                    _answers.Items.Add(new WorkFile(
                        file.FullName, file.LastWriteTime, file.Length));
            }
            else
            {
                var file = new FileInfo(document);
                if (file.Exists)
                    _answers.Items.Add(new WorkFile(
                        file.FullName, file.LastWriteTime, file.Length));
            }
            _summary.Text = $"{_project?.Title}: {_answers.Items.Count} plików projektu (na żądanie)";
        }
        catch (Exception ex)
        {
            _summary.Text = "Błąd listy plików: " + ex.GetType().Name;
            LiteEventLog.Write("project_work_files_load_failed",
                new { errorType = ex.GetType().Name });
        }
    }
    // The OBL checkpoint retention archive is outside the active docs folder.
    // Never enumerate it for unrelated projects or load it into browser DOM.
    private void ShowCheckpointArchive()
    {
        _answers.Items.Clear();
        _preview.Clear();
        if (_project?.Key != "obl")
        {
            _summary.Text = "Archiwum checkpointow dostepne tylko dla OBL.";
            return;
        }
        try
        {
            const string folder = @"D:\OpenBrowserLiteArchive\checkpoints\obl";
            if (!Directory.Exists(folder))
            {
                _summary.Text = "Brak katalogu archiwum checkpointow.";
                return;
            }
            foreach (var file in new DirectoryInfo(folder)
                .EnumerateFiles("OBL_checkpoint_*.md", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(20))
                _answers.Items.Add(new WorkFile(
                    file.FullName, file.LastWriteTime, file.Length));
            _summary.Text = $"Archiwum OBL: {_answers.Items.Count} checkpointow (na zadanie)";
        }
        catch (Exception ex)
        {
            _summary.Text = "Blad archiwum checkpointow: " + ex.GetType().Name;
            LiteEventLog.Write("checkpoint_archive_load_failed",
                new { errorType = ex.GetType().Name });
        }
    }
    private void OpenAnswer()
    {
        if (_answers.SelectedItem is WorkFile file)
        {
            try { ShowTextFile(file.Path); }
            catch (Exception ex)
            {
                _preview.Clear();
                _summary.Text = "Nie można odczytać pliku projektu: " + ex.GetType().Name;
            }
            return;
        }        if (_answers.SelectedItem is not Answer answer) return;
        try { ShowTextFile(answer.FilePath); }
        catch (Exception ex)
        {
            _preview.Clear();
            _summary.Text = "Nie można odczytać odpowiedzi: " + ex.GetType().Name;
        }
    }

    private string? WorkDocument()
    {
        if (_project is null) return null;
        if (!string.IsNullOrWhiteSpace(_project.WorkDocumentPath))
            return _project.WorkDocumentPath;
        // Only OBL has a known canonical checkpoint location. Never mix
        // another project's documents into this preview by guessing a path.
        return _project.Key == "obl"
            ? @"D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md"
            : null;
    }

    private void OpenDocument()
    {
        try
        {
            string? path = WorkDocument();
            if (path is null)
            {
                _summary.Text = "Brak dokumentu projektu. Użyj: Wskaż dokument.";
                return;
            }
            ShowTextFile(path);
        }
        catch (Exception ex)
        {
            _preview.Clear();
            _summary.Text = "Błąd dokumentu projektu: " + ex.GetType().Name;
        }
    }

    private void ChooseDocument()
    {
        if (_project is null) return;
        using var dialog = new OpenFileDialog
        {
            Title = "Wskaż dokument bieżącego projektu",
            Filter = "Dokument Markdown/TXT|*.md;*.txt",
            CheckFileExists = true, Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            LiteProjectRegistry.OpenDefault()
                .SetWorkDocumentPath(_project.Id, dialog.FileName);
            _project = LiteProjectRegistry.OpenDefault().Projects
                .FirstOrDefault(p => p.Id == _projectId);
            OpenDocument();
        }
        catch (Exception ex)
        {
            _summary.Text = "Nie można przypisać dokumentu: " + ex.GetType().Name;
            LiteEventLog.Write("work_document_assign_failed",
                new { errorType = ex.GetType().Name });
        }
    }
}