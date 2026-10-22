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
      if (options.polishStopLabel && selector.includes('aria-label="Zatrzymaj"'))
        return busy ? {} : null;
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
    document: doc, location: { hostname: "chatgpt.com", pathname: "/c/11111111-2222-3333-4444-555555555555" },
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
  const p = setup({ polishStopLabel: true });
  p.setBusy(true);
  assert.equal(p.sender.sendDirect(prompt, id), 'busy_generating',
    'Polish Zatrzymaj indicates generation, no draft injection');
  assert.equal(p.count(), 0);
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
  assert.equal(p.sender.sendDirect(prompt, id), 'busy_generating',
    'direct sender must not interrupt active generation');
  assert.equal(p.count(), 0, 'no click while generating');
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
{
  const p = setup();
  p.editor.innerText = "private draft";
  assert.equal(p.sender.sendDirect(prompt, id), "busy_draft_present");
  assert.equal(p.count(), 0, "direct sender preserves user draft");
}
{
  const p = setup();
  assert.equal(p.sender.sendDirect(prompt, id), "submission_requested");
  p.editor.innerText = "user changed draft";
  assert.equal(p.sender.submitDirect(id), "blocked_editor_changed");
  assert.equal(p.count(), 0, "no click after editor was changed");
}
{
  const p = setup();
  assert.equal(p.sender.sendDirect(prompt, id), "submission_requested");
  p.setBusy(true);
  assert.equal(p.sender.submitDirect(id), "blocked_generation");
  assert.equal(p.count(), 0, "no click when generation started after injection");
}
{
  const p = setup();
  assert.equal(p.sender.sendDirect(prompt, id), "submission_requested");
  assert.equal(p.sender.submitDirect(id), "click_invoked");
  assert.equal(p.sender.sendDirect(prompt, id), "already_attempted");
  assert.equal(p.sender.submitDirect(id), "click_invoked");
  assert.equal(p.count(), 1, "same receipt may click at most once");
}
{
  const p = setup();
  assert.equal(p.sender.sendDirect(prompt, id, true), "submission_requested");
  assert.equal(p.sender.submitDirect(id), "click_invoked",
    "host-managed wake can submit without a page timer");
  assert.equal(p.count(), 1);
}
{
  const p = setup();
  p.editor.innerText = "unsent owner text";
  assert.equal(p.sender.sendDirect(prompt, id, true), "busy_draft_present");
  assert.equal(p.count(), 0, "host-managed wake does not overwrite owner draft");
}
{
  const p = setup();
  p.win.location.pathname = "/c/abcdefabcdef%3A123456789012345678901234567890";
  assert.equal(p.sender.sendDirect(prompt, id, true), "submission_requested",
    "encoded colon in ChatGPT conversation ID must allow a wake");
}
{
  const p = setup();
  p.win.location.pathname = "/c/abcdefabcdef%2F123456789012345678901234567890";
  assert.equal(p.sender.sendDirect(prompt, id, true), "invalid",
    "unexpected URL escape remains rejected");
}
{
  const p = setup({ formButton: true, missingSendButton: true });
  p.editor.innerText = "[OB-" + id + "]\n" + prompt;
  assert.equal(p.sender.submitOwnDraft(), "click_invoked",
    "only a persisted own wake draft may be retried");
  assert.equal(p.count(), 1);
  assert.equal(p.sender.recoveredDraftVisible(), false,
    "click alone cannot confirm delivery");
  p.showAcknowledgment("[OB-" + id + "]\n" + prompt);
  assert.equal(p.sender.recoveredDraftVisible(), true,
    "receipt in user bubble confirms recovered wake delivery");
}
{
  const p = setup();
  p.editor.innerText = "Private text of the user";
  assert.equal(p.sender.submitOwnDraft(), "not_own_draft",
    "never send an ordinary unsent owner draft");
  assert.equal(p.count(), 0);
}
{
  const p = setup();
  p.editor.innerText = "[OB-" + id + "]\n" + prompt;
  p.setBusy(true);
  assert.equal(p.sender.submitOwnDraft(), "busy_generating",
    "never submit a persisted draft during generation");
  assert.equal(p.count(), 0);
}
{
  // A long-lived SPA route may wake hundreds of times without a full reload.
  // Successful sends must drop message bodies, not grow an in-memory outbox.
  const p = setup();
  for (let i = 0; i < 192; i++) {
    const receipt = i.toString(16).padStart(32, "0");
    assert.equal(p.sender.sendDirect(prompt, receipt, true), "submission_requested");
    assert.equal(p.sender.submitDirect(receipt), "click_invoked");
    assert.equal(p.sender.diagnose().pendingReceipts, 0,
      "clicked payload must be released immediately");
    p.editor.innerText = ""; // simulated successful composer clearance by React
  }
  assert.equal(p.count(), 192);
  assert.ok(p.sender.diagnose().rememberedReceipts <= 128,
    "attempted receipt metadata is bounded");
  assert.ok(p.sender.diagnose().rememberedPhases <= 128,
    "sender phase history is bounded");
}
{
  // A permanently unclickable pending draft must never retain an
  // unbounded number of old payloads across React SPA retries.
  const p = setup({ missingSendButton: true });
  const receipts = [];
  for (let i = 0; i < 12; i++) {
    const receipt = (i + 200).toString(16).padStart(32, "0");
    receipts.push(receipt);
    assert.equal(p.sender.sendDirect(prompt, receipt, true), "submission_requested");
    p.editor.innerText = ""; // test page replaces the old composer contents
  }
  assert.ok(p.sender.diagnose().pendingReceipts <= 8,
    "pending editor payloads have a hard memory bound");
  assert.equal(p.sender.phase(receipts[0]), "blocked_pending_evicted",
    "evicted oldest pending receipt cannot silently submit");
  assert.equal(p.sender.submitDirect(receipts[0]), "blocked_pending_evicted");
}
console.log("LiteResumeSenderRace: 33 PASS");
