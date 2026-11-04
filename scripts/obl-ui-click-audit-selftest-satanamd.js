const fs = require('fs');
const vm = require('vm');
const assert = require('assert');
let click;
const doc = {
  readyState: 'complete',
  body: { innerText: '' },
  addEventListener(type, fn) { if (type === 'click') click = fn; },
  querySelector() { return null; },
  querySelectorAll() { return []; }
};
const win = {
  document: doc, location: { hostname: 'chatgpt.com' },
  performance: { now: () => 125 }
};
const ctx = { window: win, Date, Array, String };
vm.runInNewContext(fs.readFileSync(
  'D:/projects/open-browser/src/BridgeBrowserAlpha0/LiteResponseSignal.js',
  'utf8'), ctx);
assert.strictEqual(typeof click, 'function');
const button = {
  tagName: 'BUTTON', innerText: 'Spróbuj ponownie',
  getAttribute() { return null; }, closest(selector) {
    return selector.startsWith('button') ? this : null;
  }
};
click({ target: { closest: () => button }, isTrusted: true });
let first = win.__OPENBROWSER_RESPONSE_SIGNALS__.read();
assert.strictEqual(first.uiActions.length, 1);
assert.strictEqual(first.uiActions[0].action, 'retry');
assert.strictEqual(first.uiActions[0].trusted, true);
assert.strictEqual(win.__OPENBROWSER_RESPONSE_SIGNALS__.read().uiActions.length, 0);
button.innerText = 'Tekst prywatny użytkownika';
click({ target: { closest: () => button }, isTrusted: true });
assert.strictEqual(win.__OPENBROWSER_RESPONSE_SIGNALS__.read().uiActions.length, 0);
console.log('UI_CLICK_AUDIT_PASS=trusted_retry+drain+privacy');
