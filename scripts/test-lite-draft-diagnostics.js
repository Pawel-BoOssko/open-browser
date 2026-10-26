const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync('D:/projects/open-browser/src/BridgeBrowserAlpha0/LiteResumeProbe.js','utf8');
function inspect(value,hidden){
 const editor={id:'prompt-textarea',value,hidden,tagName:'DIV',getAttribute(x){return null},getClientRects(){return hidden?[]:[{}]}};
 const main={querySelectorAll(){return []},querySelector(){return null}};
 const document={querySelector(q){if(q==='main,[role="main"]')return main;if(q==='#prompt-textarea')return editor;return null;},querySelectorAll(){return []}};
 const win={document,location:{hostname:'chatgpt.com',pathname:'/c/test'}};
 vm.runInNewContext(source,{window:win});
 const obs=win.__OPENBROWSER_RESUME_PROBE__.observe();
 assert.equal(obs.draftChars,value.trim().length);
 assert.equal(obs.hasDraft,value.trim().length>0);
 assert.equal(obs.editorSource,'prompt-textarea');
 assert.equal(obs.editorVisible,!hidden);
 assert.equal(Object.hasOwn(obs,'draftText'),false);
}
inspect('',false);inspect('hello',false);inspect(' hidden ',true);
console.log('DRAFT_DIAGNOSTICS_3_CASES_PASS');