"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteDomOptimizer.js"), "utf8");

function createTest(turnCount, options = {}) {
  const nodes = Array.from({ length: turnCount }, (_, index) => {
    const attrs = new Map();
    return {
      id: index, attrs, contains(element) { return element === this; },
      getAttribute(name) { return attrs.get(name) || null; },
      setAttribute(name, value) { attrs.set(name, value); },
      hasAttribute(name) { return attrs.has(name); },
      removeAttribute(name) { attrs.delete(name); }
    };
  });
  const styles = [];
  const document = {
    readyState: "complete", activeElement: options.focus ?? null,
    querySelectorAll(selector) {
      if (selector === '[data-user-message-bubble],[data-chatgpt-selection-message-id]') return this.modernNodes || [];
      assert.equal(selector, '[data-testid^="conversation-turn-"]');
      return nodes;
    },
    querySelector(selector) {
      return options.busy && selector === '[aria-busy="true"]' ? {} : null;
    },
    getElementById(id) { return styles.find(style => style.id === id) || null; },
    createElement(tag) { assert.equal(tag, "style"); return {}; },
    head: { appendChild(node) { styles.push(node); } }
  };
  let currentTime = 0;
  const window = {
    document, location: { hostname: options.host || "chatgpt.com" },
    CSS: { supports: () => options.supported !== false },
    performance: { now: () => currentTime++ },
    setInterval(fn) { this.interval = fn; return 1; }
  };
  vm.runInNewContext(script, { window, Date });
  return { nodes, document, window, styles, api: window.__OPENBROWSER_LITE_DOM__ };
}

{
  const t = createTest(59);
  assert.equal(t.api.getStatus().optimized, 0);
  assert.equal(t.nodes.length, 59);
}
{
  const t = createTest(65);
  const flag = "data-ob-lite-offscreen";
  assert.equal(t.api.getStatus().optimized, 25);
  assert.equal(t.nodes.filter(n => n.hasAttribute(flag)).length, 25);
  assert.equal(t.styles.length, 1);
  t.window.interval();
  assert.equal(t.styles.length, 1, "stylesheet must not be duplicated");
  t.api.setEnabled(false);
  assert.equal(t.nodes.filter(n => n.hasAttribute(flag)).length, 0);
  t.api.setEnabled(true);
  assert.equal(t.nodes.filter(n => n.hasAttribute(flag)).length, 25);
  t.document.activeElement = t.nodes[2];
  t.api.check();
  assert.equal(t.api.getStatus().optimized, 24, "focused old turn is protected");
  assert.equal(t.nodes[2].hasAttribute(flag), false);
  assert.equal(t.nodes.length, 65, "optimizer never removes any turn");
}
{
  const t = createTest(100, { busy: true });
  assert.equal(t.api.getStatus().optimized, 0);
  assert.equal(t.api.getStatus().skippedBusy, true);
}
{
  const t = createTest(100, { supported: false });
  assert.equal(t.api.getStatus().optimized, 0);
}
{
  const t = createTest(100, { host: "example.com" });
  assert.equal(t.api, undefined, "other domains cannot be optimized");
}
{
  const t = createTest(65);
  const flag = "data-ob-lite-offscreen";
  assert.equal(t.api.getStatus().optimized, 25);
  t.document.modernNodes = t.nodes.slice(-5);
  t.api.check();
  assert.equal(t.api.getStatus().domVariant, "dil_observe_only");
  assert.equal(t.api.getStatus().optimized, 0);
  assert.equal(t.nodes.filter(n => n.hasAttribute(flag)).length, 0,
    "legacy optimization flags must be cleared after DIL migration");
  assert.equal(t.nodes.length, 65, "no nodes may be removed");
}
console.log("LiteDomOptimizer: 6 PASS");
