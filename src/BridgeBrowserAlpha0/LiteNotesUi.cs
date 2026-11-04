using System.Runtime.InteropServices;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte virtualKey, byte scan, uint flags, UIntPtr extra);

    private readonly LiteChatNotes _noteStore = new();
    private readonly TextBox _noteEditor = new()
    {
        Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
        AcceptsReturn = true, AcceptsTab = true,
        Font = new Font("Segoe UI", 11f),
        PlaceholderText = "Notatki trwałe — dołączane do każdego wybudzenia. Zostają, dopóki ich nie zmienisz."
    };
    private readonly Label _noteState = new()
    {
        AutoSize = false, Dock = DockStyle.Bottom, Height = 54,
        Text = "TRWAŁE notatki tego czatu. Dołączane do kolejnych wybudzeń; NIE są automatycznie dopisywane do checkpointu."
    };
    private readonly Label _noteChat = new()
    {
        Dock = DockStyle.Top, Height = 44, AutoEllipsis = true,
        Text = "Trwałe notatki bieżącego czatu", Padding = new Padding(8, 8, 0, 0)
    };
    internal const int NotesDebounceMs = 3000;
    private readonly System.Windows.Forms.Timer _noteSaveTimer = new() { Interval = NotesDebounceMs };
    private string _noteKey = "inbox";
    // Snapshot used to prevent overwriting edits saved in another notes window.
    private string _noteBaseline = "";
    private bool _notesLoading;
    private bool _notesDirty;

    private Control CreateNotesUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        var header = new Label
        {
            Text = "Notatki trwałe i dyktowanie", Dock = DockStyle.Top, Height = 40,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 44, WrapContents = false, AutoScroll = false
        };
        var dictate = new Button { Text = "🎤 Dyktuj (Win+H)", Width = 145, Height = 32 };
        dictate.Click += (_, _) =>
        {
            _noteEditor.Focus();
            StartWindowsDictation();
            LiteEventLog.Write("note_dictation_requested");
        };
        var save = new Button { Text = "Zapisz", Width = 78, Height = 32 };
        save.Click += (_, _) => FlushNotes();
        actions.Controls.Add(dictate);
        actions.Controls.Add(save);

        _noteEditor.TextChanged += (_, _) =>
        {
            if (_notesLoading) return;
            _notesDirty = true;
            _noteState.Text = "Zmiany oczekują na zapis...";
            _noteSaveTimer.Stop();
            _noteSaveTimer.Start();
        };
        _noteSaveTimer.Tick += (_, _) =>
        {
            _noteSaveTimer.Stop();
            FlushNotes();
        };
        root.Controls.Add(_noteEditor);
        root.Controls.Add(_noteState);
        root.Controls.Add(actions);
        root.Controls.Add(_noteChat);
        root.Controls.Add(header);
        SwitchNotesForUrl(null);
        return root;
    }

    private void SwitchNotesForUrl(string? url)
    {
        string target = LiteChatNotes.NormalizeKey(url);
        if (target == _noteKey && !_notesLoading) return;
        if (!FlushNotes())
        {
            _noteState.Text = "Nie można zmienić notatnika: poprzednia notatka nie została zapisana.";
            return;
        }
        _noteKey = target;
        _notesLoading = true;
        try
        {
            string text = _noteStore.Load(target);
            _noteEditor.ReadOnly = false;
            _noteEditor.Text = text;
            _noteBaseline = text;
            _notesDirty = false;
            _noteState.Text = target == "inbox"
                ? "Wczytano notatki ogólne. Nie do checkpointu ani do wybudzeń bez przypisanego czatu."
                : "Wczytano notatki trwałe. Dołączane przy każdym wybudzeniu; NIE dopisywane do checkpointu.";
            _noteChat.Text = target == "inbox"
                ? "Notatki ogólne (bez przypisanego czatu)"
                : "Notatki: " + target.Replace("chatgpt.com", "");
        }
        catch (Exception ex)
        {
            _noteEditor.Clear();
            _noteState.Text = "Błąd odczytu. Dane NIE zostały nadpisane: " + ex.GetType().Name;
            _noteEditor.ReadOnly = true;
            LiteEventLog.Write("notes_load_failed", new { errorType = ex.GetType().Name });
        }
        finally { _notesLoading = false; }
    }

    private bool FlushNotes()
    {
        _noteSaveTimer.Stop();
        if (!_notesDirty || _noteEditor.ReadOnly) return true;
        try
        {
            // Project dashboard can edit the same note; never overwrite newer
            // on-disk text from a stale inspector editor.
            if (_noteStore.Load(_noteKey) != _noteBaseline)
            {
                _noteState.Text = "Konflikt zapisu: notatka zmieniona w innym oknie. Skopiuj swoj tekst przed odswiezeniem.";
                LiteEventLog.Write("notes_conflict_external_update");
                return false;
            }
            _noteStore.Save(_noteKey, _noteEditor.Text);
            if (_noteStore.Load(_noteKey) != _noteEditor.Text)
                throw new IOException("Odczyt po zapisie notatki nie zgadza sie z edytorem.");
            _noteBaseline = _noteEditor.Text;
            _notesDirty = false;
            _noteState.Text = SavedNotesStatus(DateTime.Now.ToString("HH:mm:ss"));
            LiteEventLog.Write("notes_saved", new { length = _noteEditor.TextLength });
            return true;
        }
        catch (Exception ex)
        {
            _noteState.Text = "Błąd zapisu! Notatki nadal pozostają w oknie.";
            LiteEventLog.Write("notes_save_failed", new { errorType = ex.GetType().Name });
            return false;
        }
    }

    internal static string SavedNotesStatus(string time) =>
        "Zapisano lokalnie • " + time + ". Trwała notatka do wybudzeń; NIE do checkpointu.";

    private static void StartWindowsDictation()
    {
        const byte vkWin = 0x5B;
        const byte vkH = 0x48;
        const uint keyUp = 0x0002;
        keybd_event(vkWin, 0, 0, UIntPtr.Zero);
        keybd_event(vkH, 0, 0, UIntPtr.Zero);
        keybd_event(vkH, 0, keyUp, UIntPtr.Zero);
        keybd_event(vkWin, 0, keyUp, UIntPtr.Zero);
    }
}
