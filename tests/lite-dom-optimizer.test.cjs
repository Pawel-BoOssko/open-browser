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
  if (options.busy && nodes.length) nodes[nodes.length - 1].setAttribute("aria-busy", "true");
  if (options.recentBusy && nodes.length > 1) nodes[nodes.length - 2].setAttribute("aria-busy", "true");
  if (options.staleBusy && nodes.length) nodes[0].setAttribute("aria-busy", "true");
  const styles = [];
  const main = {
    getAttribute(name) {
      return name === "aria-busy" && options.mainBusy ? "true" : null;
    },
    querySelectorAll(selector) {
      if (selector === '[data-user-message-bubble],[data-chatgpt-selection-message-id]')
        return document.modernNodes || [];
      assert.equal(selector, '[data-testid^="conversation-turn-"]');
      return nodes;
    }
  };
  let globalScans = 0;
  const document = {
    readyState: "complete", activeElement: options.focus ?? null,
    querySelectorAll(selector) {
      if (selector.startsWith('[data-testid="stop-button"],')) {
        return (options.stopButtons || []).map(kind => ({
          hidden: kind === 'hidden',
          disabled: kind === 'disabled',
          closest() { return kind === 'sidebar' ? {} : null; },
          getAttribute(name) {
            return name === 'aria-hidden' && kind === 'aria-hidden' ? 'true' : null;
          },
          getClientRects() { return kind === 'invisible' ? [] : [{}]; }
        }));
      }
      globalScans++;
      throw new Error("whole-document message scan forbidden: " + selector);
    },
    querySelector(selector) {
      if (selector === 'main,[role="main"]')
        return options.noMain ? null : main;
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
  return { nodes, document, window, styles,
    get globalScans() { return globalScans; },
    api: window.__OPENBROWSER_LITE_DOM__ };
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
  const t = createTest(100, { staleBusy: true });
  assert.equal(t.api.getStatus().skippedBusy, false,
    "stale aria-busy in an old completed turn must not block the active chat");
  assert.equal(t.api.getStatus().optimized, 60,
    "old busy marker must not disable otherwise safe legacy CSS optimization");
}
{
  const t = createTest(100, { mainBusy: true });
  assert.equal(t.api.getStatus().skippedBusy, true,
    "main itself busy still blocks mutations even without Stop button");
  assert.equal(t.api.getStatus().optimized, 0);
}
{
  const t = createTest(100, { recentBusy: true });
  assert.equal(t.api.getStatus().skippedBusy, true,
    "busy penultimate turn conservatively blocks while latest placeholder hydrates");
  assert.equal(t.api.getStatus().optimized, 0);
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
{
  const t = createTest(75, { noMain: true });
  assert.equal(t.api.getStatus().domVariant, "unhydrated",
    "missing active main must not look like an optimizable legacy conversation");
  assert.equal(t.api.getStatus().turnCount, 0);
  assert.equal(t.api.getStatus().optimized, 0);
  assert.equal(t.globalScans, 0, "unhydrated shell must not scan sidebar messages");
}
{
  const t = createTest(70);
  assert.equal(t.api.getStatus().turnCount, 70);
  assert.equal(t.api.getStatus().optimized, 30);
  assert.equal(t.globalScans, 0,
    "messages are scanned through main, never document");
}
{
  const t = createTest(70, { stopButtons: ['sidebar'] });
  assert.equal(t.api.getStatus().skippedBusy, false,
    "sidebar Stop must not block optimization of the active chat");
  assert.equal(t.api.getStatus().optimized, 30);
  assert.equal(t.globalScans, 0, "stop detector does not scan document messages");
}
{
  const t = createTest(70, { stopButtons: ['sidebar', 'active'] });
  assert.equal(t.api.getStatus().skippedBusy, true,
    "active Stop must still block even if sidebar Stop is first");
  assert.equal(t.api.getStatus().optimized, 0);
}
{
  const t = createTest(70, { stopButtons: ['portal'] });
  assert.equal(t.api.getStatus().skippedBusy, true,
    "visible Stop in top-level portal also blocks legacy CSS changes");
}
{
  const t = createTest(70, { stopButtons: ['hidden', 'disabled', 'aria-hidden', 'invisible'] });
  assert.equal(t.api.getStatus().skippedBusy, false,
    "hidden, disabled and invisible stale buttons are not active responses");
  assert.equal(t.api.getStatus().optimized, 30);
}
console.log("LiteDomOptimizer: 17 PASS (stale aria-busy isolated, recent busy protected)");
