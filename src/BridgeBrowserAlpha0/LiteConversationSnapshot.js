(function initOpenBrowserSnapshot(global) {
  "use strict";
  if (global.__OPENBROWSER_LITE_SNAPSHOT__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  function collect() {
    try {
      if (!/^\/(c|g)\//.test(global.location.pathname)) return [];
      const doc = global.document;
      // Capture completed replies only. Streaming innerText every few seconds
      // forces layout work and stores a long sequence of partial revisions.
      if (doc.querySelector('[data-testid="stop-button"],button[aria-label="Zatrzymaj"],button[aria-label="Stop generating"]'))
        return [];
      const modern = Array.from(doc.querySelectorAll(
        '[data-chatgpt-selection-message-id]'));
      const nodes = modern.length ? modern : Array.from(doc.querySelectorAll(
        '[data-message-author-role="assistant"]'));
      const results = [];
      const ids = new Set();
      for (let index = nodes.length - 1; index >= 0 && results.length < 2; index--) {
        const node = nodes[index];
        // Read only displayed text, not hidden app state or raw React data.
        const text = typeof node.innerText === "string" ? node.innerText : "";
        if (text.trim().length < 5 || text.length > 200000) continue;
        const parent = modern.length ? null :
          node.closest?.('[data-testid^="conversation-turn-"]');
        const id = (modern.length ?
          node.getAttribute?.("data-chatgpt-selection-message-id") :
          parent?.getAttribute?.("data-testid")) || "assistant-" + index;
        if (ids.has(id)) continue;
        ids.add(id);
        results.push({ turnId: id, text });
      }
      return results;
    } catch (_) {
      return [];
    }
  }

  global.__OPENBROWSER_LITE_SNAPSHOT__ = { collect };
})(window);
