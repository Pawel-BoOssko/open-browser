(function initOpenBrowserLiteDom(global) {
  "use strict";
  if (global.__OPENBROWSER_LITE_DOM__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  const doc = global.document;
  const legacySelector = '[data-testid^="conversation-turn-"]';
  const dilSelector = '[data-user-message-bubble],[data-chatgpt-selection-message-id]';
  const flag = "data-ob-lite-offscreen";
  const styleId = "openbrowser-lite-content-visibility";
  const state = {
    enabled: true, supported: !!(global.CSS && global.CSS.supports &&
      global.CSS.supports("content-visibility", "auto")),
    keepRecent: 40, minTurns: 60, turnCount: 0, optimized: 0,
    domVariant: 'unknown',
    skippedBusy: false, lastDurationMs: 0, lastCheckedAt: null, errorCount: 0
  };
  let timer = null;

  function busy() {
    return !!(doc.querySelector('[aria-busy="true"]') ||
      doc.querySelector('[data-testid="stop-button"]'));
  }

  function ensureStyle() {
    if (doc.getElementById(styleId)) return;
    const css = doc.createElement("style");
    css.id = styleId;
    css.textContent = '[data-ob-lite-offscreen="1"] {' +
      'content-visibility:auto;contain-intrinsic-size:auto 360px;}';
    (doc.head || doc.documentElement).appendChild(css);
  }

  function check() {
    const start = global.performance.now();
    try {
      const oldTurns = Array.from(doc.querySelectorAll(legacySelector));
      // DIL changed its turn wrappers in 2026. Count it accurately, but do not
      // change modern React message layout before standalone visual QA.
      const modernTurns = Array.from(doc.querySelectorAll(dilSelector));
      // A mixed page can retain legacy wrappers alongside DIL messages.
      // Prefer modern counts, but never optimize a mixed React subtree.
      const modern = modernTurns.length > 0;
      const turns = modern ? modernTurns : oldTurns;
      state.domVariant = modern ? 'dil_observe_only' : 'legacy';
      state.turnCount = turns.length;
      state.skippedBusy = busy();
      const mayOptimize = state.enabled && state.supported &&
        !modern && !state.skippedBusy && turns.length >= state.minTurns;
      if (mayOptimize) ensureStyle();
      const recentStart = Math.max(0, turns.length - state.keepRecent);
      let optimized = 0;
      for (let i = 0; i < turns.length; i++) {
        const turn = turns[i];
        const focused = !!(doc.activeElement && turn.contains(doc.activeElement));
        const shouldOptimize = mayOptimize && i < recentStart && !focused;
        if (shouldOptimize) {
          if (turn.getAttribute(flag) !== "1") turn.setAttribute(flag, "1");
          optimized++;
        } else if (turn.hasAttribute(flag)) {
          turn.removeAttribute(flag);
        }
      }
      state.optimized = optimized;
      state.lastCheckedAt = new Date().toISOString();
      state.lastDurationMs = Math.round(global.performance.now() - start);
      return getStatus();
    } catch (_) {
      state.errorCount++;
      return getStatus();
    }
  }

  function setEnabled(value) {
    state.enabled = value === true;
    return check();
  }

  function getStatus() {
    return {
      enabled: state.enabled, supported: state.supported,
      keepRecent: state.keepRecent, turnCount: state.turnCount,
      optimized: state.optimized, domVariant: state.domVariant,
      skippedBusy: state.skippedBusy,
      lastDurationMs: state.lastDurationMs, lastCheckedAt: state.lastCheckedAt,
      errorCount: state.errorCount
    };
  }

  function begin() {
    check();
    if (!timer) timer = global.setInterval(check, 30000);
  }

  global.__OPENBROWSER_LITE_DOM__ = { check, setEnabled, getStatus };
  if (doc.readyState === "loading") {
    doc.addEventListener("DOMContentLoaded", begin, { once: true });
  } else begin();
})(window);
