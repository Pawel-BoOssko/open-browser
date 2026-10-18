(function initLiteHighGuard(w) {
  "use strict";
  if (w.__OPENBROWSER_HIGH_GUARD__ || !w.document ||
      w.location.hostname !== "chatgpt.com") return;
  const modes = new Set(["instant", "medium", "high", "extra high"]);
  const clean = value => String(value || "").trim().replace(/\s+/g, " ").toLowerCase();
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
    trigger.click();
    // React may render the picker options on a later event-loop turn.
    w.setTimeout(chooseHigh, 250);
    return { state: "high_selection_requested", previous: current };
  }
  function chooseHigh() {
    const opts = Array.from(w.document.querySelectorAll(
      '[role="menuitem"],[role="option"],[role="radio"],[data-radix-collection-item],button'));
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
