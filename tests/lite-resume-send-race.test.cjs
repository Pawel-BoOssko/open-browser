"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const code = fs.readFileSync(path.join(__dirname, "../src/BridgeBrowserAlpha0/LiteResumeSender.js"), "utf8");
const id = "fedcba9876543210fedcba9876543210";
const prompt = "Write the next chapter only if work remains.";
function setup(options = {}) {
  let scheduled;
  let clicks = 0;
  let busy = false;
  let userBubbles = [];
  let unrelatedAriaBusy = false;
  let voiceClicks = 0;
  const submit = {disabled: !!options.disabledSendButton, click() { clicks++; }};
  const voice = { click() {voiceClicks++;} };
  const form = {
    querySelector(selector) {
      if (selector.includes('button[type="submit"]')) return options.disabledSendButton ? submit : submit;
      return null;
    },
    contains(node) { return node === submit; }
  };
  const editor = { innerText: "", focus() {},
    closest(selector) { return options.formButton && selector === 'form' ? form : null; } };
  let activeEditor = editor;
  const doc = {
    querySelectorAll(selector) {
      if (selector.includes('data-user-message-bubble')) return userBubbles;
      if (selector === '[aria-busy="true"]' && unrelatedAriaBusy) return [{}];
      return [];
    },
    querySelector(selector) {
      if (selector.startsWith("#prompt-textarea")) return activeEditor;
      if (selector === '[data-testid="send-button"]')
        return options.missingSendButton || options.formButton ? null : submit;
      if (selector === 'form[data-chatgpt-composer]')
        return options.formButton ? form : null;
      if (selector === 'button[aria-label="Tryb głosowy"]') return voice;
      // Simulate a DIL Stop control identified by aria-label only.
      if (options.alternateStopLabel && selector.includes('stop-button'))
        return selector.includes('Stop generating') && busy ? {} : null;
      if (selector.includes("stop-button") || selector.includes("aria-busy"))
        return busy ? {} : null;
      return null;
    },
    execCommand(_command, _ui, value) {
      editor.innerText = value + (options.editorAddsNewline ? '\n' : '');
      return true;
    }
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
    count() { return clicks; }, voiceCount() { return voiceClicks; },
    setBusy(value) { busy = value; },
    showAcknowledgment(text) { userBubbles = [{ innerText: text }]; },
    showUnrelatedBusy(value) { unrelatedAriaBusy = value; },
    replaceComposer() { activeEditor = { innerText: "", focus() {} }; }
  };
}
{
  const p = setup();
  assert.equal(p.sender.send(prompt, id), "submission_requested");
  p.run();
  assert.equal(p.count(), 1, "normal matching payload submits once");
  assert.equal(p.sender.phase(id), 'click_invoked');
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
{
  const p = setup();
  p.showUnrelatedBusy(true);
  assert.equal(p.sender.diagnose().reason, 'ready');
  assert.equal(p.sender.diagnose().globalAriaBusyCount, 1);
  assert.equal(p.sender.send(prompt, id), 'submission_requested',
    'unrelated aria-busy sidebar must not block sending');
  p.run();
  assert.equal(p.count(), 1);
}
{
  const p = setup();
  p.editor.innerText = 'private draft';
  assert.equal(p.sender.send(prompt, id), 'busy_draft_present');
  assert.equal(p.sender.diagnose().reason, 'draft_present');
  assert.equal(p.count(), 0);
}
{
  const p = setup({ missingSendButton: true });
  assert.equal(p.sender.send(prompt, id), 'submission_requested');
  p.run();
  assert.equal(p.sender.phase(id), 'draft_button_missing');
  assert.equal(p.count(), 0, 'injected draft does not imply click');
  assert.match(p.editor.innerText, /\[OB-/);
}
{
  const p = setup({ editorAddsNewline: true });
  p.sender.send(prompt, id);
  p.run();
  assert.equal(p.count(), 1, 'a trailing DOM newline should not suppress click');
}
{
  const p = setup({ disabledSendButton: true });
  p.sender.send(prompt, id);
  p.run();
  assert.equal(p.sender.phase(id), 'draft_button_disabled');
  assert.equal(p.count(), 0);
}
{
  const p = setup({ formButton: true, missingSendButton: true });
  assert.equal(p.sender.diagnose().sendButtonFound, true,
    'exact form submit button must be detected before any draft modification');
  assert.equal(p.sender.send(prompt, id), 'submission_requested');
  p.run();
  assert.equal(p.count(), 1, 'ChatGPT DIL form submit without data-testid works');
  assert.equal(p.voiceCount(), 0, 'never click adjacent purple voice button');
  assert.equal(p.sender.phase(id), 'click_invoked');
}
{
  const p = setup({ formButton: true, disabledSendButton: true });
  p.sender.send(prompt, id);
  p.run();
  assert.equal(p.sender.phase(id), 'draft_button_disabled');
  assert.equal(p.count(), 0);
}
{
  const p = setup({ alternateStopLabel: true });
  assert.equal(p.sender.send(prompt, id), 'submission_requested');
  p.setBusy(true);
  p.run();
  assert.equal(p.count(), 0, 'aria-label-only Stop blocks delayed send');
  assert.equal(p.sender.phase(id), 'blocked_generation');
}
{
  const p = setup({ alternateStopLabel: true });
  p.setBusy(true);
  assert.equal(p.sender.send(prompt, id), 'busy_generating',
    'aria-label-only Stop blocks initial send');
  assert.equal(p.count(), 0);
}
{
  const p = setup({ formButton: true, missingSendButton: true });
  assert.equal(p.sender.sendDirect(prompt, id), 'submission_requested');
  p.run();
  assert.equal(p.count(), 1, 'direct legacy-style insertion clicks DIL form submit');
  assert.equal(p.sender.phase(id), 'click_invoked');
}
{
  const p = setup();
  p.setBusy(true);
  assert.equal(p.sender.sendDirect(prompt, id), 'submission_requested',
    'direct sender bypasses the former generation/ACK preflight');
  p.run();
  assert.equal(p.count(), 1, 'direct sender still attempts click');
}
{
  const p = setup({ formButton: true, missingSendButton: true });
  assert.equal(p.sender.sendDirect(prompt, id), 'submission_requested');
  assert.equal(p.sender.submitDirect(id), 'click_invoked',
    'host can synchronously click Send when background page timers are throttled');
  assert.equal(p.count(), 1);
  p.run();
  assert.equal(p.count(), 1, 'delayed page timer does not double-click after host flush');
}
{
  const p = setup({ missingSendButton: true });
  p.sender.sendDirect(prompt, id);
  assert.equal(p.sender.submitDirect(id), 'draft_button_missing',
    'missing Send button is reported instead of pretending to send');
  assert.equal(p.count(), 0);
}
console.log("LiteResumeSenderRace: 20 PASS");
