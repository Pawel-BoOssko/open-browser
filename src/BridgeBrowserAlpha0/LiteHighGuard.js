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
    // Observed in the real ChatGPT GPT-6 picker on 2026-10-18:
    // a stable button attribute remains readable while the menu is closed.
    const picker = w.document.querySelector?.(
      'button[aria-label="Wybierz model ChatGPT"][data-selected-reasoning-effort]');
    const selected = clean(picker?.getAttribute('data-selected-reasoning-effort'));
    if (modes.has(selected)) return selected;
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
  function generationBusy() {
    const stop = w.document.querySelector(
      '[data-testid="stop-button"],button[aria-label*="Stop generating"],button[aria-label*="Przerwij generowanie"]');
    if (stop && visible(stop)) return true;
    // Keep global tool-running evidence, but not unrelated sidebar aria-busy.
    if (w.document.querySelector('[data-testid*="tool-running"],[data-state="running"]'))
      return true;
    const turns = Array.from(w.document.querySelectorAll('[data-testid^="conversation-turn-"]'));
    const currentTurn = turns.length ? turns[turns.length - 1] : null;
    return !!currentTurn?.querySelector?.('[aria-busy="true"]');
  }
  function switchHigh() {
    const current = currentMode();
    if (current === "high" || current === "extra high")
      return { state: "confirmed_high", mode: current };
    if (current === "unknown")
      return { state: "cannot_identify_current_mode", mode: current };
    const actualPicker = w.document.querySelector?.(
      'button[aria-label="Wybierz model ChatGPT"][data-selected-reasoning-effort]');
    const trigger = actualPicker || Array.from(w.document.querySelectorAll('button,[role="combobox"]'))
      .reverse().find(el => visible(el) &&
        clean(el.getAttribute("aria-label") || el.innerText || el.textContent) === current);
    if (!trigger) return { state: "no_picker", mode: current };
    // Never interrupt a generation or tool just to change its effort.
    // In particular this code must not click Stop in an unrelated chat.
    if (generationBusy())
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
        generationBusy())
      return { state: "page_changed_or_busy" };
    // 2026-10-18 real GPT-6 UI: 'Moc' is a slider from 0..2, not
    // three separate High/Medium/Instant menu buttons.
    const slider = w.document.querySelector?.(
      '[data-reasoning-slider] [role="slider"]');
    const value = Number(slider?.getAttribute('aria-valuenow'));
    const maximum = Number(slider?.getAttribute('aria-valuemax'));
    const target = slider?.closest?.('[role="menuitem"]') ||
      slider?.closest?.('[data-reasoning-slider]');
    if (slider && target && Number.isFinite(value) &&
        Number.isFinite(maximum) && maximum >= value &&
        maximum - value <= 5 && typeof w.KeyboardEvent === 'function') {
      if (value === maximum) return { state: 'already_max_slider' };
      target.focus?.();
      for (let i = value; i < maximum; i++)
        target.dispatchEvent(new w.KeyboardEvent('keydown', {
          key: 'ArrowRight', code: 'ArrowRight', bubbles: true
        }));
      return { state: 'slider_high_requested_pending_verification' };
    }
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
