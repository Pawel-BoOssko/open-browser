using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Manual recovery viewer for locally archived, DPAPI-protected answer fragments.
/// Reading does not change any ChatGPT web conversation.
/// </summary>
internal sealed class LiteArchiveViewer : Form
{
    private readonly ListBox _files = new() { Dock = DockStyle.Fill };
    private readonly TextBox _text = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Both, WordWrap = true
    };
    private readonly Label _detail = new()
    {
        Dock = DockStyle.Top, Height = 32, Text = "Wybierz kopię odpowiedzi"
    };
    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "archive");

    internal LiteArchiveViewer()
    {
        Text = "Open Browser — lokalne kopie odpowiedzi";
        Width = 1100;
        Height = 720;
        MinimumSize = new Size(700, 450);

        var layout = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            Size = new Size(1080, 680)
        };
        layout.Panel1MinSize = 180;
        layout.Panel2MinSize = 300;
        layout.SplitterDistance = 290;
        layout.Panel1.Controls.Add(_files);

        var textPanel = new Panel { Dock = DockStyle.Fill };
        var copy = new Button
        {
            Text = "Skopiuj widoczny fragment", Dock = DockStyle.Bottom, Height = 38
        };
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_text.Text))
                Clipboard.SetText(_text.Text);
        };
        textPanel.Controls.Add(_text);
        textPanel.Controls.Add(_detail);
        textPanel.Controls.Add(copy);
        layout.Panel2.Controls.Add(textPanel);
        Controls.Add(layout);

        _files.SelectedIndexChanged += (_, _) => ShowSelected();
        Load += (_, _) => RefreshFiles();
    }

    private void RefreshFiles()
    {
        _files.Items.Clear();
        if (!Directory.Exists(_folder)) return;
        foreach (var file in new DirectoryInfo(_folder)
            .GetFiles("*.dpapi").OrderByDescending(f => f.LastWriteTimeUtc).Take(300))
            _files.Items.Add(file);
        _detail.Text = $"{_files.Items.Count} ostatnich wersji (tylko lokalnie)";
    }

    private void ShowSelected()
    {
        if (_files.SelectedItem is not FileInfo file) return;
        try
        {
            if (file.Length > 1_000_000)
                throw new InvalidDataException("Zbyt duży zaszyfrowany fragment");
            var encrypted = File.ReadAllBytes(file.FullName);
            var decrypted = LiteSecureArchive.Unprotect(encrypted);
            try { _text.Text = Encoding.UTF8.GetString(decrypted); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(decrypted); }
            _detail.Text = $"Zapis: {file.LastWriteTime:yyyy-MM-dd HH:mm:ss} • {_text.TextLength} znaków";
        }
        catch (Exception ex)
        {
            _text.Clear();
            _detail.Text = "Nie można odszyfrować kopii (" + ex.GetType().Name + ")";
        }
    }
}
