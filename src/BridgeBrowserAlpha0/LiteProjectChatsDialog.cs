namespace BridgeBrowserAlpha0;

/// <summary>
/// Read-only conversation navigation for a selected project.
/// Opening an archived chat never changes its role or wake schedule.
/// </summary>
internal sealed class LiteProjectChatsDialog : Form
{
    private sealed record ChatRow(string Id, string Title, string Url,
        bool IsLead, string NativeState)
    {
        public override string ToString() => "[" + NativeState + "] " + Title;
    }
    private readonly string _projectId;
    private readonly Func<string, Task> _navigate;
    private readonly Func<string, string, string, Task<string>> _nativeRename;
    private readonly Func<string, string, Task<string>> _nativeReadTitle;
    private readonly Label _heading = new()
    {
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 11f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly ListBox _chats = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false
    };
    private readonly Label _status = new() { Dock = DockStyle.Fill };
    private readonly Button _open = new()
    {
        Text = "Otworz wybrany", Width = 150, Height = 38
    };
    private readonly Button _rename = new()
    {
        Text = "Tytul w ChatGPT", Width = 155, Height = 38
    };
    private readonly Button _readTitle = new()
    {
        Text = "Odczytaj tytul", Width = 140, Height = 38
    };
    private readonly Button _refresh = new()
    {
        Text = "Odswiez", Width = 105, Height = 38
    };
    private readonly Button _close = new()
    {
        Text = "Zamknij", Width = 110, Height = 38
    };

    internal LiteProjectChatsDialog(string projectId, Func<string, Task> navigate,
        Func<string, string, string, Task<string>> nativeRename,
        Func<string, string, Task<string>> nativeReadTitle)
    {
        _projectId = projectId;
        _navigate = navigate;
        _nativeRename = nativeRename;
        _nativeReadTitle = nativeReadTitle;
        Text = "Open Browser Lite | Rozmowy projektu";
        Width = 780;
        Height = 580;
        MinimumSize = new Size(560, 410);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(20, 16, 20, 16)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.Controls.Add(_heading, 0, 0);
        layout.Controls.Add(_chats, 0, 1);
        layout.Controls.Add(_status, 0, 2);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        buttons.Controls.Add(_close);
        buttons.Controls.Add(_open);
        buttons.Controls.Add(_rename);
        buttons.Controls.Add(_readTitle);
        buttons.Controls.Add(_refresh);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        _open.Click += async (_, _) => await OpenSelectedAsync();
        _rename.Click += async (_, _) => await RenameSelectedAsync();
        _readTitle.Click += async (_, _) => await ReadSelectedTitleAsync();
        _chats.SelectedIndexChanged += (_, _) =>
            _rename.Enabled = _chats.SelectedItem is ChatRow;
        _chats.DoubleClick += async (_, _) => await OpenSelectedAsync();
        var bookmarkMenu = new ContextMenuStrip();
        var detach = bookmarkMenu.Items.Add("Odlacz lokalny wpis (nie usuwa z ChatGPT)");
        bookmarkMenu.Opening += (_, _) =>
            detach.Enabled = _chats.SelectedItem is ChatRow r && !r.IsLead;
        detach.Click += (_, _) => DetachSelectedBookmark();
        _chats.ContextMenuStrip = bookmarkMenu;
        _refresh.Click += (_, _) => Reload();
        _close.Click += (_, _) => Close();
        Load += (_, _) => Reload();
    }

