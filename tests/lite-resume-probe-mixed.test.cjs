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
      querySelector(selector) { return selector === 'textarea,[contenteditable="true"]' ? {} : null; }
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
}
{
  const s = observe([node(1, "assistant", false)], [node(2, "user", true)]);
  assert.equal(s.role, "user");
  assert.equal(s.text, "");
}
{
  const shared = node(1, "assistant", false);
  const s = observe([shared], [shared]);
  assert.equal(s.turnCount, 1);
}
console.log("LiteResumeProbeMixed: 3 PASS");
