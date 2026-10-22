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
    // Only the active conversation's main is trusted. Sidebar previews,
    // search results or model menus may contain message-like DOM elsewhere.
    // If ChatGPT has not hydrated main, fail closed (no assistant answer).
    const main = d.querySelector('main,[role="main"]');
    // A single selector returns DOM order and deduplicates nodes matching
    // both old and DIL markers. Avoid repeated whole-document scans and the
    // O(n log n) compareDocumentPosition sort on very long conversations.
    // querySelectorAll returns a static, iterable NodeList; do not copy it
    // into an extra Array just to read its length and traverse it once.
    const parts = main ? main.querySelectorAll(
      '[data-message-author-role],[data-user-message-bubble],' +
      '[data-chatgpt-selection-message-id]') : [];
    const last = parts[parts.length - 1] || null;
    const role = last ? (
      last.getAttribute?.('data-message-author-role') ||
      (last.hasAttribute?.('data-chatgpt-selection-message-id') ? 'assistant' :
        last.hasAttribute?.('data-user-message-bubble') ? 'user' : 'unknown')) :
      'unknown';
    // Read the message metadata in ONE pass. A long conversation must not
    // allocate reversed copies or traverse all messages three extra times.
    // Reuse the newest assistant's text to avoid a second innerText read
    // (which can force layout on a large React/DIL message subtree).
    let assistantNodeCount = 0;
    let userNodeCount = 0;
    let lastAssistant = null;
    for (const part of parts) {
      const oldRole = part.getAttribute?.('data-message-author-role');
      if (oldRole === 'assistant' ||
          part.hasAttribute?.('data-chatgpt-selection-message-id')) {
        assistantNodeCount++;
        lastAssistant = part;
      }
      if (oldRole === 'user' ||
          part.hasAttribute?.('data-user-message-bubble')) userNodeCount++;
    }
    const lastVisible = last && role === 'assistant' ? visibleText(last) : '';
    const text = lastVisible.slice(-8000);
    const lastAssistantChars = lastAssistant ?
      (lastAssistant === last && role === 'assistant' ?
        lastVisible.length : visibleText(lastAssistant).length) : 0;
    const stop = Array.from(d.querySelectorAll(
      '[data-testid="stop-button"],button[aria-label*="Stop generating"],button[aria-label*="Przerwij generowanie"],button[aria-label="Zatrzymaj"]'))
      .some(button =>
        !button.closest?.('aside,nav,[role="navigation"],[data-testid="sidebar"]') &&
        !button.hidden && !button.disabled &&
        button.getAttribute?.('aria-hidden') !== 'true' &&
        (typeof button.getClientRects !== 'function' ||
          button.getClientRects().length !== 0));
    // An unrelated busy sidebar must not suppress the current chat.
    const activeTurn = last?.closest?.('[data-testid^="conversation-turn-"]');
    // Only tool state inside this conversation blocks its next wake.
    // A sidebar may animate a different chat's tool or a global widget.
    const toolBusy = !!main?.querySelector?.(
      '[data-testid*="tool-running"],[data-state="running"]') ||
      !!activeTurn?.querySelector?.('[aria-busy="true"]');
    // Use the same composer priority as LiteResumeSender: a hidden textarea
    // must not mask a user's draft in the active ChatGPT editor.
    const editor = d.querySelector('#prompt-textarea') ||
      d.querySelector('[data-testid="composer-text-input"]') ||
      d.querySelector('textarea') ||
      d.querySelector('[contenteditable="true"]');
    const draft = editor && String(editor.value || editor.innerText || "").trim().length > 0;
    // Hidden/sidebar alerts belong to other conversations. Preserve real
    // top-level toasts and active-chat alerts while rejecting nav previews.
    const errorText = Array.from(d.querySelectorAll(
      '[role="alert"],[data-testid*="error"],[data-testid*="toast"]'))
      .slice(-35).find(n => !n.closest?.(
        'aside,nav,[role="navigation"],[data-testid="sidebar"]'));
    const alertError = !!errorText &&
      /nie udało się wczytać|failed to load|przerwano|interrupted|przekroczono limit|timed out/i.test(
        String(errorText.textContent || "").slice(-400));
    // The centralized detector also checks short DIL assistant error placeholders.
    // User quotations are deliberately excluded by that detector.
    const responseError = w.__OPENBROWSER_RESPONSE_SIGNALS__?.read()?.visibleError;
    // Old failed turns and stale toasts can persist in long DIL chats. A
    // substantial, completed newest assistant reply supersedes old errors.
    // A short current error placeholder is still treated as an error.
    const error = !(role === 'assistant' && text.trim().length >= 180) &&
      (alertError || !!responseError);
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
