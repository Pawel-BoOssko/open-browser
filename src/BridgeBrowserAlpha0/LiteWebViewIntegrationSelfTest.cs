using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Actual WebView2 integration test without an account, tokens, network ChatGPT
/// or access to the live profile. chatgpt.com is mapped to local fixture files
/// inside a brand-new, disposable user data folder.
/// </summary>
internal static class LiteWebViewIntegrationSelfTest
{
    internal static int Run()
    {
        string temp = Path.Combine(Path.GetTempPath(),
            "OpenBrowser-WebView2-QA-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        int code = 9;
        try
        {
            string site = Path.Combine(temp, "site");
            Directory.CreateDirectory(Path.Combine(site, "c", "test"));
            File.WriteAllText(Path.Combine(site, "c", "test", "index.html"),
                FixtureHtml, new System.Text.UTF8Encoding(false));
            using var form = new Form {
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000), Size = new Size(640, 420),
                Text = "Open Browser isolated WebView QA", Opacity = 1
            };
            using var view = new WebView2 { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(55));
            deadline.Token.Register(() => {
                try { form.BeginInvoke(() => form.Close()); } catch { }
            });
            form.Shown += async (_, _) => {
                string phase = "form_shown";
                try {
                    phase = "create_webview_environment";
                    var env = await CoreWebView2Environment.CreateAsync(
                        userDataFolder: Path.Combine(temp, "profile"));
                    phase = "ensure_webview_core";
                    await view.EnsureCoreWebView2Async(env);
                    phase = "virtual_host_mapping";
                    var core = view.CoreWebView2;
                    core.SetVirtualHostNameToFolderMapping("chatgpt.com", site,
                        CoreWebView2HostResourceAccessKind.DenyCors);
                    phase = "install_document_scripts";
                    foreach (string name in new[] {
                        "LiteHighGuard.js", "LiteResponseSignal.js",
                        "LiteResumeProbe.js", "LiteResumeSender.js",
                        "LiteDomOptimizer.js"
                    }) {
                        string script = File.ReadAllText(Path.Combine(
                            AppContext.BaseDirectory, name));
                        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
                    }
                    var navigation = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    void onNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
                        navigation.TrySetResult(e.IsSuccess);
                    }
                    core.NavigationCompleted += onNavigated;
                    phase = "navigate_virtual_host";
                    core.Navigate("https://chatgpt.com/c/test/index.html");
                    bool success = await navigation.Task.WaitAsync(
                        TimeSpan.FromSeconds(18));
                    core.NavigationCompleted -= onNavigated;
                    Ensure(success, "local virtual host navigation");
                    phase = "check_high";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_HIGH_GUARD__?.audit()?.mode") == "high",
                        "true WebView2 recognizes High data attribute");
                    phase = "check_dil_dom_count";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__?.check()?.turnCount") == "4",
                        "DIL turn counter counts all four messages");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__?.getStatus()?.domVariant") == "dil_observe_only",
                        "DIL DOM is observed without aggressive mutation");
                    phase = "check_last_role";
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.role") == "assistant",
                        "DIL layout recognizes last assistant");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.text"))
                        .Contains("Czarny kompas"),
                        "read last DIL assistant content");
                    const string receipt = "1234567890abcdef1234567890abcdef";
                    var js = "window.__OPENBROWSER_RESUME_SENDER__?.send(" +
                        JsonSerializer.Serialize("Napisz ósmy rozdział tej testowej książki.") +
                        "," + JsonSerializer.Serialize(receipt) + ")";
                    phase = "send_test_message";
                    var state = await EvaluateAsync(core, js);
                    Ensure(state == "submission_requested", "one submission requested");
                    phase = "wait_and_check_ack";
                    await Task.Delay(850);
                    bool ack = await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_SENDER__?.hasAck('" + receipt + "')") == "true";
                    if (!ack) {
                        var bubbles = await EvaluateAsync(core,
                            "document.querySelectorAll('[data-user-message-bubble]').length");
                        var draftLength = await EvaluateAsync(core,
                            "document.querySelector('#prompt-textarea').innerText.length");
                        var includesMarker = await EvaluateAsync(core,
                            "document.querySelector('#prompt-textarea').innerText.includes('" + receipt + "')");
                        var bubbleInfo = await EvaluateAsync(core,
                            "(() => {let b=[...document.querySelectorAll('[data-user-message-bubble]')].at(-1); " +
                            "return JSON.stringify({len:b.textContent.length, marker:b.textContent.includes('" + receipt + "')," +
                            " visibleLen:b.innerText.length});})()");
                        throw new InvalidOperationException("ack_not_found; bubbles=" + bubbles +
                            "; draftLength=" + draftLength + "; markerInEditor=" + includesMarker +
                            "; lastBubble=" + bubbleInfo);
                    }
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('[data-user-message-bubble]').length") == "3",
                        "exactly one new user bubble");
                    Ensure(await EvaluateAsync(core, js) == "already_visible",
                        "replaying same receipt does not send twice");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('[data-user-message-bubble]').length") == "3",
                        "no duplicate bubble after repeated call");
                    phase = "detect_and_clear_visible_failure";
                    await core.ExecuteScriptAsync("(() => {const n=document.createElement('div'); " +
                        "n.id='qa-error';n.setAttribute('data-chatgpt-selection-message-id','failure');" +
                        "n.innerText='Nie udało się wczytać tej odpowiedzi';" +
                        "document.querySelector('main').appendChild(n);})()");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESPONSE_SIGNALS__.read().visibleError") ==
                        "response_load_failed", "DIL failure recognized as real UI error");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().error") == "true",
                        "supervisor stops when the UI shows a DIL failure");
                    await core.ExecuteScriptAsync("document.getElementById('qa-error').remove()");
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__.observe().error") == "false",
                        "test fixture error can disappear without automatic retry");
                    LiteEventLog.Write("webview_integration_selftest", new {
                        passed = true, checks = 13, offline = true
                    });
                    code = 0;
                }
                catch (Exception ex) {
                    LiteEventLog.Write("webview_integration_selftest", new {
                        passed = false, phase, errorType = ex.GetType().Name,
                        message = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message,
                        offline = true
                    });
                    code = 4;
                }
                finally { form.Close(); }
            };
            Application.Run(form);
        }
        catch (Exception ex) {
            LiteEventLog.Write("webview_integration_selftest", new {
                passed = false, errorType = ex.GetType().Name, offline = true
            });
            code = 5;
        }
        finally {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
        return code;
    }

    private static async Task<string> EvaluateAsync(CoreWebView2 core, string js)
    {
        var result = await core.ExecuteScriptAsync(js).WaitAsync(
            TimeSpan.FromSeconds(9));
        using var parsed = JsonDocument.Parse(result);
        return parsed.RootElement.ValueKind switch {
            JsonValueKind.String => parsed.RootElement.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => parsed.RootElement.ToString()
        };
    }
    private static void Ensure(bool ok, string name) {
        if (!ok) throw new InvalidOperationException(name);
    }

    private const string FixtureHtml = """
<!doctype html>
<html lang="pl"><head><meta charset="utf-8"><title>testowy</title></head>
<body>
<button aria-label="Wybierz model ChatGPT" data-selected-reasoning-effort="high">
Wysiłek myślowy</button>
<main id="conversation">
<div data-user-message-bubble>Napisz książkę przygodową.</div>
<div data-chatgpt-selection-message-id="chapter1">Rozdział pierwszy.</div>
<div data-user-message-bubble>Kontynuuj przygodę.</div>
<div data-chatgpt-selection-message-id="chapter7">Rozdział 7. Czarny kompas.
Zostały wątki do rozwinięcia.</div>
</main>
<div id="prompt-textarea" contenteditable="true" aria-label="Napisz wiadomość"></div>
<button data-testid="send-button">Wyślij</button>
<script>
document.querySelector('[data-testid="send-button"]').addEventListener('click',()=>{
 const editor=document.querySelector('#prompt-textarea');
 const item=document.createElement('div');
 item.setAttribute('data-user-message-bubble','');
 item.textContent=editor.innerText;
 document.querySelector('main').appendChild(item);
 editor.innerText='';
});
</script>
</body></html>
""";
}
