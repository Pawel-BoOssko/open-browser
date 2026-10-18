"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteDomHardPrune.js"), "utf8");
function setup(count, settings = {}) {
  const list = [];
  for (let i = 0; i < count; i++) {
    const node = {
      id: i, parentNode: {}, contains(item) { return item === this; },
      remove() { list.splice(list.indexOf(this), 1); this.parentNode = null; }
    };
    list.push(node);
  }
  const doc = {
    readyState: "complete", activeElement: settings.focus,
    querySelectorAll(selector) {
      assert.equal(selector, '[data-testid^="conversation-turn-"]');
      return list.slice();
    },
    querySelector(selector) {
      if (selector.startsWith('textarea')) return settings.draft ? { value: "draft" } : null;
      if (selector.startsWith('[aria-busy')) return settings.busy ? {} : null;
      return null;
    }
  };
  const win = {
    document: doc, location: { hostname: settings.host || "chatgpt.com" },
    setInterval(fn) { this.interval = fn; return 1; },
    setTimeout(fn) { this.initial = fn; return 1; },
    getSelection() { return settings.selection || ""; }
  };
  vm.runInNewContext(script, { window: win, Date });
  if (settings.optIn !== false && win.__OPENBROWSER_HARD_PRUNE__)
    win.__OPENBROWSER_HARD_PRUNE__.enable(true);
  return { list, doc, win, api: win.__OPENBROWSER_HARD_PRUNE__ };
}
{
  const t = setup(100, { optIn: false });
  assert.equal(t.api.status().enabled, false);
  t.win.initial();
  assert.equal(t.list.length, 100);
}
{
  const t = setup(89);
  t.api.prune();
  assert.equal(t.list.length, 89);
}
{
  const t = setup(100);
  t.api.prune();
  assert.equal(t.list.length, 24);
  assert.equal(t.api.status().removedTotal, 76);
  t.win.interval();
  assert.equal(t.list.length, 24);
}
{
  const t = setup(100, { busy: true });
  assert.equal(t.api.prune().skipped, "busy_or_draft");
  assert.equal(t.list.length, 100);
}
{
  const t = setup(100, { draft: true });
  t.api.prune();
  assert.equal(t.list.length, 100);
}
{
  const t = setup(100, { selection: "fragment" });
  t.api.prune();
  assert.equal(t.list.length, 100);
}
{
  const t = setup(100);
  t.api.enable(false);
  t.api.prune();
  assert.equal(t.list.length, 100);
}
{
  const t = setup(100, { host: "example.com" });
  assert.equal(t.api, undefined);
}
{
  const t = setup(100);
  const focused = t.list[0];
  t.doc.activeElement = focused;
  t.api.prune();
  assert.equal(t.list.length, 25);
  assert.equal(t.list.includes(focused), true);
}
{
  const t = setup(10000);
  const first = t.api.prune();
  assert.equal(t.list.length, 24, "10k synthetic turns keep only newest 24");
  assert.equal(first.lastRemoved, 9976);
  assert.equal(t.list[0].id, 9976, "newest turns retained in correct order");
  assert.equal(t.api.prune().lastRemoved, 0, "repeated cleanup is idempotent");
}
console.log("LiteDomHardPrune: 10 PASS");
