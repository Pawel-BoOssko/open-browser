using System.Diagnostics;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

public sealed partial class MainForm
{
    private readonly WebView2 _webView = new();
    private readonly Button _hideWebButton = new() { Text = "Hide web", Width = 110 };
    private readonly Label _status = new() { AutoSize = true, Text = AppConstants.AppVersion };

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
            Height = 36,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };
        panel.Controls.Add(_hideWebButton);
        panel.Controls.Add(_status);

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);
        Controls.Add(panel);
        BuildApprovalPanel();
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

    private readonly Panel _approvalPanel = new()
    {
        Dock = DockStyle.Bottom,
        Height = 180,
        Visible = false,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _approvalTitle = new()
    {
        Text = "OpenBridge",
        AutoSize = true,
        Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold),
        Location = new Point(8, 4)
    };
    private readonly TextBox _approvalDetails = new()
    {
        ReadOnly = true,
        Multiline = false,
        Location = new Point(8, 26),
        Width = 1200,
        BorderStyle = BorderStyle.None,
        BackColor = SystemColors.Control
    };
    private readonly TextBox _approvalResult = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Location = new Point(8, 50),
        Width = 1200,
        Height = 85,
        Font = new Font(FontFamily.GenericMonospace, 9),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button _copyDetailsButton = new()
    {
        Text = "Copy prompt",
        Width = 110,
        Location = new Point(8, 142),
        Enabled = false
    };
    private readonly Button _copyResultButton = new()
    {
        Text = "Copy result",
        Width = 110,
        Location = new Point(126, 142),
        Enabled = false
    };

    private void BuildApprovalPanel()
    {
        _approvalPanel.Controls.Add(_approvalTitle);
        _approvalPanel.Controls.Add(_approvalDetails);
        _approvalPanel.Controls.Add(_approvalResult);
        _approvalPanel.Controls.Add(_copyDetailsButton);
        _approvalPanel.Controls.Add(_copyResultButton);
        Controls.Add(_approvalPanel);
    }

    public void ShowApprovalResult(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowApprovalResult(text)));
            return;
        }
        PipelineRawDump.Write("09_MainFormUi.txt", text);
        _approvalDetails.Text = _runtimeApproval.HasPending ? _runtimeApproval.PendingSummary() : "";
        _approvalResult.Text = text;
        _approvalResult.SelectionStart = 0;
        _approvalResult.SelectionLength = 0;
        _copyDetailsButton.Enabled = true;
        _copyResultButton.Enabled = true;
        _approvalPanel.Visible = true;
    }
}
