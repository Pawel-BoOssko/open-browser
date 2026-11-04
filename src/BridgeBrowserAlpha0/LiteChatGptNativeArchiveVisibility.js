// Read-only batched normal ChatGPT list audit in a signed-in WebView2.
// Detail GET already proved native archival. Owner acceptance requires complete
// absence from the normal list, not a second archive-list presence check.
(() => {
  if (window.__OBL_NATIVE_VISIBILITY_V2__) return;
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  let state = "idle", busy = false;
  const fetchBounded = async (url, token) => {
    const abort = new AbortController();
    const timer = setTimeout(() => abort.abort(), 5000);
    try {
      return await fetch(url, {
        credentials: "include", cache: "no-store",
        headers: token ? { "Authorization": "Bearer " + token } : {},
        signal: abort.signal
      });
    } finally { clearTimeout(timer); }
  };
  const list = async (token, ids, archived, deadline) => {
    const found = new Set(), seen = new Set();
    const limit = 100, maxPages = 40;
    let offset = 0, pages = 0, firstTotal = null, lastTotal = null;
    let offsetRelativeTotal = false;
    const finish = (status) => ({status, pages, found: [...found], scanned: offset,
      firstTotal, lastTotal, offsetRelativeTotal});
    for (let page = 0; page < maxPages; page++) {
      if (Date.now() >= deadline) return finish("time_budget");
      const url = "/backend-api/conversations?offset=" + offset +
        "&limit=" + limit + "&is_archived=" + String(archived);
      const response = await fetchBounded(url, token);
      if (!response.ok) return finish("http_" + response.status);
      const data = await response.json();
      const items = Array.isArray(data?.items) ? data.items :
        Array.isArray(data?.conversations) ? data.conversations : null;
      if (!items || items.length > limit) return finish("invalid_page");
      const total = Number.isInteger(data?.total) && data.total >= 0 ? data.total : null;
      if (firstTotal === null) firstTotal = total;
      lastTotal = total;
      // Some authenticated list responses report "total" relative to the
      // requested offset (101, 201, 301...). Such a counter cannot prove
      // exhaustion. Require stable total-offset across every subsequent page,
      // then read through an actual empty terminal page before certifying absence.
      if (total !== null && firstTotal !== null && total !== firstTotal) {
        if (total - offset !== firstTotal) return finish("list_changed");
        offsetRelativeTotal = true;
      }
      if (offsetRelativeTotal && (total === null ||
          total - offset !== firstTotal)) return finish("list_changed");
      for (const item of items) {
        if (typeof item?.id !== "string" || typeof item?.is_archived !== "boolean")
          return finish("filter_unverifiable");
        if (item.is_archived !== archived) return finish("filter_ignored");
        if (seen.has(item.id)) return finish("paging_overlap");
        seen.add(item.id);
        if (ids.has(item.id.toLowerCase())) found.add(item.id.toLowerCase());
      }
      offset += items.length;
      pages++;
      if (archived && found.size === ids.size) return finish("found_all");
      if (items.length === 0) {
        // An empty first page with no total has no verified filter or
        // pagination evidence. Never certify every chat as absent from it.
        if (offset === 0 && total === null) return finish("empty_unverifiable");
        return finish(!offsetRelativeTotal && total !== null && offset < total
          ? "paging_gap" : "complete");
      }
      if (!offsetRelativeTotal && total !== null && offset >= total)
        return finish("complete");
    }
    return finish("incomplete");
  };
  const run = async (identifiers) => {
    if (location.origin !== "https://chatgpt.com") return {error:"wrong_origin"};
    const sessionResponse = await fetchBounded("/api/auth/session", null);
    if (!sessionResponse.ok) return {error:"session_http_" + sessionResponse.status};
    const session = await sessionResponse.json();
    const token = session?.accessToken;
    if (typeof token !== "string" || token.length < 10) return {error:"no_session"};
    const ids = new Set(identifiers.map(x => x.toLowerCase()));
    const deadline = Date.now() + 105000;
    // The independent per-chat detail GET confirmed the archived state.
    // The owner explicitly waived archive-list presence verification.
    // Skip every is_archived=true list GET to minimize rate limiting.
    const archive = {status:"not_required", pages:0, found:[], scanned:0};
    let normal, retries = 0;
    do {
      normal = await list(token, ids, false, deadline);
      if (normal.status !== "list_changed" && normal.status !== "paging_overlap") break;
      if (retries >= 2 || Date.now() >= deadline) break;
      retries++;
      await new Promise(resolve => setTimeout(resolve, retries * 300));
    } while (true);
    return {archive, normal, retries};
  };
  window.__OBL_NATIVE_VISIBILITY_V2__ = {
    auditBatch(ids) {
      if (busy) return "busy";
      if (!Array.isArray(ids) || !ids.length || ids.length > 32 ||
          ids.some(id => typeof id !== "string" || !uuid.test(id)))
        return "invalid_ids";
      if (new Set(ids.map(id => id.toLowerCase())).size !== ids.length)
        return "duplicate_ids";
      if (location.origin !== "https://chatgpt.com") return "wrong_origin";
      busy = true;
      state = "running";
      run(ids).then(report => {state = JSON.stringify(report);}, error => {
        state = JSON.stringify({error:error?.name === "AbortError" ?
          "request_timeout" : "request_error"});
      }).finally(() => {busy = false;});
      return "started";
    },
    status() { return state; }
  };
})();
