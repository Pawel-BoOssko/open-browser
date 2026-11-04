using System.Globalization;

namespace BridgeBrowserAlpha0;

/// <summary>
/// New project-centered workspace. One selected project and its independent
/// schedule are visible at a time. Archive and diagnostics stay on demand.
/// The existing sidebar remains as an advanced fallback during migration.
/// </summary>
internal sealed partial class LiteProjectDashboard : Form
{
    private sealed record ProjectRow(string Id, string Title, string Summary)
    {
        public override string ToString() => Title + "   |   " + Summary;
    }

    private readonly Func<string, Task> _navigate;
    private readonly Func<(string? Url, string? Title)> _currentChat;
    private readonly Action<string> _requestHandoff;
    private readonly Action<string> _takeOver;
    private readonly Func<string, Task<LiteNativeArchiveManualResult>> _archiveOlder;
    private readonly Func<string, string, string, Task<string>> _renameNative;
    private readonly Func<string, string, Task<string>> _readNativeTitle;
    private readonly ListBox _projects = new()
    {
        Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None
    };
    private readonly TextBox _projectSearch = new()
    {
        Dock = DockStyle.Fill, PlaceholderText = "Szukaj projektu po nazwie lub kluczu"
    };
    private readonly Label _title = new()
    {
        Dock = DockStyle.Top, Height = 58, Font = new Font(SystemFonts.DefaultFont.FontFamily, 17f, FontStyle.Bold)
    };
    private readonly Label _lead = new() { Dock = DockStyle.Top, Height = 68, AutoEllipsis = true };
    private readonly Label _schedule = new() { Dock = DockStyle.Fill, Height = 160, AutoEllipsis = true };
    private readonly Label _feedback = new() { Dock = DockStyle.Bottom, Height = 44, AutoEllipsis = true };
    private readonly NumericUpDown _minutes = new()
    {
        Minimum = 15, Maximum = 1440, Value = 25, Width = 86,
        Increment = 5, TextAlign = HorizontalAlignment.Right
    };
    private readonly Button _start = new() { Text = "Zapisz i wlacz", Width = 132, Height = 38 };
    private readonly Button _pause = new() { Text = "Wstrzymaj", Width = 108, Height = 38 };
    private readonly Button _open = new() { Text = "Otworz czat", Width = 130, Height = 38 };
    private readonly Button _notes = new() { Text = "Notatki", Width = 115, Height = 38 };
    private readonly Button _chats = new() { Text = "Rozmowy", Width = 110, Height = 38 };
    private readonly Button _status = new() { Text = "Stan", Width = 76, Height = 38 };
    private readonly Button _create = new() { Text = "+ Nowy", Width = 94, Height = 32 };
    private readonly Button _manage = new() { Text = "Zarzadzaj", Width = 114, Height = 38 };
    private string? _selectedId;
    // Only auto-select on the first load. Clearing a search must not silently
    // authorize actions against a different project.
    private bool _initialSelectionPending = true;
    private readonly System.Windows.Forms.Timer _projectStatusTimer = new()
    {
        Interval = 30000
    };

