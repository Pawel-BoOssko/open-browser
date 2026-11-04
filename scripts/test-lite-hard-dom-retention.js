const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('D:/projects/open-browser/src/BridgeBrowserAlpha0/LiteDomHardPrune.js','utf8');
function test({withStop=false, focusOldest=false, assistantsOnly=false}) {
 const arr = [];
 const main = {tagName:'MAIN', getAttribute(n){return n==='aria-busy' && withStop ? 'true' : null}, querySelectorAll(q){return q.includes('conversation-turn-')?[]:arr}};
 for(let i=0;i<12;i++){
   const assistant=assistantsOnly||i%2===1;
   const node={
     tagName:'DIV',id:i, parentNode:main,parentElement:main,hidden:false,disabled:false,
     getAttribute(key){return key==='data-user-message-bubble'&&!assistant?'':key==='data-chatgpt-selection-message-id'&&assistant?'assistant-'+i:null;},
     querySelector(){return null},contains(x){return x===this},
     closest(){return null},getClientRects(){return [{}]},
     remove(){arr.splice(arr.indexOf(this),1);this.parentNode=null;}
   };
   arr.push(node);
 }
 const stop={hidden:false,disabled:false,getAttribute(){return null},closest(){return null},getClientRects(){return [{}]}};
 const doc={ querySelector(x){return x==='main,[role="main"]'?main:null},
  querySelectorAll(){return withStop?[stop]:[]},
  activeElement: focusOldest?arr[0]:null
 };
 const win={document:doc,location:{hostname:'chatgpt.com'},setInterval(){return 1},setTimeout(){return 2},clearInterval(){},clearTimeout(){},getSelection(){return ''}};
 vm.runInNewContext(source,{window:win,console,Date,Set});
 const result=win.__OPENBROWSER_HARD_PRUNE__.prune();
 assert.equal(result.enabled,true);
 assert.equal(result.visibleTurns,focusOldest?11:10);
 assert.equal(arr.length,focusOldest?11:10);
 assert.deepEqual(arr.map(x=>x.id),focusOldest?[0,2,3,4,5,6,7,8,9,10,11]:[2,3,4,5,6,7,8,9,10,11]);
 assert.equal(result.obsoleteDropped,focusOldest?1:(assistantsOnly?2:1));
 return result;
}
test({withStop:true,assistantsOnly:true});
test({withStop:false});
test({withStop:true,focusOldest:true,assistantsOnly:true});
console.log('HARD_PRUNE_3_CASES_GENERATING_UNARCHIVED_AND_FOCUS_PROTECTED_PASS');