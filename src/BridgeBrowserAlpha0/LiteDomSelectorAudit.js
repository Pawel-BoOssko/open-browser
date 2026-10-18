(function initDomSelectorAudit(w) {
  "use strict";
  if (!w.document || w.location.hostname !== "chatgpt.com" ||
      w.__OPENBROWSER_DOM_SELECTOR_AUDIT__) return;

  // Metadata-only inspection; NEVER reads chat contents, and NEVER mutates DOM.
  const candidates = Object.freeze({
    conversationTestIds: '[data-testid^="conversation-turn-"]',
    messageRoles: '[data-message-author-role="assistant"],[data-message-author-role="user"]',
    mainArticles: 'main article',
    turnDataIds: '[data-turn-id]'
  });
  function inspect() {
    const doc = w.document;
    const counts = {};
    for (const [key, selector] of Object.entries(candidates))
      counts[key] = doc.querySelectorAll(selector).length;
    const roleCandidates = Array.from(doc.querySelectorAll(candidates.messageRoles));
    const assistant = roleCandidates.filter(n =>
      n.getAttribute("data-message-author-role") === "assistant").length;
    const user = roleCandidates.length - assistant;
    return {
      counts, assistant, user,
      mismatch: counts.conversationTestIds === 0 && roleCandidates.length > 0,
      safeToHardPrune: false,
      action: "read_only_qa_required"
    };
  }
  w.__OPENBROWSER_DOM_SELECTOR_AUDIT__ = { inspect };
})(window);
