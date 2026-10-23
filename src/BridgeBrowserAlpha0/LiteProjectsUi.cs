namespace BridgeBrowserAlpha0;

/// <summary>
/// Native project sidebar: one WebView2 instance, unlimited local projects,
/// multiple bookmarked chats with a single optional lead for each project.
/// </summary>
internal sealed partial class ChatGptLiteForm
{
    private LiteProjectRegistry? _projects;
    private readonly FlowLayoutPanel _projectList = new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
        WrapContents = false, AutoScroll = true, Padding = new Padding(5)
    };
    private readonly TextBox _projectTitle = new()
    {
        Width = 130, PlaceholderText = "Nazwa projektu"
    };
    private string? _selectedProjectId;

    private Control CreateProjectsUi()
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            BackColor = SystemColors.ControlLight
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));

        container.Controls.Add(new Label
        {
            Text = "Projekty", Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 0, 0),
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        }, 0, 0);

        var addRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var add = new Button { Text = "Dodaj", Width = 65 };
        add.Click += (_, _) =>
        {
            try
            {
                if (_projects is null) return;
                var project = _projects.AddProject(_projectTitle.Text);
                _projectTitle.Clear();
                _selectedProjectId = project.Id;
                LiteEventLog.Write("project_added", new { projectCount = _projects.Projects.Count });
                RenderProjects();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open Browser — projekty");
            }
        };
        addRow.Controls.Add(_projectTitle);
        addRow.Controls.Add(add);
        container.Controls.Add(addRow, 0, 1);

        container.Controls.Add(_projectList, 0, 2);
        _projectList.Resize += (_, _) => ResizeProjectRows();
        var pin = new Button
        {
            Text = "Przypnij bieżący czat", Dock = DockStyle.Fill,
            Padding = new Padding(3)
        };
        pin.Click += (_, _) => PinCurrentChat();
        var handoff = new Button
        {
            Text = "Poproś o HANDOFF", Dock = DockStyle.Fill,
            Height = 30
        };
        handoff.Click += (_, _) => RequestSelectedProjectHandoff();
        var transfer = new Button
        {
            Text = "Przejmij otwarty czat jako wykonawcę",
            Dock = DockStyle.Fill, Height = 30
        };
        transfer.Click += (_, _) => TakeOverSelectedProjectChat();
        var handoffActions = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3
        };
        handoffActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        handoffActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        handoffActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        handoffActions.Controls.Add(pin, 0, 0);
        handoffActions.Controls.Add(handoff, 0, 1);
        handoffActions.Controls.Add(transfer, 0, 2);
        container.Controls.Add(handoffActions, 0, 3);

        try
        {
            _projects = LiteProjectRegistry.OpenDefault();
            _selectedProjectId = _projects.Projects.FirstOrDefault()?.Id;
            RenderProjects();
            LiteEventLog.Write("projects_loaded", new { projectCount = _projects.Projects.Count });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("projects_ui_load_failed", new { errorType = ex.GetType().Name });
            _projectList.Controls.Add(new Label
            {
                AutoSize = true, Text = "Błąd rejestru projektów. Plik nie został nadpisany."
            });
        }
        return container;
    }

    private void RenderProjects()
    {
        if (_projects is null) return;
        // External pilot registration may change projects.json while the UI
        // is open. Refresh before each render, without navigating WebView2.
        try { _projects = LiteProjectRegistry.OpenDefault(); }
        catch (Exception ex) {
            LiteEventLog.Write("projects_refresh_failed",
                new { errorType = ex.GetType().Name });
            return;
        }
        _projectList.SuspendLayout();
        try
        {
            while (_projectList.Controls.Count > 0)
            {
                Control control = _projectList.Controls[0];
                _projectList.Controls.RemoveAt(0);
                control.Dispose();
            }

            List<LiteChatWakeRegistry.Chat> schedules;
            try { schedules = LiteChatWakeRegistry.Read(); }
            catch { schedules = new List<LiteChatWakeRegistry.Chat>(); }
            foreach (var project in _projects.Projects)
            {
                bool selected = project.Id == _selectedProjectId;
                var projectButton = new Button
                {
                    Width = 238, Height = 36, TextAlign = ContentAlignment.MiddleLeft,
                    Text = (selected ? "▸ " : "  ") + project.Title +
                           " (" + project.Chats.Count + ")",
                    BackColor = selected ? SystemColors.ControlDark : SystemColors.Control,
                    AutoEllipsis = true
                };
                string projectId = project.Id;
                projectButton.Click += (_, _) =>
                {
                    _selectedProjectId = projectId;
                    RenderProjects();
                };
                _projectList.Controls.Add(projectButton);
                if (!selected) continue;

                foreach (var chat in project.Chats)
                {
                    var row = new Panel { Width = 238, Height = 54 };
                    var link = new Button
                    {
                        Width = 179, Height = 29, Left = 8, Top = 0,
                        TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                        Text = (chat.Role == "lead" ? "★ " : "Archiwum: ") + chat.Title
                    };
                    string url = chat.Url;
                    link.Click += async (_, _) =>
                    {
                        LiteEventLog.Write("project_chat_opened");
                        await NavigateToProjectChatAsync(url);
                    };
                    var makeLead = new Button
                    {
                        Text = "Lead", Width = 47, Height = 29, Left = 189,
                        Top = 0, Enabled = chat.Role != "lead"
                    };
                    string chatId = chat.Id;
                    makeLead.Click += (_, _) =>
                    {
                        try
                        {
                            // Old lead cannot continue waking after a new
                            // executor is selected for the same project.
                            var currentProject = _projects.Projects.First(p =>
                                p.Id == projectId);
                            var plans = LiteChatWakeRegistry.Read();
                            foreach (var old in currentProject.Chats.Where(c =>
                                c.Id != chatId))
                            {
                                var wake = plans.FirstOrDefault(w => w.Url == old.Url);
                                if (wake?.Enabled == true)
                                {
                                    wake.Enabled = false;
                                    wake.LastState = "paused_new_project_lead";
                                }
                            }
                            LiteChatWakeRegistry.Write(plans);
                            _projects.PromoteLead(projectId, chatId);
                            LiteEventLog.Write("project_lead_changed");
                            RenderProjects();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Open Browser — projekty");
                        }
                    };
                    var wake = schedules.FirstOrDefault(x => x.Url == chat.Url);
                    var info = new Label
                    {
                        Left = 9, Top = 32, Width = 173, Height = 20,
                        AutoEllipsis = true,
                        Text = chat.Role != "lead" ? "Archiwum — bez autopracy" :
                            wake?.Enabled == true
                              ? "WŁ. " + wake.EffectiveMinutes + "m | " +
                                wake.LastState + " | " +
                                wake.DueUtc.ToLocalTime().ToString("HH:mm")
                              : "WYŁ. | bez harmonogramu"
                    };
                    var toggle = new Button
                    {
                        Left = 189, Top = 30, Width = 47, Height = 23,
                        Text = wake?.Enabled == true ? "WYŁ." : "WŁ.",
                        Enabled = chat.Role == "lead"
                    };
                    toggle.Click += (_, _) =>
                        ToggleProjectLeadWake(projectId, chatId);
                    row.Controls.Add(link);
                    row.Controls.Add(makeLead);
                    row.Controls.Add(info);
                    row.Controls.Add(toggle);
                    _projectList.Controls.Add(row);
                }
            }
        }
        finally
        {
            _projectList.ResumeLayout();
            ResizeProjectRows();
        }
    }

    // Keep project names and lead controls readable when side panels resize.
    // The previous hard-coded width of 238px clipped buttons in a 220px panel.
    private void ResizeProjectRows()
    {
        int width = Math.Max(180, _projectList.ClientSize.Width - 17);
        foreach (Control control in _projectList.Controls)
        {
            control.Width = width;
            if (control is not Panel row) continue;
            var buttons = row.Controls.OfType<Button>().ToArray();
            if (buttons.Length >= 2)
            {
                buttons[0].Width = Math.Max(120, width - 57);
                buttons[1].Left = width - 49;
            }
            foreach (var label in row.Controls.OfType<Label>())
                label.Width = Math.Max(112, width - 61);
        }
    }

    private void ToggleProjectLeadWake(string projectId, string chatId)
    {
        try
        {
            _projects = LiteProjectRegistry.OpenDefault();
            var project = _projects.Projects.FirstOrDefault(p => p.Id == projectId)
                ?? throw new InvalidOperationException("Projekt nie istnieje");
            var chat = project.Chats.FirstOrDefault(c => c.Id == chatId &&
                c.Role == "lead")
                ?? throw new InvalidOperationException(
                    "Autopraca może działać wyłącznie w czacie głównym projektu");
            var schedules = LiteChatWakeRegistry.Read();
            var existing = schedules.FirstOrDefault(w => w.Url == chat.Url);
            if (existing is null)
            {
                string prompt = "Kontynuuj projekt " + project.Title +
                    " w tym jednym czacie wykonawczym. Zapoznaj się z ostatnim " +
                    "checkpointem i bieżącym kodem, wykonaj kolejny etap, " +
                    "uruchom testy i zapisz krótki checkpoint w dokumentacji " +
                    "projektu. Nie korzystaj z ChatGPT Tasks, nie wykonuj " +
                    "ręcznych Git commit/push, nie ponawiaj niepewnych wysyłek.";
                LiteChatWakeRegistry.Configure(schedules, chat.Url, 25,
                    prompt, DateTimeOffset.UtcNow);
            }
            else
            {
                existing.Enabled = !existing.Enabled;
                existing.LastState = existing.Enabled ? "armed" : "paused";
                if (existing.Enabled)
                    existing.DueUtc = DateTimeOffset.UtcNow.AddMinutes(
                        existing.EffectiveMinutes);
            }
            if (!LiteProjectRegistry.CanScheduleProjectLeads(
                _projects.Projects, schedules))
                throw new InvalidOperationException(
                    "Nie wolno włączyć dwóch wykonawców w jednym projekcie");
            LiteChatWakeRegistry.Write(schedules);
            LiteEventLog.Write("project_lead_wake_toggled", new {
                enabled = schedules.First(w => w.Url == chat.Url).Enabled,
                projectChatCount = project.Chats.Count
            });
            RenderProjects();
            UpdateMultiWakeStatus();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("project_lead_wake_toggle_failed",
                new { errorType = ex.GetType().Name });
            MessageBox.Show(this, ex.Message, "Wybudzenia projektu");
        }
    }

    private void RequestSelectedProjectHandoff()
    {
        try
        {
            _projects = LiteProjectRegistry.OpenDefault();
            var project = _projects.Projects.FirstOrDefault(p =>
                p.Id == _selectedProjectId)
                ?? throw new InvalidOperationException("Wybierz projekt.");
            var lead = project.Chats.SingleOrDefault(c => c.Role == "lead")
                ?? throw new InvalidOperationException(
                    "Projekt nie ma jednego czatu wykonawczego.");
            var wakes = LiteChatWakeRegistry.Read();
            var wake = wakes.SingleOrDefault(w => w.Url == lead.Url)
                ?? throw new InvalidOperationException(
                    "W tym projekcie nie ma harmonogramu wykonawcy.");
            if (!LiteChatHandoff.Request(wake, manual: true,
                isRegisteredProjectLead: true))
                throw new InvalidOperationException(
                    "Prośba o HANDOFF już czeka, została wysłana albo autopraca jest wyłączona.");
            wake.DueUtc = DateTimeOffset.UtcNow;
            LiteChatWakeRegistry.Write(wakes);
            LiteEventLog.Write("project_handoff_requested", new {
                projectChatCount = project.Chats.Count,
                ownerRequested = true
            });
            MessageBox.Show(this,
                "Przy następnym BEZPIECZNYM wybudzeniu wykonawca otrzyma " +
                "jednorazową prośbę o zapis handoffu. Nie wysyłam teraz " +
                "drugiej wiadomości i nie tworzę nowego czatu.",
                "Open Browser — rotacja czatu",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            RenderProjects();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("project_handoff_request_failed",
                new { errorType = ex.GetType().Name });
            MessageBox.Show(this, ex.Message, "Open Browser — rotacja czatu");
        }
    }

    private void TakeOverSelectedProjectChat()
    {
        try
        {
            _projects = LiteProjectRegistry.OpenDefault();
            var project = _projects.Projects.FirstOrDefault(p =>
                p.Id == _selectedProjectId)
                ?? throw new InvalidOperationException("Wybierz projekt.");
            var oldLead = project.Chats.SingleOrDefault(c => c.Role == "lead")
                ?? throw new InvalidOperationException("Brak starego wykonawcy.");
            string newUrl = LiteDevWakeState.NormalizeUrl(
                _browser.CoreWebView2?.Source ?? "");
            var plans = LiteChatWakeRegistry.Read();
            var old = plans.SingleOrDefault(w => w.Url == oldLead.Url)
                ?? throw new InvalidOperationException("Brak poprzedniego harmonogramu.");
            if (!LiteChatHandoff.CanTakeOver(old, newUrl))
                throw new InvalidOperationException(
                    "Handoff nie jest gotowy lub stary wykonawca nadal pracuje. " +
                    "Wymagane: jednorazowa prośba, stabilna odpowiedź " +
                    "z [OB-STATUS: WAIT_USER] i nowy rzeczywisty URL czatu.");
            var next = project.Chats.FirstOrDefault(c => c.Url == newUrl);
            if (next is null)
                next = _projects.AddChat(project.Id, "Wykonawca po handoff", newUrl);
            // Fail closed: old schedule already disabled by WAIT_USER. Change
            // lead role before arming new one. A partial failure never allows
            // two project executors to run concurrently.
            _projects.PromoteLead(project.Id, next.Id);
            var prompt = old.Prompt.Trim();
            const string suffix = "\nNowy czat wykonawczy po rotacji: odczytaj " +
                "najnowszy HANDOFF oraz checkpoint projektu, potwierdź " +
                "przejęcie i kontynuuj sekwencyjnie. Stary czat jest archiwum.";
            if (prompt.Length + suffix.Length > 16000)
                prompt = prompt[..(16000 - suffix.Length)];
            LiteChatWakeRegistry.Configure(plans, newUrl,
                old.EffectiveMinutes, prompt + suffix, DateTimeOffset.UtcNow);
            if (!LiteProjectRegistry.CanScheduleProjectLeads(
                _projects.Projects, plans))
                throw new InvalidOperationException(
                    "Zabronione dwóch aktywnych wykonawców projektu.");
            LiteChatWakeRegistry.Write(plans);
            LiteEventLog.Write("project_handoff_new_lead_armed", new {
                oldChatArchived = true, newChatScheduled = true
            });
            RenderProjects();
            UpdateMultiWakeStatus();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("project_handoff_takeover_failed",
                new { errorType = ex.GetType().Name });
            MessageBox.Show(this, ex.Message, "Open Browser — przejęcie czatu");
        }
    }

    private void PinCurrentChat()
    {
        try
        {
            var core = _browser.CoreWebView2;
            if (_projects is null || _selectedProjectId is null || core is null)
                throw new InvalidOperationException("Wybierz projekt i poczekaj na ChatGPT");
            string title = core.DocumentTitle;
            if (title.Length > 100) title = title.Substring(0, 100);
            _projects.AddChat(_selectedProjectId, title, core.Source);
            LiteEventLog.Write("project_chat_pinned");
            RenderProjects();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Przypinanie czatu");
        }
    }
}
