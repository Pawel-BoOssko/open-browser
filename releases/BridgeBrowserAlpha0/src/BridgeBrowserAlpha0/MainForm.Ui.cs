using System.Diagnostics;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

public sealed partial class MainForm
{
    private readonly WebView2 _webView = new();
    private readonly Button _newLogButton = new() { Text = "New log", Width = 110 };
    private readonly Button _openLogsButton = new() { Text = "Open logs", Width = 105 };
    private readonly Button _openExtractedButton = new() { Text = "Extracted", Width = 105 };
    private readonly Button _exportRedactedButton = new() { Text = "Export", Width = 95 };
    private readonly Button _loadTrimmerButton = new() { Text = "Load trim", Width = 100 };
    private readonly Button _promoteTrimmerButton = new() { Text = "Promote trim", Width = 120 };
    private readonly Button _trimmerStatusButton = new() { Text = "Trim status", Width = 110 };
    private readonly Button _hideWebButton = new() { Text = "Hide web", Width = 110 };
    private readonly Label _status = new() { AutoSize = true, Text = "Starting..." };
    private readonly TextBox _diagnostics = new()
    {
        Dock = DockStyle.Bottom,
        Height = 120,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font(FontFamily.GenericMonospace, 9)
    };
    
    private bool _webHidden;

    private void InitializeUi()
    {
        Text = AppConstants.AppTitleWithVersion;
        Width = 1500;
        Height = 950;
        StartPosition = FormStartPosition.CenterScreen;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 92,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };
        panel.Controls.Add(_newLogButton);
        panel.Controls.Add(_openLogsButton);
        panel.Controls.Add(_openExtractedButton);
        panel.Controls.Add(_exportRedactedButton);
        panel.Controls.Add(_loadTrimmerButton);
        panel.Controls.Add(_promoteTrimmerButton);
        panel.Controls.Add(_trimmerStatusButton);
        panel.Controls.Add(_hideWebButton);
        panel.Controls.Add(_status);

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);
        Controls.Add(_diagnostics);
        Controls.Add(panel);
    }

    private void ToggleWebVisibility()
    {
        _webHidden = !_webHidden;
        _webView.Visible = !_webHidden;
        _hideWebButton.Text = _webHidden ? "Show web" : "Hide web";
        _log.WriteRun("app", "web_visibility", "ok", _webHidden ? "WebView hidden" : "WebView visible");
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetStatus(text)));
            return;
        }
        _status.Text = text;
    }

    private void SetDiagnostics(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetDiagnostics(text)));
            return;
        }
        _diagnostics.Text = text;
    }
}
