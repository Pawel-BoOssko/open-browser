"use strict";
const fs=require("fs"), vm=require("vm"), assert=require("assert");
const source=fs.readFileSync("D:/projects/open-browser/src/BridgeBrowserAlpha0/LiteSuccessorAssistantAnswer.js","utf8");
const receipt="e".repeat(32), target="SUCCESSOR READY "+receipt;
function node(text,{user=false,childUser=false}={}) {
  return {innerText:text,
    closest(q){assert(q.includes('data-user-message-bubble'));return user?{}:null;},
    querySelector(q){assert(q.includes('data-user-message-bubble'));return childUser?{}:null;}};
}
function run(assistant=[],selection=[],expected=target) {
  const root={querySelectorAll(q) {
    if(q==='[data-message-author-role="assistant"]') return assistant;
    if(q==='[data-chatgpt-selection-message-id]') return selection;
    throw new Error("Unexpected query: "+q);
  }};
  const document={querySelector(q){assert.equal(q,"main");return root;}};
  return JSON.parse(vm.runInNewContext("("+source+")",{document})(expected));
}
assert.equal(run([node(target)]).exact,true);
assert.equal(run([],[]).exact,false);
assert.equal(run([node("Answer: "+target)]).exact,false);
assert.equal(run([node("not it"),node(target)]).exact,true);
assert.equal(run([node(target+" extra")]).exact,false);
assert.equal(run([node(target)],[],"INVALID").exact,false);
assert.equal(run([node(" "+target+" ")]).exact,true);
assert.equal(run([],[node(target)]).exact,true);
assert.equal(run([],[node(target,{user:true})]).exact,false);
assert.equal(run([],[node(target,{childUser:true})]).exact,false);
assert.equal(run([node("other")],[node(target)]).exact,false);
assert.equal(run([],[node("Reply with exactly: "+target)]).exact,false);
assert.equal(run([],[node(target+" extra")]).exact,false);
assert.equal(run([],[node(" " + target + " ")]).via,"selection_body");
assert.equal(run([node("DATA I GODZINA WYBUDZENIA: 2026-10-29 12:00\n"+target+"\nKontynuuję pracę")]).exact,true);
assert.equal(run([],[node("DATA I GODZINA WYBUDZENIA\n"+target+"\nWykonuję zadanie")]).exact,true);
console.log("ASSISTANT_ROLE_OR_ISOLATED_SELECTION_ACK_LINE_PASS cases=16 user_false_positives_rejected");