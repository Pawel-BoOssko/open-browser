"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteResumeProbe.js"), "utf8");

function observe({ activeBusy = false, toolRunning = false,
  sidebarToolRunning = false, activeStop = false, sidebarStop = false } = {}) {
  const turn = {
    querySelector(selector) {
      return selector === '[aria-busy="true"]' && activeBusy ? {} : null;
    }
  };
  const assistant = {
    innerText: "Completed assistant reply",
    getAttribute(key) {
      return key === "data-message-author-role" ? "assistant" : null;
    },
    hasAttribute() { return false; },
    closest(selector) {
      return selector === '[data-testid^="conversation-turn-"]' ? turn : null;
    }
  };
  const main = {
    querySelector(selector) {
      if (selector.includes('[data-testid*="tool-running"]'))
        return toolRunning ? {} : null;
      return null;
    },
    querySelectorAll(selector) {
      assert.equal(selector,
        '[data-message-author-role],[data-user-message-bubble],[data-chatgpt-selection-message-id]');
      return [assistant];
    }
  };
  const doc = {
    querySelectorAll(selector) {
      if (selector.startsWith('[data-testid="stop-button"]'))
        return [...(activeStop ? [{ closest() { return null; } }] : []),
          ...(sidebarStop ? [{ closest() { return {}; } }] : [])];
      if (selector === "[data-message-author-role]") return [assistant];
      if (selector === '[data-user-message-bubble],[data-chatgpt-selection-message-id]') return [];
      if (selector === "button") return [{ innerText: "High" }];
      return [];
    },
    querySelector(selector) {
      if (selector === 'main,[role="main"]') return main;
      if (selector === "#prompt-textarea") return { innerText: "" };
      if (selector.includes('[aria-busy="true"]'))
        throw new Error("Global aria-busy must not be treated as chat tool activity");
      if (selector.includes('[data-testid*="tool-running"]'))
        return sidebarToolRunning ? {} : null;
      return null;
    }
  };
  const win = {
    document: doc,
    location: { hostname: "chatgpt.com", pathname: "/c/test" }
  };
  vm.runInNewContext(script, { window: win });
  return win.__OPENBROWSER_RESUME_PROBE__.observe();
}

assert.equal(observe().toolBusy, false);
assert.equal(observe({ activeBusy: true }).toolBusy, true);
assert.equal(observe({ toolRunning: true }).toolBusy, true);
assert.equal(observe({ sidebarToolRunning: true }).toolBusy, false,
  "sidebar running tool must not block active chat");
assert.equal(observe({ sidebarStop: true }).generating, false,
  "unrelated sidebar Stop does not block active assistant preflight");
assert.equal(observe({ activeStop: true }).generating, true,
  "active conversation Stop must block assistant preflight");
assert.equal(observe({ activeStop: true, sidebarStop: true }).generating, true,
  "unrelated sidebar cannot hide a real active Stop");
console.log("LiteResumeProbeSidebarBusy: 7 PASS");
