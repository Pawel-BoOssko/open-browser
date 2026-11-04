"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.join(__dirname, "../src/BridgeBrowserAlpha0/LiteResponseSignal.js"), "utf8");
function run(text = "", busy = false, host = "chatgpt.com", speaker = "alert") {
  const node = {
    textContent: text,
    hidden: speaker === "hiddenAlert",
    getAttribute(name) {
      return name === "aria-hidden" && speaker === "ariaHiddenAlert" ? "true" : null;
    },
    getClientRects() {
      return speaker === "detachedAlert" ? [] : [{}];
    },
    closest(selector) {
      if (speaker === "sidebar" && selector.includes("aside")) return {};
      if (speaker === "parentHiddenAlert" && selector.includes("[hidden]")) return {};
      return null;
    }
  };
  const assistantNodes = speaker === "olderError"
    ? [node, { ...node, textContent: "Now a complete newer assistant response" }]
    : speaker === "latestError"
      ? [{ ...node, textContent: "Earlier successful response" }, node]
      : [node];
  const terminalPanel = {
    textContent: text,
    innerText: text,
    parentElement: null,
    closest() { return ["terminalQuote","terminalHiddenButton"].includes(speaker) ? {} : null; },
    getAttribute() { return null; },
    getClientRects() { return [{}]; }
  };
  const terminalButton = {
    innerText: "Rozpocznij nowy czat",
    parentElement: terminalPanel,
    hidden: speaker === "terminalHiddenButton",
    getAttribute() { return null; },
    getClientRects() { return speaker === "terminalHiddenButton" ? [] : [{}]; },
    closest(selector) {
      return ["terminalQuote","terminalHiddenButton"].includes(speaker) && selector.includes("[data-message-author-role]") ? {} : null;
    }
  };
  const main = {
    textContent: speaker === "shellError" || speaker === "dilOnlyShellQuote" ? text : "",
    querySelectorAll(selector) {
      if (selector === 'button,a,[role="button"]')
        return ["terminalBanner","terminalQuote","terminalHiddenButton"].includes(speaker)
          ? [terminalButton] : [];
      if (selector.startsWith('[data-testid^="conversation-turn-"]'))
        return ["shellError","dilOnlyShellQuote","modernConversation"].includes(speaker)
          ? [] : [node];
      if (selector.startsWith('[data-user-message-bubble]'))
        return ["dilOnlyShellQuote","modernConversation"].includes(speaker)
          ? [node, { ...node, textContent: "Another hydrated turn" }] : [];
      if (selector.startsWith('[data-message-author-role'))
        return speaker === "user" || speaker === "sidebar" || speaker === "shellError" || ["terminalQuote","terminalHiddenButton"].includes(speaker) ? [] : assistantNodes;
      throw new Error("Unknown main selector: " + selector);
    }
  };
  const window = {
    performance: { now: () => 100 },
    location: { hostname: host },
    getComputedStyle() {
      return { display: speaker === "cssHiddenAlert" ? "none" : "block",
        visibility: "visible" };
    },
    document: {
      readyState: "complete",
      querySelectorAll(selector) {
        if (selector.startsWith('[data-testid="stop-button"]'))
          return [...(busy ? [{ closest() { return null; } }] : []),
            ...(speaker === "sidebarStop" ?
              [{ closest() { return {}; } }] : [])];
        if (selector.startsWith('[role="alert"]'))
          return ["alert", "sidebar", "hiddenAlert", "ariaHiddenAlert",
            "parentHiddenAlert", "detachedAlert", "cssHiddenAlert"].includes(speaker) && text
            ? [node] : [];
        if (selector.startsWith('#prompt-textarea')) return [];
        if (selector.startsWith('[data-testid^="conversation-turn-"]')) return [node];
        if (selector.startsWith('[data-message-author-role'))
          return speaker === "user" || ["terminalQuote","terminalHiddenButton"].includes(speaker) ? [] : assistantNodes;
        if (selector.startsWith('[role="status"]')) return [];
        if (selector === 'button,[role="button"]' || selector === 'h1,h2,h3,[role="heading"]') return [];
        throw new Error("Unknown selector: " + selector);
      },
      querySelector(selector) {
        if (selector === 'main,[role="main"]') return main;
        if (selector.startsWith('[data-testid="stop-button"]')) return busy ? {} : null;
        if (selector.startsWith('textarea,')) return {};
        return null;
      }
    }
  };
  vm.runInNewContext(script, { window });
  return window.__OPENBROWSER_RESPONSE_SIGNALS__?.read();
}
assert.equal(run("Nie udało się wczytać tej odpowiedzi").visibleError, "response_load_failed");
assert.equal(run("Failed to load this response").visibleError, "response_load_failed");
assert.equal(run("Przekroczono limit czasu dostarczenia wiadomości").visibleError, "delivery_timeout");
assert.equal(run("Przerwano rozumowanie").visibleError, "reasoning_interrupted");
assert.equal(run("hello", true).generatingSignal, true);
assert.equal(run("hello", false, "chatgpt.com", "sidebarStop").generatingSignal,
  false, "sidebar Stop button is not active response generation");
