using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Only one live WebView exists in Lite. Prevent losing in-flight generation,
/// an unsubmitted draft, or unsaved user notes when switching chat links.
/// </summary>
internal sealed partial class ChatGptLiteForm
{
    private bool _tabSwitchProbePending;

    private async Task NavigateToProjectChatAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var destination) ||
            destination.Scheme != "https" ||
            !string.Equals(destination.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase))
            return;
        var core = _browser.CoreWebView2;
        if (core is null || _tabSwitchProbePending) return;
        if (core.Source == url) return;

        FlushNotes();
        if (_notesDirty)
        {
            MessageBox.Show(this,
                "Nie udało się zapisać notatek bieżącego czatu. " +
                "Przełączenie wstrzymane, aby ich nie utracić.",
                "Open Browser — ochrona notatek");
            return;
        }
        if (_lastGenerationSignal)
        {
            MessageBox.Show(this,
                "ChatGPT nadal sygnalizuje generowanie odpowiedzi. " +
                "Zaczekaj na zakończenie przed zmianą rozmowy.",
                "Open Browser — aktywna odpowiedź");
            LiteEventLog.Write("tab_switch_blocked", new { reason = "generation_signal" });
            return;
        }

        _tabSwitchProbePending = true;
        bool timedOut = false;
        try
        {
            const string script = @"(() => {
                const draft = document.querySelector('textarea,[contenteditable=""true""]');
                const text = draft ? (draft.value || draft.innerText || '') : '';
                const busy = !!document.querySelector(
                    '[data-testid=""stop-button""],[aria-busy=""true""]');
                return JSON.stringify({busy, draft: text.trim().length > 0});
            })()";
            var task = core.ExecuteScriptAsync(script);
            if (await Task.WhenAny(task, Task.Delay(5000)) != task)
            {
                timedOut = true;
                LiteEventLog.Write("tab_switch_blocked",
                    new { reason = "js_probe_timeout" });
                _ = task.ContinueWith(_ =>
                {
                    try
                    {
                        if (!IsDisposed && IsHandleCreated)
                            BeginInvoke((Action)(() => _tabSwitchProbePending = false));
                    }
                    catch { }
                });
                MessageBox.Show(this,
                    "Nie można potwierdzić stanu aktywnej rozmowy. " +
                    "Nie przełączono karty; renderer może nie odpowiadać.",
                    "Open Browser — brak odpowiedzi");
                return;
            }

            string? json = JsonSerializer.Deserialize<string>(await task);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Empty state probe");
            using var parsed = JsonDocument.Parse(json);
            bool busy = parsed.RootElement.GetProperty("busy").GetBoolean();
            bool draft = parsed.RootElement.GetProperty("draft").GetBoolean();
            if (busy || draft)
            {
                LiteEventLog.Write("tab_switch_blocked", new {
                    reason = busy ? "generating_or_busy" : "unsubmitted_draft"
                });
                MessageBox.Show(this,
                    busy ? "W tym czacie trwa praca. Przełączenie wstrzymane." :
                           "Pole wpisywania zawiera niewysłaną wiadomość. " +
                           "Wyślij ją lub zapisz przed zmianą czatu.",
                    "Open Browser — ochrona rozmowy");
                return;
            }
            LiteEventLog.Write("tab_switch_accepted");
            core.Navigate(url);
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("tab_switch_probe_failed",
                new { errorType = ex.GetType().Name });
        }
        finally
        {
            if (!timedOut) _tabSwitchProbePending = false;
        }
    }
}
