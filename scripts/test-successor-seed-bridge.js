"use strict";
const fs = require("fs"), vm = require("vm"), assert = require("assert");
const file = "D:/projects/open-browser/src/BridgeBrowserAlpha0/LiteSuccessorSeedDirect.js";
const script = fs.readFileSync(file,"utf8");
const receipt = "f".repeat(32);
const content = "HANDOFF TEST " + receipt + " " + "A".repeat(70);
function environment(pathname="/", draft="", busy=false, onlyContenteditable=false) {
  let clicks=0;
  const el = {
    textContent:draft, focus(){}, dispatchEvent(){},
    closest(){return {querySelector(){return btn}}}
  };
  const btn = {disabled:false,getAttribute(){return null},
    click(){clicks++}};
  const doc = {
    querySelector(s) {
      if(s.includes("stop-button")) return busy ? {} : null;
      if(s.includes("prompt-textarea")) return (!onlyContenteditable || s.includes("contenteditable")) ? el : null;
      if(s.includes("send-button")) return btn;
      return null;
    },
    execCommand(cmd,show,value) {el.textContent=value;return true}
  };
  const w = { location:{origin:"https://chatgpt.com",pathname,hash:""},
    document:doc, Event:class{}, HTMLTextAreaElement:class{}};
  vm.runInNewContext(script,{window:w});
  return {api:w.__OBL_SUCCESSOR_SEED_V1__,el,btn,
    get clicks(){return clicks},w};
}let x=environment();
assert.equal(x.api.stage(receipt,content),"staged");
assert.equal(x.api.clickOnce(receipt),"click_invoked");
assert.equal(x.clicks,1);
assert.equal(x.api.clickOnce(receipt),"already_attempted");
assert.equal(x.clicks,1);
x=environment("/c/123");
assert.equal(x.api.stage(receipt,content),"blocked_route");
assert.equal(x.clicks,0);
x=environment("/", "existing user draft");
assert.equal(x.api.stage(receipt,content),"blocked_draft");
x=environment("/", "", true);
assert.equal(x.api.stage(receipt,content),"blocked_busy");
x=environment();
assert.equal(x.api.stage("0".repeat(32),content),"invalid_seed");
x=environment();
assert.equal(x.api.stage(receipt,content),"staged");
assert.equal(x.api.clickOnce("0".repeat(32)),"blocked_receipt");
assert.equal(x.clicks,0);
x=environment();
assert.equal(x.api.stage(receipt,content),"staged");
x.el.textContent="changed";
assert.equal(x.api.clickOnce(receipt),"blocked_changed");
assert.equal(x.clicks,0);
x=environment();
assert.equal(x.api.stage(receipt,content),"staged");
x.w.location.pathname="/c/other";
assert.equal(x.api.clickOnce(receipt),"blocked_changed");
assert.equal(x.clicks,0);
x=environment();
assert.equal(x.api.stage(receipt,content),"staged");
x.btn.disabled=true;
assert.equal(x.api.clickOnce(receipt),"blocked_no_send_button");
assert.equal(x.clicks,0);
console.log("SUCCESSOR_SEED_BRIDGE_JS_PASS cases=10 single_click=1 route_draft_busy_receipt_mutation_rejected");

x=environment("/", "", false, true);
assert.equal(x.api.stage(receipt,content),"staged");
console.log("CONTENTEDITABLE_ONLY_SELECTOR_PASS");
// Real Lexical/DIL contenteditable can normalize newlines to spaces in innerText.
x=environment();
Object.defineProperty(x.el,"innerText",{get(){return x.el.textContent.replace(/\s+/g," ");}});
const multiline="WYBUDZENIE URUCHOMIONE:\n"+receipt+"\nProjekt obl i kolejne zadania";
assert.equal(x.api.stage(receipt,multiline),"staged");
assert.equal(x.api.clickOnce(receipt),"click_invoked");
assert.equal(x.clicks,1);
console.log("NORMALIZED_RICH_EDITOR_FIRST_NUMBERED_WAKE_PASS");
// Only a previous OBL-owned, non-clicked staged wake draft may be cleared.
const owned="[OB-"+receipt+"] [OB-WAKE-000050]";
x=environment("/",owned);
assert.equal(x.api.clearOwnUnsentDraft(receipt,50),"cleared_own_draft");
assert.equal(x.clicks,0);
x=environment("/", "user private draft");
assert.equal(x.api.clearOwnUnsentDraft(receipt,50),"blocked_not_ours");
assert.equal(x.el.textContent,"user private draft");
x=environment("/",owned);
assert.equal(x.api.clearOwnUnsentDraft("0".repeat(32),50),"blocked_not_ours");
console.log("OWN_UNSENT_DRAFT_RECOVERY_PASS no_user_draft_erasure");
// Regression for real rotation 229 -> 230: successor uses the engine's
// current two-line OBL numbered prompt, not the superseded 2026-10-28 one.
const realSuccessor230 = "[OB-WAKE-000230]\nKontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.";
x=environment();
assert.equal(x.api.stage(receipt,realSuccessor230),"staged","wake230 should not be invalid_seed");
assert.equal(x.api.clickOnce(receipt),"click_invoked");
assert.equal(x.clicks,1);
console.log("REAL_WAKE230_SUCCESSOR_SEED_PASS");
