using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

/// <summary>
/// One-tab ChatGPT test shell. Deliberately does not initialize the OpenBridge
/// executor, PageTap, network inspection, conversation trimming, or injection.
/// </summary>
internal sealed class ChatGptLiteForm : Form
{
    private const string HomeUrl = "https://chatgpt.com/";
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _heartbeat = new() { Interval = 30000 };
    private DateTimeOffset _lastHeartbeat = DateTimeOffset.UtcNow;
    private readonly Button _back = new() { Text = "Wstecz", Width = 90 };
    private readonly Button _forward = new() { Text = "Dalej", Width = 85 };
    private readonly Button _reload = new() { Text = "Odśwież", Width = 95 };
    private readonly Button _home = new() { Text = "ChatGPT", Width = 95 };
    private readonly Label _status = new()
    {
        AutoSize = true,
        Padding = new Padding(12, 8, 0, 0),
        Text = "Uruchamianie..."
    };

    public ChatGptLiteForm()
    {
        Text = "Open Browser | ChatGPT Lite";
        Width = 1280;
        Height = 850;
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(8, 4, 8, 0),
            WrapContents = false,
            AutoScroll = true
        };
        toolbar.Controls.AddRange(new Control[] { _back, _forward, _reload, _home, _status });
        Controls.Add(_browser);
        Controls.Add(toolbar);

        _back.Enabled = false;
        _forward.Enabled = false;
        _reload.Click += (_, _) => _browser.CoreWebView2?.Reload();
        _home.Click += (_, _) => _browser.CoreWebView2?.Navigate(HomeUrl);
        _back.Click += (_, _) =>
        {
            if (_browser.CanGoBack) _browser.GoBack();
        };
        _forward.Click += (_, _) =>
        {
            if (_browser.CanGoForward) _browser.GoForward();
        };

        LiteEventLog.PruneOldFiles();
        LiteEventLog.Write("window_constructed", new { version = Application.ProductVersion });
        _heartbeat.Tick += (_, _) =>
        {
            var now = DateTimeOffset.UtcNow;
            LiteEventLog.Write("ui_heartbeat", new { gapSeconds = (int)(now - _lastHeartbeat).TotalSeconds });
            _lastHeartbeat = now;
        };
        _heartbeat.Start();
        FormClosing += (_, _) =>
        {
            _heartbeat.Stop();
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
            };
            core.HistoryChanged += (_, _) => UpdateNavigation();
            core.ProcessFailed += (_, e) =>
            {
                LiteEventLog.Write("webview_process_failed", new
                {
                    kind = e.ProcessFailedKind.ToString(),
                    reason = e.Reason.ToString(),
                    exitCode = e.ExitCode
                });
                _status.Text = "Błąd procesu strony: " + e.ProcessFailedKind;
                // Preserve session data. The user can use Odśwież to retry.
            };

            core.Navigate(HomeUrl);
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("webview_initialization_failed", new { errorType = ex.GetType().Name, ex.HResult });
            _status.Text = "Nie udało się uruchomić";
            MessageBox.Show(this, ex.Message, "Open Browser ChatGPT Lite",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateNavigation()
    {
        _back.Enabled = _browser.CanGoBack;
        _forward.Enabled = _browser.CanGoForward;
    }
}
