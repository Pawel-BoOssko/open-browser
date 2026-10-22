(function installOpenBrowserHardPrune(global) {
  "use strict";
  if (global.__OPENBROWSER_HARD_PRUNE__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  const config = { enabled: false, threshold: 90, keepNewest: 24, intervalMs: 120000 };
  const state = { removedTotal: 0, lastRemoved: 0, lastCheckedAt: null, skipped: null, failures: 0 };

  function isBusy(doc) {
    const input = doc.querySelector('textarea,[contenteditable="true"]');
    if (input && ((input.value || input.innerText || "").trim().length > 0)) return true;
    if (doc.querySelector('[aria-busy="true"],[data-testid="stop-button"]')) return true;
    if (global.getSelection && String(global.getSelection()).trim()) return true;
    return false;
  }

  function prune() {
    const doc = global.document;
    state.lastCheckedAt = new Date().toISOString();
    state.lastRemoved = 0;
    if (!config.enabled) { state.skipped = "disabled"; return status(); }
    if (isBusy(doc)) { state.skipped = "busy_or_draft"; return status(); }
    const turns = Array.from(doc.querySelectorAll('[data-testid^="conversation-turn-"]'));
    if (turns.length < config.threshold) {
      state.skipped = "below_threshold";
      return status();
    }
    const removeCount = Math.max(0, turns.length - config.keepNewest);
    let removed = 0;
    for (let i = 0; i < removeCount; i++) {
      const node = turns[i];
      if (!node || !node.parentNode ||
          (doc.activeElement && node.contains(doc.activeElement))) continue;
      try {
        // Do not leave historical message DOM attached in this page.
        // Full chat history remains on the ChatGPT service and in other browsers.
        node.remove();
        removed++;
      } catch (_) { state.failures++; }
    }
    state.lastRemoved = removed;
    state.removedTotal += removed;
    state.skipped = removed ? "pruned" : "nothing_removable";
    return status();
  }

  function status() {
    return {
      enabled: config.enabled, threshold: config.threshold,
      keepNewest: config.keepNewest, removedTotal: state.removedTotal,
      lastRemoved: state.lastRemoved, lastCheckedAt: state.lastCheckedAt,
      skipped: state.skipped, failures: state.failures
    };
  }

  // No periodic work, timeout, or DOM traversal while hard pruning is OFF.
  // Arm only after an explicit owner opt-in; stop both timers on opt-out.
  let timer = null;
  let initial = null;
  function stop() {
    if (timer !== null) global.clearInterval(timer);
    if (initial !== null) global.clearTimeout(initial);
    timer = null;
    initial = null;
  }
  function start() {
    if (!config.enabled || timer !== null) return;
    timer = global.setInterval(() => {
      if (config.enabled) prune();
    }, config.intervalMs);
    initial = global.setTimeout(() => {
      initial = null;
      if (config.enabled) prune();
    }, 45000);
  }
  function enable(value) {
    config.enabled = value === true;
    if (config.enabled) start();
    else stop();
    return status();
  }
  global.__OPENBROWSER_HARD_PRUNE__ = { prune, status, enable };
})(window);
