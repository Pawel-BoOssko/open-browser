using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

// Read-only, on-demand project handoff preview for the OBL project.
// It never changes the selected lead, sends a message, or modifies wake state.
internal sealed class LiteHandoffPreviewUi : Form
{
    private const string HandoffPath =
        @"D:\projects\open-browser\docs\OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md";
    private const string CheckpointPath =
        @"D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md";
    private readonly Label _status = new()
    {
        Dock = DockStyle.Top, Height = 60, AutoEllipsis = true
    };
    private readonly TextBox _text = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        WordWrap = true, ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 10f)
    };

    internal LiteHandoffPreviewUi()
    {
        Text = "Open Browser Lite: przekazanie i wznowienie pracy";
        Width = 1020;
        Height = 720;
        MinimumSize = new Size(650, 420);
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 43, WrapContents = false
        };
        var handoff = new Button { Text = "HANDOFF", Width = 115 };
        var current = new Button { Text = "Aktualny checkpoint", Width = 175 };
        var copy = new Button { Text = "Kopiuj widok", Width = 135 };
        var resume = new Button { Text = "Kopiuj wznowienie", Width = 165 };
        var export = new Button { Text = "Zapisz wznowienie", Width = 165 };
        handoff.Click += (_, _) => LoadFile(HandoffPath, true);
        current.Click += (_, _) => LoadFile(CheckpointPath, false);
        copy.Click += (_, _) =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_text.Text))
                    Clipboard.SetText(_text.Text);
                _status.Text = "Skopiowano wyswietlany dokument.";
            }
            catch (Exception ex)
            {
                _status.Text = "Blad kopiowania: " + ex.GetType().Name;
            }
        };
        resume.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(LiteHandoffResumePacket.Compose());
                _status.Text = "Skopiowano aktualna instrukcje wznowienia.";
            }
            catch (Exception ex)
            {
                _status.Text = "Blad przygotowania wznowienia: " + ex.GetType().Name;
                LiteEventLog.Write("handoff_resume_packet_failed",
                    new { errorType = ex.GetType().Name });
            }
        };
        export.Click += (_, _) =>
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Zapisz instrukcje wznowienia OBL",
                FileName = "OpenBrowserLite_resume_" +
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".md",
                Filter = "Dokument Markdown (*.md)|*.md",
                DefaultExt = "md", AddExtension = true,
                CheckPathExists = true, OverwritePrompt = true,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string sha = LiteHandoffResumePacket.ExportVerified(dialog.FileName);
                _status.Text = "Zapisano i odczytano plik wznowienia. SHA256: " +
                    sha[..16] + "...";
                LiteEventLog.Write("handoff_resume_exported",
                    new { shaPrefix = sha[..16] });
            }
            catch (Exception ex)
            {
                _status.Text = "Blad zapisu wznowienia: " + ex.GetType().Name;
                LiteEventLog.Write("handoff_resume_export_failed",
                    new { errorType = ex.GetType().Name });
            }
        };
        toolbar.Controls.AddRange(new Control[] { handoff, current, copy, resume, export });
        Controls.Add(_text);
        Controls.Add(toolbar);
        Controls.Add(_status);
        Load += (_, _) => LoadFile(HandoffPath, true);
    }

    private static string? Value(string text, string label) =>
        text.Split('\n').Select(s => s.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith(label, StringComparison.Ordinal))
            ?[label.Length..].Trim();

    private void LoadFile(string path, bool isHandoff)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= 0 || file.Length > 1_048_576)
                throw new InvalidDataException("File absent, empty, or over 1 MiB");
            string content = File.ReadAllText(path, Encoding.UTF8);
            _text.Text = content;
            if (!isHandoff)
            {
                _status.Text = "Aktualny checkpoint projektu | " +
                    file.LastWriteTime.ToString("yyyy-MM-dd HH:mm") +
                    " | dokument lokalny, tylko odczyt";
                return;
            }
            bool validFormat = content.Contains(
                "Format: OBL_HANDOFF_SNAPSHOT_V2", StringComparison.Ordinal);
            string last = Value(content, "Last wake: ") ?? "?";
            string saved = Value(content, "Saved UTC: ") ?? "?";
            string recordedHash = Value(content, "Checkpoint SHA256: ") ?? "";
            string freshness = "Brak aktualnego checkpointu";
            if (File.Exists(CheckpointPath))
            {
                string actualHash = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(CheckpointPath)))
                    .ToLowerInvariant();
                freshness = string.Equals(recordedHash, actualHash,
                    StringComparison.OrdinalIgnoreCase)
                    ? "Checkpoint zgodny z migawka"
                    : "Migawka historyczna: sprawdz aktualny checkpoint";
            }
            _status.Text = (validFormat ? "HANDOFF" : "NIEZNANY FORMAT HANDOFF") +
                " | wake " + last + " | zapis UTC " + saved +
                " | " + freshness;
        }
        catch (Exception ex)
        {
            _text.Clear();
            _status.Text = "Nie mozna odczytac dokumentu: " +
                ex.GetType().Name;
            LiteEventLog.Write("handoff_preview_load_failed",
                new { errorType = ex.GetType().Name });
        }
    }
}