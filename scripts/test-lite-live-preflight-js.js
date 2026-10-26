const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync('D:/projects/open-browser/src/BridgeBrowserAlpha0/LiteResponseSignal.js','utf8');
function check(busy){
 const marker={closest(){return null},textContent:'Completed model message'};
 const main={textContent:'Completed model message',querySelector(selector){return selector.includes('tool-running')&&busy?{}:null},querySelectorAll(selector){
  if(selector.includes('data-user-message-bubble'))return [marker];
  if(selector.includes('data-chatgpt-selection-message-id') || selector.includes('data-message-author-role'))return [marker];
  return [];
 }};
 const doc={readyState:'complete',querySelector(selector){if(selector.startsWith('main'))return main;return null},querySelectorAll(){return []}};
 const w={document:doc,location:{hostname:'chatgpt.com'},performance:{now:()=>0}};
 vm.runInNewContext(source,{window:w,console});
 const value=w.__OPENBROWSER_RESPONSE_SIGNALS__.read();
 assert.equal(value.toolBusySignal,busy);
 assert.equal(value.generatingSignal,false);
 assert.equal(value.hasDraft,false);
 return value;
}
check(false);check(true);
console.log('PREFLIGHT_TOOLBUSY_2_PASS');