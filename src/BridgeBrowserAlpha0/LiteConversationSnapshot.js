(function initOpenBrowserSnapshot(global) {
  "use strict";
  if (global.__OPENBROWSER_LITE_SNAPSHOT__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  function collect() {
    try {
      if (!/^\/(c|g)\//.test(global.location.pathname)) return [];
      const main = global.document.querySelector('main,[role="main"]');
      if (!main) return [];
      const stopping = Array.from(main.querySelectorAll(
        '[data-testid="stop-button"],button[aria-label="Zatrzymaj"],' +
        'button[aria-label="Stop generating"],button[aria-label="Przerwij generowanie"]'));
      const generating = stopping.some(n => !n.hidden && !n.disabled &&
        n.getAttribute?.("aria-hidden") !== "true" &&
        (typeof n.getClientRects !== "function" || n.getClientRects().length > 0));
      const modern = Array.from(main.querySelectorAll(
        '[data-chatgpt-selection-message-id]'));
      const nodes = modern.length ? modern : Array.from(main.querySelectorAll(
        '[data-message-author-role="assistant"]'));
      const results = [];
      const ids = new Set();
      // Streaming: capture the newest assistant only. Completed: latest two.
      // Capturing a partial every 30s is needed for recovery of long work.
      const cap = generating ? 1 : 2;
      for (let index = nodes.length - 1; index >= 0 && results.length < cap; index--) {
        const node = nodes[index];
        const text = typeof node.innerText === "string" ? node.innerText : "";
        if (text.trim().length < 5 || text.length > 200000) continue;
        const parent = modern.length ? null :
          node.closest?.('[data-testid^="conversation-turn-"]');
        const id = (modern.length ?
          node.getAttribute?.("data-chatgpt-selection-message-id") :
          parent?.getAttribute?.("data-testid")) || "assistant-" + index;
        if (ids.has(id)) continue;
        ids.add(id);
        results.push({ turnId: id, text, partial: generating });
      }
      return results;
    } catch (_) {
      return [];
    }
  }


  // Before pruning, persist the oldest answers as well as the newest ones.
  // Limit each scan to 64 historical answers to avoid a large payload.
  function collectForArchive() {
    const recent = collect();
    if (recent.some(item => item.partial)) return recent;
    if (!/^\/(c|g)\//.test(global.location.pathname)) return [];
    const main = global.document.querySelector('main,[role="main"]');
    if (!main) return [];
    const modern = Array.from(main.querySelectorAll(
      '[data-chatgpt-selection-message-id]'));
    const nodes = modern.length ? modern : Array.from(main.querySelectorAll(
      '[data-message-author-role="assistant"]'));
    const known = new Set(recent.map(item => item.turnId));
    const history = [];
    for (let i = 0; i < nodes.length && history.length < 64; i++) {
      const node = nodes[i];
      const text = typeof node.innerText === "string" ? node.innerText : "";
      if (text.trim().length < 5 || text.length > 200000) continue;
      const parent = modern.length ? null :
        node.closest?.('[data-testid^="conversation-turn-"]');
      const id = (modern.length ?
        node.getAttribute?.("data-chatgpt-selection-message-id") :
        parent?.getAttribute?.("data-testid")) || "assistant-" + i;
      if (!known.has(id)) {
        history.push({ turnId: id, text, partial: false });
        known.add(id);
      }
    }
    return history.concat(recent);
  }

  global.__OPENBROWSER_LITE_SNAPSHOT__ = { collect, collectForArchive };
})(window);
