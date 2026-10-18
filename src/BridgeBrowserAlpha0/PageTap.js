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
      return /^https:\/\/chatgpt\.com\/backend-api\/(f\/)?conversation/.test(text);
    } catch (_) {
      return false;
    }
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

  // --- TELEMETRY: SSE stream reader for POST conversation requests ---
  const readSseStream = async (response, url, method) => {
    try {
      const reader = response.body.getReader();
      const decoder = new TextDecoder("utf-8");
      let chunkIndex = 0;
      let totalBytes = 0;

      post("page_tap_trace", {
        traceType: "fetch_post_sse_stream_start",
        url,
        method,
        status: response.status
      }, "ok");

      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          post("page_tap_trace", {
            traceType: "fetch_post_sse_stream_end",
            url,
            totalChunks: chunkIndex,
            totalBytes
          }, "ok");

          post("page_fetch_sse_done", { url, method, totalChunks: chunkIndex, totalBytes });
          break;
        }

        const chunk = decoder.decode(value, { stream: true });
        totalBytes += value.byteLength;
        chunkIndex++;

        // Forward each SSE chunk to extractor
        post("page_fetch_sse_record", {
          url,
          method,
          chunkIndex,
          record: chunk.slice(0, 65536)
        });

        // Also emit trace with full length info
        post("page_tap_trace", {
          traceType: "fetch_post_sse_chunk",
          url,
          chunkIndex,
          chunkLength: chunk.length,
          totalBytesSoFar: totalBytes,
          sample: chunk.slice(0, 500)
        }, "ok");
      }
    } catch (error) {
      post("page_tap_trace", {
        traceType: "fetch_post_sse_stream_error",
        url,
        error: String(error),
        stack: error && error.stack ? String(error.stack).slice(0, 2000) : null
      }, "error");
    }
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

      post("page_tap_trace", {
        traceType: "fetch_start",
        url,
        method,
        hasInit: !!init
      }, "ok");

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

      post("page_tap_trace", {
        traceType: "fetch_response",
        url,
        method,
        isConversation,
        status: response.status,
        ok: response.ok,
        mimeType: response.headers ? response.headers.get("content-type") : null
      }, "ok");

      // --- TELEMETRY: Intercept POST conversation SSE streams ---
      if (isConversation && method.toUpperCase() === "POST") {
        const contentType = response.headers ? response.headers.get("content-type") || "" : "";
        const isEventStream = contentType.includes("text/event-stream");

        post("page_tap_trace", {
          traceType: "fetch_post_conversation_detected",
          url,
          method,
          contentType,
          isEventStream,
          status: response.status
        }, "ok");

        if (isEventStream && response.body) {
          // Clone the response so we can read the stream without consuming it
          const [streamForApp, streamForTap] = response.body.tee();

          // Read the tap stream in the background
          const tapResponse = new Response(streamForTap);
          readSseStream(tapResponse, url, method);

          // Return a response with the app stream
          const appResponse = new Response(streamForApp, {
            status: response.status,
            statusText: response.statusText,
            headers: response.headers
          });
          return appResponse;
        }
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

      post("page_tap_trace", {
        traceType: "fetch_trimmer_input",
        url,
        originalTextLength: originalText.length,
        originalTextSample: originalText.slice(0, 200)
      }, "ok");

      const trimResult = moduleApi.trimConversationResponseText(originalText, {
        url,
        method,
        status: response.status,
        durationMsBeforeTrim: Date.now() - startedAt
      });

      const result = trimResult && trimResult.result ? trimResult.result : null;

      if (trimResult && trimResult.ok && trimResult.changed && typeof trimResult.responseText === "string") {
        post("page_tap_trace", {
          traceType: "fetch_trimmer_output_trimmed",
          url,
          trimmedTextLength: trimResult.responseText.length,
          trimmedTextSample: trimResult.responseText.slice(0, 200)
        }, "ok");

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
      post("page_tap_trace", { traceType: "xhr_open", method, url }, "ok");
      post("page_xhr_open", { method, url });
      return open.apply(xhr, arguments);
    };

    xhr.addEventListener("loadend", () => {
      post("page_tap_trace", { traceType: "xhr_loadend", method, url, status: xhr.status }, "ok");
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
      const wsUrl = String(url);
      post("page_tap_trace", { traceType: "websocket_open", url: wsUrl }, "ok");
      post("page_websocket_open", { url: wsUrl });

      // --- TELEMETRY: Intercept incoming messages with NO truncation limit ---
      ws.addEventListener("message", event => {
        const fullData = typeof event.data === "string" ? event.data : "[binary]";
        const dataLength = typeof event.data === "string" ? event.data.length : -1;
        // Send full data up to 65536 chars (was 4000 before)
        const data = typeof event.data === "string"
          ? event.data.slice(0, 65536)
          : "[binary]";
        post("page_tap_trace", { 
          traceType: "websocket_message_frame", 
          url: wsUrl, 
          dataLength,
          wasTruncated: dataLength > 65536,
          sample: fullData.slice(0, 500)
        }, "ok");
        post("page_websocket_message", { url: wsUrl, data });
      });

      // --- TELEMETRY: Intercept outgoing messages (ws.send) ---
      const originalSend = ws.send.bind(ws);
      ws.send = function(data) {
        try {
          const sendData = typeof data === "string" ? data : "[binary]";
          const sendLength = typeof data === "string" ? data.length : -1;
          post("page_tap_trace", {
            traceType: "websocket_send",
            url: wsUrl,
            dataLength: sendLength,
            sample: sendData.slice(0, 500)
          }, "ok");
          post("page_websocket_send", {
            url: wsUrl,
            data: sendData.slice(0, 65536),
            dataLength: sendLength
          });
        } catch (_) {}
        return originalSend(data);
      };

      // --- TELEMETRY: WebSocket close/error ---
      ws.addEventListener("close", event => {
        post("page_tap_trace", {
          traceType: "websocket_close",
          url: wsUrl,
          code: event.code,
          reason: event.reason || "",
          wasClean: event.wasClean
        }, "ok");
        post("page_websocket_close", {
          url: wsUrl,
          code: event.code,
          reason: event.reason || "",
          wasClean: event.wasClean
        });
      });

      ws.addEventListener("error", event => {
        post("page_tap_trace", {
          traceType: "websocket_error",
          url: wsUrl
        }, "error");
        post("page_websocket_error", { url: wsUrl });
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
        post("page_tap_trace", { traceType: "eventsource_open", url: String(url) }, "ok");
        post("page_eventsource_open", { url: String(url) });
        es.addEventListener("message", event => {
          const rawData = String(event.data || "");
          post("page_tap_trace", { 
            traceType: "eventsource_message_frame", 
            url: String(url), 
            dataLength: rawData.length,
            sample: rawData.slice(0, 200)
          }, "ok");
          post("page_eventsource_message", {
            url: String(url),
            data: rawData.slice(0, 65536)
          });
        });
        // --- TELEMETRY: EventSource close/error ---
        es.addEventListener("error", event => {
          post("page_tap_trace", {
            traceType: "eventsource_error",
            url: String(url),
            readyState: es.readyState
          }, "error");
          post("page_eventsource_error", { url: String(url), readyState: es.readyState });
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
    version: "page-tap-alpha14-telemetry",
    trimmingMode: "page_fetch_wrapper",
    websocketDataLimit: 65536,
    postSseStreamCapture: true,
    websocketSendCapture: true,
    websocketCloseCapture: true
  }, "ok");
})();
