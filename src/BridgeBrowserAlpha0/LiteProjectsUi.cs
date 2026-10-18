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
        Width = 172, PlaceholderText = "Nazwa projektu"
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
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

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
        var pin = new Button
        {
            Text = "Przypnij bieżący czat", Dock = DockStyle.Fill,
            Padding = new Padding(3)
        };
        pin.Click += (_, _) => PinCurrentChat();
        container.Controls.Add(pin, 0, 3);

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
        _projectList.SuspendLayout();
        try
        {
            while (_projectList.Controls.Count > 0)
            {
                Control control = _projectList.Controls[0];
                _projectList.Controls.RemoveAt(0);
                control.Dispose();
            }

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
                    var row = new Panel { Width = 238, Height = 31 };
                    var link = new Button
                    {
                        Width = 179, Height = 29, Left = 8, Top = 0,
                        TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                        Text = (chat.Role == "lead" ? "★ " : "  ") + chat.Title
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
                            _projects.PromoteLead(projectId, chatId);
                            LiteEventLog.Write("project_lead_changed");
                            RenderProjects();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Open Browser — projekty");
                        }
                    };
                    row.Controls.Add(link);
                    row.Controls.Add(makeLead);
                    _projectList.Controls.Add(row);
                }
            }
        }
        finally
        {
            _projectList.ResumeLayout();
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
