(function initOpenBrowserResumeSender(w) {
  "use strict";
  if (w.__OPENBROWSER_RESUME_SENDER__) return;
  if (!w.document || w.location.hostname !== "chatgpt.com") return;
  const attempted = new Set();
  const phases = new Map(); // receipt -> prepared / clicked / blocked; no message text stored
  const samePayload = (value, payload) => String(value || "")
    .replace(/\u00a0/g, " ").replace(/\s+/g, " ").trim() ===
      String(payload).replace(/\u00a0/g, " ").replace(/\s+/g, " ").trim();
  function phase(receiptId) {
    if (hasAck(receiptId)) return "visible_ack";
    return phases.get(receiptId) || "not_attempted";
  }
  function stopVisible() {
    // Match the probe's supported Stop controls, including ChatGPT variants
    // that expose only an accessible label and no data-testid.
    const button = w.document.querySelector(
      '[data-testid="stop-button"],button[aria-label*="Stop generating"],button[aria-label*="Przerwij generowanie"]');
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
    const button = sendButton(el);
    return {
      reason: busyReason() || 'ready',
      sendButtonFound: !!button,
      sendButtonDisabled: !!(button?.disabled || button?.getAttribute?.('aria-disabled') === 'true'),
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
  // Find Send next to the actual editor, not a similarly labelled button
  // elsewhere on the page (or the adjacent voice-mode control).
  function sendButton(editor) {
    const form = editor?.closest?.('form') ||
      w.document.querySelector('form[data-chatgpt-composer]');
    const query = 'button[type="submit"][aria-label="Wyślij"],'+
      'button[type="submit"][aria-label="Send"],'+
      'button[type="submit"][aria-label="Send message"]';
    const local = form?.querySelector?.(query);
    if (local) return local;
    // Older ChatGPT sends through data-testid; only use it if it belongs
    // to the editor form or the editor has no discoverable form.
    const legacy = w.document.querySelector('[data-testid="send-button"]');
    if (!legacy) return null;
    return !form || form.contains?.(legacy) ? legacy : null;
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
    phases.set(receiptId, "preparing");
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
    // Never equate insertion into the editor with a send. Capture the click
    // outcome for the host; any missing control leaves an explicit draft state.
    phases.set(receiptId, "draft_prepared");
    w.setTimeout(() => {
      if (w.location.hostname !== "chatgpt.com" ||
          w.location.pathname !== targetPath) {
        phases.set(receiptId, "blocked_navigation"); return;
      }
      if (composer() !== el) {
        phases.set(receiptId, "blocked_editor_replaced"); return;
      }
      if (busy()) {
        phases.set(receiptId, "blocked_generation"); return;
      }
      if (!samePayload(el.value || el.innerText || el.textContent, payload)) {
        phases.set(receiptId, "blocked_editor_changed"); return;
      }
      // Observed ChatGPT DIL composer on 2026-10-19: the Send button has
      // no data-testid. Scope the exact submit action to the composer form;
      // never mistake the purple voice button for Send.
      const btn = sendButton(el);
      if (!btn) {
        phases.set(receiptId, "draft_button_missing"); return;
      }
      if (btn.disabled || btn.getAttribute?.('aria-disabled') === 'true') {
        phases.set(receiptId, "draft_button_disabled"); return;
      }
      try {
        btn.click();
        phases.set(receiptId, "click_invoked");
      } catch (_) { phases.set(receiptId, "draft_click_failed"); }
    }, 250);
    return "submission_requested";
  }
  w.__OPENBROWSER_RESUME_SENDER__ = { send, hasAck, diagnose, phase };
})(window);
