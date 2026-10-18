namespace BridgeBrowserAlpha0;

/// <summary>
/// Read-only review of queued notes. Copying never submits a web prompt.
/// Delivery remains a separately authorized and verified operation.
/// </summary>
internal sealed class LiteOutboxWindow : Form
{
    private readonly LiteNotesOutbox _outbox = new();
    private readonly string _chatKey;
    private readonly ListBox _items = new() { Dock = DockStyle.Fill };
    private readonly TextBox _preview = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Both, WordWrap = true
    };
    private readonly Label _label = new()
    {
        Text = "Kolejka — żadne wiadomości nie są wysyłane automatycznie",
        Dock = DockStyle.Top, Height = 42
    };
    private List<LiteNotesOutbox.Item> _queue = new();

    internal LiteOutboxWindow(string chatKey)
    {
        _chatKey = chatKey;
        Text = "Open Browser — kolejka notatek";
        Width = 940;
        Height = 580;
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            Size = new Size(920, 550)
        };
        root.Panel1MinSize = 170;
        root.Panel2MinSize = 260;
        root.SplitterDistance = 320;
        root.Panel1.Controls.Add(_items);
        var right = new Panel { Dock = DockStyle.Fill };
        var copy = new Button
        {
            Dock = DockStyle.Bottom, Height = 44,
            Text = "Kopiuj wybraną notatkę — BEZ wysyłania"
        };
        copy.Click += (_, _) =>
        {
            if (_items.SelectedIndex < 0 ||
                _items.SelectedIndex >= _queue.Count) return;
            Clipboard.SetText("Informacje od Pawła:\n" +
                _queue[_items.SelectedIndex].Text);
            _label.Text = "Skopiowano do schowka, nie wysłano do ChatGPT.";
            LiteEventLog.Write("outbox_item_copied");
        };
        right.Controls.Add(_preview);
        right.Controls.Add(copy);
        right.Controls.Add(_label);
        root.Panel2.Controls.Add(right);
        Controls.Add(root);
        _items.SelectedIndexChanged += (_, _) =>
        {
            int index = _items.SelectedIndex;
            _preview.Text = index >= 0 && index < _queue.Count
                ? _queue[index].Text : "";
        };
        Load += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        try
        {
            _queue = _outbox.List(_chatKey).ToList();
            _items.Items.Clear();
            foreach (var entry in _queue)
            {
                _items.Items.Add($"{entry.CreatedUtc.LocalDateTime:yyyy-MM-dd HH:mm} " +
                    $"[{entry.Status}] {entry.Text.Length} znaków");
            }
            _label.Text = $"{_queue.Count} zapisanych elementów. " +
                "Żaden nie został wysłany z tego okna.";
        }
        catch (Exception ex)
        {
            _preview.Text = "";
            _label.Text = "Nie udało się odczytać kolejki: " + ex.GetType().Name;
            LiteEventLog.Write("outbox_view_failed", new { errorType = ex.GetType().Name });
        }
    }
}
