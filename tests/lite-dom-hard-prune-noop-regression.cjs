const fs = require("fs");
const vm = require("vm");
const assert = require("assert");
const path = require("path");
const source = fs.readFileSync(path.resolve(__dirname, "../src/BridgeBrowserAlpha0/LiteDomHardPrune.js"), "utf8");
function makeHarness(persisted) {
  let ticks = 200000;
  const NativeDate = Date;
  class TestDate extends NativeDate { static now() { return ticks; } }
  const turns = Array.from({length:11}, (_, i) => {
    const id = "conversation-turn-" + i;
    return {
      tagName:"DIV", parentNode:{}, removed:false,
      getAttribute: name => name === "data-testid" ? id : null,
      querySelector: selector => selector.indexOf('author-role="assistant"') >= 0 ? {} : null,
      contains: () => false,
      remove() { this.removed = true; }
    };
  });
  const main = {
    getAttribute: () => null,
    querySelectorAll(selector) {
      if (selector === '[data-testid^="conversation-turn-"]')
        return turns.filter(x => !x.removed);
      return [];
    }
  };
  const doc = {querySelector:()=>main,querySelectorAll:()=>[],activeElement:null};
  const win = {document:doc,location:{hostname:"chatgpt.com"},getSelection:()=>"",
    setInterval:()=>1,setTimeout:()=>2,clearInterval:()=>{},clearTimeout:()=>{}};
  vm.runInNewContext(source, {window:win,Date:TestDate}, {timeout:1000});
  const api=win.__OPENBROWSER_HARD_PRUNE__;
  if(persisted) api.ackSaved(["conversation-turn-0"]);
  return {api,turns,step:()=>{ticks += 30000}};
}
const waiting = makeHarness(false);
waiting.api.prune();
waiting.step();waiting.api.prune();
waiting.step();waiting.api.prune();
assert.equal(waiting.api.status().enabled, true,
  "archive-not-ready must not impersonate React reinsertion");
assert.equal(waiting.api.status().removedTotal,0);
const pruned=makeHarness(true);
assert.equal(pruned.api.prune().lastRemoved,1,"persisted oldest answer may be pruned");
console.log("PASS: awaiting_archive does not disable; persisted oldest can prune");