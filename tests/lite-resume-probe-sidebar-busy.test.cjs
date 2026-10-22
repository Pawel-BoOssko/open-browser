"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteResumeProbe.js"), "utf8");

function observe({ activeBusy = false, toolRunning = false } = {}) {
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
    querySelectorAll(selector) {
      assert.equal(selector,
        '[data-message-author-role],[data-user-message-bubble],[data-chatgpt-selection-message-id]');
      return [assistant];
    }
  };
  const doc = {
    querySelectorAll(selector) {
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
        return toolRunning ? {} : null;
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
console.log("LiteResumeProbeSidebarBusy: 3 PASS");
