(function initOpenBrowserResumeProbe(w) {
  "use strict";
  if (w.__OPENBROWSER_RESUME_PROBE__) return;
  if (!w.document || w.location.hostname !== "chatgpt.com") return;

  function visibleText(node) {
    if (!node) return "";
    const rendered = String(node.innerText || "").trim();
    if (rendered) return rendered;
    const content = String(node.textContent || "").trim();
    if (content) return content;
    // Some DIL wrappers identify a message but contain no visible text
    // themselves. Use only that message's enclosing turn, never all of main.
    const turn = node.closest?.('[data-testid^="conversation-turn-"],article,[role="article"]');
    if (turn && turn !== node)
      return String(turn.innerText || turn.textContent || "").trim();
    return "";
  }

  function observe() {
    const d = w.document;
    // ChatGPT's DIL UI (verified 2026-10-18 on the owner's test chat)
    // uses data-chatgpt-selection-message-id for assistant replies and
    // data-user-message-bubble for user prompts instead of the old role.
    const legacy = Array.from(d.querySelectorAll('[data-message-author-role]'));
    const modern = Array.from(d.querySelectorAll(
      '[data-user-message-bubble],[data-chatgpt-selection-message-id]'));
    // A DIL page may contain BOTH selector families. Never let a stray
    // legacy node hide the actual newest DIL assistant response.
    const parts = Array.from(new Set([...legacy, ...modern]));
    parts.sort((a, b) => {
      if (a === b) return 0;
      const order = typeof a.compareDocumentPosition === 'function' ?
        a.compareDocumentPosition(b) : 0;
      return order & 4 ? -1 : order & 2 ? 1 : 0;
    });
    const last = parts[parts.length - 1] || null;
    const role = last ? (
      last.getAttribute?.('data-message-author-role') ||
      (last.hasAttribute?.('data-chatgpt-selection-message-id') ? 'assistant' :
        last.hasAttribute?.('data-user-message-bubble') ? 'user' : 'unknown')) :
      'unknown';
    const text = last && role === "assistant" ?
      visibleText(last).slice(-8000) : "";
    // Metadata only: distinguish an empty assistant reply from a stale user
    // bubble without copying any extra conversation text into diagnostics.
    const isAssistant = n => n.getAttribute?.('data-message-author-role') === 'assistant' ||
      n.hasAttribute?.('data-chatgpt-selection-message-id');
    const isUser = n => n.getAttribute?.('data-message-author-role') === 'user' ||
      n.hasAttribute?.('data-user-message-bubble');
    const assistantNodeCount = parts.filter(isAssistant).length;
    const userNodeCount = parts.filter(isUser).length;
    const lastAssistant = parts.slice().reverse().find(isAssistant);
    const lastAssistantChars = visibleText(lastAssistant).length;
    const stop = !!d.querySelector(
      '[data-testid="stop-button"],button[aria-label*="Stop generating"],button[aria-label*="Przerwij generowanie"]');
    // An unrelated busy sidebar must not suppress the current chat.
    const activeTurn = last?.closest?.('[data-testid^="conversation-turn-"]');
    const toolBusy = !!d.querySelector(
      '[data-testid*="tool-running"],[data-state="running"]') ||
      !!activeTurn?.querySelector?.('[aria-busy="true"]');
    // Use the same composer priority as LiteResumeSender: a hidden textarea
    // must not mask a user's draft in the active ChatGPT editor.
    const editor = d.querySelector('#prompt-textarea') ||
      d.querySelector('[data-testid="composer-text-input"]') ||
      d.querySelector('textarea') ||
      d.querySelector('[contenteditable="true"]');
    const draft = editor && String(editor.value || editor.innerText || "").trim().length > 0;
    const errorText = d.querySelector('[role="alert"],[data-testid*="error"]');
    const alertError = !!errorText &&
      /nie udało się wczytać|failed to load|przerwano|interrupted|przekroczono limit|timed out/i.test(
        String(errorText.textContent || "").slice(-400));
    // The centralized detector also checks short DIL assistant error placeholders.
    // User quotations are deliberately excluded by that detector.
    const responseError = w.__OPENBROWSER_RESPONSE_SIGNALS__?.read()?.visibleError;
    const error = alertError || !!responseError;
    const modelModes = Array.from(d.querySelectorAll('button')).slice(-45)
      .map(button => String(button.innerText || button.textContent || '').trim().toLowerCase())
      .filter(value => /^(high|instant|medium|extra high)$/.test(value));
    const verified = w.__OPENBROWSER_HIGH_GUARD__?.audit();
    const reasoningMode = verified ? verified.mode :
      (modelModes.length ? modelModes[modelModes.length - 1] : 'unknown');
    return {
      reasoningMode,
      location: w.location.pathname,
      role: role === "assistant" || role === "user" ? role : "unknown",
      text: text,
      generating: stop,
      toolBusy: toolBusy,
      hasDraft: !!draft,
      error: !!error,
      composerReady: !!editor,
      turnCount: parts.length,
      assistantNodeCount,
      userNodeCount,
      lastAssistantChars,
      observedAt: Date.now()
    };
  }
  w.__OPENBROWSER_RESUME_PROBE__ = { observe };
})(window);
