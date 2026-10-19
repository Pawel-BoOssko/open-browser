"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const source = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteDomSelectorAudit.js"), "utf8");

function inspect({ assistant = 0, user = 0, legacy = 0, overlap = false } = {}) {
  let writes = 0;
  const make = () => ({ getAttribute() { return null; },
    setAttribute() { writes++; }, removeAttribute() { writes++; } });
  const assistantNodes = Array.from({ length: assistant }, make);
  const userNodes = Array.from({ length: user }, make);
  if (overlap && assistantNodes.length && userNodes.length)
    userNodes[0] = assistantNodes[0];
  const old = Array.from({ length: legacy }, (_, i) => ({
    getAttribute(name) { return name === "data-message-author-role" ?
      (i % 2 ? "assistant" : "user") : null; }
  }));
  const document = {
    querySelectorAll(selector) {
      if (selector === '[data-chatgpt-selection-message-id]') return assistantNodes;
      if (selector === '[data-user-message-bubble]') return userNodes;
      if (selector.includes('data-message-author-role')) return old;
      if (selector.includes('conversation-turn-')) return [];
      return [];
    }
  };
  const window = { document, location: { hostname: "chatgpt.com" } };
  vm.runInNewContext(source, { window });
  return { result: window.__OPENBROWSER_DOM_SELECTOR_AUDIT__.inspect(), writes };
}
{
  const { result, writes } = inspect({ assistant: 45, user: 45 });
  assert.equal(result.domVariant, "dil");
  assert.equal(result.assistant, 45);
  assert.equal(result.user, 45);
  assert.equal(result.counts.dilMessages, 90);
  assert.equal(result.mismatch, true);
  assert.equal(result.safeToHardPrune, false);
  assert.equal(writes, 0);
}
{
  const { result } = inspect({ assistant: 45, user: 45, legacy: 3 });
  assert.equal(result.domVariant, "mixed");
  assert.equal(result.counts.dilMessages, 90);
  assert.equal(result.assistant + result.user, 90);
}
{
  const { result } = inspect({ assistant: 2, user: 2, overlap: true });
  assert.equal(result.counts.dilMessages, 3);
  assert.equal(result.assistant + result.user, 3);
}
{
  const { result } = inspect({ legacy: 4 });
  assert.equal(result.domVariant, "legacy");
  assert.equal(result.assistant, 2);
  assert.equal(result.user, 2);
}
{
  const { result } = inspect();
  assert.equal(result.domVariant, "unknown");
  assert.equal(result.counts.dilMessages, 0);
  assert.equal(result.mismatch, false);
}
console.log("LiteDomSelectorDIL: 5 PASS (read only)");
