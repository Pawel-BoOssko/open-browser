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
    intervalArms: 0, initialArms: 0, intervalClears: 0, initialClears: 0,
    setInterval(fn) { this.intervalArms++; this.interval = fn; return this.intervalArms; },
    clearInterval() { this.intervalClears++; this.interval = undefined; },
    setTimeout(fn) { this.initialArms++; this.initial = fn; return this.initialArms; },
    clearTimeout() { this.initialClears++; this.initial = undefined; },
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
  assert.equal(t.win.intervalArms, 0,
    "hard DOM default OFF never installs a periodic JS timer");
  assert.equal(t.win.initialArms, 0,
    "hard DOM default OFF never schedules its first DOM traversal");
  assert.equal(t.list.length, 100);
}
{
  const t = setup(89);
  assert.equal(t.win.intervalArms, 1,
    "explicit opt-in schedules exactly one recurring prune timer");
  assert.equal(t.win.initialArms, 1,
    "explicit opt-in schedules exactly one initial delayed prune");
  t.api.enable(true);
  assert.equal(t.win.intervalArms, 1, "repeated enable is idempotent");
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
  const staleInitial = t.win.initial;
  const staleInterval = t.win.interval;
  t.api.enable(false);
  assert.equal(t.win.intervalClears, 1,
    "opt-out clears recurring timer, not just a boolean");
  assert.equal(t.win.initialClears, 1,
    "opt-out cancels delayed initial prune");
  // Even a stale callback that has already reached the event queue must
  // never delete DOM after the owner disables pruning.
  staleInitial();
  staleInterval();
  assert.equal(t.list.length, 100);
  assert.equal(t.api.prune().skipped, "disabled");
  t.api.enable(true);
  assert.equal(t.win.intervalArms, 2,
    "re-enable starts one new timer only");
  assert.equal(t.win.initialArms, 2);
  t.api.enable(false);
  assert.equal(t.win.intervalClears, 2);
  assert.equal(t.win.initialClears, 2);
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
console.log("LiteDomHardPrune: 10 PASS; timer lifecycle and OFF-idle guards");