    internal LiteProjectDashboard(Func<string, Task> navigate,
        Func<(string? Url, string? Title)> currentChat,
        Action<string> requestHandoff, Action<string> takeOver,
        Func<string, Task<LiteNativeArchiveManualResult>> archiveOlder,
        Func<string, string, string, Task<string>> renameNative,
        Func<string, string, Task<string>> readNativeTitle)
    {
        _navigate = navigate;
        _currentChat = currentChat;
        _requestHandoff = requestHandoff;
        _takeOver = takeOver;
        _archiveOlder = archiveOlder;
        _renameNative = renameNative;
        _readNativeTitle = readNativeTitle;
        Text = "Open Browser Lite | Projekty";
        Width = 1040;
        Height = 680;
        MinimumSize = new Size(780, 480);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                _projectSearch.Focus();
                _projectSearch.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        BackColor = SystemColors.Window;
        Font = new Font("Segoe UI", 10f);

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(22, 15, 22, 16)
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 73));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Projekty\nWybierz projekt. Harmonogram dotyczy tylko jego aktualnego czatu.",
            Font = new Font("Segoe UI", 11.5f),
            ForeColor = SystemColors.ControlText
        };
        outer.Controls.Add(header, 0, 0);
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterDistance = 310
        };
        outer.Controls.Add(split, 0, 1);
        // SplitContainer starts with a small designer-sized width. Setting
        // both panel minimums before docking is laid out throws on WinForms.
        // Apply constraints only when the actual window is visible and sized.
        Shown += (_, _) =>
        {
            if (split.Width < 245 + 420 + split.SplitterWidth) return;
            split.Panel1MinSize = 245;
            split.Panel2MinSize = 420;
            split.SplitterDistance = Math.Clamp(310, 245,
                split.Width - split.SplitterWidth - 420);
        };
        Controls.Add(outer);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1,
            Padding = new Padding(0, 0, 14, 0)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var listHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false
        };
        listHeader.Controls.Add(new Label
        {
            Text = "Projekty", Width = 125, Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        });
        listHeader.Controls.Add(_create);
        left.Controls.Add(listHeader, 0, 0);
        left.Controls.Add(_projectSearch, 0, 1);
        left.Controls.Add(_projects, 0, 2);
        var refresh = new Button { Text = "Odswiez projekty", Dock = DockStyle.Top, Height = 35 };
        left.Controls.Add(refresh, 0, 3);
        split.Panel1.Controls.Add(left);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1,
            AutoScroll = true, Padding = new Padding(20, 6, 0, 0)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 166));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        right.Controls.Add(_title, 0, 0);
        right.Controls.Add(_lead, 0, 1);
        right.Controls.Add(_schedule, 0, 2);

        // Project actions remain visible within the minimum window width.
        var scheduleActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
            Margin = new Padding(0, 0, 0, 4)
        };
        scheduleActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        scheduleActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        scheduleActions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        scheduleActions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        scheduleActions.Controls.Add(new Label
        {
            Text = "Interwal (min):", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _minutes.Anchor = AnchorStyles.Left;
        scheduleActions.Controls.Add(_minutes, 1, 0);
        _start.Dock = DockStyle.Fill;
        _pause.Dock = DockStyle.Fill;
        scheduleActions.Controls.Add(_start, 0, 1);
        scheduleActions.Controls.Add(_pause, 1, 1);
        right.Controls.Add(scheduleActions, 0, 3);

        var workActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2,
            Margin = new Padding(0, 2, 0, 2)
        };
        workActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        workActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        workActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
        workActions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        workActions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        foreach (var control in new[] { _open, _notes, _chats, _status, _manage })
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3);
        }
        workActions.Controls.Add(_open, 0, 0);
        workActions.Controls.Add(_notes, 1, 0);
        workActions.Controls.Add(_chats, 2, 0);
        workActions.Controls.Add(_status, 0, 1);
        workActions.Controls.Add(_manage, 1, 1);
        right.Controls.Add(workActions, 0, 4);
        var bottom = new Panel { Dock = DockStyle.Fill };
        bottom.Controls.Add(_feedback);
        bottom.Controls.Add(new Label
        {
            Dock = DockStyle.Top, Height = 62,
            Text = "Pozostale operacje techniczne: w starym panelu podczas migracji.\n"
                + "Wlaczanie projektu nie modyfikuje harmonogramow pozostalych projektow.",
            ForeColor = SystemColors.GrayText
        });
        right.Controls.Add(bottom, 0, 5);
        split.Panel2.Controls.Add(right);

        _projects.SelectedIndexChanged += (_, _) =>
        {
            if (_projects.SelectedItem is ProjectRow p)
            {
                _selectedId = p.Id;
                ShowSelected();
            }
        };
        _projectSearch.TextChanged += (_, _) => RefreshProjects();
        _projectSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape && _projectSearch.TextLength > 0)
            {
                _projectSearch.Clear();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        refresh.Click += (_, _) => RefreshProjects();
        _start.Click += (_, _) => SetSchedule(true);
        _pause.Click += (_, _) => SetSchedule(false);
        _status.Click += (_, _) => ShowSelectedWakeStatus();
        _open.Click += async (_, _) =>
        {
            try
            {
                var p = SelectedProject();
                var lead = p?.Chats.SingleOrDefault(c => c.Role == "lead");
                if (lead is null) return;
                await _navigate(lead.Url);
            }
            catch (Exception ex) { ShowError("Nawigacja", ex); }
        };
        _notes.Click += (_, _) =>
        {
            try
            {
                var project = SelectedProject()
                    ?? throw new InvalidOperationException("Wybierz projekt.");
                if (project.Chats.Count(c => c.Role == "lead") != 1)
                    throw new InvalidOperationException("Projekt nie ma jednego leada.");
                using var editor = new LiteProjectNotesDialog(project.Id);
                editor.ShowDialog(this);
            }
            catch (Exception ex) { ShowError("Notatki", ex); }
        };
        var management = new ContextMenuStrip();
        management.Items.Add("Przypnij otwarty czat", null, (_, _) => PinOpenedChat());
        management.Items.Add("Zmien nazwe projektu", null, (_, _) => RenameSelected());
        management.Items.Add("Zmien czat glowny", null, (_, _) => ChangeSelectedLead());
        management.Items.Add("Archiwizuj starsze w ChatGPT", null,
            async (_, _) => await ArchiveSelectedProjectNativeAsync());
        management.Items.Add("Historia pracy", null, (_, _) =>
        {
            if (_selectedId is not null)
                new LiteWorkHistoryViewer(_selectedId).Show(this);
        });
        var rotation = new ToolStripMenuItem("Rotacja i HANDOFF");
        rotation.DropDownItems.Add("Zlec HANDOFF", null,
            (_, _) => RequestManualHandoff());
        rotation.DropDownItems.Add("Przejmij otwarty czat po HANDOFF", null,
            (_, _) => TakeOverAfterHandoff());
        management.Items.Add(rotation);
        var preview = management.Items.Add("Przekazanie OBL", null,
            (_, _) => new LiteHandoffPreviewUi().Show(this));
        management.Opening += (_, _) =>
        {
            var p = SelectedProject();
            preview.Enabled = p?.Key == "obl";
            rotation.Enabled = p?.Chats.Count(c => c.Role == "lead") == 1;
        };
        _manage.Click += (_, _) =>
            management.Show(_manage, new Point(0, _manage.Height));
        _create.Click += (_, _) => OnboardProject();
        _chats.Click += (_, _) =>
        {
            if (_selectedId is not null)
                new LiteProjectChatsDialog(_selectedId, _navigate, _renameNative,
                    _readNativeTitle).Show(this);
        };
        _projectStatusTimer.Tick += (_, _) =>
        {
            // Refresh telemetry, never overwrite a currently edited interval.
            if (Visible && !_minutes.ContainsFocus)
                RefreshProjects();
        };
        Shown += (_, _) => _projectStatusTimer.Start();
        FormClosed += (_, _) =>
        {
            _projectStatusTimer.Stop();
            _projectStatusTimer.Dispose();
        };
        Load += (_, _) => RefreshProjects();
    }

    private LiteProjectRegistry.Project? SelectedProject() =>
        LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(p => p.Id == _selectedId);

    internal void RefreshFromOwner() => RefreshProjects();

    private void RefreshProjects()
    {
        try
        {
            var db = LiteProjectRegistry.OpenDefault();
            var wakes = LiteChatWakeRegistry.Read();
            var current = _selectedId;
            int oldTopIndex = _projects.TopIndex;
            var filter = _projectSearch.Text.Trim();
            _projects.BeginUpdate();
            try
            {
                _projects.Items.Clear();
                foreach (var project in db.Projects)
                {
                    if (!LiteProjectDashboardFilter.Matches(project.Title, project.Key, filter))
                        continue;
                    var lead = project.Chats.SingleOrDefault(c => c.Role == "lead");
                    var wake = lead is null ? null : wakes.FirstOrDefault(w => w.Url == lead.Url);
                    var state = LiteProjectDashboardScheduleLabel.Format(
                        lead is not null, wake?.Enabled == true,
                        wake?.EffectiveMinutes ?? 0,
                        wake?.DueUtc ?? default);
                    _projects.Items.Add(new ProjectRow(project.Id, project.Title, state));
                }
                int selected = -1;
                for (int i = 0; i < _projects.Items.Count; i++)
                    if ((_projects.Items[i] as ProjectRow)?.Id == current) { selected = i; break; }
                // Do not silently retarget project commands when a search hides
                // the selected project. Require a click for the new selection.
                if (selected < 0 && filter.Length == 0 &&
                    _initialSelectionPending && _projects.Items.Count > 0)
                    selected = 0;
                if (selected >= 0)
                    _projects.SelectedIndex = selected;
                else
                    _selectedId = null;
                _initialSelectionPending = false;
                if (_projects.Items.Count > 0)
                    _projects.TopIndex = Math.Min(oldTopIndex, _projects.Items.Count - 1);
            }
            finally { _projects.EndUpdate(); }
            ShowSelected();
        }
        catch (Exception ex) { ShowError("Odczyt projektow", ex); }
    }

    private void ShowSelected()
    {
        try
        {
            var project = SelectedProject();
            if (project is null)
            {
                _title.Text = _projectSearch.TextLength > 0
                    ? "Wybierz projekt z wynikow" : "Wybierz projekt";
                _lead.Text = "";
                _schedule.Text = "";
                _feedback.Text = _projectSearch.TextLength > 0
                    ? (_projects.Items.Count == 0
                        ? "Brak projektow pasujacych do wyszukiwania."
                        : "Wybierz projekt z listy przed wykonaniem operacji.")
                    : "Wybierz projekt z listy.";
                _start.Enabled = _pause.Enabled = _open.Enabled =
                    _notes.Enabled = _chats.Enabled = _status.Enabled =
                    _manage.Enabled = false;
                return;
            }
            _title.Text = project.Title;
            var lead = project.Chats.SingleOrDefault(c => c.Role == "lead");
            var wake = lead is null ? null : LiteChatWakeRegistry.Read()
                .FirstOrDefault(w => w.Url == lead.Url);
            _lead.Text = lead is null ? "Czat glowny: nieprzypisany" :
                "Czat glowny: " + lead.Title;
            _schedule.Text = LiteWakeStatusExplanation.Display(wake);
            _minutes.Value = Math.Clamp(wake?.EffectiveMinutes ?? 25, 15, 1440);
            _start.Enabled = lead is not null;
            _pause.Enabled = wake?.Enabled == true;
            _open.Enabled = lead is not null;
            _notes.Enabled = lead is not null;
            _chats.Enabled = project.Chats.Count > 0;
            _status.Enabled = true;
            _manage.Enabled = true;
            _feedback.Text = wake is not null && !string.IsNullOrWhiteSpace(wake.HandoffPhase)
                ? "HANDOFF: " + wake.HandoffPhase + " | " + wake.LastState
                : "Zmieniasz ustawienia tylko projektu: " + project.Title;
        }
        catch (Exception ex) { ShowError("Stan projektu", ex); }
    }

    private void SetSchedule(bool enabled)
    {
        try
        {
            var db = LiteProjectRegistry.OpenDefault();
            var p = db.Projects.SingleOrDefault(p => p.Id == _selectedId)
                ?? throw new InvalidOperationException("Nie wybrano projektu.");
            var lead = p.Chats.SingleOrDefault(c => c.Role == "lead")
                ?? throw new InvalidOperationException("Projekt wymaga jednego czatu glownego.");
            var all = LiteChatWakeRegistry.Read();
            int minutes = (int)_minutes.Value;
            bool changed = LiteProjectScheduleEdit.Apply(
                all, lead.Url, enabled, minutes, DateTimeOffset.UtcNow);
            if (!changed)
            {
                LiteEventLog.Write("project_dashboard_schedule_unchanged", new {
                    projectKey = p.Key, enabled, minutes, duePreserved = true
                });
                RefreshProjects();
                return;
            }
            if (!LiteProjectRegistry.CanScheduleProjectLeads(db.Projects, all))
                throw new InvalidOperationException("Niedozwolony aktywny czat projektu.");
            LiteChatWakeRegistry.Write(all);
            var verified = LiteChatWakeRegistry.Read().Single(w => w.Url == lead.Url);
            if (verified.Enabled != enabled ||
                (enabled && verified.EffectiveMinutes != (int)_minutes.Value))
                throw new InvalidDataException("Weryfikacja harmonogramu nie powiodla sie.");
            LiteEventLog.Write("project_dashboard_schedule_saved", new
            {
                projectKey = p.Key, enabled, minutes = verified.EffectiveMinutes
            });
            RefreshProjects();
        }
        catch (Exception ex) { ShowError("Zapis harmonogramu", ex); }
    }

    private string? RequestProjectName(string caption, string initial = "")
    {
        using var dialog = new Form
        {
            Text = caption, Width = 470, Height = 190,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            ShowInTaskbar = false
        };
        var label = new Label
        {
            Text = "Nazwa projektu (1-120 znakow)", Left = 18,
            Top = 14, Width = 410, Height = 26
        };
        var name = new TextBox
        {
            Left = 18, Top = 43, Width = 415, Text = initial,
            MaxLength = 120
        };
        var ok = new Button
        {
            Text = "Zapisz", Left = 244, Top = 88,
            Width = 90, DialogResult = DialogResult.OK
        };
        var cancel = new Button
        {
            Text = "Anuluj", Left = 344, Top = 88,
            Width = 90, DialogResult = DialogResult.Cancel
        };
        dialog.Controls.AddRange(new Control[] { label, name, ok, cancel });
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(this) == DialogResult.OK ? name.Text.Trim() : null;
    }

    private void CreateProject()
    {
        string? name = RequestProjectName("Nowy projekt");
        if (name is null) return;
        try
        {
            var added = LiteProjectRegistry.OpenDefault().AddProject(name);
            _selectedId = added.Id;
            LiteEventLog.Write("dashboard_project_added",
                new { projectKey = added.Key });
            RefreshProjects();
        }
        catch (Exception ex) { ShowError("Nowy projekt", ex); }
    }

    private void RenameSelected()
    {
        try
        {
            var project = SelectedProject()
                ?? throw new InvalidOperationException("Wybierz projekt.");
            string? name = RequestProjectName("Zmien nazwe projektu", project.Title);
            if (name is null || name == project.Title) return;
            LiteProjectRegistry.OpenDefault().RenameProject(project.Id, name);
            LiteEventLog.Write("dashboard_project_renamed",
                new { projectKey = project.Key });
            RefreshProjects();
        }
        catch (Exception ex) { ShowError("Zmiana nazwy", ex); }
    }

    private void PinOpenedChat()
    {
        try
        {
            var project = SelectedProject()
                ?? throw new InvalidOperationException("Wybierz projekt.");
            var (url, title) = _currentChat();
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Brak otwartego czatu ChatGPT.");
            bool hadLead = project.Chats.Any(c => c.Role == "lead");
            var db = LiteProjectRegistry.OpenDefault();
            var saved = db.AddChat(project.Id,
                (title ?? "").Length > 100 ? title![..100] : title ?? "", url);
            // An empty project gets its first lead; existing lead remains
            // unchanged, and newly pinned conversations are archive entries.
            if (!hadLead)
                db.PromoteLead(project.Id, saved.Id);
            LiteEventLog.Write("dashboard_project_chat_pinned",
                new { projectKey = project.Key, firstLead = !hadLead });
            RefreshProjects();
        }
        catch (Exception ex) { ShowError("Przypinanie czatu", ex); }
    }

    private void ShowError(string operation, Exception ex)
    {
        _feedback.Text = operation + ": " + ex.GetType().Name + ": " + ex.Message;
        LiteEventLog.Write("project_dashboard_error", new
        {
            operation, errorType = ex.GetType().Name, message = ex.Message
        });
    }
}
