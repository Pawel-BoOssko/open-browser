(function initOpenBrowserSnapshot(global) {
  "use strict";
  if (global.__OPENBROWSER_LITE_SNAPSHOT__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  function isGenerating(main) {
    // Fail closed: a streaming response is PARTIAL even when React temporarily
    // removes or changes the Stop button. The DOM pruning guard also treats
    // aria-busy as active. Never give an incomplete turn a FINAL archive ACK.
    if (main.getAttribute?.("aria-busy") === "true" ||
        main.querySelector?.('[aria-busy="true"]') ||
        // Tool execution can remain active without aria-busy or a Stop button.
        // Keep its unfinished assistant turn PARTIAL until the tool stops.
        main.querySelector?.('[data-testid*="tool-running"],[data-state="running"]'))
      return true;
    const controls = global.document.querySelectorAll(
      '[data-testid="stop-button"],button[aria-label="Zatrzymaj"],' +
      'button[aria-label="Stop generating"],button[aria-label="Przerwij generowanie"]');
    return Array.from(controls).some(n => !n.hidden && !n.disabled &&
      !n.closest?.('aside,nav,[role="navigation"]') &&
      n.getAttribute?.("aria-hidden") !== "true" &&
      (typeof n.getClientRects !== "function" || n.getClientRects().length > 0));
  }

  function isUserMessage(node) {
    // Selection IDs are not proof of assistant authorship. The role can be
    // declared on a parent turn while the ID belongs to a nested child.
    return !!node?.closest?.('[data-user-message-bubble],[data-message-author-role="user"]');
  }

  function collect() {
    try {
      if (!/^\/(c|g)\//.test(global.location.pathname)) return [];
      const main = global.document.querySelector('main,[role="main"]');
      if (!main) return [];
      const generating = isGenerating(main);
      if (generating) {
        // Never classify a previously FINISHED assistant as a new PARTIAL
        // while the newest USER message still awaits its assistant turn.
        const turns = main.querySelectorAll(
          '[data-user-message-bubble],[data-chatgpt-selection-message-id],' +
          '[data-message-author-role]');
        const latest = turns[turns.length - 1];
        if (!latest || isUserMessage(latest)) return [];
      }
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
        if (isUserMessage(node)) continue;
        const text = typeof node.innerText === "string" ? node.innerText : "";
        if (text.trim().length < 5 || text.length > 200000) {
          // During streaming this is the newest assistant, not a reason to
          // fall back to an older completed answer and label it PARTIAL.
          if (generating) return [];
          continue;
        }
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
    // While generating with no assistant turn yet, do NOT perform a
    // historical FINAL sweep either: the old answer is not the new one.
    const activeMain = global.document.querySelector('main,[role="main"]');
    if (recent.some(item => item.partial) ||
        (activeMain && isGenerating(activeMain))) return recent;
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
      if (isUserMessage(node)) continue;
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
