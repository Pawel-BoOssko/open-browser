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
    private NetworkLogger? _networkLogger;
    private bool _webHidden;

    public MainForm()
    {
        _extractor = new ResponseExtractor(_log);
        _moduleManager = new BridgeBrowserModuleManager(_log);
        Text = "Bridge Browser v0.01.0-alpha.13";
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

        _newLogButton.Click += (_, _) => StartNewRun();
        _openLogsButton.Click += (_, _) => OpenFolder(AppPaths.Logs);
        _openExtractedButton.Click += (_, _) => OpenFolder(AppPaths.Extracted);
        _exportRedactedButton.Click += (_, _) => ExportRedactedRun();
        _loadTrimmerButton.Click += async (_, _) => await LoadTrimmerAsync();
        _promoteTrimmerButton.Click += async (_, _) => await PromoteTrimmerAsync();
        _trimmerStatusButton.Click += async (_, _) => await ShowTrimmerStatusAsync();
        _hideWebButton.Click += (_, _) => ToggleWebVisibility();
        _diagnosticsTimer.Tick += async (_, _) => await RefreshDiagnosticsAsync(false);

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
            _log.WriteApp("app", "app_start", "ok", "Bridge Browser alpha started", new { root = AppPaths.Root, version = "v0.01.0-alpha.13" });
            SetStatus("Creating WebView2 environment...");

            var env = await CoreWebView2Environment.CreateAsync(null, AppPaths.Profile);
            await _webView.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = true;

            _webView.CoreWebView2.NavigationStarting += (_, e) =>
            {
                _log.WriteRun("webview", "navigation_start", "ok", e.Uri);
                SetStatus("Navigation: " + e.Uri);
            };
            _webView.CoreWebView2.NavigationCompleted += async (_, e) =>
            {
                _log.WriteRun("webview", "navigation_completed", e.IsSuccess ? "ok" : "error", e.IsSuccess ? "Navigation completed" : e.WebErrorStatus.ToString(), new { e.HttpStatusCode });
                SetStatus(e.IsSuccess ? "Ready" : "Navigation error: " + e.WebErrorStatus);
                await RefreshDiagnosticsAsync(false);
            };
            _webView.CoreWebView2.ProcessFailed += (_, e) =>
            {
                _log.WriteRun("webview", "error", "error", "WebView process failed", new { e.ProcessFailedKind, e.Reason, e.ExitCode });
                SetStatus("WebView process failed: " + e.ProcessFailedKind);
            };
            _webView.CoreWebView2.WebMessageReceived += (_, e) => OnPageMessage(e);

            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PageTap.Script);
            await _moduleManager.InitializeAsync(_webView.CoreWebView2);

            _networkLogger = new NetworkLogger(_webView.CoreWebView2, _log, _extractor);
            StartNewRun();
            await _networkLogger.InitializeAsync();

            _diagnosticsTimer.Start();
            _log.WriteRun("webview", "webview_ready", "ok", "WebView2 ready");
            _webView.CoreWebView2.Navigate("https://chatgpt.com/");
        }
        catch (Exception ex)
        {
            _log.WriteApp("app", "error", "error", "Startup failed", new { ex.Message, ex.StackTrace });
            MessageBox.Show("Bridge Browser startup failed:\n" + ex.Message, "Bridge Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Startup failed: " + ex.Message);
        }
    }

    private void StartNewRun()
    {
        try
        {
            var runId = _log.StartNewRun();
            _extractor.StartRun();
            SetStatus("Run: " + runId);
        }
        catch (Exception ex)
        {
            _log.WriteApp("app", "error", "error", "Cannot start new run", new { ex.Message });
            SetStatus("Cannot start new run: " + ex.Message);
        }
    }

    private void OnPageMessage(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "page_message" : "page_message";
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : "ok";
            object? data = root.TryGetProperty("data", out var d) ? JsonSerializer.Deserialize<object>(d.GetRawText()) : JsonSerializer.Deserialize<object>(json);
            _log.WriteRun("page_tap", eventType, status, null, data);


            if (root.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.TryGetProperty("chunk", out var chunk) && chunk.ValueKind == JsonValueKind.String)
                    _extractor.AddRaw("page_tap", eventType, chunk.GetString() ?? "");
                else if (dataEl.TryGetProperty("responseText", out var responseText) && responseText.ValueKind == JsonValueKind.String)
                    _extractor.AddRaw("page_tap", eventType, responseText.GetString() ?? "");
                else if (dataEl.TryGetProperty("data", out var wsData) && wsData.ValueKind == JsonValueKind.String)
                    _extractor.AddRaw("page_tap", eventType, wsData.GetString() ?? "");
            }

            if (eventType is "page_fetch_done" or "page_xhr_done" or "page_eventsource_error")
                _extractor.Finish();

            if (eventType.StartsWith("loaded_turns_monitor", StringComparison.OrdinalIgnoreCase))
                _ = RefreshDiagnosticsAsync(false);
        }
        catch (Exception ex)
        {
            _log.WriteRun("page_tap", "error", "error", "Failed to handle page message", new { ex.Message, raw = e.WebMessageAsJson });
        }
    }

    private async Task LoadTrimmerAsync()
    {
        try
        {
            var version = await _moduleManager.LoadCurrentConversationTrimmerAsync();
            SetStatus("Loaded trimmer: " + version);
            await RefreshDiagnosticsAsync(true);
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
            await RefreshDiagnosticsAsync(true);
        }
        catch (Exception ex)
        {
            _log.WriteRun("modules", "module_promote", "error", "Promote trimmer failed", new { ex.Message });
            SetStatus("Promote trimmer failed: " + ex.Message);
        }
    }



    private async Task ShowTrimmerStatusAsync()
    {
        await RefreshDiagnosticsAsync(true);
        MessageBox.Show(_diagnostics.Text, "Bridge Browser trimmer status", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RefreshDiagnosticsAsync(bool writeLog)
    {
        if (_webView.CoreWebView2 == null) return;
        try
        {
            var statusJson = await _moduleManager.GetConversationTrimmerStatusJsonAsync();
            if (writeLog)
                _log.WriteRun("modules", "module_status", "ok", "Conversation trimmer status", JsonSerializer.Deserialize<object>(statusJson));

            SetDiagnostics(statusJson);
        }
        catch (Exception ex)
        {
            SetDiagnostics("trimmer status error: " + ex.Message);
        }
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
            MessageBox.Show("Redacted export failed:\n" + ex.Message, "Bridge Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
