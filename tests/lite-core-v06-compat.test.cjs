"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const file = path.join(__dirname, "../releases/BridgeBrowserAlpha0/modules/",
  "conversation-trimmer/versions/conversation-trimmer-core-v06-port-01/",
  "conversation-trimmer.js");
const script = fs.readFileSync(file, "utf8");
const win = {};
const ctx = {
  window: win, Date, JSON, Set,
  document: { querySelectorAll() { return []; }, body: { innerText: "" } },
  setInterval() { return 1; }, setTimeout() { return 1; },
  clearInterval() {}, performance: { now() { return 0; } }
};
vm.runInNewContext(script, ctx);
const api = win.__BRIDGE_BROWSER_MODULES__.conversationTrimmer;
assert.equal(api.config.keepRenderableMessages, 40);
assert.equal(api.config.maxLoadedTurns, 100);
assert.equal(api.config.pollIntervalMs, 600000);

function conversation(n) {
  const mapping = {};
  for (let i = 0; i < n; i++) {
    const id = "n" + i;
    mapping[id] = {
      id, parent: i ? "n" + (i - 1) : null,
      children: i + 1 < n ? ["n" + (i + 1)] : [],
      message: {
        author: { role: i % 2 ? "assistant" : "user" },
        content: { content_type: "text", parts: ["synthetic_" + i] }
      }
    };
  }
  return { mapping, current_node: "n" + (n - 1), title: "synthetic" };
}
const small = conversation(20);
const same = api.trimConversationPayload(small);
assert.equal(same.changed, false);
assert.equal(Object.keys(same.payload.mapping).length, 20);

const large = conversation(90);
const before = JSON.stringify(large);
const trimmed = api.trimConversationPayload(large);
assert.equal(trimmed.changed, true);
assert.ok(Object.keys(trimmed.payload.mapping).length < 90);
assert.equal(trimmed.payload.current_node, "n89");
assert.equal(JSON.stringify(large), before, "original payload must be untouched");
for (const [id, node] of Object.entries(trimmed.payload.mapping)) {
  if (node.parent) assert.ok(trimmed.payload.mapping[node.parent], "parent of " + id);
  for (const child of node.children)
    assert.ok(trimmed.payload.mapping[child], "child of " + id);
}
const active = [];
let cursor = trimmed.payload.current_node;
while (cursor) {
  assert.ok(!active.includes(cursor), "no cycle");
  active.push(cursor);
  cursor = trimmed.payload.mapping[cursor].parent;
}
assert.ok(active.length >= 40, "recent messages retained");
assert.ok(active.length < 90, "older messages dropped");
const malformed = api.trimConversationResponseText("{not-json");
assert.equal(malformed.changed, false);
assert.equal(malformed.ok, false);
console.log("Core-v06 synthetic mapping: PASS. Not enabled in Lite.");