    private void DetachSelectedBookmark()
    {
        if (_chats.SelectedItem is not ChatRow selected || selected.IsLead) return;
        if (MessageBox.Show(this,
            "Odlaczyc wylacznie lokalny wpis tej rozmowy z projektu OBL?\n" +
            selected.Url + "\nNie usuwa to rozmowy w ChatGPT ani historii projektu.",
            "Odlacz lokalny wpis", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            LiteProjectChatLocalDetach.Remove(LiteProjectRegistry.OpenDefault(),
                _projectId, selected.Id, selected.Url);
            if (LiteProjectRegistry.OpenDefault().Projects.Single(p => p.Id == _projectId)
                .Chats.Any(c => c.Id == selected.Id))
                throw new InvalidDataException("Lokalny wpis nadal istnieje po zapisie.");
            LiteEventLog.Write("project_chat_local_bookmark_detached", new
            {
                projectId = _projectId, chatId = selected.Id,
                nativeConversationDeleted = false, wakeScheduleChanged = false
            });
            Reload();
            (Owner as LiteProjectDashboard)?.RefreshFromOwner();
        }
        catch (Exception ex)
        {
            _status.Text = ex.GetType().Name + ": " + ex.Message;
            LiteEventLog.Write("project_chat_local_bookmark_detach_failed", new
            { errorType = ex.GetType().Name, message = ex.Message });
        }
    }

    private void Reload()
    {
        try
        {
            string? previous = (_chats.SelectedItem as ChatRow)?.Id;
            var project = LiteProjectRegistry.OpenDefault().Projects
                .SingleOrDefault(p => p.Id == _projectId)
                ?? throw new InvalidDataException("Projekt nie istnieje.");
            var chats = project.Chats.OrderByDescending(c => c.Role == "lead")
                .ThenByDescending(c => c.AddedUtc)
                .ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
            _chats.BeginUpdate();
            try
            {
                _chats.Items.Clear();
                int previousIndex = 0;
                foreach (var chat in chats)
                {
                    string state;
                    if (chat.Role == "lead")
                        state = "GLOWNY";
                    else
                    {
                        bool protectedPrevious = previousIndex++ < 2;
                        state = chat.NativeArchivedUtc.HasValue
                            ? chat.NativeArchiveVisibilityVerifiedUtc.HasValue
                                ? "ARCH. CHATGPT: LISTA POTW."
                                : chat.NativeArchiveOwnerAccepted429Utc.HasValue
                                    ? "ARCH. CHATGPT: 429 AKCEPT." : "ARCH. CHATGPT: LISTA NIEPOTW."
                            : protectedPrevious ? "POPRZEDNI, LOKALNY"
                            : chat.NativeArchiveVerifyPendingUtc.HasValue
                                ? "ARCHIWIZACJA: WERYFIKACJA"
                                : "DO ARCHIWIZACJI W CHATGPT";
                    }
                    _chats.Items.Add(new ChatRow(chat.Id, chat.Title,
                        chat.Url, chat.Role == "lead", state));
                }
                int found = -1;
                for (int i = 0; i < _chats.Items.Count; i++)
                    if ((_chats.Items[i] as ChatRow)?.Id == previous)
                    { found = i; break; }
                if (found < 0 && _chats.Items.Count != 0) found = 0;
                if (found >= 0) _chats.SelectedIndex = found;
            }
            finally { _chats.EndUpdate(); }
            var oldChats = chats.Where(c => c.Role != "lead").ToList();
            int nativeArchived = oldChats.Count(c => c.NativeArchivedUtc.HasValue);
            int listVerified = oldChats.Count(c => c.NativeArchiveVisibilityVerifiedUtc.HasValue);
            int ownerAccepted429 = oldChats.Count(c => c.NativeArchiveOwnerAccepted429Utc.HasValue && !c.NativeArchiveVisibilityVerifiedUtc.HasValue);
            int pending = oldChats.Skip(2).Count(c =>
                !c.NativeArchivedUtc.HasValue && c.NativeArchiveVerifyPendingUtc.HasValue);
            int toArchive = oldChats.Skip(2).Count(c =>
                !c.NativeArchivedUtc.HasValue && !c.NativeArchiveVerifyPendingUtc.HasValue);
            _heading.Text = project.Title + " | " + chats.Count + " rozmow";
            _status.Text = "ChatGPT: archiwum " + nativeArchived +
                " (lista potw. " + listVerified + ", 429 zaakceptowane " + ownerAccepted429 + "), do archiwizacji " +
                toArchive + ", do weryfikacji " + pending +
                ". Otwarcie rozmowy nie zmienia leada.";
            _open.Enabled = chats.Count > 0;
            _rename.Enabled = chats.Count > 0;
            _readTitle.Enabled = chats.Count > 0;
        }
        catch (Exception ex)
        {
            _open.Enabled = false;
            _rename.Enabled = false;
            _readTitle.Enabled = false;
            _status.Text = ex.GetType().Name + ": " + ex.Message;
            LiteEventLog.Write("project_chats_view_failed",
                new { errorType = ex.GetType().Name });
        }
    }

    private async Task ReadSelectedTitleAsync()
    {
        if (_chats.SelectedItem is not ChatRow selected) return;
        _readTitle.Enabled = _rename.Enabled = _open.Enabled = false;
        _status.Text = "Odczyt tytulu z ChatGPT: czekam na odpowiedz serwera...";
        try
        {
            string title = await _nativeReadTitle(_projectId, selected.Id);
            Reload();
            (Owner as LiteProjectDashboard)?.RefreshFromOwner();
            _status.Text = "Tytul odczytany z ChatGPT: " + title;
        }
        catch (Exception ex)
        {
            _status.Text = "Odczyt tytulu niepowodzeniem: " +
                ex.GetType().Name + ": " + ex.Message;
            LiteEventLog.Write("project_chat_title_read_ui_failed",
                new { errorType = ex.GetType().Name, error = ex.Message });
        }
        finally
        {
            _readTitle.Enabled = _rename.Enabled = _open.Enabled =
                _chats.SelectedItem is ChatRow;
        }
    }

    private async Task RenameSelectedAsync()
    {
        if (_chats.SelectedItem is not ChatRow selected) return;
        using var dialog = new Form {
            Text = "Zmien tytul rozmowy w ChatGPT",
            Width = 520, Height = 190,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent
        };
        var label = new Label {
            Text = "Nowy tytul rozmowy ChatGPT (1-120 znakow)",
            Left = 18, Top = 12, Width = 470, Height = 24
        };
        var input = new TextBox {
            Text = selected.Title, MaxLength = 120,
            Left = 18, Top = 42, Width = 467
        };
        var save = new Button {
            Text = "Zmien w ChatGPT", Left = 260, Top = 88,
            Width = 125, DialogResult = DialogResult.OK
        };
        var cancel = new Button {
            Text = "Anuluj", Left = 395, Top = 88,
            Width = 90, DialogResult = DialogResult.Cancel
        };
        dialog.Controls.AddRange(new Control[] { label, input, save, cancel });
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string title = input.Text.Trim();
        if (title == selected.Title) return;
        _rename.Enabled = false;
        _open.Enabled = false;
        _status.Text = "Zmiana tytulu: czekam na potwierdzenie serwera ChatGPT...";
        try
        {
            if (title.Length == 0)
                throw new ArgumentException("Tytul nie moze byc pusty.");
            // Reload the association before sending the actual server PATCH.
            var project = LiteProjectRegistry.OpenDefault().Projects
                .SingleOrDefault(p => p.Id == _projectId);
            if (project?.Chats.Any(c => c.Id == selected.Id &&
                c.Url == selected.Url && c.Title == selected.Title) != true)
                throw new InvalidOperationException(
                    "Rozmowa zostala zmieniona. Odswiez liste.");
            string confirmedTitle = await _nativeRename(_projectId, selected.Id, title);
            Reload();
            (Owner as LiteProjectDashboard)?.RefreshFromOwner();
            _status.Text = "Potwierdzono tytul w ChatGPT: " + confirmedTitle;
        }
        catch (Exception ex)
        {
            _status.Text = "Zmiana tytulu niepotwierdzona: " +
                ex.GetType().Name + ": " + ex.Message;
            LiteEventLog.Write("project_chat_rename_ui_failed",
                new { errorType = ex.GetType().Name, error = ex.Message });
        }
        finally
        {
            _rename.Enabled = _chats.SelectedItem is ChatRow;
            _open.Enabled = _chats.SelectedItem is ChatRow;
        }
    }
    private async Task OpenSelectedAsync()
    {
        if (_chats.SelectedItem is not ChatRow selected) return;
        _open.Enabled = false;
        try
        {
            // Prevent a stale list from opening a chat moved to another project.
            var current = LiteProjectRegistry.OpenDefault().Projects
                .SingleOrDefault(p => p.Id == _projectId);
            if (current?.Chats.Any(c => c.Id == selected.Id && c.Url == selected.Url) != true)
                throw new InvalidDataException(
                    "Przypisanie czatu zmienilo sie. Odswiez liste.");
            await _navigate(selected.Url);
            LiteEventLog.Write("project_dashboard_chat_opened",
                new { projectKey = current.Key, isLead = selected.IsLead });
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = "Nie otwarto: " + ex.GetType().Name + ": " + ex.Message;
            LiteEventLog.Write("project_dashboard_chat_open_failed",
                new { errorType = ex.GetType().Name });
        }
        finally { _open.Enabled = _chats.SelectedItem is ChatRow; }
    }
}
