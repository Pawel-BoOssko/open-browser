// Run ONLY in the verified retired conversation's isolated WebView2.
// The host revalidates project lead and old route before each invocation.
(() => {
  const oldId = "__OLD_UUID__";
  if (location.origin !== "https://chatgpt.com" ||
      location.pathname !== "/c/" + oldId) return "wrong_route";
  const isStop = button => {
    const testid = (button.getAttribute("data-testid") || "").toLowerCase();
    const label = (button.getAttribute("aria-label") ||
      button.getAttribute("title") || "").toLowerCase().trim();
    return testid === "stop-button" || testid === "stop-response-button" ||
      ["stop generating", "stop response", "stop", "zatrzymaj",
       "zatrzymaj generowanie", "przerwij generowanie"].includes(label);
  };
  const isVisible = button => {
    if (button.disabled || button.hidden || button.getAttribute("aria-hidden") === "true" ||
        button.closest('aside,nav,[role="navigation"],[data-testid="sidebar"],[inert],[aria-hidden="true"]'))
      return false;
    const css = getComputedStyle(button);
    return button.getClientRects().length > 0 &&
      css.display !== "none" && css.visibility !== "hidden" && css.opacity !== "0";
  };
  const stop = [...document.querySelectorAll("button")].find(
    button => isStop(button) && isVisible(button));
  if (!stop) return "stop_unavailable";
  if ("__CLICK_ALLOWED__" !== "yes") return "stop_still_visible";
  stop.click();
  return "stop_click_invoked";
})();