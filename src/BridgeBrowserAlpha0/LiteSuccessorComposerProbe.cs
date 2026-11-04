using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

// One-shot diagnostic, not an executor. It cannot click, type or send.
// Reuses the existing logged-in OBL worker WebView2 profile, closes after 30s.
internal static class LiteSuccessorComposerProbe
{
    private static string LogFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "diagnostics",
        "successor-composer-probe.jsonl");

    internal static int Run()
    {
        if (!string.Equals(Environment.MachineName, "SATANAMD",
            StringComparison.OrdinalIgnoreCase)) return 2;
        int exit = 9;
        string phase = "form_setup";
        using var form = new Form {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000), Size = new Size(620, 450),
            Text = "OBL successor isolated composer probe"
        };
        using var view = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(30));
        timeout.Token.Register(() => {
            try { form.BeginInvoke(() => form.Close()); } catch { }
        });
        form.Shown += async (_, _) => {
            try
            {
                var profile = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "OpenBrowser", "ChatGPT-Lite", "Profile");
                phase = "create_environment";
                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: profile);
                phase = "initialize_webview";
                await view.EnsureCoreWebView2Async(env);
                var core = view.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.MemoryUsageTargetLevel =
                    CoreWebView2MemoryUsageTargetLevel.Low;                var navigated = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                core.NavigationCompleted += (_, e) =>
                    navigated.TrySetResult(e.IsSuccess);
                phase = "navigate_homepage";
                core.Navigate("https://chatgpt.com/");
                if (!await navigated.Task.WaitAsync(
                    TimeSpan.FromSeconds(15)))
                    throw new InvalidDataException("Navigation failed");
                await Task.Delay(2000, timeout.Token);
                phase = "first_composer_probe";
                var a = await Capture(core, 1);
                await Task.Delay(1300, timeout.Token);
                phase = "second_composer_probe";
                var b = await Capture(core, 1);
                // The only success is the existing strict verifier.
                phase = "verify_probe_pair";
                LiteHandoffRouteEvidence.VerifyBlankComposerPair(
                    a, b, DateTimeOffset.UtcNow);
                exit = 0;
                Record("verified_blank_composer", "root",
                    a.ComposerReady, a.ComposerEmpty, 0);
            }
            catch (Exception ex)
            {
                exit = 5;
                Record("no_verified_blank_composer",
                    phase + ":" + ex.GetType().Name + ":" +
                    ex.HResult.ToString("X8"), false, false, 5);
            }
            finally { form.Close(); }
        };
        Application.Run(form);
        return exit;
    }

    internal static async Task<LiteHandoffRouteEvidence.Sample> Capture(
        CoreWebView2 core, long navigationEpoch)
    {
        const string probe = """
            (() => {
              const e=document.querySelector(
                '#prompt-textarea,[data-testid="composer-text-input"],[contenteditable="true"]');
              const text=e?(e.innerText||e.value||''):'';
              const generating=!!document.querySelector(
                'button[data-testid="stop-button"],button[aria-label="Stop generating"]');
              return JSON.stringify([!!e,text.trim().length===0,generating]);
            })()
            """;
        string raw = await core.ExecuteScriptAsync(probe);
        string? json = JsonSerializer.Deserialize<string>(raw);
        bool[]? values = JsonSerializer.Deserialize<bool[]>(json ?? "[]");
        if (values is not { Length: 3 })
            throw new InvalidDataException("Invalid probe values");
        return new LiteHandoffRouteEvidence.Sample(
            core.Source, checked((int)core.BrowserProcessId), DateTimeOffset.UtcNow,
            values[0], values[1], values[2], false,
            "webview_response_probe", true, navigationEpoch);
    }
    private static void Record(string stage, string detail,
        bool ready, bool empty, int code)
    {
        try
        {
            string f = LogFile;
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            string line = JsonSerializer.Serialize(new {
                schema=1, utc=DateTimeOffset.UtcNow, stage, detail,
                composerReady=ready, composerEmpty=empty, exitCode=code,
                chatCreated=false, seedSent=false, leadChanged=false
            });
            File.AppendAllText(f, line + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch { /* Diagnostic write cannot cause a browser side effect. */ }
    }
}
