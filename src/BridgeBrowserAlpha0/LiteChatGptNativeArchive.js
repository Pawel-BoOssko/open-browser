// Runs inside a signed-in ChatGPT WebView2. Archives one exact older
// conversation without navigating the active lead or touching Delete.
(() => {
  if (window.__OBL_NATIVE_ARCHIVE__) return;
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  let state = "idle";
  let busy = false;
  const getStatus = (data) => {
    if (!data || typeof data !== "object") return null;
    if (typeof data.is_archived === "boolean") return data.is_archived;
    if (data.conversation && typeof data.conversation.is_archived === "boolean")
      return data.conversation.is_archived;
    return null;
  };
  const run = async (id, verifyOnly = false) => {
    if (location.origin !== "https://chatgpt.com") return "wrong_origin";
    const sessionController = new AbortController();
    const sessionTimeout = setTimeout(() => sessionController.abort(), 5000);
    let session;
    try {
      const response = await fetch("/api/auth/session", {
        credentials: "include", cache: "no-store", signal: sessionController.signal
      });
      if (!response.ok) return "session_http_" + response.status;
      session = await response.json();
    } finally {
      clearTimeout(sessionTimeout);
    }
    const token = session?.accessToken;
    if (typeof token !== "string" || token.length < 10) return "missing_session_token";
    const endpoint = "/backend-api/conversation/" + id;
    const request = async (method, payload, timeoutMs = 5000) => {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), timeoutMs);
      try {
        const response = await fetch(endpoint, {
          method, credentials: "include", cache: "no-store",
          headers: {
            "Authorization": "Bearer " + token,
            ...(payload ? {"Content-Type": "application/json"} : {})
          },
          ...(payload ? {body: JSON.stringify(payload)} : {}),
          signal: controller.signal
        });
        if (!response.ok) return {ok: false, status: response.status};
        if (method === "PATCH") return {ok: true};
        const body = await response.json();
        return {ok: true, archived: getStatus(body)};
      } finally {
        clearTimeout(timeout);
      }
    };
    let before;
    // The first server GET can be slow after consecutive archives.
    // A real wake242 timed out at 5s before any PATCH; use the same
    // 12s budget as independent post-PATCH verification.
    try { before = await request("GET", undefined, 12000); }
    catch (error) {
      if (error?.name === "AbortError") return verifyOnly ? "verify_timeout" : "read_timeout";
      throw error;
    }
    if (!before.ok) return (verifyOnly ? "verify_http_" : "read_http_") + before.status;
    if (before.archived === true) return "already_archived";
    if (verifyOnly) return "verify_unarchived"; // NEVER PATCH a pending request.
    if (before.archived !== false) return "archive_state_unknown";
    let patched;
    try { patched = await request("PATCH", {is_archived: true}); }
    catch (error) {
      // No reliable response, but the server might have committed the write.
      return error?.name === "AbortError" ? "verify_timeout" : "verify_request_error";
    }
    if (!patched.ok) return "patch_http_" + patched.status;
    // After a successful PATCH, a failed GET must not cause another PATCH.
    let after;
    try { after = await request("GET", undefined, 12000); }
    catch (error) {
      if (error?.name === "AbortError") return "verify_timeout";
      return "verify_request_error";
    }
    if (!after.ok) return "verify_http_" + after.status;
    return after.archived === true ? "archived" : "verify_unarchived";
  };
  window.__OBL_NATIVE_ARCHIVE__ = {
    start(id, verifyOnly = false) {
      if (busy) return "busy";
      if (!uuid.test(id)) return "invalid_id";
      if (location.origin !== "https://chatgpt.com") return "wrong_origin";
      busy = true;
      state = "running";
      run(id, verifyOnly).then(result => { state = result; },
        error => { state = error?.name === "AbortError" ? "timeout" : "request_error"; })
        .finally(() => { busy = false; });
      return "started";
    },
    status() { return state; }
  };
})();