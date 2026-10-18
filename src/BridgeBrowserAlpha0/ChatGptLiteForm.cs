using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// One-tab ChatGPT test shell. Deliberately does not initialize the OpenBridge
/// executor, PageTap, network inspection, or response interception.
/// It has an isolated, non-destructive DOM paint optimization.
/// </summary>
internal sealed partial class ChatGptLiteForm : Form
{
    private const string HomeUrl = "https://chatgpt.com/";
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _heartbeat = new() { Interval = 30000 };
    private readonly System.Windows.Forms.Timer _domMonitor = new() { Interval = 30000 };
    private readonly Button _domToggle = new() { Text = "DOM: włączony", Width = 125 };
    private readonly Label _domInfo = new() { AutoSize = true, Padding = new Padding(8, 8, 0, 0), Text = "DOM: oczekiwanie" };
    private bool _domEnabled = true;
    private bool _domProbePending;
    private DateTimeOffset _lastHeartbeat = DateTimeOffset.UtcNow;
    private readonly Button _back = new() { Text = "Wstecz", Width = 90 };
    private readonly Button _forward = new() { Text = "Dalej", Width = 85 };
    private readonly Button _reload = new() { Text = "Odśwież", Width = 95 };
    private readonly Button _home = new() { Text = "ChatGPT", Width = 95 };
    private readonly Button _openArchive = new() { Text = "Archiwum", Width = 90 };
    private readonly Label _buildVersion = new()
    {
        AutoSize = true,
        Padding = new Padding(8, 8, 8, 0),
        Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        Text = LiteBuildInfo.Display()
    };
    private readonly Label _status = new()
    {
        AutoSize = true,
        Padding = new Padding(12, 8, 0, 0),
        Text = "Uruchamianie..."
    };

