"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const scripts = ["LiteResumeProbe.js", "LiteResumeSender.js"].map(name =>
  fs.readFileSync(path.join(__dirname, "../src/BridgeBrowserAlpha0", name), "utf8"));
function page(overrides = {}) {
  const editor = { innerText: overrides.draft || "", focus() {} };
  const sent = [];
  let pending = "";
  const doc = {
    querySelectorAll(selector) {
      if (selector === 'button') return [{ innerText: overrides.effort || 'High' }];
      if (selector === '[data-user-message-bubble],[data-chatgpt-selection-message-id]')
        return [];
      if (selector === '[data-message-author-role]') {
        return overrides.lastUser ? [{
          getAttribute() { return "user"; }, innerText: "User's message"
        }] : [{
          getAttribute() { return "assistant"; },
          innerText: "Napisano rozdział pierwszy, pozostał rozdział drugi."
        }];
      }
      if (selector === '[data-message-author-role="user"],[data-user-message-bubble]' ||
          selector === '[data-message-author-role="user"]')
        return sent.map(value => ({ innerText: value }));
      throw new Error("Unknown selector " + selector);
    },
    querySelector(selector) {
      if (selector === "#prompt-textarea,[data-testid=\"composer-text-input\"],textarea" ||
          selector === 'textarea,[contenteditable="true"]') return editor;
      if (selector === '[data-testid="send-button"]')
        return { disabled: false, click() { sent.push(pending); } };
      if (selector.includes('stop-button') || selector.includes('aria-busy'))
        return overrides.busy ? {} : null;
      if (selector === '[role="alert"],[data-testid*="error"]') return null;
      return null;
    },
    execCommand(command, _, value) {
      assert.equal(command, "insertText");
      pending = value; editor.innerText = value; return true;
    }
  };
  const w = {
    document: doc,
    location: { hostname: overrides.host || "chatgpt.com",
      pathname: overrides.path || "/c/test" },
    setTimeout(callback) { callback(); },
    HTMLTextAreaElement: function() {}
  };
  for (const script of scripts) vm.runInNewContext(script, { window: w });
  return { w, sent, editor };
}
{
  const p = page();
  assert.equal(p.w.__OPENBROWSER_RESUME_PROBE__.observe().role, "assistant");
  assert.equal(p.w.__OPENBROWSER_RESUME_PROBE__.observe().generating, false);
}
{
  const p = page({ busy: true });
  assert.equal(p.w.__OPENBROWSER_RESUME_PROBE__.observe().generating, true);
}
{
  const p = page({ lastUser: true });
  assert.equal(p.w.__OPENBROWSER_RESUME_PROBE__.observe().role, "user");
}
{
  const p = page({ host: "example.com" });
  assert.equal(p.w.__OPENBROWSER_RESUME_PROBE__, undefined);
}
{
  const p = page();
  const id = "0123456789abcdef0123456789abcdef";
  assert.equal(p.w.__OPENBROWSER_RESUME_SENDER__.send(
    "Kontynuuj drugi rozdział książki.", id), "submission_requested");
  assert.equal(p.sent.length, 1);
  assert.equal(p.w.__OPENBROWSER_RESUME_SENDER__.hasAck(id), true);
  assert.equal(p.w.__OPENBROWSER_RESUME_SENDER__.send(
    "Kontynuuj drugi rozdział książki.", id), "already_visible");
  assert.equal(p.sent.length, 1, "no duplicate");
}
{
  const p = page({ draft: "Unsent private message" });
  assert.equal(p.w.__OPENBROWSER_RESUME_SENDER__.send(
    "Kontynuuj drugi rozdział książki.",
    "0123456789abcdef0123456789abcdef"), "busy");
}
{
  const p = page({ path: "/settings" });
  assert.equal(p.w.__OPENBROWSER_RESUME_SENDER__.send(
    "Kontynuuj drugi rozdział książki.",
    "0123456789abcdef0123456789abcdef"), "invalid");
}
console.log("LiteResumeAgent: 7 PASS");
