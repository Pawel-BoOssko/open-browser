"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.join(__dirname, "../src/BridgeBrowserAlpha0/LiteResponseSignal.js"), "utf8");
function run(text = "", busy = false, host = "chatgpt.com", speaker = "alert") {
  const node = { textContent: text };
  const window = {
    performance: { now: () => 100 },
    location: { hostname: host },
    document: {
      readyState: "complete",
      querySelectorAll(selector) {
        if (selector.startsWith('[role="alert"]'))
          return speaker === "alert" && text ? [node] : [];
        if (selector.startsWith('[data-testid^="conversation-turn-"]')) return [node];
        if (selector.startsWith('[data-message-author-role'))
          return speaker === "user" ? [] : [node];
        throw new Error("Unknown selector: " + selector);
      },
      querySelector(selector) {
        if (selector.startsWith('[data-testid="stop-button"]')) return busy ? {} : null;
        if (selector.startsWith('textarea,')) return {};
        return null;
      }
    }
  };
  vm.runInNewContext(script, { window });
  return window.__OPENBROWSER_RESPONSE_SIGNALS__?.read();
}
assert.equal(run("Nie udało się wczytać tej odpowiedzi").visibleError, "response_load_failed");
assert.equal(run("Failed to load this response").visibleError, "response_load_failed");
assert.equal(run("Przekroczono limit czasu dostarczenia wiadomości").visibleError, "delivery_timeout");
assert.equal(run("Przerwano rozumowanie").visibleError, "reasoning_interrupted");
assert.equal(run("hello", true).generatingSignal, true);
assert.equal(run("hello").visibleError, null);
assert.equal(run("hello", false, "untrusted.com"), undefined);
assert.equal(run("Nie udało się wczytać tej odpowiedzi", false,
  "chatgpt.com", "dil").visibleError, "response_load_failed",
  "short DIL assistant failure should block the supervisor");
assert.equal(run("Przekroczono limit czasu dostarczenia wiadomości", false,
  "chatgpt.com", "user").visibleError, null,
  "user quoting an error must never trigger a false UI failure");
assert.equal(run("Przekroczono limit czasu dostarczenia wiadomości ".repeat(8),
  false, "chatgpt.com", "assistant").visibleError, null,
  "long assistant text discussing an error is not an error placeholder");
console.log("LiteResponseSignal: 10 PASS");