    public ChatGptLiteForm()
    {
        Text = "Open Browser Lite | " + LiteBuildInfo.Display();
        Width = 1280;
        Height = 850;
        MinimumSize = new Size(980, 600);
        StartPosition = FormStartPosition.CenterScreen;
        try
        {
            Icon = LiteBrandIcon.Create();
            LiteBrandIcon.Export();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("brand_window_icon_failed", new { errorType = ex.GetType().Name });
        }

        InitializeWindowRecovery();

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8, 4, 8, 0),
            WrapContents = false,
            AutoScroll = true
        };
        toolbar.Controls.AddRange(new Control[] { _buildVersion, _highIndicator, _devWakeToggle, _devWakeStatus, _resumeToggle, _resumeStatus, _back, _forward, _reload, _home, _domToggle, _hardPruneToggle, _archiveToggle, _openArchive, _status, _domInfo });
        var content = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Size = new Size(1150, 750)
        };
        content.Panel1MinSize = 205;
        content.Panel2MinSize = 400;
        content.SplitterDistance = 270;
        content.Panel1.Controls.Add(CreateProjectsUi());
        content.Panel2.Controls.Add(_browser);
        var workspace = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Size = new Size(1200, 780)
        };
        workspace.Panel1MinSize = 660;
        workspace.Panel2MinSize = 225;
        workspace.SplitterDistance = 900;
        workspace.Panel1.Controls.Add(content);
        workspace.Panel2.Controls.Add(CreateNotesUi());
        Controls.Add(workspace);
        Controls.Add(toolbar);

        _back.Enabled = false;
        _forward.Enabled = false;
        _reload.Click += (_, _) => _browser.CoreWebView2?.Reload();
        _home.Click += (_, _) => _browser.CoreWebView2?.Navigate(HomeUrl);
        _openArchive.Click += (_, _) => new LiteArchiveViewer().Show(this);
        _back.Click += (_, _) =>
        {
            if (_browser.CanGoBack) _browser.GoBack();
        };
        _forward.Click += (_, _) =>
        {
            if (_browser.CanGoForward) _browser.GoForward();
        };

        _domToggle.Click += async (_, _) =>
        {
            _domEnabled = !_domEnabled;
            _domToggle.Text = _domEnabled ? "DOM: włączony" : "DOM: wyłączony";
            LiteEventLog.Write("dom_mode_changed", new { enabled = _domEnabled });
            await ApplyDomModeAsync();
        };
        _domMonitor.Tick += async (_, _) => await PollDomAsync();
        InitializeCapture();
        InitializeHardPruneUi();
        InitializeResumeUi();
        InitializeHighUi();
        InitializeDevWake();

        LiteEventLog.PruneOldFiles();
        LiteEventLog.Write("window_constructed", new { version = Application.ProductVersion });
        _heartbeat.Tick += (_, _) =>
        {
            var now = DateTimeOffset.UtcNow;
            LiteEventLog.Write("ui_heartbeat", new { gapSeconds = (int)(now - _lastHeartbeat).TotalSeconds });
            _lastHeartbeat = now;
        };
        _heartbeat.Start();
        FormClosing += (_, e) =>
        {
            if (!FlushNotes())
            {
                e.Cancel = true;
                LiteEventLog.Write("window_close_blocked", new { reason = "notes_not_saved" });
                return;
            }
            _heartbeat.Stop();
            _domMonitor.Stop();
            _captureTimer.Stop();
            _responseTimer.Stop();
            _resumeTimer.Stop();
            _devWakeTimer.Stop();
            _highTimer.Stop();
            _pruneReportTimer.Stop();
            _noteSaveTimer.Stop();
            DisposeWindowRecovery();
            LiteEventLog.Write("window_closing");
        };
        Load += async (_, _) => await InitializeBrowserAsync();
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            // Keep login state outside the repository and outside the main OpenBridge profile.
            // This profile is never included in Git or Autosync.
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var profile = Path.Combine(localData, "OpenBrowser", "ChatGPT-Lite", "Profile");
            Directory.CreateDirectory(profile);

            _status.Text = "Uruchamianie silnika WebView2...";
            LiteEventLog.Write("webview_initializing");
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
            await _browser.EnsureCoreWebView2Async(environment);
            LiteEventLog.Write("webview_initialized", new { browserVersion = environment.BrowserVersionString });

            var core = _browser.CoreWebView2;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.PermissionRequested += (_, e) =>
            {
                if (e.PermissionKind != CoreWebView2PermissionKind.Microphone) return;
                bool chatGpt = Uri.TryCreate(e.Uri, UriKind.Absolute, out var origin) &&
                    (origin.Host == "chatgpt.com" || origin.Host == "www.chatgpt.com");
                e.State = chatGpt ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
                e.Handled = true;
                LiteEventLog.Write("microphone_permission", new { host = LiteEventLog.HostOnly(e.Uri), allowed = chatGpt });
            };
            string domScriptFile = Path.Combine(AppContext.BaseDirectory, "LiteDomOptimizer.js");
            string domScript = await File.ReadAllTextAsync(domScriptFile);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(domScript);
            LiteEventLog.Write("dom_observer_installed");
            await InstallCaptureScriptAsync(core);
            await InstallResponseDiagnosticsAsync(core);
            await InstallHardPruneScriptAsync(core);
            await InstallResumeScriptsAsync(core);
            await InstallHighScriptAsync(core);

            core.NavigationStarting += (_, e) =>
            {
                _status.Text = "Ładowanie...";
                LiteEventLog.Write("navigation_start", new { host = LiteEventLog.HostOnly(e.Uri) });
            };
            core.NavigationCompleted += (_, e) =>
            {
                _status.Text = e.IsSuccess ? "Gotowe" : "Błąd: " + e.WebErrorStatus;
                LiteEventLog.Write("navigation_completed", new { e.IsSuccess, webError = e.WebErrorStatus.ToString(), host = LiteEventLog.HostOnly(core.Source) });
                UpdateNavigation();
                if (!_domEnabled && e.IsSuccess) _ = ApplyDomModeAsync();
                if (e.IsSuccess) _ = ApplyHardPruneModeAsync();
            };
            core.HistoryChanged += (_, _) => UpdateNavigation();
            core.SourceChanged += (_, _) => SwitchNotesForUrl(core.Source);
            core.ProcessFailed += (_, e) =>
            {
                LiteEventLog.Write("webview_process_failed", new
                {
                    kind = e.ProcessFailedKind.ToString(),
                    reason = e.Reason.ToString(),
                    exitCode = e.ExitCode
                });
                _status.Text = "Błąd procesu strony: " + e.ProcessFailedKind;
                _domInfo.Text = "DOM: renderer niedostępny";
                // Preserve session data. The user can use Odśwież to retry.
            };

            var startUrl = _devWake.Enabled && LiteDevWakeState.IsChatUrl(_devWake.TargetUrl)
                ? _devWake.TargetUrl : HomeUrl;
            core.Navigate(startUrl);
            _domMonitor.Start();
            _captureTimer.Start();
            _responseTimer.Start();
            _pruneReportTimer.Start();
            _resumeTimer.Start();
            _devWakeTimer.Start();
            _highTimer.Start();
            _ = VerifyHighAsync();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("webview_initialization_failed", new { errorType = ex.GetType().Name, ex.HResult });
            _status.Text = "Nie udało się uruchomić";
            MessageBox.Show(this, ex.Message, "Open Browser ChatGPT Lite",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ApplyDomModeAsync()
    {
        var core = _browser.CoreWebView2;
        if (core is null) return;
        try
        {
            string value = _domEnabled ? "true" : "false";
            var call = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_LITE_DOM__?.setEnabled(" + value + ")");
            if (await Task.WhenAny(call, Task.Delay(5000)) != call)
            {
                LiteEventLog.Write("dom_mode_apply_timeout");
                return;
            }
            await call;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("dom_mode_apply_failed", new { errorType = ex.GetType().Name });
        }
    }

    private async Task PollDomAsync()
    {
        var core = _browser.CoreWebView2;
        if (core is null || _domProbePending) return;
        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var url) ||
            url.Host != "chatgpt.com") return;

        _domProbePending = true;
        try
        {
            var call = core.ExecuteScriptAsync(
                "window.__OPENBROWSER_LITE_DOM__ ? JSON.stringify(window.__OPENBROWSER_LITE_DOM__.getStatus()) : null");
            if (await Task.WhenAny(call, Task.Delay(7000)) != call)
            {
                _domInfo.Text = "DOM: strona nie odpowiada";
                LiteEventLog.Write("dom_probe_timeout");
            }
            // A single pending probe prevents piling up unresponsive script calls.
            var raw = await call;
            var decoded = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrWhiteSpace(decoded)) return;
            using var status = JsonDocument.Parse(decoded);
            var root = status.RootElement;
            int turns = root.GetProperty("turnCount").GetInt32();
            int optimized = root.GetProperty("optimized").GetInt32();
            int duration = root.GetProperty("lastDurationMs").GetInt32();
            bool supported = root.GetProperty("supported").GetBoolean();
            _domInfo.Text = supported
                ? $"DOM: {turns} tur / {optimized} odciążonych"
                : "DOM: brak content-visibility";
            LiteEventLog.Write("dom_status", new
            {
                turns, optimized, duration, supported,
                modeEnabled = root.GetProperty("enabled").GetBoolean(),
                errorCount = root.GetProperty("errorCount").GetInt32()
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("dom_probe_failed", new { errorType = ex.GetType().Name });
        }
        finally
        {
            _domProbePending = false;
        }
    }

    private void UpdateNavigation()
    {
        _back.Enabled = _browser.CanGoBack;
        _forward.Enabled = _browser.CanGoForward;
    }
}
