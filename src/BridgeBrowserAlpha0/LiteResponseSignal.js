(function initOpenBrowserResponseSignals(global) {
  "use strict";
  if (global.__OPENBROWSER_RESPONSE_SIGNALS__) return;
  if (!global.document || global.location.hostname !== "chatgpt.com") return;

  // Bounded, content-free click journal. isTrusted=false is NOT proof of OBL.
  const clickQueue = [];
  let clickSeq = 0;
  const clickKinds = [
    ["resume", /^(?:wzn[oó]w|kontynuuj|resume|continue)$/i],
    ["retry", /^(?:spr[oó]buj ponownie|pon[oó]w|retry|try again)$/i],
    ["reload", /^(?:od[sś]wie[zż]|prze[lł]aduj|reload|refresh)$/i],
    ["stop", /^(?:zatrzymaj|przerwij|stop|stop generating)$/i],
    ["send", /^(?:wy[sś]lij|send|send message)$/i],
    ["new_chat", /^(?:rozpocznij nowy czat|nowy czat|start (?:a )?new chat|new chat)$/i]
  ];
  if (typeof global.document.addEventListener === "function")
    global.document.addEventListener("click", event => {
    const button = event.target?.closest?.('button,[role="button"],a');
    if (!button || button.closest?.('aside,nav,[role="navigation"]')) return;
    const label = String(button.getAttribute?.('aria-label') ||
      button.innerText || button.textContent || '').trim().slice(0, 90);
    const kind = clickKinds.find(item => item[1].test(label));
    if (!kind) return;
    clickQueue.push({
      id: String(Date.now()) + '-' + (++clickSeq),
      action: kind[0], trusted: event.isTrusted === true,
      utcMs: Date.now(), monotonicMs: Math.round(global.performance.now())
    });
    if (clickQueue.length > 64) clickQueue.shift();
  }, true);

  const phrases = [
    ["response_load_failed", /nie udało się wczytać tej odpowiedzi|couldn.t load this response|failed to load (this |the )?response/i],
    ["delivery_timeout", /przekroczono limit czasu dostarczenia|message delivery timed out|delivery time limit exceeded/i],
    ["reasoning_interrupted", /przerwano rozumowanie|reasoning interrupted/i]
  ];
  // A terminal banner is recognized only with its own "start new chat"
  // control. User quotations and sidebar previews are never sufficient.
  const lengthLimitPhrase = /osi[aą]gni[eę]to maksymaln[aą] d[lł]ugo[sś][cć] (?:dla )?tej konwersacji|(?:you['’]?ve|you have) reached the maximum length for this conversation|(?:this|the) conversation (?:has )?reached (?:its|the) (?:maximum )?length limit/i;
  const extraLengthPhrase = /(?:przekroczono|osi[aą]gni[eę]to|osi[aą]gni[eę]ty) (?:maksymaln[aą] |dozwolon[aą] )?d[lł]ugo[sś][cć] (?:tego |tej )?(?:czatu|rozmowy|konwersacji)|(?:ten|ta) (?:czat|rozmowa) (?:jest |sta[lł]a si[eę] )?za d[lł]ug[ai]|conversation (?:is |has become )?too long|maximum conversation length reached/i;
  const newChatControl = /^(?:rozpocznij nowy czat|utw[oó]rz nowy czat|start (?:a )?new chat|create (?:a )?new chat)$/i;
  function conversationLengthPanel(main) {
    if (!main) return false;
    const controls = main.querySelectorAll('button,a,[role="button"]');
    for (const btn of Array.from(controls).slice(-100)) {
      if (!isVisible(btn) || btn.closest?.(
          '[data-message-author-role],[data-user-message-bubble],aside,nav')) continue;
      const label = String(btn.innerText || btn.textContent || '').trim();
      if (!newChatControl.test(label)) continue;
      let panel = btn.parentElement;
      for (let depth = 0; panel && panel !== main && depth < 5;
           depth++, panel = panel.parentElement) {
        if (panel.closest?.('[data-message-author-role="user"],[data-user-message-bubble]')) break;
        const content = String(panel.innerText || panel.textContent || '').trim();
        if (content.length <= 750 && (lengthLimitPhrase.test(content) || extraLengthPhrase.test(content)) &&
            isVisible(panel)) return true;
      }
    }
    return false;
  }
  // The localized ChatGPT crash page is not an assistant error bubble.
  // Require the error heading and a visible Retry control, with no turns.
  function fullPageAppFailure(doc, turnCount) {
    if (turnCount !== 0) return false;
    const buttons = Array.from(doc.querySelectorAll('button,[role="button"]'));
    const retry = buttons.some(n => isVisible(n) &&
      /^(?:spr[oó]buj ponownie|try again|retry)$|^(?:od[sś]wie[zż]|reload)$/i.test(
        String(n.textContent || n.innerText || '').trim()));
    if (!retry) return false;
    const pageProblem = /chatgpt napotka[łl] problem|chatgpt (?:ran into|encountered) (?:a |an )?(?:problem|error)/i;
    const headings = Array.from(doc.querySelectorAll('h1,h2,h3,[role="heading"]'));
    if (headings.some(n => isVisible(n) &&
        pageProblem.test(String(n.textContent || '').trim()))) return true;
    // ChatGPT DIL often renders the title as a plain div, not h1.
    // Require a short, visible FULL-PAGE body and Retry; not quoted messages.
    const body = String(doc.body?.innerText || '').trim();
    return body.length > 0 && body.length < 1600 && pageProblem.test(body);
  }
  function terminalLengthNotice(doc) {
    const nodes = Array.from(doc.querySelectorAll(
      '[role="alert"],[role="status"],[aria-live],p,span')).slice(-350);
    return nodes.some(n => {
      if (!isVisible(n) || n.closest?.(
        '[data-message-author-role],[data-user-message-bubble],[data-chatgpt-selection-message-id],textarea,[contenteditable="true"],aside,nav')) return false;
      const text = String(n.innerText || n.textContent || '').trim();
      return text.length > 0 && text.length <= 300 &&
        (lengthLimitPhrase.test(text) || extraLengthPhrase.test(text));
    });
  }  function connectionFailure(doc) {
    // Banner/toast outside message bubbles; never infer from the user's text.
    const pattern = /(?:po[lł][aą]czenie zosta[lł]o przerwane|connection (?:was |has been )?interrupted|connection (?:was |has been )?lost|utracono po[lł][aą]czenie)/i;
    const nodes = Array.from(doc.querySelectorAll(
      '[role="status"],[role="alert"],[aria-live],p,span')).slice(-350);
    return nodes.some(n => {
      if (!isVisible(n) || n.closest?.(
        '[data-message-author-role],[data-user-message-bubble],[data-chatgpt-selection-message-id],textarea,[contenteditable="true"],aside,nav')) return false;
      const content = String(n.innerText || n.textContent || '').trim();
      return content.length > 0 && content.length <= 180 &&
        pattern.test(content);
    });
  }  function inspectNode(node) {
    const content = (node && node.textContent || "").slice(-900);
    if (lengthLimitPhrase.test(content) || extraLengthPhrase.test(content)) return "conversation_length_exceeded";
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
    let error = conversationLengthPanel(main) ? 'conversation_length_exceeded' : null;
    let errorSource = error ? 'terminal_new_chat_panel' : 'none';
    if (!error && fullPageAppFailure(doc, turnCount)) {
      error = 'chatgpt_app_problem';
      errorSource = 'full_page_app_error';
    }
    let ignoredHiddenAlerts = 0;
    // Keep *visible* global error toasts, but ignore hidden stale alerts
    // and any alerts inside sidebar/navigation.
    const alerts = Array.from(doc.querySelectorAll(
      '[role="alert"],[data-testid*="error"],[data-testid*="toast"]'))
      .filter(node => !node.closest?.(
        'aside,nav,[role="navigation"],[data-testid="sidebar"]'));
    for (const node of alerts.slice(-35)) {
      if (error === 'conversation_length_exceeded') break;
      if (!isVisible(node)) {
        if (inspectNode(node)) ignoredHiddenAlerts++;
        continue;
      }
      const alertError = inspectNode(node);
      if (alertError && !error) {
        error = alertError; errorSource = 'visible_alert'; break;
      }
    }
    if (!error && terminalLengthNotice(doc)) {
      error = 'conversation_length_exceeded';
      errorSource = 'visible_terminal_notice';
    }    if (!error && connectionFailure(doc)) {
      error = 'connection_interrupted';
      errorSource = 'visible_connection_status';
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
      toolBusySignal: !!main?.querySelector?.('[data-testid*="tool-running"],[data-state="running"]') ||
        !!Array.from(legacyTurns.length ? legacyTurns : dilTurns).at(-1)?.closest?.('[data-testid^="conversation-turn-"]')?.querySelector?.('[aria-busy="true"]'),      hasDraft: Array.from(doc.querySelectorAll('#prompt-textarea,[data-testid="composer-text-input"],textarea,[contenteditable="true"]')).some(e => String(e.value || e.innerText || "").trim().length > 0),
      visibleError: error,
      errorSource,
      ignoredHiddenAlerts,
      uiActions: clickQueue.splice(0, 32),
      durationMs: Math.round(global.performance.now() - t0)
    };
  }
  global.__OPENBROWSER_RESPONSE_SIGNALS__ = { read };
})(window);
