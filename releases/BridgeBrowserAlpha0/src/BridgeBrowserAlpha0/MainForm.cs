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

        var psExecutor = new BridgeBrowserAlpha0.OpenBridgeHost.GeneralCommand.GeneralCommandExecutor(
            "powershell.exe", "-NoProfile -Command \"{prompt}\"");
        var host = new OpenBridgeHost.OpenBridgeHost(psExecutor);
        _runtimeApproval = new OpenBridgeHost.OpenBridgeRuntimeApproval(host, @"D:\projects\open-browser", _log);

        _extractor.OnEnvelopeDetected = parseResult =>
        {
            if (parseResult.Envelope == null)
            {
                var msg = BridgeBrowserAlpha0.OpenBridgeProtocol.OpenBridgeEnvelopeParser.ErrorToUserMessage(parseResult);
                _log.WriteRun("runtime_approval", "envelope_parse_error", "error", msg);
                _ = SendWithDeadlineAsync(msg);
                return;
            }

            var ok = _runtimeApproval.TrySetPending(parseResult, out var error);
            if (ok)
            {
                SetStatus("Executing command...");
                ShowOutputResult("Executing...");
                _ = ExecuteAndInjectResultAsync();
            }
            else
            {
                var msg = $"[OpenBridge] {error}";
                _log.WriteRun("runtime_approval", "envelope_rejected", "warning", msg);
                _ = SendWithDeadlineAsync(msg);
            }
        };

        InitializeUi();

        _hideWebButton.Click += (_, _) => ToggleWebVisibility();
        _testInjectButton.Click += async (_, _) => await TestInjectAsync();
        _copyOutputButton.Click += (_, _) => CopyOutput();

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
            _log.WriteApp("app", "app_start", "ok", $"{AppConstants.AppTitle} alpha started", new { root = AppPaths.Root, build = AppConstants.BuildInfo });
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

    private static int ComputeHumanDelayMs()
    {
        const int baseDelayMs = 20_000;
        // Box-Muller: truncated normal(mean=22000, std=11000, min=0, max=50000)
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[8];
        rng.GetBytes(bytes);
        double u1 = BitConverter.ToUInt32(bytes, 0) / (double)uint.MaxValue;
        double u2 = BitConverter.ToUInt32(bytes, 4) / (double)uint.MaxValue;
        if (u1 <= 0) u1 = 0.0001;
        double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        double raw = 22_000 + normal * 11_000;
        int extra = (int)Math.Clamp(raw, 0, 50_000);
        return baseDelayMs + extra;
    }

    private volatile bool _cycleClosed;

    private async Task SendTextToChatAsync(string text)
    {
        if (_webView.CoreWebView2 == null || string.IsNullOrWhiteSpace(text)) return;
        if (_cycleClosed) return;
        try
        {
            text = Humanizer.Wrap(text);
            var delayMs = ComputeHumanDelayMs();
            _log.WriteRun("runtime_approval", "human_delay", "ok", $"Delaying {delayMs}ms before inject");
            await Task.Delay(delayMs);
            if (_cycleClosed) return;
            var safeText = System.Text.Json.JsonSerializer.Serialize(text);
            var js = "var el=document.querySelector('#prompt-textarea,.ProseMirror,[contenteditable=true]');" +
                $"if(el){{el.focus();el.textContent={safeText};el.dispatchEvent(new Event('input',{{bubbles:true}}));" +
                "setTimeout(function(){" +
                "var btn=document.querySelector('[data-testid=\\'send-button\\']')||document.querySelector('button[aria-label*=\\'Send\\'] i')||document.querySelector('button svg');" +
                "if(btn&&btn.tagName!=='svg'){btn.click();console.log('OpenBridge: send btn clicked');}" +
                "else if(btn&&btn.tagName==='svg'){btn.closest('button')?.click();console.log('OpenBridge: send svg->btn clicked');}" +
                "else{console.log('OpenBridge: no send btn, falling back');}" +
                "},500);" +
                "console.log('OpenBridge: injected');}else{console.log('OpenBridge: no el');}";
            await _webView.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch (Exception ex)
        {
            _log.WriteRun("runtime_approval", "inject_error", "error", ex.Message);
        }
    }

    private async Task InjectImmediatelyAsync(string text)
    {
        if (_webView.CoreWebView2 == null || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var safeText = System.Text.Json.JsonSerializer.Serialize(text);
            var js = "var el=document.querySelector('#prompt-textarea,.ProseMirror,[contenteditable=true]');" +
                $"if(el){{el.focus();el.textContent={safeText};el.dispatchEvent(new Event('input',{{bubbles:true}}));" +
                "setTimeout(function(){" +
                "var btn=document.querySelector('[data-testid=\\'send-button\\']')||document.querySelector('button[aria-label*=\\'Send\\'] i')||document.querySelector('button svg');" +
                "if(btn&&btn.tagName!=='svg'){btn.click();console.log('OpenBridge: send btn clicked');}" +
                "else if(btn&&btn.tagName==='svg'){btn.closest('button')?.click();console.log('OpenBridge: send svg->btn clicked');}" +
                "else{console.log('OpenBridge: no send btn, falling back');}" +
                "},500);" +
                "console.log('OpenBridge: immediate inject');}else{console.log('OpenBridge: no el');}";
            await _webView.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch (Exception ex)
        {
            _log.WriteRun("runtime_approval", "inject_error", "error", ex.Message);
        }
    }

    private async Task SendWithDeadlineAsync(string text)
    {
        const int cycleTimeoutMs = 120_000;
        _cycleClosed = false;
        var deadline = Task.Delay(cycleTimeoutMs);
        var work = SendTextToChatAsync(text);
        var completed = await Task.WhenAny(work, deadline);
        if (completed == deadline)
        {
            _cycleClosed = true;
            _log.WriteRun("runtime_approval", "cycle_timeout", "error", "Error feedback cycle timeout");
            await InjectImmediatelyAsync("[OpenBridge] Timeout: no response within 120s.");
        }
        else
        {
            _cycleClosed = true;
            await work;
        }
    }

    private async Task ExecuteAndInjectResultAsync()
    {
        const int cycleTimeoutMs = 120_000;
        _cycleClosed = false;

        async Task DoCycleAsync()
        {
            var result = await _runtimeApproval.ExecutePendingAsync();
            var output = result.StdoutPreview ?? "";
            if (string.IsNullOrWhiteSpace(output))
                output = result.StderrPreview ?? "";
            ShowOutputResult(output);

            var msg = !string.IsNullOrWhiteSpace(output)
                ? output
                : $"[OpenBridge] Process exited with code {result.ExitCode}. No output.";
            _log.WriteRun("runtime_approval", "webview_inject", "ok", "Injecting result into chat input", new { outputLength = msg.Length });
            await SendTextToChatAsync(msg);
            SetStatus(result.Status == HostExecutionStatus.Ok ? "OK" : "Failed: " + result.ErrorCode);
        }

        try
        {
            var deadline = Task.Delay(cycleTimeoutMs);
            var work = DoCycleAsync();
            var completed = await Task.WhenAny(work, deadline);

            if (completed == deadline)
            {
                _cycleClosed = true;
                ShowOutputResult("Timeout: cycle exceeded 120s");
                _log.WriteRun("runtime_approval", "cycle_timeout", "error", "Cycle timeout — injecting fallback");
                await InjectImmediatelyAsync("[OpenBridge] Timeout: no response within 120s.");
                SetStatus("Timeout");
            }
            else
            {
                _cycleClosed = true;
                await work;
            }
        }
        catch (Exception ex)
        {
            _cycleClosed = true;
            ShowOutputResult("Execution error: " + ex.Message);
            SetStatus("Execution error: " + ex.Message);
            _log.WriteRun("runtime_approval", "host_execution_failed", "error", ex.Message);
        }
    }

    private async Task TestInjectAsync()
    {
        if (_webView.CoreWebView2 == null) { SetStatus("WebView not ready"); return; }
        try
        {
            var js = "console.log('OpenBridge DIAG: running');" +
                "var el=document.querySelector('[data-placeholder],#prompt-textarea,.ProseMirror,div[contenteditable=true],[contenteditable=true]');" +
                "if(el){el.focus();el.textContent='TEST_INJECT';el.dispatchEvent(new Event('input',{bubbles:true}));" +
                "console.log('OpenBridge DIAG: injected. tag='+el.tagName+' id='+el.id+' class='+el.className);}" +
                "else{console.log('OpenBridge DIAG: NO ELEMENT');" +
                "document.querySelectorAll('*').forEach(function(n){if(n.contentEditable==='true'||n.contentEditable===''||n.contentEditable===true)console.log('CE: '+n.tagName+'#'+n.id+'.'+n.className);});" +
                "}" +
                "var sendBtn=document.querySelector('[data-testid=\\'send-button\\']');" +
                "console.log('OpenBridge DIAG: send-btn data-testid:',sendBtn);" +
                "var sendAria=document.querySelector('button[aria-label*=\\'Send\\']');" +
                "console.log('OpenBridge DIAG: send-btn aria-label:',sendAria);" +
                "var svgBtn=document.querySelector('button svg');" +
                "console.log('OpenBridge DIAG: send-btn button svg parent:',svgBtn?svgBtn.closest('button'):null);";
            await _webView.CoreWebView2.ExecuteScriptAsync(js);
            SetStatus("Test inject done. Press F12 and check Console.");
        }
        catch (Exception ex) { SetStatus("Inject error: " + ex.Message); }
    }

    private void CopyOutput()
    {
        var text = _outputResult.Text;
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            SetStatus("Output copied.");
        }
    }
}
