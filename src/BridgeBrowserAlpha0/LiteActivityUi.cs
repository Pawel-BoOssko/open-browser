using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Read-only view of the existing engine-owned mechanical journal and
/// privacy-filtered Lite diagnostics. No new storage or scheduler.
/// </summary>
internal sealed partial class ChatGptLiteForm
{
    private readonly System.Windows.Forms.Timer _activityTimer =
        new() { Interval = 12000 };
    private readonly TextBox _activityView = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, WordWrap = false,
        Font = new Font("Consolas", 9.5f),
        BackColor = Color.FromArgb(251, 252, 254)
    };
    private readonly Label _activityInfo = new()
    {
        Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 4, 0),
        Text = "Ostatnie wybudzenia i zdarzenia • tylko odczyt"
    };
    private TabControl? _inspectorTabs;
    private bool _activityShowAll;

    private Control CreateInspectorUi()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var notes = new TabPage("Notatki");
        notes.Controls.Add(CreateNotesUi());
        var activity = new TabPage("Zdarzenia");
        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 39,
            WrapContents = false, Padding = new Padding(4)
        };
        var refresh = new Button { Text = "Odśwież", Width = 80, Height = 28 };
        refresh.Click += (_, _) => RefreshActivityView();
        var copy = new Button { Text = "Kopiuj", Width = 72, Height = 28 };
        copy.Click += (_, _) =>
        {
            if (_activityView.TextLength > 0)
                Clipboard.SetText(_activityView.Text);
        };
        var all = new CheckBox { Text = "Wszystkie", Width = 102,
            Height = 28, Checked = false };
        all.CheckedChanged += (_, _) =>
        {
            _activityShowAll = all.Checked;
            RefreshActivityView();
        };
        commands.Controls.Add(refresh);
        commands.Controls.Add(copy);
        commands.Controls.Add(all);
        activity.Controls.Add(_activityView);
        activity.Controls.Add(commands);
        activity.Controls.Add(_activityInfo);
        tabs.TabPages.Add(notes);
        tabs.TabPages.Add(activity);
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab == activity) RefreshActivityView();
        };
        _inspectorTabs = tabs;
        _activityTimer.Tick += (_, _) =>
        {
            if (!IsDisposed && _inspectorTabs?.SelectedIndex == 1)
                RefreshActivityView();
        };
        _activityTimer.Start();
        return tabs;
    }

    private static IEnumerable<string> LastLines(string path, int bytes = 65536)
    {
        if (!File.Exists(path)) yield break;
        using var stream = new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long offset = Math.Max(0, stream.Length - bytes);
        stream.Position = offset;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string data = reader.ReadToEnd();
        if (offset > 0)
        {
            int newline = data.IndexOf('\n');
            if (newline < 0) yield break;
            data = data[(newline + 1)..];
        }
        foreach (var line in data.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line)) yield return line;
    }

    internal static string? DescribeActivityLine(string line, bool showAll = false)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("utc", out var utc) ||
                utc.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(utc.GetString(), out var date))
                return null;
            string time = date.ToLocalTime().ToString("dd.MM HH:mm:ss");
            if (root.TryGetProperty("actor", out var actor))
            {
                if (!root.TryGetProperty("stage", out var stageValue) ||
                    stageValue.ValueKind != JsonValueKind.String) return null;
                string stage = stageValue.GetString() ?? "";
                // At two-minute retry intervals 'due/deferred' can bury
                // meaningful events. Keep them accessible under Wszystkie.
                if (!showAll && (stage is "due" or "deferred" or
                    "attempt_reserved" or "submission_attempted"))
                    return null;
                string sequence = root.TryGetProperty("wakeSequence", out var seq) &&
                    seq.ValueKind == JsonValueKind.Number
                    ? "#" + seq.GetInt64().ToString("D6") : "—";
                string label = stage switch
                {
                    "due" => "Termin wybudzenia",
                    "attempt_reserved" => "Przygotowano próbę",
                    "click_invoked" => "Kliknięto Wyślij",
                    "receipt_confirmed" => "Wiadomość widoczna w UI (ACK)",
                    "receipt_uncertain" => "Niepewna wysyłka — bez ponawiania",
                    "chat_started" => "Czat rozpoczął pracę",
                    "chat_checkpoint" => "Zapisano checkpoint",
                    "chat_finished" => "Czat zakończył pracę",
                    "chat_blocked" => "Czat zablokowany",
                    "engine_failure" => "Błąd silnika",
                    "deferred" => "Wybudzenie odroczone",
                    "submission_attempted" => "Próba kliknięcia Wyślij",
                    "definitely_unsent" => "Na pewno niewysłane",
                    _ => ""
                };
                if (label.Length == 0) return null;
                string reason = root.TryGetProperty("result", out var result) &&
                    result.ValueKind == JsonValueKind.String
                    ? result.GetString() ?? "" : "";
                // Engine reasons are short diagnostic codes; keep logs compact.
                if (reason.Length > 54) reason = reason[..54] + "…";
                string receipt = root.TryGetProperty("receipt", out var code) &&
                    code.ValueKind == JsonValueKind.String &&
                    code.GetString() is string id && id.Length == 32
                    ? " " + id[..8] : "";
                return time + "   " + sequence + receipt + "   " + label +
                    (reason.Length > 0 && (stage is "deferred" or "engine_failure")
                        ? " (" + reason.Replace('\r',' ').Replace('\n',' ') + ")" : "");
            }
            string type = root.TryGetProperty("eventName", out var ev)
                ? ev.GetString() ?? "" : "";
            string item = type switch
            {
                "response_error_transition" => "Zmiana stanu błędu odpowiedzi",
                "response_probe_delayed" => "Opóźnienie renderera",
                "dom_probe_timeout" => "Renderer nie odpowiedział",
                "multi_wake_failed" => "Błąd mechanizmu wybudzania",
                "multi_wake_send" => "Wynik próby wybudzenia",
                "owner_intervention_queue_failed" => "Błąd powiadomienia",
                _ => ""
            };
            return item.Length == 0 ? null : time + "   Aplikacja   " + item;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException
            or FormatException or ArgumentException or KeyNotFoundException)
        {
            return null;
        }
    }

    private void RefreshActivityView()
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenBrowser", "ChatGPT-Lite", "diagnostics");
            var items = new List<(DateTimeOffset Utc, string Label)>();
            var files = new List<string> {
                Path.Combine(folder, "wake-lifecycle.jsonl")
            };
            foreach (var date in new[] { DateTime.UtcNow, DateTime.UtcNow.AddDays(-1) })
                files.Add(Path.Combine(folder,
                    "lite-events-" + date.ToString("yyyy-MM-dd") + ".jsonl"));
            foreach (string file in files)
            foreach (string line in LastLines(file))
            {
                string? label = DescribeActivityLine(line, _activityShowAll);
                if (label is null) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (DateTimeOffset.TryParse(
                        doc.RootElement.GetProperty("utc").GetString(), out var dt))
                        items.Add((dt, label));
                }
                catch (Exception ex) when (ex is JsonException or
                    InvalidOperationException or FormatException or KeyNotFoundException) { }
            }
            _activityView.Text = items
                .OrderByDescending(x => x.Utc).Take(32)
                .Aggregate(new StringBuilder(),
                    (builder, x) => builder.AppendLine(x.Label)).ToString();
            _activityInfo.Text = (_activityShowAll
                    ? "Wszystkie etapy" : "Najważniejsze zdarzenia") +
                " • " + DateTime.Now.ToString("HH:mm:ss") +
                " • bez treści rozmów";
        }
        catch (Exception ex)
        {
            _activityInfo.Text = "Błąd podglądu: " + ex.GetType().Name;
            LiteEventLog.Write("activity_view_failed",
                new { errorType = ex.GetType().Name });
        }
    }
}
