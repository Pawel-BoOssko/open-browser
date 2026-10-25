(function installOpenBrowserHardPrune(global) {
  "use strict";
  if (global.__OPENBROWSER_HARD_PRUNE__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  // Owner requirement: no historical scroll, keep at most ten messages.
  const config = { enabled: true, threshold: 11, keepNewest: 10, intervalMs: 30000 };
  const state = {
    removedTotal: 0, lastRemoved: 0, lastCheckedAt: null,
    lastVisibleTurns: 0, skipped: null, failures: 0, domVariant: "unknown",
    lastPrunedAt: 0, lastTailKey: null, reinsertions: 0
  };
  const doc = global.document;
  // Only a successful, durable Markdown write in the host may acknowledge an answer.
  const savedAnswers = new Set();
  const modernSelector = '[data-user-message-bubble],[data-chatgpt-selection-message-id]';
  const legacySelector = '[data-testid^="conversation-turn-"]';

  function isVisible(element) {
    return !element.hidden && !element.disabled &&
      element.getAttribute?.("aria-hidden") !== "true" &&
      (typeof element.getClientRects !== "function" ||
        element.getClientRects().length > 0);
  }

  function busy(main) {
    if (main.getAttribute?.("aria-busy") === "true") return true;
    const controls = doc.querySelectorAll(
      '[data-testid="stop-button"],button[aria-label="Stop generating"],' +
      'button[aria-label="Przerwij generowanie"],button[aria-label="Zatrzymaj"]');
    if (Array.from(controls).some(button =>
      !button.closest?.('aside,nav,[role="navigation"]') && isVisible(button)))
      return true;
    const drafts = doc.querySelectorAll(
      '#prompt-textarea,[data-testid="composer-text-input"],textarea,[contenteditable="true"]');
    if (Array.from(drafts).some(editor =>
      String(editor.value || editor.innerText || "").trim().length > 0))
      return true;
    if (global.getSelection && String(global.getSelection()).trim()) return true;
    return false;
  }

  // Find the highest React container that belongs to exactly one message.
  // Never choose a wrapper that also contains a second message or composer.
  function isolatedRoot(marker, markers, main) {
    let node = marker;
    let safe = null;
    while (node && node !== main) {
      const contains = markers.reduce((n, item) =>
        n + (node === item || node.contains?.(item) ? 1 : 0), 0);
      if (contains !== 1) break;
      if (node.querySelector?.(
        '#prompt-textarea,[data-testid="composer-text-input"],textarea,[contenteditable="true"]'))
        break;
      if (["DIV", "ARTICLE", "SECTION", "LI"].includes(node.tagName))
        safe = node;
      node = node.parentElement || node.parentNode;
    }
    return safe;
  }

  function collect(main) {
    const legacy = Array.from(main.querySelectorAll(legacySelector));
    const modern = Array.from(main.querySelectorAll(modernSelector));
    if (legacy.length && modern.length)
      return { reason: "mixed_dom", turns: [], type: "mixed", tailKey: "" };
    if (legacy.length)
      return { turns: legacy, markers: legacy, type: "legacy",
        tailKey: legacy.at(-1)?.getAttribute?.("data-testid") || "" };
    if (!modern.length)
      return { reason: "no_messages", turns: [], type: "unknown", tailKey: "" };
    const ids = modern.map(n => n.getAttribute?.("data-chatgpt-selection-message-id"))
      .filter(Boolean);
    if (new Set(ids).size !== ids.length)
      return { reason: "duplicate_dil_ids", turns: [], type: "dil", tailKey: "" };
    const turns = modern.map(n => isolatedRoot(n, modern, main));
    if (turns.some(n => !n) || new Set(turns).size !== turns.length)
      return { reason: "unsafe_wrapper", turns: [], type: "dil", tailKey: "" };
    for (let i = 0; i < turns.length; i++)
      for (let j = i + 1; j < turns.length; j++)
        if (turns[i].contains?.(turns[j]) || turns[j].contains?.(turns[i]))
          return { reason: "overlapping_wrappers", turns: [], type: "dil", tailKey: "" };
    return { turns, markers: modern, type: "dil", tailKey: ids.at(-1) || "" };
  }

  let timer = null;
  let initial = null;
  function stop() {
    if (timer !== null) global.clearInterval(timer);
    if (initial !== null) global.clearTimeout(initial);
    timer = null;
    initial = null;
  }

  function status() {
    return {
      enabled: config.enabled, threshold: config.threshold,
      keepNewest: config.keepNewest, removedTotal: state.removedTotal,
      lastRemoved: state.lastRemoved, lastCheckedAt: state.lastCheckedAt,
      visibleTurns: state.lastVisibleTurns, domVariant: state.domVariant,
      skipped: state.skipped, failures: state.failures,
      reinsertions: state.reinsertions
    };
  }

  function ackSaved(ids) {
    if (!Array.isArray(ids)) return status();
    for (const id of ids) {
      if (typeof id === "string" && id.length > 0 && id.length < 512)
        savedAnswers.add(id);
    }
    return status();
  }

  function isPersistedOrUser(result, index) {
    const marker = result.markers[index];
    if (!marker) return false;
    if (result.type === "dil") {
      // A user bubble has no model response to archive.
      if (typeof marker.getAttribute === "function" &&
          marker.getAttribute("data-user-message-bubble") !== null)
        return true;
      const id = marker.getAttribute?.("data-chatgpt-selection-message-id");
      return typeof id === "string" && savedAnswers.has(id);
    }
    // Legacy wrappers are accepted only with a known author role.
    if (marker.querySelector?.('[data-message-author-role="user"]'))
      return true;
    if (marker.querySelector?.('[data-message-author-role="assistant"]')) {
      const id = marker.getAttribute?.("data-testid");
      return !!id && savedAnswers.has(id);
    }
    return false;
  }

  function prune() {
    state.lastCheckedAt = new Date().toISOString();
    state.lastRemoved = 0;
    if (!config.enabled) { state.skipped = "disabled"; return status(); }
    const main = doc.querySelector('main,[role="main"]');
    if (!main) { state.skipped = "unhydrated"; return status(); }
    if (busy(main)) { state.skipped = "busy_or_draft"; return status(); }
    const result = collect(main);
    state.domVariant = result.type;
    if (result.reason) { state.skipped = result.reason; return status(); }
    const turns = result.turns;
    state.lastVisibleTurns = turns.length;
    if (turns.length < config.threshold) {
      state.skipped = "below_threshold";
      return status();
    }
    const now = Date.now();
    if (state.lastPrunedAt && now - state.lastPrunedAt < 120000 &&
        result.tailKey && result.tailKey === state.lastTailKey) {
      state.reinsertions++;
      if (state.reinsertions >= 2) {
        config.enabled = false;
        state.skipped = "react_reinserted_old_turns";
        stop();
        return status();
      }
    } else state.reinsertions = 0;
    let removed = 0;
    let waitingForArchive = false;
    const limit = turns.length - config.keepNewest;
    for (let i = 0; i < limit; i++) {
      // Never delete an assistant answer not verified on disk.
      // Preserve chronological sequence if any historic answer is missing.
      if (!isPersistedOrUser(result, i)) {
        waitingForArchive = true;
        break;
      }
      const node = turns[i];
      if (!node || !node.parentNode ||
          (doc.activeElement && node.contains?.(doc.activeElement))) {
        state.failures++;
        continue;
      }
      try { node.remove(); removed++; }
      catch (_) { state.failures++; }
    }
    state.removedTotal += removed;
    state.lastRemoved = removed;
    if (removed > 0) state.lastPrunedAt = now;
    if (removed > 0) state.lastTailKey = result.tailKey;
    state.lastVisibleTurns = turns.length - removed;
    state.skipped = waitingForArchive ? "awaiting_archive" :
      removed ? "pruned" : "nothing_removable";
    return status();
  }

  function start() {
    if (!config.enabled || timer !== null) return;
    timer = global.setInterval(() => { if (config.enabled) prune(); },
      config.intervalMs);
    initial = global.setTimeout(() => {
      initial = null;
      if (config.enabled) prune();
    }, 45000);
  }
  function enable(value) {
    config.enabled = value === true;
    if (config.enabled) start(); else stop();
    return status();
  }

  global.__OPENBROWSER_HARD_PRUNE__ = { prune, status, enable, ackSaved };
  start();
})(window);
