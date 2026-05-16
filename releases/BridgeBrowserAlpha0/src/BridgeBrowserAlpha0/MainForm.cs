using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

public sealed partial class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _diagnosticsTimer = new() { Interval = 5000 };
    private readonly LogWriter _log = new();
    private readonly ResponseExtractor _extractor;
    private readonly BridgeBrowserModuleManager _moduleManager;
    private readonly WebViewMessageHandler _messageHandler;
    private readonly DiagnosticsController _diagnosticsController;
    private BrowserTabRuntime? _tabRuntime;

    public MainForm()
    {
        _extractor = new ResponseExtractor(_log);
        _moduleManager = new BridgeBrowserModuleManager(_log);
        _diagnosticsController = new DiagnosticsController(_log, _moduleManager, SetDiagnostics, () => _webView.CoreWebView2 != null);
        _messageHandler = new WebViewMessageHandler(_log, _extractor, () => { _ = _diagnosticsController.RefreshAsync(false); });
        
        InitializeUi();

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
            _log.WriteApp("app", "app_start", "ok", $"{AppConstants.AppTitle} alpha started", new { root = AppPaths.Root, version = AppConstants.AppVersion });
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
            MessageBox.Show($"{AppConstants.AppTitle} startup failed:\n" + ex.Message, AppConstants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        MessageBox.Show(_diagnostics.Text, $"{AppConstants.AppTitle} trimmer status", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("Redacted export failed:\n" + ex.Message, AppConstants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
