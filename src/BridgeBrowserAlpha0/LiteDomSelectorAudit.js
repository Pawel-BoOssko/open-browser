(function initDomSelectorAudit(w) {
  "use strict";
  if (!w.document || w.location.hostname !== "chatgpt.com" ||
      w.__OPENBROWSER_DOM_SELECTOR_AUDIT__) return;

  // Metadata-only inspection; NEVER reads chat contents, and NEVER mutates DOM.
  const candidates = Object.freeze({
    conversationTestIds: '[data-testid^="conversation-turn-"]',
    messageRoles: '[data-message-author-role="assistant"],[data-message-author-role="user"]',
    dilAssistant: '[data-chatgpt-selection-message-id]',
    dilUser: '[data-user-message-bubble]',
    mainArticles: 'main article',
    turnDataIds: '[data-turn-id]'
  });
  function inspect() {
    const doc = w.document;
    const counts = {};
    for (const [key, selector] of Object.entries(candidates))
      counts[key] = doc.querySelectorAll(selector).length;
    const roleCandidates = Array.from(doc.querySelectorAll(candidates.messageRoles));
    const modernAssistant = Array.from(doc.querySelectorAll(candidates.dilAssistant));
    const modernUser = Array.from(doc.querySelectorAll(candidates.dilUser));
    const modernNodes = new Set([...modernAssistant, ...modernUser]);
    // The same DOM element can match both selectors. Count it only once.
    const modernUsers = new Set(modernUser);
    const modernCount = modernNodes.size;
    const legacyAssistant = roleCandidates.filter(n =>
      n.getAttribute("data-message-author-role") === "assistant").length;
    const assistant = modernCount ? modernCount - modernUsers.size : legacyAssistant;
    const user = modernCount ? modernUsers.size : roleCandidates.length - legacyAssistant;
    counts.dilMessages = modernCount;
    const domVariant = modernCount ? (roleCandidates.length ? "mixed" : "dil") :
      (roleCandidates.length || counts.conversationTestIds ? "legacy" : "unknown");
    return {
      counts, assistant, user, domVariant,
      mismatch: counts.conversationTestIds === 0 && assistant + user > 0,
      safeToHardPrune: false,
      action: "read_only_qa_required"
    };
  }
  w.__OPENBROWSER_DOM_SELECTOR_AUDIT__ = { inspect };
})(window);
