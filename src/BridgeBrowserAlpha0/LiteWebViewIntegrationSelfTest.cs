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
            const string firstChatId = "11111111-1111-1111-1111-111111111111";
            const string secondChatId = "22222222-2222-2222-2222-222222222222";
            string firstDir = Path.Combine(site, "c", firstChatId);
            string secondDir = Path.Combine(site, "c", secondChatId);
            Directory.CreateDirectory(firstDir);
            Directory.CreateDirectory(secondDir);
            File.WriteAllText(Path.Combine(firstDir, "index.html"),
                FixtureHtml, new System.Text.UTF8Encoding(false));
            File.WriteAllText(Path.Combine(secondDir, "index.html"),
                FixtureHtml.Replace("Czarny kompas", "Zielona latarnia",
                    StringComparison.Ordinal), new System.Text.UTF8Encoding(false));
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
                    var core = view.CoreWebView2;
                    phase = "check_worker_memory_target";
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                    Ensure(core.MemoryUsageTargetLevel == CoreWebView2MemoryUsageTargetLevel.Low,
                        "WebView2 supports Low worker memory target");
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                    Ensure(core.MemoryUsageTargetLevel == CoreWebView2MemoryUsageTargetLevel.Normal,
                        "WebView2 restores Normal before wake navigation");
                    phase = "virtual_host_mapping";
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
                    phase = "check_hydrated_worker";
                    Ensure(await EvaluateAsync(core,
                        ChatGptLiteForm.WakeWorkerHydratedScript) == "true",
                        "background worker readiness requires composer and message history");
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
                    phase = "worker_reuse_same_chat_isolated";
                    const string chatUrl =
                        "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
                    const string otherUrl =
                        "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
                    await core.ExecuteScriptAsync(
                        "history.replaceState({},'', '/c/11111111-1111-1111-1111-111111111111')");
                    Ensure(LiteDevWakeState.NormalizeUrl(core.Source) == chatUrl,
                        "offline fixture now has a valid normalized chat URL");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl),
                        "hydrated background chat may be reused without reload");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, otherUrl),
                        "a different background chat may never reuse the current DOM");
                    await core.ExecuteScriptAsync("""
                        window.__qaSavedTurns = Array.from(document.querySelector('main').children);
                        document.querySelector('main').replaceChildren();
                        """);
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl),
                        "composer-only application shell cannot be reused");
                    await core.ExecuteScriptAsync(
                        "document.querySelector('main').replaceChildren(...window.__qaSavedTurns)");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl),
                        "background chat becomes reusable when history returns");
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
                    phase = "check_dil_soft_pilot";
                    await core.ExecuteScriptAsync("""
                        (() => {
                          const root = document.querySelector('main');
                          for (let i = 0; i < 95; i++) {
                            const n = document.createElement('div');
                            n.style.minHeight = '145px';
                            n.setAttribute('data-chatgpt-selection-message-id', 'qa-' + i);
                            n.textContent = 'Offline historical assistant turn ' + i;
                            root.appendChild(n);
                          }
                        })()
                        """);
                    // Hidden sidebar aria-busy in the fixture must not block a
                    // deliberately enabled, offscreen-only CSS pilot.
                    Ensure(await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().modernSoftEnabled") == "false",
                        "modern soft optimizer remains disabled by default");
                    string elementCount = await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.getStatus().totalDomElements");
                    Ensure(int.TryParse(elementCount, out int totalElements) && totalElements > 0,
                        "WebView2 periodically records DOM element count");
                    Ensure(await EvaluateAsync(core,
                        "Number.isFinite(window.__OPENBROWSER_LITE_DOM__.getStatus().domSampleAt) && window.__OPENBROWSER_LITE_DOM__.getStatus().domSampleAt > 0") == "true",
                        "WebView2 records time of the DOM census");
                    string softCount = await EvaluateAsync(core,
                        "window.__OPENBROWSER_LITE_DOM__.setModernSoftEnabled(true).optimized");
                    Ensure(int.TryParse(softCount, out int softTurns) &&
                        softTurns >= 50 && softTurns <= 60,
                        "real WebView2 applies soft CSS to old block DIL messages");
                    await core.ExecuteScriptAsync(
                        "window.__OPENBROWSER_LITE_DOM__.setModernSoftEnabled(false)");
                    Ensure(await EvaluateAsync(core,
                        "document.querySelectorAll('[data-ob-lite-offscreen]').length") == "0",
                        "soft CSS pilot is fully reversible");

                    // The same WebView2 worker must isolate two different
                    // chat histories after real document navigations.
                    phase = "background_two_chat_navigation";
                    var secondNavigation = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    void secondCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
                        secondNavigation.TrySetResult(e.IsSuccess);
                    core.NavigationCompleted += secondCompleted;
                    core.Navigate("https://chatgpt.com/c/" + secondChatId + "/index.html");
                    Ensure(await secondNavigation.Task.WaitAsync(TimeSpan.FromSeconds(18)),
                        "second offline chat loaded in existing worker");
                    core.NavigationCompleted -= secondCompleted;
                    await core.ExecuteScriptAsync(
                        "history.replaceState({}, '', '/c/" + secondChatId + "')");
                    Ensure(LiteDevWakeState.NormalizeUrl(core.Source) == otherUrl,
                        "second chat has its own exact UUID URL");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl),
                        "first chat cannot use second chat's hydrated DOM");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, otherUrl),
                        "second chat can reuse its own loaded history");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.text"))
                        .Contains("Zielona latarnia"),
                        "second chat has its distinct assistant answer");
                    var firstNavigation = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    void firstCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
                        firstNavigation.TrySetResult(e.IsSuccess);
                    core.NavigationCompleted += firstCompleted;
                    core.Navigate("https://chatgpt.com/c/" + firstChatId + "/index.html");
                    Ensure(await firstNavigation.Task.WaitAsync(TimeSpan.FromSeconds(18)),
                        "first offline chat reloads in the same worker");
                    core.NavigationCompleted -= firstCompleted;
                    await core.ExecuteScriptAsync(
                        "history.replaceState({}, '', '/c/" + firstChatId + "')");
                    Ensure(!await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, otherUrl),
                        "second chat cannot use reloaded first chat DOM");
                    Ensure(await ChatGptLiteForm.IsWorkerHydratedForChatAsync(core, chatUrl),
                        "first chat hydrates again after switching back");
                    Ensure((await EvaluateAsync(core,
                        "window.__OPENBROWSER_RESUME_PROBE__?.observe()?.text"))
                        .Contains("Czarny kompas"),
                        "original answer returns without cross-chat contamination");

                    phase = "archive_cross_navigation_fence";
                    Ensure(ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 10, new Uri(chatUrl), chatUrl),
                        "archive accepts same chat and same navigation epoch");
                    Ensure(!ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 11, new Uri(chatUrl), chatUrl),
                        "archive rejects stale script after navigation");
                    Ensure(!ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 10, new Uri(chatUrl), otherUrl),
                        "archive rejects a completed script from another chat");
                    Ensure(!ChatGptLiteForm.IsArchiveResultCurrent(
                        10, 10, new Uri(chatUrl), "about:blank"),
                        "archive rejects non-ChatGPT navigation result");
                    LiteEventLog.Write("webview_integration_selftest", new {
                        passed = true, checks = 38, offline = true
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
<aside aria-busy="true" style="display:none">Background spinner, not a response</aside>
<main id="conversation">
<div data-user-message-bubble>Napisz książkę przygodową.</div>
<div data-chatgpt-selection-message-id="chapter1">Rozdział pierwszy.</div>
<div data-user-message-bubble>Kontynuuj przygodę.</div>
<div data-chatgpt-selection-message-id="chapter7">Rozdział 7. Czarny kompas.
Zostały wątki do rozwinięcia.</div>
</main>
<form data-chatgpt-composer>
<div id="prompt-textarea" contenteditable="true" aria-label="Napisz wiadomość"></div>
<button type="submit" aria-label="Wyślij">Wyślij</button>
<button type="button" aria-label="Tryb głosowy">Mów</button>
</form>
<script>
document.querySelector('form[data-chatgpt-composer]').addEventListener('submit',(e)=>{
 e.preventDefault();
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
