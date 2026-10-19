(function initOpenBrowserResumeSender(w) {
  "use strict";
  if (w.__OPENBROWSER_RESUME_SENDER__) return;
  if (!w.document || w.location.hostname !== "chatgpt.com") return;
  const attempted = new Set();
  function stopVisible() {
    const button = w.document.querySelector('[data-testid="stop-button"]');
    if (!button || button.disabled || button.hidden ||
        button.getAttribute?.('aria-hidden') === 'true') return false;
    if (typeof button.getClientRects === 'function' &&
        button.getClientRects().length === 0) return false;
    return true;
  }
  // Aria-busy on an unrelated sidebar or hidden streaming placeholder is
  // not evidence that the current composer cannot submit a new message.
  function busy() { return stopVisible(); }
  function busyReason() {
    const el = composer();
    if (!el) return 'missing_composer';
    if (busy()) return 'generating';
    if (String(el.value || el.innerText || '').trim()) return 'draft_present';
    return '';
  }
  function diagnose() {
    const el = composer();
    return {
      reason: busyReason() || 'ready',
      editorTag: el?.tagName || 'unknown',
      editorChars: String(el?.value || el?.innerText || '').length,
      stopPresent: !!w.document.querySelector('[data-testid="stop-button"]'),
      stopVisible: busy(),
      // Only counts; never copies user drafts or page content.
      globalAriaBusyCount: w.document.querySelectorAll?.('[aria-busy="true"]')?.length || 0
    };
  }
  function composer() {
    // Prefer the actual ChatGPT editor over unrelated or hidden textareas.
    // Keep this order in sync with LiteResumeProbe.observe().
    return w.document.querySelector('#prompt-textarea') ||
      w.document.querySelector('[data-testid="composer-text-input"]') ||
      w.document.querySelector('textarea') ||
      w.document.querySelector('[contenteditable="true"]');
  }
  function hasAck(receiptId) {
    // ChatGPT DIL renderer uses data-user-message-bubble (verified 2026-10-18).
    // The legacy selector is kept for older or alternate UI variants.
    return Array.from(w.document.querySelectorAll(
      '[data-message-author-role="user"],[data-user-message-bubble]'))
      .slice(-8).some(n => String(n.innerText || n.textContent || "").includes(receiptId));
  }
  function send(text, receiptId) {
    if (!/^\/c\//.test(w.location.pathname) ||
        !/^[a-f0-9]{32}$/.test(receiptId) ||
        text.length < 16 || text.length > 1700) return "invalid";
    if (hasAck(receiptId)) return "already_visible";
    if (attempted.has(receiptId)) return "already_attempted";
    const reason = busyReason();
    if (reason) return 'busy_' + reason;
    const el = composer();
    const targetPath = w.location.pathname;
    attempted.add(receiptId);
    const payload = "[OB-" + receiptId + "]\n" + text;
    el.focus();
    if ("value" in el) {
      const native = Object.getOwnPropertyDescriptor(
        w.HTMLTextAreaElement.prototype, "value");
      if (!native || !native.set) return "unsupported";
      native.set.call(el, payload);
      el.dispatchEvent(new w.InputEvent("input", { bubbles: true, data: payload,
        inputType: "insertText" }));
    } else {
      if (!w.document.execCommand("insertText", false, payload))
        return "unsupported";
    }
    // Recheck state just before clicking: React may replace the composer, the
    // operator can type a draft, or the user may navigate during this delay.
    w.setTimeout(() => {
      if (w.location.hostname !== "chatgpt.com" ||
          w.location.pathname !== targetPath || composer() !== el || busy() ||
          String(el.value || el.innerText || "") !== payload) return;
      const btn = w.document.querySelector('[data-testid="send-button"]');
      if (btn && !btn.disabled) btn.click();
    }, 250);
    return "submission_requested";
  }
  w.__OPENBROWSER_RESUME_SENDER__ = { send, hasAck, diagnose };
})(window);
