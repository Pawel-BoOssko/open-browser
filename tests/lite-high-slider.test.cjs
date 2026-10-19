"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const js = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteHighGuard.js"), "utf8");

function site(mode, { draft = false, busy = false, sidebarBusy = false } = {}) {
  let open = false, arrows = 0, focused = false;
  const picker = {
    hidden: false, disabled: false, innerText: "Wysiłek myślowy",
    getAttribute(name) { return name === "data-selected-reasoning-effort" ? mode : null; },
    getClientRects() { return [1]; },
    click() { open = true; }
  };
  const parent = {
    focus() { focused = true; },
    dispatchEvent(e) {
      if (e.key === "ArrowRight") {
        arrows++;
        mode = "high";
      }
      return true;
    }
  };
  const slider = {
    getAttribute(name) {
      return name === "aria-valuenow" ? (mode === "high" ? "2" : "1")
           : name === "aria-valuemax" ? "2" : null;
    },
    closest() { return parent; }
  };
  const editor = { value: draft ? "Unsent notes" : "" };
  const doc = {
    querySelector(s) {
      if (s.includes('button[aria-label="Wybierz model ChatGPT"]')) return picker;
      if (s.includes('contenteditable')) return editor;
      if (s.includes('stop-button'))
        return busy ? { hidden: false, getAttribute() { return null; }, getClientRects() { return [1]; } } : null;
      if (s.includes('aria-busy')) return sidebarBusy ? {} : null;
      if (s.includes('data-reasoning-slider')) return open ? slider : null;
      return null;
    },
    querySelectorAll(s) {
      if (s.includes('data-radix-collection-item')) return [];
      return [picker];
    }
  };
  let pending;
  const w = {
    document: doc, location: { hostname: "chatgpt.com", pathname: "/c/testowy" },
    KeyboardEvent: function(type, options) { return { type, ...options }; },
    setTimeout(callback) { pending = callback; }
  };
  vm.runInNewContext(js, { window: w });
  return {
    api: w.__OPENBROWSER_HIGH_GUARD__,
    runMenu() { pending?.(); },
    get arrows() { return arrows; },
    get focused() { return focused; }
  };
}
{
  const p = site("high");
  assert.equal(p.api.audit().mode, "high", "picker data attribute works while menu closed");
  assert.equal(p.api.switchHigh().state, "confirmed_high");
  assert.equal(p.arrows, 0);
}
{
  const p = site("medium");
  assert.equal(p.api.audit().mode, "medium");
  assert.equal(p.api.switchHigh().state, "high_selection_requested");
  p.runMenu();
  assert.equal(p.arrows, 1, "one keyboard step to last position");
  assert.equal(p.focused, true, "keyboard target focused");
  assert.equal(p.api.audit().mode, "high", "model effort confirmed via attribute");
}
{
  const p = site("medium", { busy: true });
  assert.equal(p.api.switchHigh().state, "generation_busy");
  assert.equal(p.arrows, 0);
}
{
  const p = site("medium", { draft: true });
  assert.equal(p.api.switchHigh().state, "user_draft_present");
  assert.equal(p.arrows, 0);
}
{
  const p = site("medium", { sidebarBusy: true });
  assert.equal(p.api.switchHigh().state, "high_selection_requested",
    "busy sidebar does not block real effort picker");
  p.runMenu();
  assert.equal(p.arrows, 1, "slider can advance despite sidebar busy indicator");
  assert.equal(p.api.audit().mode, "high");
}
console.log("LiteHighSlider: 5 PASS");
