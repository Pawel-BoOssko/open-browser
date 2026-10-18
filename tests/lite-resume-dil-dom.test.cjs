"use strict";
const fs = require("node:fs"), path = require("node:path");
const vm = require("node:vm"), assert = require("node:assert/strict");
const source = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteResumeProbe.js"), "utf8");

function sample(variant, lastRole = "assistant") {
  const assistants = [
    { type: "assistant", text: "Rozdział pierwszy, dalsze prace do wykonania." },
    { type: "assistant", text: "Rozdział drugi, wątki do rozwinięcia." }
  ];
  const users = [
    { type: "user", text: "Napisz dalszy rozdział." },
    { type: "user", text: "Kontynuuj przygodę." }
  ];
  const messages = lastRole === "user"
    ? [users[0], assistants[0], assistants[1], users[1]]
    : [users[0], assistants[0], users[1], assistants[1]];
  const nodes = messages.map(msg => ({
    innerText: msg.text, textContent: msg.text,
    value: null,
    hasAttribute(attr) {
      return variant === "dil" &&
        (attr === "data-chatgpt-selection-message-id" && msg.type === "assistant" ||
         attr === "data-user-message-bubble" && msg.type === "user");
    },
    getAttribute(attr) {
      if (variant === "legacy" && attr === "data-message-author-role") return msg.type;
      return null;
    }
  }));
  const doc = {
    querySelectorAll(sel) {
      if (sel === "[data-message-author-role]")
        return variant === "legacy" ? nodes : [];
      if (sel.includes("[data-user-message-bubble]"))
        return variant === "dil" ? nodes : [];
      if (sel === "button") return [];
      return [];
    },
    querySelector(sel) {
      if (sel.includes("contenteditable")) return { value: "", innerText: "" };
      return null;
    }
  };
  const win = { document: doc, location: { hostname: "chatgpt.com", pathname: "/c/test" } };
  vm.runInNewContext(source, { window: win });
  return win.__OPENBROWSER_RESUME_PROBE__.observe();
}
{
  const state = sample("dil", "assistant");
  assert.equal(state.role, "assistant");
  assert.match(state.text, /Rozdział drugi/);
  assert.equal(state.turnCount, 4);
}
{
  const state = sample("dil", "user");
  assert.equal(state.role, "user");
  assert.equal(state.text, "");
}
{
  const state = sample("legacy", "assistant");
  assert.equal(state.role, "assistant");
  assert.match(state.text, /Rozdział drugi/);
}
{
  const state = sample("legacy", "user");
  assert.equal(state.role, "user");
  assert.equal(state.text, "");
}
console.log("LiteResumeDilDom: 4 PASS");
