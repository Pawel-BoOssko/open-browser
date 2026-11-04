// Native ChatGPT conversation title change, initiated only by an explicit user action.
(() => {
  if (window.__OBL_NATIVE_RENAME__) return;
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  let busy = false;
  let state = "idle";
  const readTitle = obj => {
    if (typeof obj?.title === "string") return obj.title;
    if (typeof obj?.conversation?.title === "string") return obj.conversation.title;
    return null;
  };
  const run = async (id, expected, desired) => {
    const fetchTimed = async (url, options, ms) => {
      const abort = new AbortController();
      const timer = setTimeout(() => abort.abort(), ms);
      try { return await fetch(url, {...options, signal: abort.signal}); }
      finally { clearTimeout(timer); }
    };
    let sessionResponse;
    try {
      sessionResponse = await fetchTimed("/api/auth/session",
        {credentials:"include", cache:"no-store"}, 5000);
    } catch (e) { return e?.name === "AbortError" ? "session_timeout" : "session_request_error"; }
    if (!sessionResponse.ok) return "session_http_" + sessionResponse.status;
    let session;
    try { session = await sessionResponse.json(); }
    catch (_) { return "session_invalid_json"; }
    const token = session?.accessToken;
    if (typeof token !== "string" || token.length < 10) return "missing_session_token";
    const endpoint = "/backend-api/conversation/" + id;
    const request = async (method, body, ms) => {
      const headers = {"Authorization": "Bearer " + token};
      if (body !== undefined) headers["Content-Type"] = "application/json";
      try {
        const response = await fetchTimed(endpoint, {
          method, credentials:"include", cache:"no-store", headers,
          ...(body === undefined ? {} : {body:JSON.stringify(body)})
        }, ms);
        if (!response.ok) return {error: "http_" + response.status};
        if (method === "PATCH") return {ok: true};
        const json = await response.json();
        return {ok: true, title: readTitle(json)};
      } catch (e) {
        return {error:e?.name === "AbortError" ? "timeout" : "request_error"};
      }
    };
    const before = await request("GET", undefined, 12000);
    if (!before.ok) return "read_" + before.error;
    if (before.title === desired) return "already_named";
    if (before.title === null) return "server_title_unknown";
    // Never overwrite a native title changed outside OBL since the local readback.
    if (before.title !== expected) return "server_title_changed";
    // A local bookmark may have an older title; the explicit user action updates ChatGPT.
    const patched = await request("PATCH", {title:desired}, 5000);
    if (!patched.ok) return "patch_" + patched.error;
    const after = await request("GET", undefined, 12000);
    if (!after.ok) return "verify_" + after.error;
    return after.title === desired ? "renamed" : "verify_mismatch";
  };
  window.__OBL_NATIVE_RENAME__ = {
    start(id, expected, desired) {
      if (busy) return "busy";
      if (location.origin !== "https://chatgpt.com") return "wrong_origin";
      if (!uuid.test(id)) return "invalid_id";
      if (typeof expected !== "string" || typeof desired !== "string" ||
          !desired.trim() || desired.length > 120) return "invalid_title";
      busy = true;
      state = "running";
      run(id, expected, desired).then(s => {state = s;},
        e => {state = e?.name === "AbortError" ? "timeout" : "request_error";})
        .finally(() => {busy = false;});
      return "started";
    },
    status() { return state; }
  };
})();
