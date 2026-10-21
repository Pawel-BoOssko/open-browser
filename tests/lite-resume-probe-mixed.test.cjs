"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteResumeProbe.js"), "utf8");
function node(index, role, modern) {
  return {
    index, innerText: role === "assistant" ? "Completed assistant response" : "User request",
    compareDocumentPosition(other) { return index < other.index ? 4 : index > other.index ? 2 : 0; },
    getAttribute(key) { return !modern && key === "data-message-author-role" ? role : null; },
    hasAttribute(key) { return modern && key === (role === "assistant" ?
      "data-chatgpt-selection-message-id" : "data-user-message-bubble"); }
  };
}
function observe(legacy, modern) {
  const w = {
    location: { hostname: "chatgpt.com", pathname: "/c/00000000-1111-2222-3333-444444444444" },
    document: {
      querySelectorAll(selector) {
        if (selector === "[data-message-author-role]") return legacy;
        if (selector === "[data-user-message-bubble],[data-chatgpt-selection-message-id]") return modern;
        if (selector === "button") return [{ innerText: "High" }];
        return [];
      },
      querySelector(selector) { return selector === '#prompt-textarea' ? {} : null; }
    }
  };
  vm.runInNewContext(source, { window: w });
  return w.__OPENBROWSER_RESUME_PROBE__.observe();
}
{
  const s = observe([node(1, "user", false)], [node(2, "assistant", true)]);
  assert.equal(s.role, "assistant");
  assert.equal(s.text, "Completed assistant response");
  assert.equal(s.turnCount, 2);
  assert.equal(s.assistantNodeCount, 1);
  assert.equal(s.userNodeCount, 1);
  assert.equal(s.lastAssistantChars, "Completed assistant response".length);
}
{
  const s = observe([node(1, "assistant", false)], [node(2, "user", true)]);
  assert.equal(s.role, "user");
  assert.equal(s.text, "");
  assert.equal(s.assistantNodeCount, 1);
  assert.equal(s.userNodeCount, 1);
  assert.equal(s.lastAssistantChars, "Completed assistant response".length);
}
{
  const shared = node(1, "assistant", false);
  const s = observe([shared], [shared]);
  assert.equal(s.turnCount, 1);
  assert.equal(s.assistantNodeCount, 1);
  assert.equal(s.userNodeCount, 0);
}
{
  const s = observe([], []);
  assert.equal(s.turnCount, 0);
  assert.equal(s.assistantNodeCount, 0);
  assert.equal(s.userNodeCount, 0);
  assert.equal(s.lastAssistantChars, 0);
}
{
  const a = node(1, "assistant", true);
  a.innerText = "";
  a.textContent = "Recovered visible assistant content";
  const s = observe([], [a]);
  assert.equal(s.role, "assistant");
  assert.equal(s.text, "Recovered visible assistant content");
  assert.equal(s.lastAssistantChars, a.textContent.length);
}
{
  const a = node(1, "assistant", true);
  a.innerText = "";
  a.textContent = "";
  a.closest = () => ({ innerText: "Completed text from enclosing turn" });
  const s = observe([], [a]);
  assert.equal(s.text, "Completed text from enclosing turn");
}
console.log("LiteResumeProbeMixed: 6 PASS");
