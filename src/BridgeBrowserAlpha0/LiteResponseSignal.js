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

  function read() {
    const t0 = global.performance.now();
    const doc = global.document;
    const turns = doc.querySelectorAll('[data-testid^="conversation-turn-"]');
    const assistants = doc.querySelectorAll(
      '[data-message-author-role="assistant"],[data-chatgpt-selection-message-id]');
    let error = null;
    const alerts = doc.querySelectorAll('[role="alert"],[data-testid*="error"],[data-testid*="toast"]');
    for (const node of Array.from(alerts).slice(-35)) {
      error = inspectNode(node);
      if (error) break;
    }
    if (!error) {
      // Never interpret a user's quote of an error as a browser failure.
      // Inline assistant failure placeholders are short; long assistant text
      // discussing an error is not evidence that the UI itself failed.
      for (const node of Array.from(assistants).slice(-3)) {
        if (String(node.textContent || "").trim().length > 180) continue;
        error = inspectNode(node);
        if (error) break;
      }
    }
    const stop = !!doc.querySelector('[data-testid="stop-button"],button[aria-label="Stop generating"],button[aria-label="Przerwij generowanie"],button[aria-label="Zatrzymaj"]');
    return {
      documentReady: doc.readyState,
      hasComposer: !!doc.querySelector('textarea,[contenteditable="true"]'),
      turnCount: turns.length,
      assistantCount: assistants.length,
      generatingSignal: stop,
      visibleError: error,
      durationMs: Math.round(global.performance.now() - t0)
    };
  }
  global.__OPENBROWSER_RESPONSE_SIGNALS__ = { read };
})(window);
