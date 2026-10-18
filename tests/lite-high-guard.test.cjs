"use strict";
const fs=require("node:fs"),vm=require("node:vm"),path=require("node:path"),assert=require("node:assert/strict");
const source=fs.readFileSync(path.join(__dirname,"../src/BridgeBrowserAlpha0/LiteHighGuard.js"),"utf8");
function create(mode="instant",option=true,host="chatgpt.com"){
 let button={hidden:false,disabled:false,getAttribute(name){return name==="aria-hidden"?null:null},innerText:mode,getClientRects(){return [1]},click(){opened=true}};
 let opened=false, chosen=0;
 let high={hidden:false,disabled:false,getAttribute(){return null},innerText:"High",getClientRects(){return [1]},click(){chosen++;button.innerText="High"}};
 const doc={querySelectorAll(selector){
   if(selector.includes('role="menuitem"'))return opened&&option?[high]:[];
   return [button];
 }};
 const win={document:doc,location:{hostname:host},setTimeout(fn){fn()}};
 vm.runInNewContext(source,{window:win});
 return {api:win.__OPENBROWSER_HIGH_GUARD__,get chosen(){return chosen}};
}
{const p=create("high");assert.equal(p.api.audit().compliant,true);}
{const p=create("extra high");assert.equal(p.api.audit().compliant,true);}
{const p=create("instant");assert.equal(p.api.audit().mode,"instant");assert.equal(p.api.switchHigh().state,"high_selection_requested");assert.equal(p.chosen,1);assert.equal(p.api.audit().mode,"high");}
{const p=create("medium");p.api.switchHigh();assert.equal(p.api.audit().compliant,true);}
{const p=create("instant",false);p.api.switchHigh();assert.equal(p.api.audit().compliant,false);}
{const p=create("strange");assert.equal(p.api.switchHigh().state,"cannot_identify_current_mode");}
{const p=create("instant",true,"example.org");assert.equal(p.api,undefined);}
console.log("LiteHighGuard: 7 PASS");