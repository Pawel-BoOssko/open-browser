"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteDomOptimizer.js"), "utf8");
function rig(count = 100, options = {}) {
  let changes = 0;
  const nodes = Array.from({ length: count }, (_, i) => {
    const attrs = new Map();
    return {
      tagName: options.tagName || "DIV",
      contains(element) { return this === element; },
      getBoundingClientRect() { return { height: options.height ?? 220 }; },
      hasAttribute(name) {
        if (name === "data-user-message-bubble") return i % 2 === 0;
        if (name === "data-chatgpt-selection-message-id") return i % 2 === 1;
        return attrs.has(name);
      },
      getAttribute(name) { return attrs.get(name) ?? null; },
      setAttribute(name, value) { changes++; attrs.set(name, value); },
      removeAttribute(name) { changes++; attrs.delete(name); }
    };
  });
  const styles = [];
  let domCensusCalls = 0;
  const old = options.mixed ? [{ hasAttribute() { return false; } }] : [];
  const doc = {
    readyState: "complete", activeElement: null,
    getElementsByTagName(selector) {
      assert.equal(selector, "*");
      domCensusCalls++;
      return { length: options.domElements ?? 7777 };
    },
    querySelectorAll(selector) {
      if (selector === '[data-testid^="conversation-turn-"]') return old;
      if (selector === '[data-user-message-bubble],[data-chatgpt-selection-message-id]')
        return nodes;
      throw new Error("unexpected selector " + selector);
    },
    querySelector(selector) {
      if (selector === 'main,[role="main"]') return {
        querySelectorAll: sel => doc.querySelectorAll(sel)
      };
      if (options.busy && selector.includes('aria-label="Zatrzymaj"')) return {};
      return null;
    },
    getElementById(id) { return styles.find(x => x.id === id) || null; },
    createElement(name) { assert.equal(name, "style"); return { id: "", textContent: "" }; },
    head: { appendChild(style) { styles.push(style); } }
  };
  const win = {
    document: doc,
    location: { hostname: "chatgpt.com" },
    CSS: { supports() { return true; } },
    getComputedStyle() { return { display: options.display || "block" }; },
    performance: { now() { return 1; } },
    setInterval() { return 1; }
  };
  vm.runInNewContext(source, { window: win, Date });
  return { nodes, doc, win, styles,
    api: win.__OPENBROWSER_LITE_DOM__,
    get changes() { return changes; },
    get domCensusCalls() { return domCensusCalls; } };
}
const flag = "data-ob-lite-offscreen";
{
  const t = rig(90, { domElements: 12345 });
  assert.equal(t.api.getStatus().totalDomElements, 12345);
  assert.equal(t.domCensusCalls, 1);
  t.api.check();
  t.api.check();
  assert.equal(t.domCensusCalls, 1,
    "DOM census must not rescan on each 30-second optimizer check");
}
{
  const t = rig(90, { busy: true, domElements: 22222 });
  assert.equal(t.api.getStatus().totalDomElements, null,
    "busy page must not perform whole-document enumeration");
  assert.equal(t.domCensusCalls, 0);
}
{
  const t = rig();
  assert.equal(t.api.getStatus().domVariant, "dil_observe_only");
  assert.equal(t.api.getStatus().optimized, 0);
  assert.equal(t.changes, 0, "modern DOM remains untouched by default");
  const state = t.api.setModernSoftEnabled(true);
  assert.equal(state.domVariant, "dil_soft_pilot");
  assert.equal(state.optimized, 60);
  assert.equal(t.nodes.filter(n => n.getAttribute(flag) === "1").length, 60);
  assert.equal(t.styles.length, 1);
  t.doc.activeElement = t.nodes[5];
  t.api.check();
  assert.equal(t.api.getStatus().optimized, 59, "focused message is protected");
  assert.equal(t.nodes[5].hasAttribute(flag), false);
  t.api.setModernSoftEnabled(false);
  assert.equal(t.api.getStatus().optimized, 0);
  assert.equal(t.nodes.filter(n => n.getAttribute(flag) === "1").length, 0,
    "all pilot mutations are reversible");
}
{
  const t = rig(100, { mixed: true });
  t.api.setModernSoftEnabled(true);
  assert.equal(t.api.getStatus().domVariant, "dil_observe_only");
  assert.equal(t.api.getStatus().optimized, 0, "mixed React DOM stays untouched");
}
{
  const t = rig(100, { tagName: "SPAN" });
  t.api.setModernSoftEnabled(true);
  assert.equal(t.api.getStatus().optimized, 0, "inline text cannot be soft-pruned");
}
{
  const t = rig(100, { height: 20 });
  t.api.setModernSoftEnabled(true);
  assert.equal(t.api.getStatus().optimized, 0, "tiny wrappers are ineligible");
}
{
  const t = rig(100, { busy: true });
  t.api.setModernSoftEnabled(true);
  assert.equal(t.api.getStatus().optimized, 0, "Polish Stop pauses DOM mutations");
}
console.log("LiteDomDilSoftPilot: 7 PASS (feature disabled by default)");
