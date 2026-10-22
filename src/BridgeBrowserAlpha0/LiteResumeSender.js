(function initOpenBrowserResumeSender(w) {
  "use strict";
  if (w.__OPENBROWSER_RESUME_SENDER__) return;
  if (!w.document || w.location.hostname !== "chatgpt.com") return;
  // Bounded metadata: long-lived SPA documents must not accumulate receipts
  // and old pending message bodies indefinitely.
  const MAX_RECEIPTS = 128;
  const MAX_PENDING = 8;
  const PENDING_LIFETIME_MS = 15 * 60 * 1000;
  const attempted = new Set();
  const phases = new Map(); // receipt -> latest phase, no message text
  const directPending = new Map(); // receipt -> current editor/path/payload, briefly
  function rememberAttempt(id) {
    attempted.add(id);
    while (attempted.size > MAX_RECEIPTS)
      attempted.delete(attempted.values().next().value);
  }
  function rememberPhase(id, value) {
    phases.set(id, value);
    while (phases.size > MAX_RECEIPTS)
      phases.delete(phases.keys().next().value);
  }
  function forgetDirect(id, result) {
    directPending.delete(id);
    rememberPhase(id, result);
    return result;
  }
  function expireDirectPending() {
    const now = Date.now();
    for (const [id, item] of directPending) {
      if (now - item.createdAt >= PENDING_LIFETIME_MS)
        forgetDirect(id, "blocked_pending_expired");
    }
    while (directPending.size >= MAX_PENDING) {
      const id = directPending.keys().next().value;
      forgetDirect(id, "blocked_pending_evicted");
    }
  }
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
      '[data-testid="stop-button"],button[aria-label*="Stop generating"],button[aria-label*="Przerwij generowanie"],button[aria-label="Zatrzymaj"]');
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
      globalAriaBusyCount: w.document.querySelectorAll?.('[aria-busy="true"]')?.length || 0,
      // Counts only: no prompt text, drafts or receipt IDs in diagnostics.
      rememberedReceipts: attempted.size,
      rememberedPhases: phases.size,
      pendingReceipts: directPending.size
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
    // Only the current conversation's MAIN user bubbles prove delivery.
    // Sidebar previews or assistant text quoting a receipt are not an ACK.
    // Never fall back to document-wide search on a shell without main.
    if (!/^[a-f0-9]{32}$/.test(receiptId)) return false;
    const main = w.document.querySelector('main,[role="main"]');
    if (!main) return false;
    const marker = "[OB-" + receiptId + "]";
    return Array.from(main.querySelectorAll(
      '[data-message-author-role="user"],[data-user-message-bubble]'))
      .slice(-8).some(n => String(n.innerText || n.textContent || "").includes(marker));
  }
  function send(text, receiptId) {
    if (!/^\/c\//.test(w.location.pathname) ||
        !/^[a-f0-9]{32}$/.test(receiptId) ||
        text.length < 16 || text.length > 1700) return "invalid";
    if (hasAck(receiptId)) { directPending.delete(receiptId); return "already_visible"; }
    if (attempted.has(receiptId)) return "already_attempted";
    const reason = busyReason();
    if (reason) return 'busy_' + reason;
    const el = composer();
    const targetPath = w.location.pathname;
    rememberAttempt(receiptId);
    rememberPhase(receiptId, "preparing");
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
    rememberPhase(receiptId, "draft_prepared");
    w.setTimeout(() => {
      if (w.location.hostname !== "chatgpt.com" ||
          w.location.pathname !== targetPath) {
        rememberPhase(receiptId, "blocked_navigation"); return;
      }
      if (composer() !== el) {
        rememberPhase(receiptId, "blocked_editor_replaced"); return;
      }
      if (busy()) {
        rememberPhase(receiptId, "blocked_generation"); return;
      }
      if (!samePayload(el.value || el.innerText || el.textContent, payload)) {
        rememberPhase(receiptId, "blocked_editor_changed"); return;
      }
      // Observed ChatGPT DIL composer on 2026-10-19: the Send button has
      // no data-testid. Scope the exact submit action to the composer form;
      // never mistake the purple voice button for Send.
      const btn = sendButton(el);
      if (!btn) {
        rememberPhase(receiptId, "draft_button_missing"); return;
      }
      if (btn.disabled || btn.getAttribute?.('aria-disabled') === 'true') {
        rememberPhase(receiptId, "draft_button_disabled"); return;
      }
      try {
        btn.click();
        rememberPhase(receiptId, "click_invoked");
      } catch (_) { rememberPhase(receiptId, "draft_click_failed"); }
    }, 250);
    return "submission_requested";
  }
  // Bring back the pre-Lite direct editor injection path. Delivery auditing is
  // intentionally not a prerequisite for trying the Send action.
  function sendDirect(text, receiptId, hostManaged = false, expectedPath = null) {
    // A host-selected chat URL is a security boundary, not a suggestion.
    // SPA navigation after host preflight must never inject into another chat.
    if (expectedPath !== null &&
        (typeof expectedPath !== "string" || w.location.pathname !== expectedPath))
      return "blocked_navigation";
    if (!/^\/c\/(?:[a-z0-9-]|%3[Aa]){30,80}$/.test(w.location.pathname) ||
        !/^[a-f0-9]{32}$/.test(receiptId) ||
        typeof text !== "string" || text.length < 16 || text.length > 45000)
      return "invalid";
    expireDirectPending();
    if (hasAck(receiptId)) { directPending.delete(receiptId); return "already_visible"; }
    if (attempted.has(receiptId)) return "already_attempted";
    const reason = busyReason();
    if (reason) return "busy_" + reason;
    const el = composer();
    if (!el) return "missing_composer";
    const targetPath = w.location.pathname;
    const payload = text;
    rememberAttempt(receiptId);
    directPending.set(receiptId, {
      editor: el, path: targetPath, payload, createdAt: Date.now()
    });
    el.focus();
    try {
      if ("value" in el) {
        const native = Object.getOwnPropertyDescriptor(
          w.HTMLTextAreaElement.prototype, "value");
        if (native?.set) native.set.call(el, payload);
        else el.value = payload;
        el.dispatchEvent(new w.Event("input", { bubbles: true }));
      } else if (!w.document.execCommand?.("insertText", false, payload)) {
        el.textContent = payload;
        el.dispatchEvent(new w.Event("input", { bubbles: true }));
      }
    } catch (_) {
      el.textContent = payload;
      el.dispatchEvent(new w.Event("input", { bubbles: true }));
    }
    rememberPhase(receiptId, "direct_injected");
    const trySubmit = (remaining) => {
      const phaseNow = phases.get(receiptId) || "";
      if (phaseNow === "click_invoked" || phaseNow === "form_submitted" ||
          phaseNow.startsWith("blocked_")) {
        directPending.delete(receiptId);
        return;
      }
      if (w.location.pathname !== targetPath) {
        forgetDirect(receiptId, "blocked_navigation"); return;
      }
      if (composer() !== el || busy() ||
          !samePayload(el.value || el.innerText || el.textContent, payload)) {
        forgetDirect(receiptId, "blocked_changed_or_busy"); return;
      }
      const form = el.closest?.("form") || w.document.querySelector("form[data-chatgpt-composer]");
      const btn = sendButton(el) ||
        form?.querySelector?.('button[type="submit"]:not([disabled])') ||
        w.document.querySelector('[data-testid="send-button"],button[aria-label="Wyślij"],button[aria-label="Send"],button[aria-label="Send message"]');
      if (btn && !btn.disabled) {
        btn.click();
        forgetDirect(receiptId, "click_invoked");
      } else if (remaining > 0) {
        w.setTimeout(() => trySubmit(remaining - 1), 250);
      } else {
        // requestSubmit on an unhydrated React form is not evidence of delivery.
        rememberPhase(receiptId, "draft_button_missing");
      }
    };
    if (!hostManaged) w.setTimeout(() => trySubmit(6), 500);
    return "submission_requested";
  }
  // Called synchronously by the host after an awaited delay. Background tabs
  // can throttle page setTimeout, so delivery must not depend on that timer.
  function submitDirect(receiptId) {
    const previous = phases.get(receiptId);
    if (previous === "click_invoked" || previous === "form_submitted" ||
        previous?.startsWith("blocked_")) {
      directPending.delete(receiptId);
      return previous;
    }
    const pending = directPending.get(receiptId);
    if (!pending) return "unknown_receipt";
    if (w.location.pathname !== pending.path)
      return forgetDirect(receiptId, "blocked_navigation");
    const el = pending.editor;
    if (composer() !== el)
      return forgetDirect(receiptId, "blocked_editor_replaced");
    if (busy())
      return forgetDirect(receiptId, "blocked_generation");
    if (!samePayload(el.value || el.innerText || el.textContent, pending.payload))
      return forgetDirect(receiptId, "blocked_editor_changed");
    const form = el.closest?.("form") ||
      w.document.querySelector("form[data-chatgpt-composer]");
    const button = sendButton(el) ||
      form?.querySelector?.('button[type="submit"]:not([disabled])') ||
      w.document.querySelector('[data-testid="send-button"],button[aria-label="Wyślij"],button[aria-label="Send"],button[aria-label="Send message"]');
    if (button && !button.disabled &&
        button.getAttribute?.('aria-disabled') !== 'true') {
      button.click();
      return forgetDirect(receiptId, "click_invoked");
    }
    // Fail closed rather than silently submitting a stale or unhydrated form.
    return "draft_button_missing";
  }
  // Recover only our own persisted wake draft after a stopped or interrupted
  // send. Never touch ordinary user text. The host still confirms delivery.
  let lastRecoveredReceipt = null;
  function recoveredDraftVisible() {
    return lastRecoveredReceipt !== null && hasAck(lastRecoveredReceipt);
  }
  function submitOwnDraft() {
    if (busy()) return "busy_generating";
    const el = composer();
    if (!el) return "missing_composer";
    const value = String(el.value || el.innerText || el.textContent || "");
    const match = /^\[OB-([a-f0-9]{32})\]\s*\n[\s\S]{16,45000}$/.exec(value);
    if (!match) return "not_own_draft";
    lastRecoveredReceipt = match[1];
    if (hasAck(match[1])) return "already_visible";
    const form = el.closest?.("form") ||
      w.document.querySelector("form[data-chatgpt-composer]");
    const btn = sendButton(el) ||
      form?.querySelector?.('button[type="submit"]:not([disabled])') ||
      w.document.querySelector('[data-testid="send-button"],button[aria-label="Wyślij"],button[aria-label="Send"],button[aria-label="Send message"]');
    if (!btn || btn.disabled || btn.getAttribute?.("aria-disabled") === "true")
      return "draft_button_missing";
    try {
      btn.click();
      return "click_invoked";
    } catch (_) { return "click_failed"; }
  }
  w.__OPENBROWSER_RESUME_SENDER__ = { send, sendDirect, submitDirect,
    submitOwnDraft, recoveredDraftVisible, hasAck, diagnose, phase };
})(window);
