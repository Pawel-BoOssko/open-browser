"use strict";
const fs = require("fs");
const vm = require("vm");
const path = require("path");
const src = fs.readFileSync(path.join(__dirname, "..", "src", "BridgeBrowserAlpha0",
  "LiteResponseSignal.js"), "utf8");
function node(text) {
  return {
    textContent: text, innerText: text, hidden: false, disabled: false,
    getClientRects: () => [1], getAttribute: () => null, closest: () => null
  };
}
function probe({body="",retry="",alert="",status=""}={}) {
  const button = retry ? node(retry) : null;
  const alertNode = alert ? node(alert) : null;
  const statusNode = status ? node(status) : null;
  const main = { querySelectorAll: () => [], querySelector: () => null,
    textContent: "", innerText: "" };
  const document = {
    readyState: "complete", body: { innerText: body },
    querySelector: key => key === 'main,[role="main"]' ? main : null,
    querySelectorAll: key => {
      if (key === 'button,[role="button"]') return button ? [button] : [];
      if (key === '[role="alert"],[data-testid*="error"],[data-testid*="toast"]')
        return alertNode ? [alertNode] : [];
      if (key === '[role="status"],[role="alert"],[aria-live],p,span')
        return [statusNode,alertNode].filter(Boolean);
      return [];
    }
  };
  const window = { document, location: { hostname: 'chatgpt.com' },
    performance: { now: () => 1 },
    getComputedStyle: () => ({display:'block',visibility:'visible'}) };
  vm.runInNewContext(src, {window}, {timeout:1000});
  return window.__OPENBROWSER_RESPONSE_SIGNALS__.read().visibleError;
}
const cases = [
  ["full_page_plus_unrelated_alert", {body:"ChatGPT ran into a problem. Try again.", retry:"Try again", alert:"Unrelated notice"}, "chatgpt_app_problem"],
  ["full_page_div_no_heading", {body:"ChatGPT napotka\u0142 problem. Co\u015B posz\u0142o nie tak. Aby kontynuowa\u0107, spr\u00F3buj ponownie.",retry:"Spr\u00F3buj ponownie"}, "chatgpt_app_problem"],
  ["interrupted_connection", {status:"Po\u0142\u0105czenie zosta\u0142o przerwane. Oczekiwanie na pe\u0142n\u0105 odpowied\u017A"}, "connection_interrupted"],
  ["terminal_polish_banner", {alert:"Przekroczono maksymaln\u0105 d\u0142ugo\u015B\u0107 tej rozmowy"}, "conversation_length_exceeded"],
  ["terminal_english_banner", {alert:"Conversation is too long"}, "conversation_length_exceeded"],
  ["plain_text_no_retry", {body:"ChatGPT napotkaĹ‚ problem"}, null],
  ["normal_empty_chat", {}, null]
];
for(const [name,input,expected] of cases){
 const got=probe(input);
 if(got!==expected) { console.error(name+" FAIL expected="+expected+" got="+got); process.exitCode=1; }
 else console.log(name+" PASS");
}
if(!process.exitCode) console.log("ALL_PASS="+cases.length);
