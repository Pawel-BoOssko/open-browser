using System.Diagnostics;
using System.Text.Json;
using BridgeBrowserAlpha0.OpenBridgeHost;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

public sealed partial class MainForm : Form
{
    private readonly LogWriter _log = new();
    private readonly ResponseExtractor _extractor;
    private readonly BridgeBrowserModuleManager _moduleManager;
    private readonly WebViewMessageHandler _messageHandler;
    private readonly OpenBridgeHost.OpenBridgeRuntimeApproval _runtimeApproval;
    private BrowserTabRuntime? _tabRuntime;

    public MainForm()
    {
        _extractor = new ResponseExtractor(_log);
        _moduleManager = new BridgeBrowserModuleManager(_log);
        _messageHandler = new WebViewMessageHandler(_log, _extractor, () => { });
        _runtimeApproval = new OpenBridgeHost.OpenBridgeRuntimeApproval(@"D:\projects\open-browser", _log);

        _extractor.OnEnvelopeDetected = parseResult =>
        {
            var ok = _runtimeApproval.TrySetPending(parseResult, out var error);
            if (ok)
            {
                var processAvail = _runtimeApproval.IsProcessAvailable();
                var processMsg = _runtimeApproval.ProcessAvailableMessage();
                ShowPendingCommand(_runtimeApproval.PendingSummary(), processAvail, processMsg);
            }
            else
            {
                _log.WriteRun("runtime_approval", "envelope_rejected", "warning",
                    "Envelope detection not promoted to pending",
                    new { error });
            }
        };

        InitializeUi();

        _hideWebButton.Click += (_, _) => ToggleWebVisibility();
        _approveDryRunButton.Click += async (_, _) => await ExecuteCommandAsync();
        _rejectButton.Click += (_, _) => RejectRuntimeCommand();
        _copyDetailsButton.Click += (_, _) => CopyApprovalPrompt();
        _copyResultButton.Click += (_, _) => CopyApprovalOutput();

        Load += async (_, _) => await InitializeAsync();
        FormClosing += (_, _) =>
        {
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
                (_) => Task.CompletedTask
            );

            await _tabRuntime.InitializeAsync();
        }
        catch (Exception ex)
        {
            _log.WriteApp("app", "error", "error", "Startup failed", new { ex.Message, ex.StackTrace });
            MessageBox.Show($"{AppConstants.AppTitle} startup failed:\n" + ex.Message, AppConstants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Startup failed: " + ex.Message);
        }
    }

    private async Task ExecuteCommandAsync()
    {
        try
        {
            SetStatus("Executing...");
            ShowApprovalResult("Executing...");
            var result = await _runtimeApproval.ApproveDryRunAsync();
            var output = result.StdoutPreview ?? "";
            ShowApprovalResult(output);
            SetStatus(result.Status == HostExecutionStatus.Ok ? "OK" : "Failed: " + result.ErrorCode);
        }
        catch (Exception ex)
        {
            ShowApprovalResult("Execution error: " + ex.Message);
            SetStatus("Execution error: " + ex.Message);
            _log.WriteRun("runtime_approval", "host_execution_failed", "error", ex.Message);
        }
    }

    private void RejectRuntimeCommand()
    {
        _runtimeApproval.Reject();
        SetStatus("Command rejected.");
        HidePendingCommand();
    }

    private void CopyApprovalPrompt()
    {
        var details = _runtimeApproval.PendingCommandDetails();
        if (!string.IsNullOrEmpty(details))
        {
            Clipboard.SetText(details);
            SetStatus("Prompt copied.");
        }
    }

    private void CopyApprovalOutput()
    {
        var text = _approvalResult.Text;
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            SetStatus("Output copied.");
        }
    }
}
