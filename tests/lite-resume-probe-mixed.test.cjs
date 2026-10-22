"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteResumeProbe.js"), "utf8");
function node(index, role, modern) {
  return {
    index, innerText: role === "assistant" ? "Completed assistant response" : "User request",
    compareDocumentPosition(other) { return index < other.index ? 4 : index > other.index ? 2 : 0; },
    getAttribute(key) { return !modern && key === "data-message-author-role" ? role : null; },
    hasAttribute(key) { return modern && key === (role === "assistant" ?
      "data-chatgpt-selection-message-id" : "data-user-message-bubble"); }
  };
}
function observe(legacy, modern, options = {}) {
  const active = Array.from(new Set([...legacy, ...modern]))
    .sort((a, b) => a.index - b.index);
  const main = {
    querySelectorAll(selector) {
      assert.equal(selector,
        '[data-message-author-role],[data-user-message-bubble],[data-chatgpt-selection-message-id]');
      return active;
    }
  };
  const w = {
    location: { hostname: "chatgpt.com", pathname: "/c/00000000-1111-2222-3333-444444444444" },
    __OPENBROWSER_RESPONSE_SIGNALS__: { read() { return { visibleError: options.responseError ? 'response_load_failed' : null }; } },
    document: {
      querySelectorAll(selector) {
        if (selector.startsWith('[data-testid="stop-button"]'))
          return options.polishStop ? [{ closest() { return null; } }] : [];
        if (selector === "[data-message-author-role]") return [...legacy, ...(options.sidebar || [])];
        if (selector === "[data-user-message-bubble],[data-chatgpt-selection-message-id]") return modern;
        if (selector === "button") return [{ innerText: "High" }];
        return [];
      },
      querySelector(selector) {
        if (selector === 'main,[role="main"]') return options.noMain ? null : main;
        if (selector.includes('button[aria-label="Zatrzymaj"]')) return options.polishStop ? {} : null;
        return selector === '#prompt-textarea' ? {} : null;
      }
    }
  };
  vm.runInNewContext(source, { window: w });
  return w.__OPENBROWSER_RESUME_PROBE__.observe();
}
{
  const s = observe([node(1, "user", false)], [node(2, "assistant", true)]);
  assert.equal(s.role, "assistant");
  assert.equal(s.text, "Completed assistant response");
  assert.equal(s.turnCount, 2);
  assert.equal(s.assistantNodeCount, 1);
  assert.equal(s.userNodeCount, 1);
  assert.equal(s.lastAssistantChars, "Completed assistant response".length);
}
{
  const s = observe([node(1, "assistant", false)], [node(2, "user", true)]);
  assert.equal(s.role, "user");
  assert.equal(s.text, "");
  assert.equal(s.assistantNodeCount, 1);
  assert.equal(s.userNodeCount, 1);
  assert.equal(s.lastAssistantChars, "Completed assistant response".length);
}
{
  const shared = node(1, "assistant", false);
  const s = observe([shared], [shared]);
  assert.equal(s.turnCount, 1);
  assert.equal(s.assistantNodeCount, 1);
  assert.equal(s.userNodeCount, 0);
}
{
  const s = observe([], []);
  assert.equal(s.turnCount, 0);
  assert.equal(s.assistantNodeCount, 0);
  assert.equal(s.userNodeCount, 0);
  assert.equal(s.lastAssistantChars, 0);
}
{
  const a = node(1, "assistant", true);
  a.innerText = "";
  a.textContent = "Recovered visible assistant content";
  const s = observe([], [a]);
  assert.equal(s.role, "assistant");
  assert.equal(s.text, "Recovered visible assistant content");
  assert.equal(s.lastAssistantChars, a.textContent.length);
}
{
  const a = node(1, "assistant", true);
  a.innerText = "";
  a.textContent = "";
  a.closest = () => ({ innerText: "Completed text from enclosing turn" });
  const s = observe([], [a]);
  assert.equal(s.text, "Completed text from enclosing turn");
}
{
  const a = node(1, "assistant", true);
  a.innerText = "Completed assistant response. ".repeat(11);
  const s = observe([], [a], { responseError: true });
  assert.equal(s.error, false, "stale error must not block substantial newest assistant response");
}
{
  const a = node(1, "assistant", true);
  a.innerText = "Failed to load this response";
  const s = observe([], [a], { responseError: true });
  assert.equal(s.error, true, "short current failure must remain recognized");
}
{
  const a = node(1, "assistant", true);
  const s = observe([], [a], { polishStop: true });
  assert.equal(s.generating, true, "Polish Stop button indicates active generation");
}
{
  const s = observe([node(1, "user", false)], [],
    { sidebar: [node(999, "assistant", false)] });
  assert.equal(s.role, "user",
    "sidebar assistant preview must not look like the last active answer");
  assert.equal(s.assistantNodeCount, 0);
}
{
  const s = observe([], [], { sidebar: [node(999, "assistant", false)], noMain: true });
  assert.equal(s.role, "unknown",
    "unhydrated main must not fall back to the sidebar");
  assert.equal(s.turnCount, 0);
}
console.log("LiteResumeProbeMixed: 11 PASS");
