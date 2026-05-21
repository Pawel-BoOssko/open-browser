(() => {
  if (window.__bridgeBrowserPageTapInstalled) return;
  window.__bridgeBrowserPageTapInstalled = true;

  const post = (eventType, data, status, message) => {
    try {
      chrome.webview.postMessage({
        source: "page",
        eventType,
        status: status || null,
        message: message || null,
        data: data || null,
        ts: new Date().toISOString()
      });
    } catch (_) {}
  };

  const looksLikeConversationRequest = (url, method) => {
    try {
      const text = String(url || "");
      return /^https:\/\/chatgpt\.com\/backend-api\/conversation/.test(text);
    } catch (_) {
      return false;
    }
  };

  const parseSseFromText = (url, text) => {
    const records = text.replace(/\r\n/g, "\n").replace(/\r/g, "\n").split("\n\n");
    for (const record of records) {
      const trimmed = record.trim();
      if (!trimmed || trimmed === "[DONE]") continue;
      // Extract data: lines
      const lines = trimmed.split("\n");
      let data = "";
      for (const line of lines) {
        if (line.startsWith("data:")) {
          data += line.slice(5).replace(/^\s+/, "");
        }
      }
      if (data) {
        post("page_fetch_sse_record", { url, record: data });
      }
    }
    post("page_fetch_sse_done", { url });
  };

  const cloneHeaders = (headers) => {
    const out = {};
    try {
      if (!headers) return out;
      headers.forEach((value, key) => {
        const k = String(key || "");
        if (/cookie|authorization|token|proof|sentinel|session|jwt|bearer/i.test(k)) {
          out[k] = "[REDACTED]";
        } else {
          out[k] = String(value || "").slice(0, 500);
        }
      });
    } catch (_) {}
    return out;
  };

  const buildResponse = (text, original) => {
    const headers = new Headers(original.headers);
    headers.set("content-length", new Blob([text]).size.toString());
    return new Response(text, {
      status: original.status,
      statusText: original.statusText,
      headers
    });
  };

  const originalFetch = window.fetch;
  window.fetch = async function(input, init) {
    const startedAt = Date.now();
    let url = "";
    let method = "GET";

    try {
      if (typeof input === "string" || input instanceof URL) {
        url = String(input);
      } else if (input && input.url) {
        url = String(input.url);
      }

      if (init && init.method) method = String(init.method);
      else if (input && input.method) method = String(input.method);

      post("page_fetch_start", {
        url,
        method,
        isConversationGet: looksLikeConversationRequest(url, method)
      });
    } catch (error) {
      post("page_fetch_start_error", { error: String(error) }, "error");
    }

    const response = await originalFetch.apply(this, arguments);

    try {
      const isConversation = looksLikeConversationRequest(url, method);

      if (isConversation) {
        // Parse SSE from conversation responses in background
        try {
          const sseClone = response.clone();
          sseClone.text().then(text => parseSseFromText(url, text)).catch(() => {});
        } catch (_) {}
      }

      if (!isConversation || method.toUpperCase() !== "GET") {
        return response;
      }

      // Trimmer path: only for GET conversation requests with module available
      post("trimmer_fetch_seen", {
        url,
        method,
        status: response.status,
        ok: response.ok,
        headers: cloneHeaders(response.headers)
      }, "ok");

      const moduleApi = window.__BRIDGE_BROWSER_MODULES__ &&
        window.__BRIDGE_BROWSER_MODULES__.conversationTrimmer;

      if (!moduleApi || typeof moduleApi.trimConversationResponseText !== "function") {
        post("trimmer_fetch_passthrough", {
          reason: "module_not_loaded",
          url,
          status: response.status
        }, "partial");
        return response;
      }

      const clone = response.clone();
      const originalText = await clone.text();

      const trimResult = moduleApi.trimConversationResponseText(originalText, {
        url,
        method,
        status: response.status,
        durationMsBeforeTrim: Date.now() - startedAt
      });

      const result = trimResult && trimResult.result ? trimResult.result : null;

      if (trimResult && trimResult.ok && trimResult.changed && typeof trimResult.responseText === "string") {
        post("trimmer_fetch_trimmed", {
          url,
          status: response.status,
          changed: true,
          result
        }, "ok");
        return buildResponse(trimResult.responseText, response);
      }

      post("trimmer_fetch_passthrough", {
        url,
        status: response.status,
        changed: trimResult ? !!trimResult.changed : false,
        reason: result ? result.reason : "no_result",
        result
      }, "ok");

      return response;
    } catch (error) {
      post("trimmer_fetch_error", {
        url,
        method,
        error: String(error),
        stack: error && error.stack ? String(error.stack).slice(0, 2000) : null
      }, "error");

      return response;
    }
  };

  const OriginalXHR = window.XMLHttpRequest;
  window.XMLHttpRequest = function() {
    const xhr = new OriginalXHR();
    let method = null;
    let url = null;

    const open = xhr.open;
    xhr.open = function(m, u) {
      method = m;
      url = u;
      post("page_xhr_open", { method, url });
      return open.apply(xhr, arguments);
    };

    xhr.addEventListener("loadend", () => {
      post("page_xhr_done", {
        method,
        url,
        status: xhr.status
      });
    });

    return xhr;
  };

  try {
    const OriginalWebSocket = window.WebSocket;
    window.WebSocket = function(url, protocols) {
      const ws = protocols ? new OriginalWebSocket(url, protocols) : new OriginalWebSocket(url);
      post("page_websocket_open", { url: String(url) });
      ws.addEventListener("message", event => {
        const data = typeof event.data === "string"
          ? event.data.slice(0, 4000)
          : "[binary]";
        post("page_websocket_message", { url: String(url), data });
      });
      return ws;
    };
    window.WebSocket.prototype = OriginalWebSocket.prototype;
  } catch (error) {
    post("page_websocket_wrap_error", { error: String(error) }, "error");
  }

  try {
    const OriginalEventSource = window.EventSource;
    if (OriginalEventSource) {
      window.EventSource = function(url, config) {
        const es = new OriginalEventSource(url, config);
        post("page_eventsource_open", { url: String(url) });
        es.addEventListener("message", event => {
          post("page_eventsource_message", {
            url: String(url),
            data: String(event.data || "").slice(0, 4000)
          });
        });
        return es;
      };
      window.EventSource.prototype = OriginalEventSource.prototype;
    }
  } catch (error) {
    post("page_eventsource_wrap_error", { error: String(error) }, "error");
  }


  window.__BRIDGE_LOADED_TURNS_MONITOR__ = window.__BRIDGE_LOADED_TURNS_MONITOR__ || null;

  setInterval(() => {
    try {
      const allAssistants = document.querySelectorAll('[data-message-author-role="assistant"]');
      const latestMessage = allAssistants.length > 0 ? allAssistants[allAssistants.length - 1] : null;
      const root = latestMessage || document;
      
      // Query standard <a> download links
      const links = Array.from(root.querySelectorAll("a[href*='mnt/data'], a[href*='backend-api/files'], a[href*='sandbox:']"));
      
      // Query ChatGPT React button-style download elements
      const buttons = Array.from(root.querySelectorAll("button.behavior-btn"));
      const fileButtons = buttons.filter(btn => {
        const text = (btn.textContent || "").trim();
        // Match standard file extensions (e.g. .txt, .csv, .zip, .png, etc.)
        return /\.[a-zA-Z0-9]{2,5}$/.test(text);
      });
      
      // Combine candidates
      const candidates = [...links, ...fileButtons];
      
      candidates.forEach(element => {
        if (!element.dataset.openbridgeDownloadAttempted) {
          element.dataset.openbridgeDownloadAttempted = '1';
          
          const urlOrName = element.href || element.textContent || "unknown";
          post("page_sandbox_download_candidate", { url: urlOrName }, "ok");
          
          // Dispatch full user-like event sequence
          const evts = ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'];
          evts.forEach(evName => {
            element.dispatchEvent(new MouseEvent(evName, {
              view: window,
              bubbles: true,
              cancelable: true,
              buttons: 1
            }));
          });
          post("page_sandbox_download_click", { url: urlOrName }, "ok");
        }
      });
    } catch (_) {}
  }, 2000);

  post("page_tap_installed", {
    version: "page-tap-alpha13",
    trimmingMode: "page_fetch_wrapper"
  }, "ok");
})();
