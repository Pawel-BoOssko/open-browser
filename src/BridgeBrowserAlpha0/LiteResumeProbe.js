(function initOpenBrowserResumeProbe(w) {
  "use strict";
  if (w.__OPENBROWSER_RESUME_PROBE__) return;
  if (!w.document || w.location.hostname !== "chatgpt.com") return;

  function observe() {
    const d = w.document;
    const parts = Array.from(d.querySelectorAll('[data-message-author-role]'));
    let last = parts[parts.length - 1] || null;
    const role = last ? last.getAttribute("data-message-author-role") : "";
    const text = last && role === "assistant" ?
      String(last.innerText || "").slice(-8000) : "";
    const stop = !!d.querySelector(
      '[data-testid="stop-button"],button[aria-label*="Stop generating"],button[aria-label*="Przerwij generowanie"]');
    const toolBusy = !!d.querySelector(
      '[data-testid*="tool-running"],[data-state="running"],[aria-busy="true"]');
    const editor = d.querySelector('textarea,[contenteditable="true"]');
    const draft = editor && String(editor.value || editor.innerText || "").trim().length > 0;
    const errorText = d.querySelector('[role="alert"],[data-testid*="error"]');
    const error = errorText &&
      /nie udało się wczytać|failed to load|przerwano|interrupted|przekroczono limit|timed out/i.test(
        String(errorText.textContent || "").slice(-400));
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
