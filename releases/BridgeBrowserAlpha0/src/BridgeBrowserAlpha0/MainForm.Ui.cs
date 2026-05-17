using System.Diagnostics;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

public sealed partial class MainForm
{
    private readonly WebView2 _webView = new();
    private readonly Button _hideWebButton = new() { Text = "Hide web", Width = 110 };
    private readonly Label _status = new() { AutoSize = true, Text = "Starting..." };

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

    // ---- Runtime approval panel ----

    private readonly Panel _approvalPanel = new()
    {
        Dock = DockStyle.Bottom,
        Height = 260,
        Visible = false,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _approvalTitle = new()
    {
        Text = "Pending PS Command",
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
    private readonly Label _approvalProcessStatus = new()
    {
        Text = "",
        AutoSize = true,
        ForeColor = Color.DarkGreen,
        Location = new Point(8, 50)
    };
    private readonly Button _approveDryRunButton = new()
    {
        Text = "Execute",
        Width = 140,
        Location = new Point(8, 76),
        BackColor = Color.LightGreen,
        Enabled = false
    };
    private readonly Button _rejectButton = new()
    {
        Text = "Reject",
        Width = 100,
        Location = new Point(156, 76),
        Enabled = false
    };
    private readonly Button _copyDetailsButton = new()
    {
        Text = "Copy prompt",
        Width = 110,
        Location = new Point(264, 76),
        Enabled = false
    };
    private readonly TextBox _approvalResult = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Location = new Point(8, 110),
        Width = 1200,
        Height = 100,
        Font = new Font(FontFamily.GenericMonospace, 9),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button _copyResultButton = new()
    {
        Text = "Copy result",
        Width = 110,
        Location = new Point(8, 218),
        Enabled = false,
        Visible = false
    };

    private void BuildApprovalPanel()
    {
        _approvalPanel.Controls.Add(_approvalTitle);
        _approvalPanel.Controls.Add(_approvalDetails);
        _approvalPanel.Controls.Add(_approvalProcessStatus);
        _approvalPanel.Controls.Add(_approveDryRunButton);
        _approvalPanel.Controls.Add(_rejectButton);
        _approvalPanel.Controls.Add(_copyDetailsButton);
        _approvalPanel.Controls.Add(_approvalResult);
        _approvalPanel.Controls.Add(_copyResultButton);
        Controls.Add(_approvalPanel);
    }

    public void ShowPendingCommand(string summary, bool processAvailable, string? processMessage)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowPendingCommand(summary, processAvailable, processMessage)));
            return;
        }
        _approvalDetails.Text = summary;
        _approvalResult.Text = "";
        _approveDryRunButton.Enabled = true;
        _rejectButton.Enabled = true;
        _approvalProcessStatus.Text = processMessage ?? "";
        _approvalProcessStatus.ForeColor = processAvailable ? Color.DarkGreen : Color.DarkOrange;
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
        _approveDryRunButton.Enabled = false;
        _rejectButton.Enabled = false;
        _copyDetailsButton.Enabled = false;
        _copyResultButton.Visible = false;
        _approvalDetails.Text = "";
        _approvalResult.Text = "";
        _approvalProcessStatus.Text = "";
    }

    public void ShowApprovalResult(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowApprovalResult(text)));
            return;
        }
        PipelineRawDump.Write("09_MainFormUi.txt", text);
        _approvalResult.Text = text;
        _approvalResult.SelectionStart = 0;
        _approvalResult.SelectionLength = 0;
        _approveDryRunButton.Enabled = false;
        _rejectButton.Enabled = false;
        _copyResultButton.Visible = true;
        _copyResultButton.Enabled = true;
    }
}
