(function initOpenBrowserResponseSignals(global) {
  "use strict";
  if (global.__OPENBROWSER_RESPONSE_SIGNALS__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  const phrases = [
    ["response_load_failed", /nie udało się wczytać tej odpowiedzi|couldn.t load this response|failed to load (this |the )?response/i],
    ["delivery_timeout", /przekroczono limit czasu dostarczenia|message delivery timed out|delivery time limit exceeded/i],
    ["reasoning_interrupted", /przerwano rozumowanie|reasoning interrupted/i]
  ];
  function inspectNode(node) {
    const content = (node && node.textContent || "").slice(-900);
    for (const [kind, pattern] of phrases) if (pattern.test(content)) return kind;
    return null;
  }

  function isVisible(node) {
    if (!node || node.hidden || node.getAttribute?.('aria-hidden') === 'true' ||
        node.closest?.('[hidden],[aria-hidden="true"],[inert]')) return false;
    if (typeof node.getClientRects === 'function' &&
        node.getClientRects().length === 0) return false;
    const style = typeof global.getComputedStyle === 'function'
      ? global.getComputedStyle(node) : null;
    return !style || (style.display !== 'none' &&
      style.visibility !== 'hidden' && style.visibility !== 'collapse');
  }

  function read() {
    const t0 = global.performance.now();
    const doc = global.document;
    // A sidebar preview can resemble an assistant error. Only message
    // placeholders in the active conversation are authoritative.
    const main = doc.querySelector('main,[role="main"]');
    const legacyTurns = main?.querySelectorAll('[data-testid^="conversation-turn-"]') || [];
    const dilTurns = main?.querySelectorAll(
      '[data-user-message-bubble],[data-chatgpt-selection-message-id]') || [];
    const modern = dilTurns.length > 0;
    const turnCount = modern ? dilTurns.length : legacyTurns.length;
    const assistants = main?.querySelectorAll(
      '[data-message-author-role="assistant"],[data-chatgpt-selection-message-id]') || [];
    let error = null;
    let errorSource = 'none';
    let ignoredHiddenAlerts = 0;
    // Keep *visible* global error toasts, but ignore hidden stale alerts
    // and any alerts inside sidebar/navigation.
    const alerts = Array.from(doc.querySelectorAll(
      '[role="alert"],[data-testid*="error"],[data-testid*="toast"]'))
      .filter(node => !node.closest?.(
        'aside,nav,[role="navigation"],[data-testid="sidebar"]'));
    for (const node of alerts.slice(-35)) {
      if (!isVisible(node)) {
        if (inspectNode(node)) ignoredHiddenAlerts++;
        continue;
      }
      error = inspectNode(node);
      if (error) { errorSource = 'visible_alert'; break; }
    }
    if (!error && main && turnCount === 0) {
      // A failed conversation fetch often leaves only a short shell in MAIN.
      // This is not the same as an assistant response failure, and cannot
      // safely be inferred from user quotes or sidebar previews.
      const shell = String(main.textContent || "").trim();
      if (shell.length > 0 && shell.length < 350 &&
          /nie udało się wczytać tej rozmowy(?: chatgpt)?|couldn.t load (this |the )?conversation|failed to load (this |the )?conversation|unable to load conversation/i.test(shell))
      {
        error = "conversation_load_failed";
        errorSource = 'main_empty_shell';
      }
    }
    if (!error) {
      // Never interpret a user's quote of an error as a browser failure.
      // Inline assistant failure placeholders are short; long assistant text
      // discussing an error is not evidence that the UI itself failed.
      // Only the newest visible assistant reply can block the next action.
      // A failed earlier reply can remain in the React tree while a newer
      // answer is already being generated or has completed. Checking the
      // last three produced a false permanent response_load_failed.
      const newestAssistant = Array.from(assistants).reverse()
        .find(node => isVisible(node));
      if (newestAssistant &&
          String(newestAssistant.textContent || "").trim().length <= 180) {
        error = inspectNode(newestAssistant);
        if (error) errorSource = 'short_assistant_placeholder';
      }
    }
    const stop = Array.from(doc.querySelectorAll(
      '[data-testid="stop-button"],button[aria-label="Stop generating"],button[aria-label="Przerwij generowanie"],button[aria-label="Zatrzymaj"]'))
      .some(button =>
        !button.closest?.('aside,nav,[role="navigation"],[data-testid="sidebar"]') &&
        !button.hidden && !button.disabled &&
        button.getAttribute?.('aria-hidden') !== 'true' &&
        (typeof button.getClientRects !== 'function' ||
          button.getClientRects().length !== 0));
    return {
      documentReady: doc.readyState,
      hasComposer: !!doc.querySelector('textarea,[contenteditable="true"]'),
      turnCount,
      domVariant: modern ? 'dil' : (legacyTurns.length ? 'legacy' : 'unhydrated'),
      assistantCount: assistants.length,
      generatingSignal: stop,
      visibleError: error,
      errorSource,
      ignoredHiddenAlerts,
      durationMs: Math.round(global.performance.now() - t0)
    };
  }
  global.__OPENBROWSER_RESPONSE_SIGNALS__ = { read };
})(window);
