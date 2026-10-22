"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.join(__dirname,
  "../src/BridgeBrowserAlpha0/LiteTabSwitchSafety.js"), "utf8");

function probe({ stop = false, hiddenStop = false, sidebarStop = false,
  sidebarDraft = "", draft = "", hostname = "chatgpt.com",
  globalAriaBusy = false } = {}) {
  function node(extra = {}) {
    return {
      closest(selector) {
        return extra.sidebar && selector.includes("aside") ? {} : null;
      },
      getAttribute() { return null; },
      getClientRects() { return extra.hidden ? [] : [{}]; },
      hidden: !!extra.hidden,
      disabled: false,
      value: extra.value || "",
      innerText: extra.value || ""
    };
  }
  const window = {
    location: { hostname },
    document: {
      querySelectorAll(selector) {
        if (selector.includes("stop-button")) {
          return [
            ...(stop ? [node()] : []),
            ...(hiddenStop ? [node({ hidden: true })] : []),
            ...(sidebarStop ? [node({ sidebar: true })] : [])
          ];
        }
        if (selector.includes("#prompt-textarea")) {
          return [
            ...(draft ? [node({ value: draft })] : []),
            ...(sidebarDraft ? [node({ value: sidebarDraft, sidebar: true })] : [])
          ];
        }
        if (globalAriaBusy && selector.includes("aria-busy"))
          return [node()];
        throw Error("Unexpected selector " + selector);
      }
    }
  };
  vm.runInNewContext(script, { window });
  const p = window.__OPENBROWSER_TAB_SWITCH_SAFETY__;
  return p?.read();
}
assert.equal(probe().busy, false);
assert.equal(probe({ globalAriaBusy: true }).busy, false,
  "app shell or sidebar aria-busy is not active model generation");
assert.equal(probe({ stop: true }).busy, true,
  "visible Stop generating blocks switching");
assert.equal(probe({ hiddenStop: true }).busy, false);
assert.equal(probe({ sidebarStop: true }).busy, false);
assert.equal(probe({ draft: "incomplete message" }).draft, true);
assert.equal(probe({ draft: "  " }).draft, false);
assert.equal(probe({ sidebarDraft: "navigation note" }).draft, false);
assert.equal(probe({ hostname: "malicious.example" }), undefined);
console.log("LiteTabSwitchSafety: 9 PASS");
