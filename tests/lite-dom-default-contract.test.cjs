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
const installLogged = getBoolean(ui,
  /hard_dom_script_installed"\s*,\s*new\s*\{\s*defaultEnabled\s*=\s*(true|false)/,
  "installed mode log");

test("hard DOM pruning starts disabled in both UI and script", () => {
  assert.equal(jsEnabled, false, "JavaScript must be opt-in");
  assert.equal(uiEnabled, false, "WinForms must be opt-in");
});
test("hard DOM UI and script defaults agree", () => {
  assert.equal(uiEnabled, jsEnabled,
    "UI may re-enable destructive pruning on navigation");
});
test("hard DOM install log reflects actual initial state", () => {
  assert.equal(installLogged, uiEnabled,
    "Telemetry must not report an incorrect default");
});
test("hard DOM opt-in control is visible", () => {
  assert.match(ui, /Text\s*=\s*"DOM mocne: NIE"/);
  assert.match(ui, /_hardPruneToggle\.Click/);
});
test("hard prune report timer never starts unconditionally at startup", () => {
  assert.doesNotMatch(form, /_pruneReportTimer\.Start\s*\(/,
    "OFF feature must create zero periodic WinForms callbacks");
  assert.match(form, /_pruneReportTimer\.Stop\s*\(/,
    "form close still stops and disposes timer work");
});
test("hard prune report timer is armed ONLY by explicit UI opt-in", () => {
  assert.match(ui, /_hardPruneToggle\.Click[\s\S]*?UpdateHardPruneReportTimer\(\)/);
  assert.match(ui, /if\s*\(ShouldPollHardPrune\(_hardPruneEnabled\)\)[\s\S]*?_pruneReportTimer\.Start\(\)\s*;\s*else\s*_pruneReportTimer\.Stop\(\)/);
  assert.match(ui, /private\s+bool\s+_hardPruneEnabled\s*=\s*false\s*;/);
});
