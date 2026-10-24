"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../src/BridgeBrowserAlpha0");
const src = fs.readFileSync(path.join(root, "LiteConversationSnapshot.js"), "utf8");
function snapshot({ generating = false, pathname = "/c/id", hydrated = true } = {}) {
  const old = ["pierwsza odpowiedź", "druga odpowiedź", "trzecia odpowiedź"];
  const assistant = old.map((innerText, i) => ({
    innerText, getAttribute(name) {
      return name === "data-chatgpt-selection-message-id" ? "turn" + i : null;
    }
  }));
  const main = {
    querySelectorAll(query) {
      if (query.includes("stop-button")) return generating ?
        [{ hidden: false, disabled: false, getClientRects: () => [1],
           getAttribute: () => null }] : [];
      if (query.includes("data-chatgpt-selection-message-id")) return assistant;
      if (query.includes('data-message-author-role')) return assistant;
      return [];
    }
  };
  const global = { location: { hostname: "chatgpt.com", pathname },
    document: { querySelector(query) {
      return query === 'main,[role="main"]' && hydrated ? main : null;
    } }
  };
  vm.runInNewContext(src, {window: global});
  return global.__OPENBROWSER_LITE_SNAPSHOT__.collect();
}
const done = snapshot();
assert.equal(done.length, 2);
assert.equal(done[0].partial, false);
assert.equal(done[0].turnId, "turn2");
assert.equal(done[1].turnId, "turn1");
const partial = snapshot({generating:true});
assert.equal(partial.length, 1);
assert.equal(partial[0].partial, true);
assert.equal(partial[0].turnId, "turn2");
assert.equal(snapshot({hydrated:false}).length, 0);
assert.equal(snapshot({pathname:"/"}).length, 0);
const captureCs = fs.readFileSync(path.join(root, "LiteConversationCaptureUi.cs"), "utf8");
assert.match(captureCs, /_markdownArchive\.Save\(chatUrl, turnId, text, partial\)/);
assert.match(captureCs, /!partial && _archive\.Save\(chatUrl, turnId, text\)/);
const md = fs.readFileSync(path.join(root,"LiteResponseMarkdownArchive.cs"),"utf8");
assert.match(md, /index\.jsonl/);
assert.match(md, /IsPersisted\(/);
assert.match(captureCs, /collectForArchive\(\)/);
assert.match(captureCs, /ackSaved\(/);
assert.match(md, /File\.Move\(temp, file, true\)/);
console.log("LiteArchiveMarkdown: 10 assertions PASS");
