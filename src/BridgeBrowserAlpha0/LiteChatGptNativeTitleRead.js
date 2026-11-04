// Read-only native ChatGPT title lookup; never PATCHes or navigates.
(() => {
  if (window.__OBL_NATIVE_TITLE_READ__) return;
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  let state = "idle", busy = false;
  const getWithDeadline = async (url, token, ms) => {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), ms);
    try {
      return await fetch(url, {
        credentials: "include", cache: "no-store",
        headers: token ? {Authorization: "Bearer " + token} : {},
        signal: controller.signal
      });
    } finally { clearTimeout(timer); }
  };
  const run = async (id) => {
    try {
      const sessionResponse = await getWithDeadline("/api/auth/session", null, 5000);
      if (!sessionResponse.ok) return {state:"failed", code:"session_http_" + sessionResponse.status};
      const session = await sessionResponse.json();
      const token = session?.accessToken;
      if (typeof token !== "string" || token.length < 10)
        return {state:"failed", code:"missing_session_token"};
      const response = await getWithDeadline("/backend-api/conversation/" + id, token, 12000);
      if (!response.ok) return {state:"failed", code:"detail_http_" + response.status};
      const data = await response.json();
      const title = typeof data?.title === "string" ? data.title :
        typeof data?.conversation?.title === "string" ? data.conversation.title : null;
      if (title === null) return {state:"failed", code:"detail_title_missing"};
      return {state:"ok", title};
    } catch (error) {
      return {state:"failed", code:error?.name === "AbortError" ? "request_timeout" : "request_error"};
    }
  };
  window.__OBL_NATIVE_TITLE_READ__ = {
    start(id) {
      if (busy) return "busy";
      if (location.origin !== "https://chatgpt.com") return "wrong_origin";
      if (typeof id !== "string" || !uuid.test(id)) return "invalid_id";
      busy = true;
      state = "running";
      run(id).then(result => { state = JSON.stringify(result); })
        .finally(() => { busy = false; });
      return "started";
    },
    status() { return state; }
  };
})();
