"use strict";
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const assert = require("node:assert/strict");
const source = path.join(__dirname, "../src/BridgeBrowserAlpha0");
const ui = fs.readFileSync(path.join(source, "LiteDomPruneUi.cs"), "utf8");
const form = fs.readFileSync(path.join(source, "ChatGptLiteForm.cs"), "utf8");
const js = fs.readFileSync(path.join(source, "LiteDomHardPrune.js"), "utf8");
function getBoolean(text, regex, name) {
  const match = text.match(regex);
  assert.ok(match, "Cannot locate " + name);
  return match[1] === "true";
}
const uiEnabled = getBoolean(ui,
  /private\s+bool\s+_hardPruneEnabled\s*=\s*(true|false)\s*;/,
  "WinForms default");
const jsEnabled = getBoolean(js,
  /const\s+config\s*=\s*\{\s*enabled:\s*(true|false)\s*,/,
  "JavaScript default");
const installed = getBoolean(ui,
  /hard_dom_script_installed"\s*,\s*new\s*\{\s*defaultEnabled\s*=\s*(true|false)/,
  "installed mode log");
test("owner policy: hard DOM pruning enabled by default", () => {
  assert.equal(uiEnabled, true);
  assert.equal(jsEnabled, true);
  assert.equal(installed, true);
});
test("owner policy: keep maximum ten historical message nodes", () => {
  assert.match(js, /threshold:\s*11,\s*keepNewest:\s*10/);
  assert.match(ui, /keepNewest\s*=\s*10,\s*threshold\s*=\s*11/);
});
test("JavaScript and C# have the same initial state", () => {
  assert.equal(uiEnabled, jsEnabled);
});
test("owner can disable hard pruning from toolbar", () => {
  assert.match(ui, /Text\s*=\s*"DOM mocne: TAK"/);
  assert.match(ui, /_hardPruneToggle\.Click/);
  assert.match(ui, /UpdateHardPruneReportTimer\(\)/);
});
test("status report is enabled at startup but stopped when owner disables", () => {
  assert.match(ui, /_pruneReportTimer\.Tick[\s\S]*?UpdateHardPruneReportTimer\(\);/);
  assert.match(ui, /if\s*\(ShouldPollHardPrune\(_hardPruneEnabled\)\)[\s\S]*?_pruneReportTimer\.Start\(\)\s*;\s*else\s*_pruneReportTimer\.Stop\(\)/);
  assert.match(form, /_pruneReportTimer\.Stop\s*\(/);
});
test("React loop guard can disable pruning and notify UI", () => {
  assert.match(js, /react_reinserted_old_turns/);
  assert.match(ui, /hard_dom_auto_disabled/);
});