assert.equal(run("hello", true, "chatgpt.com", "sidebarStop").generatingSignal,
  true, "actual active Stop still blocks alongside unrelated sidebar");
assert.equal(run("hello").visibleError, null);
assert.equal(run("Nie udało się wczytać tej rozmowy ChatGPT", false, "chatgpt.com", "shellError").visibleError,
  "conversation_load_failed", "failed Polish conversation shell is not healthy hydration");
assert.equal(run("Couldn't load this conversation", false, "chatgpt.com", "shellError").visibleError,
  "conversation_load_failed", "English conversation load failure is recognized");
assert.equal(run("Nie udało się wczytać tej rozmowy ChatGPT", false, "chatgpt.com", "user").visibleError,
  null, "user quote in a real turn must not trigger shell failure");
assert.equal(run(("Failed to load conversation; ").repeat(18), false, "chatgpt.com", "shellError").visibleError,
  null, "long main content must not be classified as a short error shell");
assert.equal(run("hello", false, "untrusted.com"), undefined);
assert.equal(run("Nie udało się wczytać tej odpowiedzi", false,
  "chatgpt.com", "dil").visibleError, "response_load_failed",
  "short DIL assistant failure should block the supervisor");
assert.equal(run("Przekroczono limit czasu dostarczenia wiadomości", false,
  "chatgpt.com", "user").visibleError, null,
  "user quoting an error must never trigger a false UI failure");
assert.equal(run("Przekroczono limit czasu dostarczenia wiadomości ".repeat(8),
  false, "chatgpt.com", "assistant").visibleError, null,
  "long assistant text discussing an error is not an error placeholder");
assert.equal(run("Failed to load this response", false, "chatgpt.com", "sidebar").visibleError,
  null, "sidebar alert quoting response failure must not block active chat");
const sidebarStatus = run("Failed to load this response", false, "chatgpt.com", "sidebar");
assert.equal(sidebarStatus.assistantCount, 0,
  "sidebar assistant markers are not active chat messages");
assert.equal(run("Failed to load this response", false, "chatgpt.com", "alert").visibleError,
  "response_load_failed", "real global error toast remains recognized");
for (const mode of ["hiddenAlert", "ariaHiddenAlert", "parentHiddenAlert",
  "detachedAlert", "cssHiddenAlert"]) {
  const state = run("Failed to load this response", false, "chatgpt.com", mode);
  assert.equal(state.visibleError, null, mode + " is not a visible active failure");
  assert.equal(state.ignoredHiddenAlerts, 1);
}
assert.equal(run("Failed to load this response", false, "chatgpt.com", "alert")
  .errorSource, "visible_alert");
assert.equal(run("Failed to load this response", false, "chatgpt.com", "dil")
  .errorSource, "short_assistant_placeholder");
assert.equal(run("Failed to load this response", false, "chatgpt.com", "olderError")
  .visibleError, null, "previous failed assistant turn must not block a newer reply");
assert.equal(run("Failed to load this response", false, "chatgpt.com", "latestError")
  .visibleError, "response_load_failed", "latest visible assistant error must still block");
const modern = run("Complete assistant message", false, "chatgpt.com", "modernConversation");
assert.equal(modern.domVariant, "dil", "Modern hydrated DOM is recognized without legacy wrappers");
assert.equal(modern.turnCount, 2, "Modern turn count reflects DIL and not zero legacy turns");
const quotedShell = run("Couldn't load this conversation", false, "chatgpt.com", "dilOnlyShellQuote");
assert.equal(quotedShell.turnCount, 2);
assert.equal(quotedShell.visibleError, null,
  "modern hydrated turns prevent a false empty-shell conversation failure");
assert.equal(run("Couldn't load this conversation", false,
  "chatgpt.com", "shellError").domVariant, "unhydrated");
const terminalPL = "Osiągnięto maksymalną długość dla tej konwersacji, możesz jednak dalej rozmawiać.";
const terminalEN = "You've reached the maximum length for this conversation, but you can continue by starting a new chat.";
assert.equal(run(terminalPL, false, "chatgpt.com", "terminalBanner").visibleError,
  "conversation_length_exceeded", "Polish terminal banner with new-chat control is detected");
assert.equal(run(terminalEN, false, "chatgpt.com", "terminalBanner").visibleError,
  "conversation_length_exceeded", "English terminal banner is detected");
assert.equal(run(terminalPL, false, "chatgpt.com", "terminalBanner").errorSource,
  "terminal_new_chat_panel", "structured terminal panel source is reported");
assert.equal(run(terminalPL, false, "chatgpt.com", "terminalQuote").visibleError,
  null, "quoted user text with a new-chat-like control cannot trigger");
assert.equal(run(terminalPL, false, "chatgpt.com", "terminalHiddenButton").visibleError,
  null, "hidden terminal control is not accepted");
assert.equal(run(terminalPL, false, "chatgpt.com", "user").visibleError,
  null, "user quotation alone never blocks wake");
assert.equal(run(terminalPL, false, "chatgpt.com", "alert").visibleError,
  "conversation_length_exceeded", "visible terminal alert is classified");
console.log("LiteResponseSignal: terminal banners, negative controls and historical checks PASS");
