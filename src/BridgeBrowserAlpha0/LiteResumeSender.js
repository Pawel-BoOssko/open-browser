(function initOpenBrowserResumeSender(w) {
  "use strict";
  if (w.__OPENBROWSER_RESUME_SENDER__) return;
  if (!w.document || w.location.hostname !== "chatgpt.com") return;
  const attempted = new Set();
  function busy() {
    return !!w.document.querySelector('[data-testid="stop-button"],[aria-busy="true"]');
  }
  function composer() {
    return w.document.querySelector('#prompt-textarea,[data-testid="composer-text-input"],textarea');
  }
  function hasAck(receiptId) {
    return Array.from(w.document.querySelectorAll('[data-message-author-role="user"]'))
      .slice(-5).some(n => String(n.innerText || "").includes(receiptId));
  }
  function send(text, receiptId) {
    if (!/^\/c\//.test(w.location.pathname) ||
        !/^[a-f0-9]{32}$/.test(receiptId) ||
        text.length < 16 || text.length > 1700) return "invalid";
    if (hasAck(receiptId)) return "already_visible";
    if (attempted.has(receiptId)) return "already_attempted";
    const el = composer();
    if (!el || busy() || String(el.value || el.innerText || "").trim()) return "busy";
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
    // Next tick allows React to update the enabled state; uncertain after this point.
    w.setTimeout(() => {
      const btn = w.document.querySelector('[data-testid="send-button"]');
      if (btn && !btn.disabled && !busy()) btn.click();
    }, 250);
    return "submission_requested";
  }
  w.__OPENBROWSER_RESUME_SENDER__ = { send, hasAck };
})(window);
