(function initOpenBrowserSnapshot(global) {
  "use strict";
  if (global.__OPENBROWSER_LITE_SNAPSHOT__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  function collect() {
    try {
      if (!/^\/(c|g)\//.test(global.location.pathname)) return [];
      const nodes = global.document.querySelectorAll(
        '[data-message-author-role="assistant"]');
      const results = [];
      for (let index = nodes.length - 1; index >= 0 && results.length < 2; index--) {
        const node = nodes[index];
        // innerText excludes non-rendered content; do not read hidden application state.
        const text = typeof node.innerText === "string" ? node.innerText : "";
        if (text.trim().length < 5 || text.length > 200000) continue;
        const parent = node.closest('[data-testid^="conversation-turn-"]');
        const id = parent ? parent.getAttribute("data-testid") : "assistant-" + index;
        results.push({ turnId: id, text });
      }
      return results;
    } catch (_) {
      return [];
    }
  }

  global.__OPENBROWSER_LITE_SNAPSHOT__ = { collect };
})(window);
