using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

public sealed class BrowserTabRuntime
{
    private readonly CoreWebView2 _core;
    private readonly LogWriter _log;
    private readonly ResponseExtractor _extractor;
    private readonly BridgeBrowserModuleManager _moduleManager;
    private readonly WebViewMessageHandler _messageHandler;
    private readonly Action<string> _setStatus;
    private readonly Func<bool, Task> _refreshDiagnosticsAsync;
    private NetworkLogger? _networkLogger;

    public BrowserTabRuntime(
        CoreWebView2 core,
        LogWriter log,
        ResponseExtractor extractor,
        BridgeBrowserModuleManager moduleManager,
        WebViewMessageHandler messageHandler,
        Action<string> setStatus,
        Func<bool, Task> refreshDiagnosticsAsync)
    {
        _core = core;
        _log = log;
        _extractor = extractor;
        _moduleManager = moduleManager;
        _messageHandler = messageHandler;
        _setStatus = setStatus;
        _refreshDiagnosticsAsync = refreshDiagnosticsAsync;
    }

    public async Task InitializeAsync()
    {
        _core.Settings.AreDevToolsEnabled = true;
        _core.Settings.IsWebMessageEnabled = true;
        _core.Settings.AreDefaultContextMenusEnabled = true;
        _core.Settings.IsStatusBarEnabled = true;

        _core.NavigationStarting += (_, e) =>
        {
            _log.WriteRun("webview", "navigation_start", "ok", e.Uri);
            _setStatus("Navigation: " + e.Uri);
        };
        _core.NavigationCompleted += async (_, e) =>
        {
            _log.WriteRun("webview", "navigation_completed", e.IsSuccess ? "ok" : "error", e.IsSuccess ? "Navigation completed" : e.WebErrorStatus.ToString(), new { e.HttpStatusCode });
            _setStatus(e.IsSuccess ? "Ready" : "Navigation error: " + e.WebErrorStatus);
            await _refreshDiagnosticsAsync(false);
        };
        _core.ProcessFailed += (_, e) =>
        {
            _log.WriteRun("webview", "error", "error", "WebView process failed", new { e.ProcessFailedKind, e.Reason, e.ExitCode });
            _setStatus("WebView process failed: " + e.ProcessFailedKind);
        };
        _core.WebMessageReceived += (_, e) => _messageHandler.HandleWebMessage(e);

        await _core.AddScriptToExecuteOnDocumentCreatedAsync(PageTap.Script);
        await _moduleManager.InitializeAsync(_core);

        _networkLogger = new NetworkLogger(_core, _log, _extractor);
        StartNewRun();
        await _networkLogger.InitializeAsync();

        _core.SourceChanged += (_, _) =>
        {
            try
            {
                var url = _core.Source;
                if (!string.IsNullOrEmpty(url))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"/c/([a-f0-9-]+)");
                    if (match.Success)
                        AppConstants.ConversationId = match.Groups[1].Value;
                }
            }
            catch { }
        };

        _log.WriteRun("webview", "webview_ready", "ok", "WebView2 ready");
        _core.Navigate("https://chatgpt.com/");
    }

    public void StartNewRun()
    {
        try
        {
            var runId = _log.StartNewRun();
            _extractor.StartRun();
            _setStatus("Run: " + runId);
        }
        catch (Exception ex)
        {
            _log.WriteApp("app", "error", "error", "Cannot start new run", new { ex.Message });
            _setStatus("Cannot start new run: " + ex.Message);
        }
    }
}
