"use strict";
const assert=require("node:assert/strict");
const fs=require("node:fs");
const vm=require("node:vm");
const path=require("node:path");
const script=fs.readFileSync(path.join(__dirname,"../src/BridgeBrowserAlpha0/LiteDomSelectorAudit.js"),"utf8");
function inspect(roleCount,oldCount,host="chatgpt.com"){
  const records=Array.from({length:roleCount},(_,i)=>({
    getAttribute(name){return name==="data-message-author-role"?
       (i%2?"assistant":"user"):null;}
  }));
  const doc={
    querySelectorAll(s) {
      if(s.includes('conversation-turn-'))return Array.from({length:oldCount},()=>({}));
      if(s.includes('data-message-author-role'))return records;
      return [];
    }
  };
  const win={document:doc,location:{hostname:host}};
  vm.runInNewContext(script,{window:win});
  return win.__OPENBROWSER_DOM_SELECTOR_AUDIT__?.inspect();
}
{const o=inspect(30,0);assert.equal(o.mismatch,true);
 assert.equal(o.safeToHardPrune,false);assert.equal(o.assistant,15);
 assert.equal(o.user,15);}
{const o=inspect(50,50);assert.equal(o.mismatch,false);
 assert.equal(o.safeToHardPrune,false);}
{const o=inspect(0,0);assert.equal(o.mismatch,false);
 assert.equal(o.safeToHardPrune,false);}
assert.equal(inspect(10,0,"example.com"),undefined);
console.log("LiteDomSelectorAudit: 4 PASS (metadata only, no DOM mutation)");
