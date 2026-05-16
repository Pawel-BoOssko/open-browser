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

    private void SetDiagnostics(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetDiagnostics(text)));
            return;
        }
        _diagnostics.Text = text;
    }

    // ---- Runtime approval panel ----

    private readonly Panel _approvalPanel = new()
    {
        Dock = DockStyle.Bottom,
        Height = 200,
        Visible = false,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _approvalTitle = new()
    {
        Text = "Pending CC Command — Operator Approval Required",
        AutoSize = true,
        Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold),
        Location = new Point(8, 4)
    };
    private readonly Label _approvalDetails = new()
    {
        AutoSize = true,
        Location = new Point(8, 26),
        MaximumSize = new Size(1200, 52)
    };
    private readonly Label _approvalWarning = new()
    {
        Text = "DryRun only. No Claude Code process will be launched from runtime.",
        AutoSize = true,
        ForeColor = Color.DarkOrange,
        Location = new Point(8, 84)
    };
    private readonly Button _approveButton = new()
    {
        Text = "Approve (DryRun)",
        Width = 140,
        Location = new Point(8, 110),
        BackColor = Color.LightGreen,
        Enabled = false
    };
    private readonly Button _rejectButton = new()
    {
        Text = "Reject",
        Width = 100,
        Location = new Point(156, 110),
        Enabled = false
    };
    private readonly Button _copyDetailsButton = new()
    {
        Text = "Copy details",
        Width = 110,
        Location = new Point(264, 110),
        Enabled = false
    };
    private readonly Label _approvalResult = new()
    {
        AutoSize = true,
        Location = new Point(8, 142),
        MaximumSize = new Size(1200, 30),
        ForeColor = Color.DarkBlue
    };
    private readonly Button _copyResultButton = new()
    {
        Text = "Copy result",
        Width = 110,
        Location = new Point(8, 170),
        Enabled = false,
        Visible = false
    };

    private void BuildApprovalPanel()
    {
        _approvalPanel.Controls.Add(_approvalTitle);
        _approvalPanel.Controls.Add(_approvalDetails);
        _approvalPanel.Controls.Add(_approvalWarning);
        _approvalPanel.Controls.Add(_approveButton);
        _approvalPanel.Controls.Add(_rejectButton);
        _approvalPanel.Controls.Add(_copyDetailsButton);
        _approvalPanel.Controls.Add(_approvalResult);
        _approvalPanel.Controls.Add(_copyResultButton);
        Controls.Add(_approvalPanel);
    }

    public void ShowPendingCommand(string summary)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowPendingCommand(summary)));
            return;
        }
        _approvalDetails.Text = summary;
        _approvalResult.Text = "";
        _approveButton.Enabled = true;
        _rejectButton.Enabled = true;
        _copyDetailsButton.Enabled = true;
        _copyResultButton.Visible = false;
        _approvalPanel.Visible = true;
    }

    public void HidePendingCommand()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(HidePendingCommand));
            return;
        }
        _approvalPanel.Visible = false;
        _approveButton.Enabled = false;
        _rejectButton.Enabled = false;
        _copyDetailsButton.Enabled = false;
        _copyResultButton.Visible = false;
        _approvalDetails.Text = "";
        _approvalResult.Text = "";
    }

    public void ShowApprovalResult(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowApprovalResult(text)));
            return;
        }
        _approvalResult.Text = text;
        _approveButton.Enabled = false;
        _rejectButton.Enabled = false;
        _copyResultButton.Visible = true;
        _copyResultButton.Enabled = true;
    }
}
