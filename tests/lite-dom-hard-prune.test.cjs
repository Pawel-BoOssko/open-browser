"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const code = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteDomHardPrune.js"), "utf8");

function element(tag, attrs = {}) {
  return {
    tagName: tag, attrs, children: [], parentNode: null, parentElement: null,
    appendChild(child) { child.parentNode = this; child.parentElement = this;
      this.children.push(child); return child; },
    getAttribute(name) { return this.attrs[name] ?? null; },
    contains(node) {
      return node === this || this.children.some(child => child.contains(node));
    },
    querySelector(sel) {
      if (sel.includes('data-message-author-role="assistant"'))
        return this.children.find(c =>
          c.attrs["data-message-author-role"] === "assistant") || null;
      if (sel.includes('data-message-author-role="user"'))
        return this.children.find(c =>
          c.attrs["data-message-author-role"] === "user") || null;
      return null;
    },
    remove() {
      if (this.parentNode) {
        this.parentNode.children = this.parentNode.children.filter(c => c !== this);
        this.parentElement = null; this.parentNode = null;
      }
    }
  };
}
function setup(count, opts = {}) {
  const main = element("MAIN");
  const doc = {
    activeElement: null,
    querySelector(sel) { return sel === 'main,[role="main"]' && !opts.noMain ? main : null; },
    querySelectorAll(sel) {
      if (sel.includes('stop-button')) return opts.busy ?
        [{hidden: false, disabled: false, getAttribute: () => null,
          getClientRects: () => [1], closest: () => null}] : [];
      if (sel.includes('prompt-textarea')) return opts.draft ?
        [{value:"something"}] : [];
      throw new Error("unexpected document selector: " + sel);
    }
  };
  main.querySelectorAll = (sel) => {
    if (sel.includes('conversation-turn-')) {
      return main.children.filter(n => n.attrs["data-testid"]);
    }
    if (sel.includes('data-user-message-bubble') ||
        sel.includes('data-chatgpt-selection-message-id')) {
      return opts.legacy ? [] : main.children.flatMap(n => n.children);
    }
    throw new Error("unexpected main selector " + sel);
  };
  function append(i) {
    const wrapper = element("DIV");
    if (opts.legacy) wrapper.attrs["data-testid"] = "conversation-turn-" + i;
    const marker = element("DIV", opts.legacy ?
      {"data-message-author-role": i % 2 === 0 ? "user" : "assistant"} :
      i % 2 === 0 ?
      {"data-user-message-bubble": ""} :
      {"data-chatgpt-selection-message-id": "assistant-" + i});
    wrapper.appendChild(marker); main.appendChild(wrapper);
    return wrapper;
  }
  for(let i=0;i<count;i++) append(i);
  const win = {
    document: doc, location:{hostname: opts.host || "chatgpt.com"},
    intervalArms:0,initialArms:0,intervalClears:0,initialClears:0,
    setInterval(fn) {this.intervalArms++;this.tick=fn;return 12;},
    clearInterval() {this.intervalClears++;this.tick=null;},
    setTimeout(fn) {this.initialArms++;this.initial=fn;return 13;},
    clearTimeout() {this.initialClears++;this.initial=null;},
    getSelection() {return opts.selection || ""}
  };
  vm.runInNewContext(code, {window:win,Date});
  if (opts.archived !== false && win.__OPENBROWSER_HARD_PRUNE__)
    win.__OPENBROWSER_HARD_PRUNE__.ackSaved(
      Array.from({length:count}, (_,i) => opts.legacy ?
        "conversation-turn-" + i : "assistant-" + i));
  return {win,doc,main,append,api:win.__OPENBROWSER_HARD_PRUNE__};
}
{
  const x = setup(100);
  assert.equal(x.api.status().enabled,true);
  assert.equal(x.api.status().keepNewest,10);
  assert.equal(x.win.intervalArms,1);
  assert.equal(x.win.initialArms,1);
  assert.equal(x.api.prune().lastRemoved,90);
  assert.equal(x.main.children.length,10);
  assert.equal(x.api.prune().lastRemoved,0);
}
{
  const x = setup(11);
  assert.equal(x.api.prune().lastRemoved,1);
  assert.equal(x.main.children.length,10);
}
{
  const x = setup(10);
  assert.equal(x.api.prune().skipped,"below_threshold");
  assert.equal(x.main.children.length,10);
}
{
  const x = setup(19,{busy:true});
  assert.equal(x.api.prune().skipped,"busy_or_draft");
  assert.equal(x.main.children.length,19);
}
{
  const x = setup(19,{draft:true});
  assert.equal(x.api.prune().skipped,"busy_or_draft");
  assert.equal(x.main.children.length,19);
}
{
  const x = setup(19,{selection:"selected"});
  assert.equal(x.api.prune().skipped,"busy_or_draft");
  assert.equal(x.main.children.length,19);
}
{
  const x = setup(23);
  const focused = x.main.children[0];
  x.doc.activeElement = focused;
  x.api.prune();
  assert.ok(x.main.children.includes(focused));
  assert.equal(x.main.children.length,11);
}
{
  const x = setup(30);
  x.api.enable(false);
  assert.equal(x.api.prune().skipped,"disabled");
  assert.equal(x.main.children.length,30);
  assert.equal(x.win.intervalClears,1);
  assert.equal(x.win.initialClears,1);
}
{
  const x = setup(100,{legacy:true});
  assert.equal(x.api.prune().lastRemoved,90);
  assert.equal(x.main.children.length,10);
}
{
  const x = setup(100,{host:"example.com"});
  assert.equal(x.api,undefined);
}
{
  const x = setup(16);
  x.api.prune();
  // Simulate React putting the same old messages back without new answer.
  for(let n=0;n<4;n++) x.append(100+n);
  // New tail IDs change, so this must NOT trigger the loop guard.
  assert.ok(x.api.prune().enabled);
}
{
  const x = setup(15,{archived:false});
  const result = x.api.prune();
  assert.equal(result.skipped,"awaiting_archive",
    "cannot remove an unarchived assistant answer");
  assert.ok(x.main.children.length > 10);
  x.api.ackSaved(["assistant-1","assistant-3"]);
  const after = x.api.prune();
  assert.equal(after.lastRemoved, 4);
  assert.equal(x.main.children.length,10);
}
console.log("LiteDomHardPrune: DIL and legacy 12 test scenarios PASS");
