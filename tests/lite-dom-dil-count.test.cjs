"use strict";
const assert=require("node:assert/strict");
const fs=require("node:fs");
const vm=require("node:vm");
const path=require("node:path");
const source=fs.readFileSync(path.join(__dirname,
 "../src/BridgeBrowserAlpha0/LiteDomOptimizer.js"),"utf8");
function run(variant,n){
 let writes=0,styles=0;
 const nodes=Array.from({length:n},(_,i)=>({
  id:i, attrs:{}, contains(){return false;},
  getAttribute(key){return this.attrs[key]??null;},
  setAttribute(key,v){this.attrs[key]=v;writes++;},
  hasAttribute(key){
   if(variant==="dil"||variant==="mixed"){
    if(key==="data-user-message-bubble")return i%2===0;
    if(key==="data-chatgpt-selection-message-id")return i%2===1;
   }
   return Object.prototype.hasOwnProperty.call(this.attrs,key);
  },
  removeAttribute(key){delete this.attrs[key];writes++;}
 }));
 const doc={
  readyState:"complete",activeElement:null,head:{appendChild(){styles++;}},
  querySelectorAll(sel){
   if(sel.includes("conversation-turn-"))return variant==="legacy"?nodes:(variant==="mixed"?nodes.slice(0,3):[]);
   if(sel.includes("data-user-message-bubble"))return variant==="dil"||variant==="mixed"?nodes:[];
   return [];
  },
  querySelector(){return null;},
  getElementById(){return null;},
  createElement(){return {textContent:"",id:""};}
 };
 const win={
  document:doc,location:{hostname:"chatgpt.com"},
  CSS:{supports(){return true;}},performance:{now(){return 1;}},
  setInterval(){return 1;}
 };
 vm.runInNewContext(source,{window:win,Date});
 return {status:win.__OPENBROWSER_LITE_DOM__.getStatus(),writes,styles};
}
{
 const x=run("dil",90);
 assert.equal(x.status.turnCount,90,"count modern DIL assistant/user messages");
 assert.equal(x.status.dilAssistantCount,45);
 assert.equal(x.status.dilUserCount,45);
 assert.equal(x.status.domVariant,"dil_observe_only");
 assert.equal(x.status.optimized,0,"no dangerous React subtree changes");
 assert.equal(x.writes,0,"DIL remains untouched");
}
{
 const x=run("mixed",90);
 assert.equal(x.status.domVariant,"dil_observe_only","mixed legacy+DIL is modern");
 assert.equal(x.status.turnCount,90,"count actual DIL messages, not stray legacy wrappers");
 assert.equal(x.status.dilAssistantCount,45);
 assert.equal(x.status.dilUserCount,45);
 assert.equal(x.status.optimized,0,"never optimize mixed React DOM");
 assert.equal(x.writes,0,"mixed DOM remains untouched");
}
{
 const x=run("legacy",90);
 assert.equal(x.status.domVariant,"legacy");
 assert.equal(x.status.turnCount,90);
 assert.equal(x.status.optimized,50);
 assert.equal(x.writes,50);
}
{
 const x=run("dil",0);
 assert.equal(x.status.turnCount,0);
 assert.equal(x.writes,0);
}
console.log("LiteDomDilCount: 4 PASS");
