function (expected) {
  "use strict";
  if (typeof expected !== "string" || !/^SUCCESSOR READY [a-f0-9]{32}$/.test(expected))
    return JSON.stringify({exact:false,assistantCount:0,selectionCount:0,via:"none"});
  const root = document.querySelector("main");
  if (!root) return JSON.stringify({exact:false,assistantCount:0,selectionCount:0,via:"none"});
  // Exact assistant role is preferred. Never inspect whole conversation turns.
  const assistants = [...root.querySelectorAll('[data-message-author-role="assistant"]')];
  const direct = assistants.some(el =>
    typeof el.innerText === "string" && el.innerText.trim().split(/\r?\n/).some(line => line.trim() === expected));
  if (direct) return JSON.stringify({exact:true,assistantCount:assistants.length,selectionCount:0,via:"assistant_role"});
  // ChatGPT also renders assistant message bodies using selection-message-id
  // WITHOUT a data-message-author-role attribute. Only use these isolated
  // bodies when the direct assistant selector found no assistant nodes.
  // Exclude any body inside/containing a user-message or user-role marker.
  let selected = [];
  if (!assistants.length) {
    selected = [...root.querySelectorAll('[data-chatgpt-selection-message-id]')];
  }
  const fallback = selected.some(el => {
    if (typeof el.innerText !== "string" || !el.innerText.trim().split(/\r?\n/).some(line => line.trim() === expected))
      return false;
    if (el.closest?.('[data-message-author-role="user"],[data-user-message-bubble]'))
      return false;
    if (el.querySelector?.('[data-message-author-role="user"],[data-user-message-bubble]'))
      return false;
    return true;
  });
  return JSON.stringify({exact:fallback,assistantCount:assistants.length,
    selectionCount:selected.length,via:fallback?"selection_body":"none"});
}