(function initLiteHighGuard(w) {
  "use strict";
  if (w.__OPENBROWSER_HIGH_GUARD__ || !w.document ||
      w.location.hostname !== "chatgpt.com") return;
  const modes = new Set(["instant", "medium", "high", "extra high"]);
  const clean = value => {
    const label = String(value || "").trim().replace(/\s+/g, " ").toLowerCase();
    return ({"wysoki":"high", "wysoka":"high", "średni":"medium",
      "sredni":"medium", "natychmiastowy":"instant"})[label] || label;
  };
  const visible = node => !node.hidden && node.getAttribute("aria-hidden") !== "true" &&
    (!node.getClientRects || node.getClientRects().length > 0);

  function currentMode() {
    const controls = Array.from(w.document.querySelectorAll(
      'button,[role="combobox"],[data-testid*="model-picker"]'));
    // Only visible selectable model/effort controls, not conversation paragraphs.
    for (const el of controls.reverse()) {
      if (!visible(el)) continue;
      const label = clean(el.getAttribute("aria-label") || el.innerText || el.textContent);
      if (modes.has(label)) return label;
    }
    return "unknown";
  }
  function hasUserDraft() {
    const editor = w.document.querySelector('textarea,[contenteditable="true"]');
    return !!editor && !!String(editor.value || editor.innerText || '').trim();
  }
  function switchHigh() {
    const current = currentMode();
    if (current === "high" || current === "extra high")
      return { state: "confirmed_high", mode: current };
    if (current === "unknown")
      return { state: "cannot_identify_current_mode", mode: current };
    const trigger = Array.from(w.document.querySelectorAll('button,[role="combobox"]'))
      .reverse().find(el => visible(el) &&
        clean(el.getAttribute("aria-label") || el.innerText || el.textContent) === current);
    if (!trigger) return { state: "no_picker", mode: current };
    // Never interrupt a generation or tool just to change its effort.
    // In particular this code must not click Stop in an unrelated chat.
    if (w.document.querySelector('[data-testid="stop-button"],[aria-busy="true"]'))
      return { state: "generation_busy", mode: current };
    if (hasUserDraft()) return { state: "user_draft_present", mode: current };
    const selectedPath = w.location.pathname;
    trigger.click();
    // React may render the picker options on a later event-loop turn.
    w.setTimeout(() => chooseHigh(selectedPath), 250);
    return { state: "high_selection_requested", previous: current, stopRequested: false };
  }
  function chooseHigh(selectedPath) {
    if (w.location.hostname !== "chatgpt.com" || w.location.pathname !== selectedPath ||
        hasUserDraft() ||
        w.document.querySelector('[data-testid="stop-button"],[aria-busy="true"]'))
      return { state: "page_changed_or_busy" };
    const opts = Array.from(w.document.querySelectorAll(
      '[role="menuitem"],[role="option"],[role="radio"],[data-radix-collection-item]'));
    const high = opts.reverse().find(el => visible(el) &&
      clean(el.getAttribute("aria-label") || el.innerText || el.textContent) === "high");
    if (high && !high.disabled) {
      high.click();
      return { state: "high_selected_pending_verification" };
    }
    return { state: "high_option_not_found" };
  }
  function audit() {
    const mode = currentMode();
    return { mode, compliant: mode === "high" || mode === "extra high" };
  }
  w.__OPENBROWSER_HIGH_GUARD__ = { audit, switchHigh };
})(window);
