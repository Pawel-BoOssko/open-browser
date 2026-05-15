(function installConversationTrimmerModule(global) {
  "use strict";

  const MODULE_VERSION = "conversation-trimmer-core-v06-port-01";

  const CONFIG = {
    keepRenderableMessages: 40,
    minPrefixNodes: 5,
    maxLoadedTurns: 100,
    pollIntervalMs: 600000,
    minMsAfterReload: 600000
  };

  const state = {
    version: MODULE_VERSION,
    config: { ...CONFIG },
    counters: {
      calls: 0,
      trimApplied: 0,
      trimPassthrough: 0,
      trimError: 0
    },
    lastResult: null,
    logs: []
  };

  function nowIso() {
    return new Date().toISOString();
  }

  function post(eventType, status, data) {
    try {
      if (global.chrome && global.chrome.webview && typeof global.chrome.webview.postMessage === "function") {
        global.chrome.webview.postMessage({ source: "conversation-trimmer", eventType, status: status || "ok", data: data || null });
      }
    } catch (_) {}
  }

  function log(type, data) {
    const item = {
      ts: nowIso(),
      version: MODULE_VERSION,
      type,
      data
    };

    state.logs.push(item);
    if (state.logs.length > 200) state.logs.shift();
    post("trimmer_" + type, "ok", item);

    return item;
  }

  function cloneJson(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function getMessage(node) {
    return node && node.message ? node.message : null;
  }

  function getRole(node) {
    const msg = getMessage(node);
    return msg && msg.author ? msg.author.role || null : null;
  }

  function getContentType(node) {
    const msg = getMessage(node);
    return msg && msg.content ? msg.content.content_type || null : null;
  }

  function getPartsText(node) {
    const msg = getMessage(node);
    const parts = msg && msg.content && Array.isArray(msg.content.parts)
      ? msg.content.parts
      : [];

    return parts.map(part => {
      if (typeof part === "string") return part;
      try {
        return JSON.stringify(part);
      } catch (_) {
        return "";
      }
    }).join("\n");
  }

  function isRenderableMessageNode(node) {
    const role = getRole(node);
    const contentType = getContentType(node);

    if (role !== "user" && role !== "assistant") return false;
    if (contentType !== "text") return false;

    return getPartsText(node).trim().length > 0;
  }

  function buildActivePath(mapping, currentNodeId) {
    const path = [];
    const seen = new Set();
    let id = currentNodeId;

    while (id && mapping[id] && !seen.has(id)) {
      seen.add(id);
      path.push(id);
      id = mapping[id].parent || null;
    }

    path.reverse();
    return path;
  }

  function findFallbackCurrentNode(mapping) {
    const ids = Object.keys(mapping || {});

    for (let i = ids.length - 1; i >= 0; i--) {
      const id = ids[i];
      const node = mapping[id];
      const children = Array.isArray(node && node.children) ? node.children : [];

      if (children.length === 0) return id;
    }

    return ids.length ? ids[ids.length - 1] : null;
  }

  function choosePrefixEndIndex(path, mapping) {
    const minPrefixEnd = Math.min(
      Math.max(CONFIG.minPrefixNodes - 1, 0),
      Math.max(path.length - 1, 0)
    );

    let idx = minPrefixEnd;

    for (let i = 0; i < path.length; i++) {
      const node = mapping[path[i]];

      if (isRenderableMessageNode(node)) {
        idx = Math.max(idx, i - 1);
        break;
      }
    }

    return Math.min(idx, Math.max(path.length - 1, 0));
  }

  function chooseTailStartIndex(path, mapping) {
    let remaining = CONFIG.keepRenderableMessages;

    for (let i = path.length - 1; i >= 0; i--) {
      const node = mapping[path[i]];

      if (isRenderableMessageNode(node)) {
        remaining -= 1;

        if (remaining <= 0) {
          return i;
        }
      }
    }

    return 0;
  }

  function rebuildMapping(mapping, path, keepIds, prefixEndIndex, tailStartIndex) {
    const newMapping = {};

    for (const id of keepIds) {
      if (!mapping[id]) continue;

      const node = cloneJson(mapping[id]);
      const originalChildren = Array.isArray(node.children) ? node.children : [];

      node.children = originalChildren.filter(childId => keepIds.has(childId));

      if (node.parent && !keepIds.has(node.parent)) {
        node.parent = null;
      }

      newMapping[id] = node;
    }

    const hasGap = tailStartIndex > prefixEndIndex + 1;

    if (hasGap) {
      const lastPrefixId = path[prefixEndIndex];
      const firstTailId = path[tailStartIndex];

      if (newMapping[lastPrefixId] && newMapping[firstTailId]) {
        newMapping[firstTailId].parent = lastPrefixId;

        const children = Array.isArray(newMapping[lastPrefixId].children)
          ? newMapping[lastPrefixId].children.slice()
          : [];

        if (!children.includes(firstTailId)) {
          children.push(firstTailId);
        }

        newMapping[lastPrefixId].children = children.filter(childId => keepIds.has(childId));
      }
    }

    return newMapping;
  }

  function validateTrimmedMapping(payload, mapping) {
    const currentNode = payload.current_node;
    const errors = [];

    if (!mapping || typeof mapping !== "object") {
      errors.push("mapping_missing");
    }

    if (!currentNode || !mapping[currentNode]) {
      errors.push("current_node_missing_from_trimmed_mapping");
    }

    for (const [id, node] of Object.entries(mapping || {})) {
      if (node.parent && !mapping[node.parent]) {
        errors.push(`missing_parent:${id}->${node.parent}`);
      }

      const children = Array.isArray(node.children) ? node.children : [];

      for (const childId of children) {
        if (!mapping[childId]) {
          errors.push(`missing_child:${id}->${childId}`);
        }
      }
    }

    return {
      ok: errors.length === 0,
      errors
    };
  }

  function trimConversationPayload(payload) {
    if (!payload || typeof payload !== "object") {
      return {
        changed: false,
        reason: "payload_not_object",
        payload
      };
    }

    const mapping = payload.mapping;

    if (!mapping || typeof mapping !== "object") {
      return {
        changed: false,
        reason: "mapping_missing",
        payload
      };
    }

    const beforeCount = Object.keys(mapping).length;
    const currentNode = payload.current_node || findFallbackCurrentNode(mapping);

    if (!currentNode || !mapping[currentNode]) {
      return {
        changed: false,
        reason: "current_node_not_found",
        payload
      };
    }

    const path = buildActivePath(mapping, currentNode);

    if (!path.length) {
      return {
        changed: false,
        reason: "active_path_empty",
        payload
      };
    }

    const renderableCount = path.reduce((count, id) => {
      return count + (isRenderableMessageNode(mapping[id]) ? 1 : 0);
    }, 0);

    if (renderableCount <= CONFIG.keepRenderableMessages) {
      return {
        changed: false,
        reason: "already_small_enough",
        payload,
        stats: {
          beforeCount,
          renderableCount,
          pathLength: path.length,
          currentNode
        }
      };
    }

    const prefixEndIndex = choosePrefixEndIndex(path, mapping);
    const tailStartIndexRaw = chooseTailStartIndex(path, mapping);
    const tailStartIndex = Math.max(tailStartIndexRaw, prefixEndIndex + 1);

    const keepIds = new Set();

    for (let i = 0; i <= prefixEndIndex && i < path.length; i++) {
      keepIds.add(path[i]);
    }

    for (let i = tailStartIndex; i < path.length; i++) {
      keepIds.add(path[i]);
    }

    keepIds.add(currentNode);

    const newMapping = rebuildMapping(
      mapping,
      path,
      keepIds,
      prefixEndIndex,
      tailStartIndex
    );

    const trimmedPayload = {
      ...payload,
      current_node: currentNode,
      mapping: newMapping
    };

    const validation = validateTrimmedMapping(trimmedPayload, newMapping);

    if (!validation.ok) {
      return {
        changed: false,
        reason: "validation_failed",
        validation,
        payload,
        stats: {
          beforeCount,
          proposedAfterCount: Object.keys(newMapping).length,
          renderableCount,
          pathLength: path.length,
          currentNode
        }
      };
    }

    const afterCount = Object.keys(newMapping).length;

    return {
      changed: true,
      reason: "trimmed",
      payload: trimmedPayload,
      stats: {
        beforeCount,
        afterCount,
        removedCount: beforeCount - afterCount,
        renderableCount,
        pathLength: path.length,
        prefixEndIndex,
        tailStartIndex,
        currentNode
      }
    };
  }

  function trimConversationResponseText(responseText, meta) {
    state.counters.calls += 1;

    const startedAt = performance.now();
    const bytesBefore = responseText.length;

    let payload;

    try {
      payload = JSON.parse(responseText);
    } catch (error) {
      if (bytesBefore === 0) {
        const emptyResult = {
          ok: false,
          changed: false,
          reason: "empty_response",
          error: String(error),
          bytesBefore,
          meta: meta || null
        };

        state.lastResult = emptyResult;
        log("empty_response", emptyResult);

        return {
          ok: false,
          changed: false,
          responseText,
          result: emptyResult
        };
      }

      state.counters.trimError += 1;

      const parseResult = {
        ok: false,
        changed: false,
        reason: "json_parse_failed",
        error: String(error),
        bytesBefore,
        preview: responseText.slice(0, 300),
        meta: meta || null
      };

      state.lastResult = parseResult;
      log("json_parse_failed", parseResult);

      return {
        ok: false,
        changed: false,
        responseText,
        result: parseResult
      };
    }

    const beforeMappingCount = payload && payload.mapping
      ? Object.keys(payload.mapping).length
      : null;

    const trimResult = trimConversationPayload(payload);
    const outputText = JSON.stringify(trimResult.payload);

    const afterMappingCount = trimResult.payload && trimResult.payload.mapping
      ? Object.keys(trimResult.payload.mapping).length
      : null;

    if (trimResult.changed) {
      state.counters.trimApplied += 1;
    } else {
      state.counters.trimPassthrough += 1;
    }

    const finalResult = {
      ok: true,
      changed: trimResult.changed,
      reason: trimResult.reason,
      title: payload.title || null,
      conversation_id: payload.conversation_id || null,
      current_node: payload.current_node || null,
      bytesBefore,
      bytesAfter: outputText.length,
      beforeMappingCount,
      afterMappingCount,
      durationMs: Math.round(performance.now() - startedAt),
      stats: trimResult.stats || null,
      validation: trimResult.validation || null,
      meta: meta || null
    };

    state.lastResult = finalResult;
    log(trimResult.changed ? "trim_applied" : "trim_passthrough", finalResult);

    return {
      ok: true,
      changed: trimResult.changed,
      responseText: outputText,
      result: finalResult
    };
  }

  function createLoadedTurnsMonitor(options) {
    const config = {
      maxLoadedTurns: 100,
      pollIntervalMs: 600000,
      minMsAfterReload: 600000,
      ...options
    };

    const monitorState = {
      version: "loaded-turns-monitor-v01",
      startedAt: new Date().toISOString(),
      lastCheckAt: null,
      lastReloadAt: 0,
      refreshPending: false,
      lastTurns: null,
      lastIdle: null,
      lastDecision: null
    };

    function countTurns() {
      return document.querySelectorAll('[data-testid^="conversation-turn-"]').length;
    }

    function inputHasText() {
      const textareas = [...document.querySelectorAll("textarea")];

      for (const t of textareas) {
        if ((t.value || "").trim().length > 0) return true;
      }

      const editables = [...document.querySelectorAll('[contenteditable="true"]')];

      for (const e of editables) {
        if ((e.innerText || "").trim().length > 0) return true;
      }

      return false;
    }

    function appearsToBeStreaming() {
      const bodyText = document.body ? document.body.innerText || "" : "";

      if (/Stop generating|Przerwij generowanie|Generating|Answering now/i.test(bodyText)) {
        return true;
      }

      const busy = document.querySelector('[aria-busy="true"]');
      if (busy) return true;

      return false;
    }

    function defaultIsIdle() {
      if (inputHasText()) return false;
      if (appearsToBeStreaming()) return false;
      return true;
    }

    function check() {
      const now = Date.now();
      const turns = countTurns();
      const idle = typeof config.isIdle === "function"
        ? !!config.isIdle()
        : defaultIsIdle();

      monitorState.lastCheckAt = new Date().toISOString();
      monitorState.lastTurns = turns;
      monitorState.lastIdle = idle;

      const overLimit = turns > config.maxLoadedTurns;
      const reloadCooldownOk = now - monitorState.lastReloadAt >= config.minMsAfterReload;

      if (!overLimit) {
        monitorState.lastDecision = "below_limit";
        const result = { action: "none", reason: "below_limit", turns, idle, maxLoadedTurns: config.maxLoadedTurns, refreshPending: monitorState.refreshPending };
        post("loaded_turns_monitor_check", "ok", result);
        return result;
      }

      if (!idle) {
        monitorState.refreshPending = true;
        monitorState.lastDecision = "pending_not_idle";
        const result = { action: "pending", reason: "not_idle", turns, idle, maxLoadedTurns: config.maxLoadedTurns, refreshPending: true };
        post("loaded_turns_monitor_pending", "pending", result);
        return result;
      }

      if (!reloadCooldownOk) {
        monitorState.refreshPending = true;
        monitorState.lastDecision = "pending_cooldown";
        const result = { action: "pending", reason: "cooldown", turns, idle, maxLoadedTurns: config.maxLoadedTurns, refreshPending: true };
        post("loaded_turns_monitor_pending", "pending", result);
        return result;
      }

      monitorState.refreshPending = false;
      monitorState.lastReloadAt = now;
      monitorState.lastDecision = "reload";

      const reloadInfo = {
        turns,
        maxLoadedTurns: config.maxLoadedTurns,
        checkedAt: monitorState.lastCheckAt
      };

      if (typeof config.onBeforeReload === "function") {
        config.onBeforeReload(reloadInfo);
      }

      post("loaded_turns_monitor_refresh", "ok", reloadInfo);

      if (typeof config.reload === "function") {
        config.reload();
      } else {
        location.reload();
      }

      return { action: "reload", reason: "over_limit_idle", turns, idle, maxLoadedTurns: config.maxLoadedTurns };
    }

    const timerId = setInterval(check, config.pollIntervalMs);
    setTimeout(check, 30000);

    return {
      version: "loaded-turns-monitor-v01",
      config: {
        maxLoadedTurns: config.maxLoadedTurns,
        pollIntervalMs: config.pollIntervalMs,
        minMsAfterReload: config.minMsAfterReload
      },
      state: monitorState,
      check,
      getStatus() {
        return {
          version: "loaded-turns-monitor-v01",
          config: this.config,
          state: { ...monitorState }
        };
      },
      stop() {
        clearInterval(timerId);
      }
    };
  }

  function startLoadedTurnsMonitor() {
    if (global.__BRIDGE_BROWSER_TURNS_MONITOR__ && typeof global.__BRIDGE_BROWSER_TURNS_MONITOR__.stop === "function") {
      try { global.__BRIDGE_BROWSER_TURNS_MONITOR__.stop(); } catch (_) {}
    }

    global.__BRIDGE_BROWSER_TURNS_MONITOR__ = createLoadedTurnsMonitor({
      maxLoadedTurns: CONFIG.maxLoadedTurns,
      pollIntervalMs: CONFIG.pollIntervalMs,
      minMsAfterReload: CONFIG.minMsAfterReload,
      onBeforeReload(info) {
        log("loaded_turns_before_reload", info);
      }
    });

    return global.__BRIDGE_BROWSER_TURNS_MONITOR__;
  }

  function getStatus() {
    const monitor = global.__BRIDGE_BROWSER_TURNS_MONITOR__ && typeof global.__BRIDGE_BROWSER_TURNS_MONITOR__.getStatus === "function"
      ? global.__BRIDGE_BROWSER_TURNS_MONITOR__.getStatus()
      : null;

    return {
      version: MODULE_VERSION,
      config: { ...CONFIG },
      counters: { ...state.counters },
      lastResult: state.lastResult,
      loadedTurns: monitor && monitor.state ? monitor.state.lastTurns : null,
      maxLoadedTurns: CONFIG.maxLoadedTurns,
      refreshPending: monitor && monitor.state ? monitor.state.refreshPending : false,
      lastRefreshAt: monitor && monitor.state && monitor.state.lastReloadAt ? new Date(monitor.state.lastReloadAt).toISOString() : null,
      monitor,
      logsTail: state.logs.slice(-50)
    };
  }

  const moduleApi = {
    id: "conversation-trimmer",
    version: MODULE_VERSION,
    config: CONFIG,
    trimConversationPayload,
    trimConversationResponseText,
    createLoadedTurnsMonitor,
    startLoadedTurnsMonitor,
    getStatus
  };

  global.__BRIDGE_BROWSER_MODULES__ = global.__BRIDGE_BROWSER_MODULES__ || {};
  global.__BRIDGE_BROWSER_MODULES__.conversationTrimmer = moduleApi;

  if (typeof global.__BRIDGE_REGISTER_MODULE__ === "function") {
    global.__BRIDGE_REGISTER_MODULE__("conversation-trimmer", moduleApi);
  }

  startLoadedTurnsMonitor();
  log("module_loaded", { version: MODULE_VERSION, config: CONFIG });
})(window);
