using System.Runtime.InteropServices;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte virtualKey, byte scan, uint flags, UIntPtr extra);

    private readonly LiteChatNotes _noteStore = new();
    private readonly LiteNotesOutbox _notesOutbox = new();
    private readonly TextBox _noteEditor = new()
    {
        Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
        AcceptsReturn = true, AcceptsTab = true,
        Font = new Font("Segoe UI", 11f),
        PlaceholderText = "Notuj obserwacje i pomysły. Zapis automatyczny."
    };
    private readonly Label _noteState = new()
    {
        AutoSize = false, Dock = DockStyle.Bottom, Height = 54,
        Text = "Notatki są lokalne; automatyczne wysyłanie do czatu jest osobnym etapem."
    };
    private readonly Label _noteChat = new()
    {
        Dock = DockStyle.Top, Height = 44, AutoEllipsis = true,
        Text = "Notatki bieżącego czatu", Padding = new Padding(8, 8, 0, 0)
    };
    private readonly System.Windows.Forms.Timer _noteSaveTimer = new() { Interval = 850 };
    private string _noteKey = "inbox";
    private bool _notesLoading;
    private bool _notesDirty;

    private Control CreateNotesUi()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        var header = new Label
        {
            Text = "Notatki i dyktowanie", Dock = DockStyle.Top, Height = 40,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 78, WrapContents = true, AutoScroll = true
        };
        var dictate = new Button { Text = "🎤 Dyktuj (Win+H)", Width = 145, Height = 32 };
        dictate.Click += (_, _) =>
        {
            _noteEditor.Focus();
            StartWindowsDictation();
            LiteEventLog.Write("note_dictation_requested");
        };
        var copy = new Button { Text = "Kopiuj do czatu", Width = 134, Height = 32 };
        copy.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_noteEditor.Text)) return;
            FlushNotes();
            Clipboard.SetText("Informacje od Pawła:\n" + _noteEditor.Text);
            _noteState.Text = "Skopiowano. Możesz wkleić do czatu; nie wysłano automatycznie.";
            LiteEventLog.Write("notes_copied_to_clipboard");
        };
        var queue = new Button { Text = "Do kolejki", Width = 106, Height = 32 };
        queue.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_noteEditor.Text)) return;
            FlushNotes();
            try
            {
                var item = _notesOutbox.EnqueueDraft(_noteKey, _noteEditor.Text);
                _noteState.Text = item is null
                    ? "Treść już jest w kolejce; nie wysłano do ChatGPT."
                    : "Dodano do kolejki lokalnej; nie wysłano do ChatGPT.";
                LiteEventLog.Write("notes_outbox_queued", new { newItem = item is not null });
            }
            catch (Exception ex)
            {
                _noteState.Text = "Błąd kolejki. Notatka pozostała w edytorze.";
                LiteEventLog.Write("notes_outbox_failed", new { errorType = ex.GetType().Name });
            }
        };
        var save = new Button { Text = "Zapisz", Width = 78, Height = 32 };
        save.Click += (_, _) => FlushNotes();
        actions.Controls.Add(dictate);
        actions.Controls.Add(copy);
        actions.Controls.Add(queue);
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
            _notesDirty = false;
            _noteState.Text = "Zapis lokalny, zaszyfrowany. Nie przekazano do modelu.";
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
            _noteStore.Save(_noteKey, _noteEditor.Text);
            _notesDirty = false;
            _noteState.Text = "Zapisano lokalnie • " + DateTime.Now.ToString("HH:mm:ss") +
                ". Oczekuje na ręczne przekazanie.";
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
