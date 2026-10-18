"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(
  path.join(__dirname, "../src/BridgeBrowserAlpha0/LiteConversationSnapshot.js"),
  "utf8"
);
function init(opts = {}) {
  const nodes = (opts.texts || []).map((text, n) => ({
    innerText: text, closest() { return {
      getAttribute() { return "conversation-turn-" + n; }
    }; }
  }));
  let reads = 0;
  const window = {
    location: { hostname: opts.host || "chatgpt.com", pathname: opts.pathname || "/c/one" },
    document: {
      querySelectorAll(sel) {
        assert.equal(sel, '[data-message-author-role="assistant"]');
        reads++;
        return nodes;
      }
    }
  };
  vm.runInNewContext(script, { window });
  return { api: window.__OPENBROWSER_LITE_SNAPSHOT__, reads };
}
{
  const a = init({ texts: ["First answer", "Second answer", "Third answer"] });
  const results = a.api.collect();
  assert.equal(results.length, 2);
  assert.equal(results[0].text, "Third answer");
  assert.equal(results[1].turnId, "conversation-turn-1");
}
{
  const a = init({ texts: ["", "ab", "Valid visible answer"] });
  assert.equal(a.api.collect().length, 1);
}
{
  const a = init({ texts: ["x".repeat(200001), "Visible answer"] });
  assert.equal(a.api.collect().length, 1);
}
{
  const a = init({ pathname: "/", texts: ["Visible answer"] });
  assert.equal(a.api.collect().length, 0);
}
{
  const a = init({ host: "example.com" });
  assert.equal(a.api, undefined);
}
console.log("LiteConversationSnapshot: 5 PASS");
