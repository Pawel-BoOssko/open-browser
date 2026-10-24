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
  const make = (text, n, modern) => ({
    innerText: text,
    getAttribute(name) {
      return modern && name === "data-chatgpt-selection-message-id"
        ? (opts.ids?.[n] ?? "dil-" + n) : null;
    },
    closest() { return {
      getAttribute() { return "conversation-turn-" + n; }
    }; }
  });
  const legacy = (opts.texts || []).map((t, n) => make(t, n, false));
  const modern = (opts.modernTexts || []).map((t, n) => make(t, n, true));
  let reads = 0;
  const window = {
    location: { hostname: opts.host || "chatgpt.com",
      pathname: opts.pathname || "/c/one" },
    document: {
      querySelectorAll(sel) {
        reads++;
        if (sel.includes('[data-testid="stop-button"]')) return opts.generating ? [{}] : [];
        if (sel === '[data-chatgpt-selection-message-id]') return modern;
        if (sel === '[data-message-author-role="assistant"]') return legacy;
        throw new Error("unknown selector " + sel);
      },
      querySelector(sel) {
        if (sel === 'main,[role="main"]') return this;
        if (sel.includes('aria-label="Zatrzymaj"')) return opts.generating ? {} : null;
        return null;
      }
    }
  };
  vm.runInNewContext(script, { window });
  return { api: window.__OPENBROWSER_LITE_SNAPSHOT__,
    get reads() { return reads; } };
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
  assert.equal(a.reads, 0, "non-conversation pages are not inspected");
}
{
  const a = init({ host: "example.com" });
  assert.equal(a.api, undefined);
}
{
  const a = init({ modernTexts: ["First modern answer",
    "Second modern answer", "Third modern answer"] });
  const results = a.api.collect();
  assert.equal(results.length, 2);
  assert.equal(results[0].turnId, "dil-2");
  assert.equal(results[0].text, "Third modern answer");
  assert.equal(results[1].turnId, "dil-1");
}
{
  const a = init({ texts: ["Legacy answer"],
    modernTexts: ["Modern answer"] });
  assert.equal(a.api.collect()[0].text, "Modern answer",
    "modern DIL wins in mixed layouts");
}
{
  const a = init({ modernTexts: ["Answer A", "Answer B", "Answer C"],
    ids: ["one", "same", "same"] });
  const results = a.api.collect();
  assert.equal(results.length, 2);
  assert.equal(results[0].text, "Answer C");
  assert.equal(results[1].text, "Answer A",
    "duplicate DIL IDs cannot overwrite saved answers");
}
{
  const a = init({ modernTexts: ["Streaming partial answer"],
    generating: true });
  const parts = a.api.collect();
  assert.equal(parts.length, 1,
    "archive newest partial answer while Stop is visible");
  assert.equal(parts[0].partial, true);
  assert.ok(a.reads > 0, "one bounded snapshot during generation");
}
{
  const a = init({ modernTexts: [
    "Pierwsza odpowiedź", "Druga odpowiedź",
    "Trzecia odpowiedź", "Czwarta odpowiedź"
  ] });
  const all = a.api.collectForArchive();
  assert.equal(all.length, 4, "archive historical and latest answers");
  assert.equal(all[0].turnId, "dil-0", "archive oldest first");
  assert.equal(all[3].turnId, "dil-2", "latest reply remains captured");
}
{
  const a = init({ modernTexts: ["Streaming partial answer"],
    generating: true });
  const all = a.api.collectForArchive();
  assert.equal(all.length, 1);
  assert.equal(all[0].partial, true,
    "never mark an active partial answer as completed");
}
console.log("LiteConversationSnapshot: 11 PASS");
