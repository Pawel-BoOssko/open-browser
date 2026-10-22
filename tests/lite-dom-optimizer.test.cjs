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
      return options.busy && selector === 'main [aria-busy="true"]' ? {} : null;
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
    intervalArms: 0,
    setInterval(fn) { this.intervalArms++; this.interval = fn; return this.intervalArms; }
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
  assert.equal(t.window.intervalArms, 0,
    "Lite host owns the 30s DOM monitoring cycle; JS installs no duplicate timer");
  assert.equal(t.api.getStatus().turnCount, 65);
  t.api.check();
  assert.equal(t.styles.length, 1, "stylesheet must not be duplicated");
  t.api.setEnabled(false);
  assert.equal(t.nodes.filter(n => n.hasAttribute(flag)).length, 0);
  t.api.setEnabled(true);
  assert.equal(t.window.intervalArms, 0,
    "toggling optimizer does not install a second monitoring scheduler");
  assert.equal(t.nodes.filter(n => n.hasAttribute(flag)).length, 25);
  t.document.activeElement = t.nodes[2];
  t.api.check();
  assert.equal(t.api.getStatus().optimized, 24, "focused old turn is protected");
  assert.equal(t.nodes[2].hasAttribute(flag), false);
  assert.equal(t.nodes.length, 65, "optimizer never removes any turn");
}
{
  const t = createTest(65);
  assert.equal(t.api.getStatus().turnCount, 65);
  t.nodes.pop();
  assert.equal(t.api.getStatus().turnCount, 65,
    "status getter is read-only and has no extra DOM scan");
  assert.equal(t.api.check().turnCount, 64,
    "single foreground host probe updates status after DOM changes");
  assert.equal(t.window.intervalArms, 0,
    "host drives refresh; no duplicate JS polling timer");
}
{
  const host = fs.readFileSync(path.join(__dirname,
    "../src/BridgeBrowserAlpha0/ChatGptLiteForm.cs"), "utf8");
  assert.match(host, /_domMonitor\s*=\s*new\(\)\s*\{\s*Interval\s*=\s*30000\s*\}/,
    "WinForms retains its single 30-second DOM monitoring cadence");
  assert.match(host, /JSON\.stringify\(window\.__OPENBROWSER_LITE_DOM__\.check\(\)\)/,
    "host actively refreshes DOM state when reporting it");
  assert.doesNotMatch(script, /setInterval\s*\(/,
    "optimizer must not register another independent JavaScript monitor");
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
console.log("LiteDomOptimizer: 8 PASS (single host DOM monitor)");
