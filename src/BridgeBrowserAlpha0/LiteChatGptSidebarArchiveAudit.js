// Read only rendered ChatGPT sidebar links. Positive presence is evidence.
// Absence in a virtualized/scrolled sidebar is NOT archive verification.
(() => {
  if (window.__OBL_SIDEBAR_ARCHIVE_AUDIT__) return;
  const pattern = /^\/c\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/?$/i;
  const idFormat = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  const isRendered = (link) => {
    if (!link.getClientRects || link.getClientRects().length === 0) return false;
    for (let element = link; element; element = element.parentElement) {
      if (element.hidden || element.getAttribute?.("aria-hidden") === "true" ||
          element.hasAttribute?.("inert")) return false;
      const style = window.getComputedStyle(element);
      if (style.display === "none" || style.visibility === "hidden" ||
          style.visibility === "collapse" || Number(style.opacity) === 0) return false;
    }
    return true;
  };
  window.__OBL_SIDEBAR_ARCHIVE_AUDIT__ = {
    inspect(ids) {
      if (location.origin !== "https://chatgpt.com") return JSON.stringify({status:"wrong_origin"});
      if (!Array.isArray(ids) || ids.length > 100 || ids.some(x => typeof x !== "string" || !idFormat.test(x)))
        return JSON.stringify({status:"invalid_ids"});
      const targets = new Set(ids.map(x => x.toLowerCase()));
      const displayed = new Set(), inSidebarDom = new Set();
      let sidebarLinks = 0, renderedSidebarLinks = 0;
      for (const link of document.querySelectorAll('a[href]')) {
        if (!link.closest('nav, aside, [data-testid*="sidebar"], [data-testid*="history"]')) continue;
        let path;
        try { path = new URL(link.href, location.href).pathname; } catch { continue; }
        const match = pattern.exec(path);
        if (!match) continue;
        sidebarLinks++;
        const id = match[1].toLowerCase();
        if (targets.has(id)) inSidebarDom.add(id);
        if (isRendered(link)) {
          renderedSidebarLinks++;
          if (targets.has(id)) displayed.add(id);
        }
      }
      return JSON.stringify({status:"observed", sidebarLinks, renderedSidebarLinks,
        visibleArchivedIds:[...displayed], sidebarDomArchivedIds:[...inSidebarDom],
        checked:targets.size, absenceProven:false});
    }
  };
})();
