(function initTabSwitchSafety(global) {
  "use strict";
  if (global.__OPENBROWSER_TAB_SWITCH_SAFETY__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  function insideNavigation(node) {
    return !!node.closest?.('aside,nav,[role="navigation"],[data-testid="sidebar"]');
  }
  function read() {
    const doc = global.document;
    // A generic aria-busy=true on sidebar, toast or app shell is not an
    // in-flight answer. Check an actual visible STOP generating control.
    const controls = doc.querySelectorAll(
      '[data-testid="stop-button"],button[aria-label="Stop generating"],' +
      'button[aria-label="Przerwij generowanie"],button[aria-label="Zatrzymaj"]');
    const busy = Array.from(controls).some(button =>
      !insideNavigation(button) && !button.hidden && !button.disabled &&
      button.getAttribute?.("aria-hidden") !== "true" &&
      (typeof button.getClientRects !== "function" ||
        button.getClientRects().length > 0));
    const editors = doc.querySelectorAll(
      '#prompt-textarea,[data-testid="composer-text-input"],textarea,[contenteditable="true"]');
    const draft = Array.from(editors).some(node =>
      !insideNavigation(node) &&
      String(node.value || node.innerText || "").trim().length > 0);
    return { busy, draft };
  }
  global.__OPENBROWSER_TAB_SWITCH_SAFETY__ = { read };
})(window);
