using System.Diagnostics;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

public sealed partial class MainForm
{
    private readonly WebView2 _webView = new();
    private readonly Button _hideWebButton = new() { Text = "Hide web", Width = 110 };
    private readonly Button _testInjectButton = new() { Text = "Test inject", Width = 110, BackColor = Color.LightYellow };
    private readonly Label _status = new() { AutoSize = true, Text = "..." };

    private bool _webHidden;

    private void InitializeUi()
    {
        Text = AppConstants.AppTitleWithBuild;
        Width = 1500;
        Height = 950;
        StartPosition = FormStartPosition.CenterScreen;

        try
        {
            var pngPath = Path.Combine(AppContext.BaseDirectory, "app_icon.png");
            if (File.Exists(pngPath))
            {
                var bmp = new Bitmap(pngPath);
                Icon = Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch { }

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };
        panel.Controls.Add(_hideWebButton);
        panel.Controls.Add(_testInjectButton);
        panel.Controls.Add(_status);

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);
        Controls.Add(panel);
        BuildOutputPanel();
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

    // ---- Output panel ----

    private readonly Panel _outputPanel = new()
    {
        Dock = DockStyle.Bottom,
        Height = 160,
        Visible = false,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _outputTitle = new()
    {
        Text = "Output",
        AutoSize = true,
        Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold),
        Location = new Point(8, 4)
    };
    private readonly TextBox _outputResult = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Location = new Point(8, 26),
        Width = 1200,
        Height = 90,
        Font = new Font(FontFamily.GenericMonospace, 9),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button _copyOutputButton = new()
    {
        Text = "Copy output",
        Width = 110,
        Location = new Point(8, 124),
        Enabled = false
    };

    private void BuildOutputPanel()
    {
        _outputPanel.Controls.Add(_outputTitle);
        _outputPanel.Controls.Add(_outputResult);
        _outputPanel.Controls.Add(_copyOutputButton);
        Controls.Add(_outputPanel);
    }

    public void ShowOutputResult(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowOutputResult(text)));
            return;
        }
        PipelineRawDump.Write("09_MainFormUi.txt", text);
        _outputResult.Text = text;
        _outputResult.SelectionStart = 0;
        _outputResult.SelectionLength = 0;
        _copyOutputButton.Enabled = true;
        _outputPanel.Visible = true;
    }
}
