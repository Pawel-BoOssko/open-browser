(function initOpenBrowserResumeProbe(w) {
  "use strict";
  if (w.__OPENBROWSER_RESUME_PROBE__) return;
  if (!w.document || w.location.hostname !== "chatgpt.com") return;

  function observe() {
    const d = w.document;
    // ChatGPT's DIL UI (verified 2026-10-18 on the owner's test chat)
    // uses data-chatgpt-selection-message-id for assistant replies and
    // data-user-message-bubble for user prompts instead of the old role.
    const legacy = Array.from(d.querySelectorAll('[data-message-author-role]'));
    const modern = legacy.length ? [] : Array.from(d.querySelectorAll(
      '[data-user-message-bubble],[data-chatgpt-selection-message-id]'));
    const parts = legacy.length ? legacy : modern;
    const last = parts[parts.length - 1] || null;
    const role = last ? (legacy.length ? last.getAttribute('data-message-author-role') :
      last.hasAttribute('data-chatgpt-selection-message-id') ? 'assistant' :
      last.hasAttribute('data-user-message-bubble') ? 'user' : 'unknown') : 'unknown';
    const text = last && role === "assistant" ?
      String(last.innerText || "").slice(-8000) : "";
    const stop = !!d.querySelector(
      '[data-testid="stop-button"],button[aria-label*="Stop generating"],button[aria-label*="Przerwij generowanie"]');
    const toolBusy = !!d.querySelector(
      '[data-testid*="tool-running"],[data-state="running"],[aria-busy="true"]');
    const editor = d.querySelector('textarea,[contenteditable="true"]');
    const draft = editor && String(editor.value || editor.innerText || "").trim().length > 0;
    const errorText = d.querySelector('[role="alert"],[data-testid*="error"]');
    const alertError = !!errorText &&
      /nie udało się wczytać|failed to load|przerwano|interrupted|przekroczono limit|timed out/i.test(
        String(errorText.textContent || "").slice(-400));
    // The centralized detector also checks short DIL assistant error placeholders.
    // User quotations are deliberately excluded by that detector.
    const responseError = w.__OPENBROWSER_RESPONSE_SIGNALS__?.read()?.visibleError;
    const error = alertError || !!responseError;
    const modelModes = Array.from(d.querySelectorAll('button')).slice(-45)
      .map(button => String(button.innerText || button.textContent || '').trim().toLowerCase())
      .filter(value => /^(high|instant|medium|extra high)$/.test(value));
    const verified = w.__OPENBROWSER_HIGH_GUARD__?.audit();
    const reasoningMode = verified ? verified.mode :
      (modelModes.length ? modelModes[modelModes.length - 1] : 'unknown');
    return {
      reasoningMode,
      location: w.location.pathname,
      role: role === "assistant" || role === "user" ? role : "unknown",
      text: text,
      generating: stop,
      toolBusy: toolBusy,
      hasDraft: !!draft,
      error: !!error,
      composerReady: !!editor,
      turnCount: parts.length,
      observedAt: Date.now()
    };
  }
  w.__OPENBROWSER_RESUME_PROBE__ = { observe };
})(window);
