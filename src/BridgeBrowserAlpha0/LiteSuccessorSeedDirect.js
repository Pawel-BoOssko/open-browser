(function (w) {
  "use strict";
  // First message only, only on logged-in new-chat "/" route.
  // No timeouts, intervals, retry, or hidden clicks.
  if (w.__OBL_SUCCESSOR_SEED_V1__) return;
  let pending = null;
  let attempted = false;
  let lastInspection = {reason:"not_checked"};
  const goodReceipt = r => typeof r === "string" && /^[a-f0-9]{32}$/.test(r);
  // The engine's current successor wake is a two-line numbered OBL action.
  // Do not pin this bridge to the retired 2026-10-28 generic sentence.
  const numberedAction = content => /^\[OB-WAKE-\d{6}\]\n(?:Kontynuuj projekt: [^\r\n]{24,800}|Kontynuuj pracę w swoim projekcie zgodnie z zasadami\.)$/.test(content);
  // P0 2026-11-02: successor N+1 uses the full, owner-approved OBL
  // instruction payload. The old two-line-only validator is NIEAKTUALNE
  // for registered OBL leads. Keep both formats for compatibility.
  const extendedNumberedAction = content =>
    /^\[OB-WAKE-\d{6}\]\nOBL, tylko SATANAMD\.[\s\S]{0,23000}\nKontynuuj projekt: [^\r\n]{24,800}$/.test(content);
  const root = () => w.location.origin === "https://chatgpt.com" &&
    w.location.pathname === "/" && !w.location.hash;
  const editor = () => w.document.querySelector(
    '#prompt-textarea,[data-testid="composer-text-input"],textarea,[contenteditable="true"]');
  const body = e => ((e?.value ?? e?.innerText ?? e?.textContent) || "").trim();
  // ChatGPT rich editors normalize line breaks and whitespace in innerText.
  // Match the existing working LiteResumeSender equivalence without losing content.
  const samePayload = (actual, expected) => String(actual || "")
    .replace(/\u00a0/g, " ").replace(/\s+/g, " ").trim() ===
      String(expected).replace(/\u00a0/g, " ").replace(/\s+/g, " ").trim();
  const busy = () => !!w.document.querySelector(
    'button[data-testid="stop-button"],button[aria-label="Stop generating"]');
  function stage(receipt, content) {
    if (attempted || pending) return "already_attempted";
    if (!root()) return "blocked_route";
    if (!goodReceipt(receipt) || typeof content !== "string" ||
        content.length < 32 || content.length > 24000 ||
        !(content.includes(receipt) || numberedAction(content) ||
          extendedNumberedAction(content))) {
      lastInspection = {reason:"invalid_seed", chars:content?.length || 0,
        numbered: /^\[OB-WAKE-\d{6}\]/.test(content || ""),
        recognizedAction: numberedAction(content || "") ||
          extendedNumberedAction(content || "")};
      return "invalid_seed";
    }
    const el = editor();
    if (!el) return "blocked_missing_composer";
    if (body(el)) return "blocked_draft";
    if (busy()) return "blocked_busy";
    pending = {receipt, content, editor:el};
    try {
      el.focus();
      if ("value" in el) {
        const setter = Object.getOwnPropertyDescriptor(
          w.HTMLTextAreaElement.prototype, "value")?.set;
        if (setter) setter.call(el, content); else el.value = content;
      } else if (!w.document.execCommand?.("insertText", false, content)) {
        el.textContent = content;
      }
      el.dispatchEvent(new w.Event("input", {bubbles:true}));
      if (!samePayload(body(el), content)) {
        lastInspection = {reason:"content_mismatch", expectedChars:content.length,
          actualChars:body(el).length, editorTag:el.tagName || "unknown",
          hasReceipt:body(el).includes(receipt)};
        pending = null;
        return "blocked_content_mismatch";
      }
      lastInspection = {reason:"staged", expectedChars:content.length,
        actualChars:body(el).length, editorTag:el.tagName || "unknown",
        hasReceipt:body(el).includes(receipt)};
      return "staged";
    } catch (_) {
      pending = null;
      return "blocked_editor_error";
    }
  }
  // Only during PREPARING after a definitely non-clicked previous stage.
  // Never delete a user's draft: require both persisted own receipt and wake ID.
  function clearOwnUnsentDraft(receipt, wakeNumber) {
    if (attempted || pending || !root() || !goodReceipt(receipt) ||
        !Number.isInteger(wakeNumber) || wakeNumber < 1)
      return "blocked_state";
    const el = editor();
    if (!el) return "blocked_missing_composer";
    const current = body(el);
    if (!current) return "already_blank";
    const marker = "[OB-" + receipt + "]";
    const numbered = "[OB-WAKE-" + String(wakeNumber).padStart(6, "0") + "]";
    // The minimal OBL wake carries its numeric ID, not an extra receipt.
    const minimalOwn = current === numbered + "\nKontynuuj pracę w swoim projekcie zgodnie z zasadami.";
    if ((!current.includes(marker) && !minimalOwn) ||
        !current.includes(numbered) ||
        w.document.querySelector('main [data-user-message-bubble]'))
      return "blocked_not_ours";
    try {
      el.focus();
      if ("value" in el) {
        const setter = Object.getOwnPropertyDescriptor(
          w.HTMLTextAreaElement.prototype, "value")?.set;
        if (setter) setter.call(el, ""); else el.value = "";
      } else {
        w.document.execCommand?.("selectAll", false, null);
        w.document.execCommand?.("delete", false, null);
        if (body(el)) el.textContent = "";
      }
      el.dispatchEvent(new w.Event("input", {bubbles:true}));
      return body(el) === "" ? "cleared_own_draft" : "blocked_clear_failed";
    } catch (_) {
      return "blocked_clear_exception";
    }
  }
  function clickOnce(receipt) {
    if (attempted) return "already_attempted";
    // A click may be dispatched even if a JS event handler throws.
    // Never retry any click returned by this function.
    const p = pending;
    if (!p || p.receipt !== receipt) return "blocked_receipt";
    if (!root() || editor() !== p.editor || busy() ||
        !samePayload(body(p.editor), p.content)) {
      pending = null;
      return "blocked_changed";
    }
    const form = p.editor.closest?.("form");
    const btn = form?.querySelector?.('button[type="submit"]:not([disabled])') ||
      w.document.querySelector('[data-testid="send-button"],button[aria-label="Wyślij"],button[aria-label="Send"]');
    if (!btn || btn.disabled || btn.getAttribute?.("aria-disabled") === "true")
      return "blocked_no_send_button";
    attempted = true;
    pending = null;
    try { btn.click(); return "click_invoked"; }
    catch (_) { return "click_uncertain"; }
  }
  w.__OBL_SUCCESSOR_SEED_V1__ = Object.freeze({stage, clickOnce,
    clearOwnUnsentDraft, diagnose: () => lastInspection});
})(window);