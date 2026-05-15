using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

public sealed class MainForm : Form
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
    private readonly System.Windows.Forms.Timer _diagnosticsTimer = new() { Interval = 5000 };
    private readonly LogWriter _log = new();
    private readonly ResponseExtractor _extractor;
    private readonly BridgeBrowserModuleManager _moduleManager;
    private readonly WebViewMessageHandler _messageHandler;
    private readonly DiagnosticsController _diagnosticsController;
    private BrowserTabRuntime? _tabRuntime;
    private bool _webHidden;

    public MainForm()
    {
        _extractor = new ResponseExtractor(_log);
        _moduleManager = new BridgeBrowserModuleManager(_log);
        _diagnosticsController = new DiagnosticsController(_log, _moduleManager, SetDiagnostics, () => _webView.CoreWebView2 != null);
        _messageHandler = new WebViewMessageHandler(_log, _extractor, () => { _ = _diagnosticsController.RefreshAsync(false); });
        Text = "Open Browser v0.01.0-alpha.13";
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

        _newLogButton.Click += (_, _) => _tabRuntime?.StartNewRun();
        _openLogsButton.Click += (_, _) => OpenFolder(AppPaths.Logs);
        _openExtractedButton.Click += (_, _) => OpenFolder(AppPaths.Extracted);
        _exportRedactedButton.Click += (_, _) => ExportRedactedRun();
        _loadTrimmerButton.Click += async (_, _) => await LoadTrimmerAsync();
        _promoteTrimmerButton.Click += async (_, _) => await PromoteTrimmerAsync();
        _trimmerStatusButton.Click += async (_, _) => await ShowTrimmerStatusAsync();
        _hideWebButton.Click += (_, _) => ToggleWebVisibility();
        _diagnosticsTimer.Tick += async (_, _) => await _diagnosticsController.RefreshAsync(false);

        Load += async (_, _) => await InitializeAsync();
        FormClosing += (_, _) =>
        {
            _diagnosticsTimer.Stop();
            _extractor.Finish();
            _log.WriteApp("app", "app_exit", "ok", "Application closing");
            _log.Dispose();
        };
    }

    private async Task InitializeAsync()
    {
        try
        {
            AppPaths.EnsureAll();
            _log.WriteApp("app", "app_start", "ok", "Open Browser alpha started", new { root = AppPaths.Root, version = "v0.01.0-alpha.13" });
            SetStatus("Creating WebView2 environment...");

            var env = await CoreWebView2Environment.CreateAsync(null, AppPaths.Profile);
            await _webView.EnsureCoreWebView2Async(env);

            _tabRuntime = new BrowserTabRuntime(
                _webView.CoreWebView2,
                _log,
                _extractor,
                _moduleManager,
                _messageHandler,
                SetStatus,
                (writeLog) => _diagnosticsController.RefreshAsync(writeLog)
            );

            await _tabRuntime.InitializeAsync();

            _diagnosticsTimer.Start();
        }
        catch (Exception ex)
        {
            _log.WriteApp("app", "error", "error", "Startup failed", new { ex.Message, ex.StackTrace });
            MessageBox.Show("Open Browser startup failed:\n" + ex.Message, "Open Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Startup failed: " + ex.Message);
        }
    }

    private async Task LoadTrimmerAsync()
    {
        try
        {
            var version = await _moduleManager.LoadCurrentConversationTrimmerAsync();
            SetStatus("Loaded trimmer: " + version);
            await _diagnosticsController.RefreshAsync(true);
        }
        catch (Exception ex)
        {
            _log.WriteRun("modules", "module_load", "error", "Load trimmer failed", new { ex.Message });
            SetStatus("Load trimmer failed: " + ex.Message);
        }
    }

    private async Task PromoteTrimmerAsync()
    {
        try
        {
            var version = await _moduleManager.PromoteLatestConversationTrimmerAsync();
            SetStatus("Promoted trimmer: " + version);
            await _diagnosticsController.RefreshAsync(true);
        }
        catch (Exception ex)
        {
            _log.WriteRun("modules", "module_promote", "error", "Promote trimmer failed", new { ex.Message });
            SetStatus("Promote trimmer failed: " + ex.Message);
        }
    }



    private async Task ShowTrimmerStatusAsync()
    {
        await _diagnosticsController.RefreshAsync(true);
        MessageBox.Show(_diagnostics.Text, "Open Browser trimmer status", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportRedactedRun()
    {
        try
        {
            _extractor.Finish();
            var exportPath = RedactedExport.ExportRun(_log.RunId);
            _log.WriteRun("export", "redacted_export", "ok", "GitHub-safe redacted export created", new { exportPath });
            SetStatus("Redacted export: " + exportPath);
            OpenFolder(exportPath);
        }
        catch (Exception ex)
        {
            _log.WriteRun("export", "redacted_export", "error", "Redacted export failed", new { ex.Message });
            SetStatus("Redacted export failed: " + ex.Message);
            MessageBox.Show("Redacted export failed:\n" + ex.Message, "Open Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
