namespace BridgeBrowserAlpha0;

/// <summary>
/// Edit the selected project's active lead notes without navigating its chat.
/// Uses exactly the same LiteChatNotes store/key as automatic wake delivery.
/// An optimistic content check prevents overwriting edits from another window.
/// </summary>
internal sealed class LiteProjectNotesDialog : Form
{
    private readonly string _projectId;
    private readonly LiteChatNotes _store = new();
    private string _noteKey = "";
    private string _leadUrl = "";
    private string _baseline = "";
    private string _projectKey = "";
    private bool _loading;
    private readonly Label _heading = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 11f, FontStyle.Bold)
    };
    private readonly TextBox _editor = new()
    {
        Dock = DockStyle.Fill, Multiline = true,
        AcceptsReturn = true, AcceptsTab = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Segoe UI", 11f)
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true
    };
    private readonly Button _save = new()
    {
        Text = "Zapisz", Width = 115, Height = 36
    };
    private readonly Button _reload = new()
    {
        Text = "Odswiez z dysku", Width = 145, Height = 36
    };
    private readonly Button _close = new()
    {
        Text = "Zamknij", Width = 110, Height = 36
    };

    internal LiteProjectNotesDialog(string projectId)
    {
        _projectId = projectId;
        Text = "Open Browser Lite | Notatki projektu";
        Width = 770;
        Height = 580;
        MinimumSize = new Size(570, 390);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10f);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(20, 16, 20, 16)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(_heading, 0, 0);
        layout.Controls.Add(_editor, 0, 1);
        layout.Controls.Add(_status, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(_close);
        buttons.Controls.Add(_save);
        buttons.Controls.Add(_reload);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);

        _editor.TextChanged += (_, _) =>
        {
            if (!_loading)
                _status.Text = _editor.Text == _baseline
                    ? "Brak niezapisanych zmian."
                    : "Zmiany oczekuja na zapis.";
        };
        _save.Click += (_, _) => SaveNotes();
        _reload.Click += (_, _) =>
        {
            if (ConfirmDiscard()) LoadNotes();
        };
        _close.Click += (_, _) => Close();
        FormClosing += (_, e) =>
        {
            if (!ConfirmDiscard()) e.Cancel = true;
        };
        Load += (_, _) => LoadNotes();
    }

    private bool ConfirmDiscard()
    {
        if (_editor.Text == _baseline) return true;
        return MessageBox.Show(this,
            "Notatki maja niezapisane zmiany. Odrzucic te zmiany?",
            "Niezapisane notatki",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private void LoadNotes()
    {
        try
        {
            var project = LiteProjectRegistry.OpenDefault().Projects
                .SingleOrDefault(p => p.Id == _projectId)
                ?? throw new InvalidDataException("Nie znaleziono projektu.");
            var lead = project.Chats.SingleOrDefault(c => c.Role == "lead")
                ?? throw new InvalidDataException("Projekt nie ma jednego czatu glownego.");
            _leadUrl = lead.Url;
            _projectKey = project.Key;
            _noteKey = LiteChatNotes.NormalizeKey(_leadUrl);
            if (_noteKey == "inbox")
                throw new InvalidDataException("Bledny identyfikator czatu glownego.");

            string loaded = _store.Load(_noteKey);
            _loading = true;
            try
            {
                _editor.ReadOnly = false;
                _editor.Text = loaded;
                _baseline = loaded;
            }
            finally { _loading = false; }
            _heading.Text = project.Title + " | " + lead.Title
                + "\nTrwale notatki leada. Dolaczane do jego wybudzen.";
            _status.Text = "Wczytano z lokalnego magazynu. Zapisz po zmianach.";
            _save.Enabled = true;
            LiteEventLog.Write("project_notes_opened", new
            {
                projectKey = _projectKey, hasNotes = loaded.Length > 0
            });
        }
        catch (Exception ex)
        {
            _editor.ReadOnly = true;
            _save.Enabled = false;
            _status.Text = "Odczyt nieudany: " + ex.GetType().Name + ": " + ex.Message;
            LiteEventLog.Write("project_notes_open_failed",
                new { errorType = ex.GetType().Name });
        }
    }

    private void SaveNotes()
    {
        try
        {
            if (_editor.ReadOnly || _noteKey == "")
                throw new InvalidOperationException("Notatnik nie jest dostepny.");

            // A rotated lead must never receive edits meant for its predecessor.
            var project = LiteProjectRegistry.OpenDefault().Projects
                .SingleOrDefault(p => p.Id == _projectId)
                ?? throw new InvalidDataException("Projekt nie istnieje.");
            var currentLead = project.Chats.SingleOrDefault(c => c.Role == "lead");
            if (currentLead?.Url != _leadUrl)
                throw new InvalidDataException(
                    "Zmiana leada w trakcie edycji. Skopiuj notatke i otworz notatnik ponownie.");

            // Protect edits made in the old notes sidebar or another dashboard.
            if (_store.Load(_noteKey) != _baseline)
                throw new InvalidDataException(
                    "Notatka zostala zmieniona w innym oknie. "
                    + "Skopiuj swoje zmiany przed odswiezeniem.");
            if (_editor.Text == _baseline)
            {
                _status.Text = "Brak zmian do zapisania.";
                return;
            }

            _store.Save(_noteKey, _editor.Text);
            if (_store.Load(_noteKey) != _editor.Text)
                throw new IOException("Odczyt po zapisie nie zgadza sie z edytorem.");
            _baseline = _editor.Text;
            _status.Text = "Zapisano " + DateTime.Now.ToString("HH:mm:ss")
                + ". Zmiany beda dolaczane do wybudzen tego leada.";
            LiteEventLog.Write("project_notes_saved", new
            {
                projectKey = _projectKey,
                length = _baseline.Length, verified = true
            });
        }
        catch (Exception ex)
        {
            _status.Text = "Nie zapisano: " + ex.GetType().Name
                + ": " + ex.Message;
            LiteEventLog.Write("project_notes_save_failed", new
            {
                projectKey = _projectKey,
                errorType = ex.GetType().Name
            });
        }
    }
}
