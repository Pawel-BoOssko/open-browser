"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const code = fs.readFileSync(path.join(__dirname, "../src/BridgeBrowserAlpha0/LiteResumeSender.js"), "utf8");
const id = "fedcba9876543210fedcba9876543210";
const prompt = "Write the next chapter only if work remains.";
function setup() {
  let scheduled;
  let clicks = 0;
  let busy = false;
  let userBubbles = [];
  const editor = { innerText: "", focus() {} };
  let activeEditor = editor;
  const doc = {
    querySelectorAll(selector) {
      return selector.includes('data-user-message-bubble') ? userBubbles : [];
    },
    querySelector(selector) {
      if (selector.startsWith("#prompt-textarea")) return activeEditor;
      if (selector === '[data-testid="send-button"]')
        return { disabled: false, click() { clicks++; } };
      if (selector.includes("stop-button") || selector.includes("aria-busy"))
        return busy ? {} : null;
      return null;
    },
    execCommand(_command, _ui, value) { editor.innerText = value; return true; }
  };
  const win = {
    document: doc, location: { hostname: "chatgpt.com", pathname: "/c/test" },
    setTimeout(callback) { scheduled = callback; },
    HTMLTextAreaElement: function() {}
  };
  vm.runInNewContext(code, { window: win });
  const sender = win.__OPENBROWSER_RESUME_SENDER__;
  return {
    win, editor, sender, run() { assert.equal(typeof scheduled, "function"); scheduled(); },
    count() { return clicks; }, setBusy(value) { busy = value; },
    showAcknowledgment(text) { userBubbles = [{ innerText: text }]; },
    replaceComposer() { activeEditor = { innerText: "", focus() {} }; }
  };
}
{
  const p = setup();
  assert.equal(p.sender.send(prompt, id), "submission_requested");
  p.run();
  assert.equal(p.count(), 1, "normal matching payload submits once");
}
{
  const p = setup();
  p.sender.send(prompt, id);
  p.editor.innerText = "Private user draft";
  p.run();
  assert.equal(p.count(), 0, "must not submit user draft entered after scheduling");
}
{
  const p = setup();
  p.sender.send(prompt, id);
  p.win.location.pathname = "/c/other";
  p.run();
  assert.equal(p.count(), 0, "must not submit after navigation");
}
{
  const p = setup();
  p.sender.send(prompt, id);
  p.setBusy(true);
  p.run();
  assert.equal(p.count(), 0, "must not interrupt generation");
}
{
  const p = setup();
  p.sender.send(prompt, id);
  p.replaceComposer();
  p.run();
  assert.equal(p.count(), 0, "must not click another composer after React replacement");
}
{
  const p = setup();
  p.showAcknowledgment("[OB-" + id + "]\n" + prompt);
  assert.equal(p.sender.hasAck(id), true,
    "DIL user bubble must acknowledge already submitted receipt");
  assert.equal(p.sender.send(prompt, id), "already_visible",
    "do not submit a new message when DIL DOM already contains receipt");
  assert.equal(p.count(), 0);
}
{
  const p = setup();
  p.showAcknowledgment("Different message without the receipt");
  assert.equal(p.sender.hasAck(id), false);
}
console.log("LiteResumeSenderRace: 7 PASS");
