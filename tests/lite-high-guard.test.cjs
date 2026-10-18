"use strict";
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const assert = require("node:assert/strict");
const source = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteHighGuard.js"), "utf8");
function create(mode = "instant", option = true, host = "chatgpt.com") {
  let opened = false, chosen = 0, stopped = 0, busy = false, draft = '', delayed = null;
  const button = {
    hidden: false, disabled: false,
    getAttribute() { return null; }, innerText: mode,
    getClientRects() { return [1]; }, click() { opened = true; }
  };
  const high = {
    hidden: false, disabled: false, innerText: "High",
    getAttribute() { return null; },
    getClientRects() { return [1]; },
    click() { chosen++; button.innerText = "High"; }
  };
  const stop = { disabled: false, click() { stopped++; } };
  const doc = {
    querySelector(selector) {
      if (selector.includes('contenteditable')) return { innerText: draft };
      return selector.includes("stop-button") && busy ? stop : null;
    },
    querySelectorAll(selector) {
      if (selector.includes('role="menuitem"')) return opened && option ? [high] : [];
      return [button];
    }
  };
  const win = {
    document: doc, location: { hostname: host, pathname: "/c/original" },
    setTimeout(fn) { delayed = fn; }
  };
  vm.runInNewContext(source, { window: win });
  return {
    api: win.__OPENBROWSER_HIGH_GUARD__, win,
    flush() { if (delayed) { const fn = delayed; delayed = null; fn(); } },
    setBusy(v) { busy = v; }, setDraft(v) { draft = v; },
    get chosen() { return chosen; },
    get stopped() { return stopped; }
  };
}
{ const p = create("high"); assert.equal(p.api.audit().compliant, true); }
{ const p = create("Wysoki"); assert.equal(p.api.audit().compliant, true); }
{ const p = create("extra high"); assert.equal(p.api.audit().compliant, true); }
{
  const p = create("instant");
  assert.equal(p.api.audit().mode, "instant");
  assert.equal(p.api.switchHigh().state, "high_selection_requested");
  assert.equal(p.chosen, 0, "async selector not yet used");
  p.flush();
  assert.equal(p.chosen, 1);
  assert.equal(p.api.audit().mode, "high");
}
{ const p = create("medium"); p.api.switchHigh(); p.flush(); assert.equal(p.api.audit().compliant, true); }
{ const p = create("instant", false); p.api.switchHigh(); p.flush(); assert.equal(p.api.audit().compliant, false); }
{ const p = create("strange"); assert.equal(p.api.switchHigh().state, "cannot_identify_current_mode"); }
{ const p = create("instant", true, "example.org"); assert.equal(p.api, undefined); }
{
  const p = create("instant");
  p.setBusy(true);
  assert.equal(p.api.switchHigh().state, "generation_busy");
  assert.equal(p.stopped, 0, "must never interrupt generation");
  assert.equal(p.chosen, 0, "must not select while busy");
}
{
  const p = create("instant");
  p.api.switchHigh(); p.win.location.pathname = "/c/other"; p.flush();
  assert.equal(p.chosen, 0, "delayed menu must not switch a different chat");
}
{
  const p = create("instant");
  p.api.switchHigh(); p.setBusy(true); p.flush();
  assert.equal(p.chosen, 0, "delayed menu must not switch while active");
}
{
  const p = create("instant");
  p.api.switchHigh(); p.win.location.hostname = "example.com"; p.flush();
  assert.equal(p.chosen, 0, "menu callback stays on ChatGPT origin");
}
{
  const p = create("instant"); p.setDraft("Unsent user note");
  assert.equal(p.api.switchHigh().state, "user_draft_present");
  assert.equal(p.chosen, 0);
}
{
  const p = create("instant"); p.api.switchHigh(); p.setDraft("Unsent user note");
  p.flush(); assert.equal(p.chosen, 0, "draft blocks delayed High click");
}
console.log("LiteHighGuard: 14 PASS");
