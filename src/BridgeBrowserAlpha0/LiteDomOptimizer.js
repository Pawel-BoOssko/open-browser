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
    enabled: true, modernSoftEnabled: false, supported: !!(global.CSS && global.CSS.supports &&
      global.CSS.supports("content-visibility", "auto")),
    keepRecent: 40, minTurns: 60, turnCount: 0, optimized: 0,
    dilAssistantCount: 0, dilUserCount: 0,
    domVariant: 'unknown',
    skippedBusy: false, lastDurationMs: 0, lastCheckedAt: null, errorCount: 0,
    totalDomElements: null, domSampleAt: null
  };
  // The WinForms _domMonitor already invokes check() every 30 seconds.
  // Do not install a competing JavaScript polling timer in the same page.

  function busy() {
    // A sidebar may retain aria-busy even when the assistant is idle.
    // Only an active response in main or a visible Stop control blocks CSS changes.
    return !!(doc.querySelector('main [aria-busy="true"]') ||
      doc.querySelector('[data-testid="stop-button"],button[aria-label="Zatrzymaj"]'));
  }

  function ensureStyle() {
    if (doc.getElementById(styleId)) return;
    const css = doc.createElement("style");
    css.id = styleId;
    css.textContent = '[data-ob-lite-offscreen="1"] {' +
      'content-visibility:auto;contain-intrinsic-size:auto 360px;}';
    (doc.head || doc.documentElement).appendChild(css);
  }

  function safeModernBlock(node) {
    if (!node || !['DIV', 'ARTICLE', 'SECTION'].includes(node.tagName))
      return false;
    const rect = node.getBoundingClientRect?.();
    if (!rect || rect.height < 80) return false;
    const display = global.getComputedStyle?.(node)?.display;
    return display === 'block' || display === 'flex' || display === 'grid';
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
      // When a page switches from legacy turns to DIL, clear only flags
      // previously installed by this optimizer. DIL stays observe-only.
      if (modern) {
        for (const oldTurn of oldTurns) {
          if (oldTurn.hasAttribute(flag)) oldTurn.removeAttribute(flag);
        }
      }
      const turns = modern ? modernTurns : oldTurns;
      const maySoftModern = modern && state.modernSoftEnabled && oldTurns.length === 0;
      state.domVariant = modern ? (maySoftModern ? 'dil_soft_pilot' : 'dil_observe_only') : 'legacy';
      state.turnCount = turns.length;
      // DIL role metadata only. A node matching both markers is a user bubble,
      // so the two role counters cannot exceed the total message count.
      state.dilUserCount = modernTurns.filter(n =>
        n.hasAttribute?.('data-user-message-bubble')).length;
      state.dilAssistantCount = modernTurns.filter(n =>
        n.hasAttribute?.('data-chatgpt-selection-message-id') &&
        !n.hasAttribute?.('data-user-message-bubble')).length;
      state.skippedBusy = busy();
      // Metadata-only DOM sample every ~2 minutes. Existing resource logs
      // then reveal whether DOM node growth correlates with renderer RAM.
      if (!state.skippedBusy &&
          (!state.domSampleAt || Date.now() - state.domSampleAt >= 120000) &&
          typeof doc.getElementsByTagName === 'function') {
        state.totalDomElements = doc.getElementsByTagName('*').length;
        state.domSampleAt = Date.now();
      }
      const mayOptimize = state.enabled && state.supported &&
        (!modern || maySoftModern) && !state.skippedBusy && turns.length >= state.minTurns;
      if (mayOptimize) ensureStyle();
      const recentStart = Math.max(0, turns.length - state.keepRecent);
      let optimized = 0;
      for (let i = 0; i < turns.length; i++) {
        const turn = turns[i];
        const focused = !!(doc.activeElement && turn.contains(doc.activeElement));
        const shouldOptimize = mayOptimize && i < recentStart && !focused &&
          (!modern || safeModernBlock(turn));
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

  function setModernSoftEnabled(value) {
    state.modernSoftEnabled = value === true;
    return check();
  }

  function getStatus() {
    return {
      enabled: state.enabled, modernSoftEnabled: state.modernSoftEnabled,
      supported: state.supported,
      keepRecent: state.keepRecent, turnCount: state.turnCount,
      dilAssistantCount: state.dilAssistantCount, dilUserCount: state.dilUserCount,
      optimized: state.optimized, domVariant: state.domVariant,
      skippedBusy: state.skippedBusy,
      lastDurationMs: state.lastDurationMs, lastCheckedAt: state.lastCheckedAt,
      errorCount: state.errorCount,
      totalDomElements: state.totalDomElements,
      domSampleAt: state.domSampleAt
    };
  }

  function begin() {
    // Initial on-load census; subsequent work is owned by the host.
    check();
  }

  global.__OPENBROWSER_LITE_DOM__ = { check, setEnabled,
    setModernSoftEnabled, getStatus };
  if (doc.readyState === "loading") {
    doc.addEventListener("DOMContentLoaded", begin, { once: true });
  } else begin();
})(window);
