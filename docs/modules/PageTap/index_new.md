# PageTap Module Index

Purpose:
Describes the PageTap module and its role in observing and interacting with the WebView page used by OpenBrowser.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/templates/module-docs-template_new.md
- docs/agent/current-state_new.md
- docs/modules/PageTap/help_new.md
- docs/modules/PageTap/rules_new.md

Impacts:
- docs/modules/PageTap/help_new.md
- docs/modules/PageTap/rules_new.md
- docs/modules/Downloads/index_new.md
- docs/agent/current-state_new.md
- docs/runtime/hst-status_new.md

Update rule:
Update this file when PageTap responsibilities, DOM interaction behavior, page observation, generated-file detection, or WebView interaction boundaries change.

Latest:
- PageTap is the runtime module that observes and interacts with the web page inside WebView.
- PageTap participates in detecting ChatGPT-generated file controls for Downloads.
- PageTap must not be treated as the source of long-term conversation truth.
- DOM interaction is operational surface handling, not project memory.

## Responsibility

PageTap owns page-side observation and interaction logic inside the WebView context.

Current documented responsibilities:
- observe model/page output where needed;
- detect relevant UI controls;
- support guarded activation of download controls;
- support OpenBrowser runtime interaction with the current web UI.

## Boundaries

PageTap does:
- interact with the rendered page;
- support UI detection and activation;
- provide page-side signals to runtime modules.

PageTap does not:
- own project documentation truth;
- own persistent architecture decisions;
- replace Host status;
- act as a general database of conversation history.

## Related documents

- docs/modules/PageTap/help_new.md
- docs/modules/PageTap/rules_new.md
- docs/modules/Downloads/index_new.md
- docs/runtime/modules-index_new.md
- docs/agent/current-state_new.md

## Implementation evidence

- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/PageTap.js: detects ChatGPT download buttons, including button.behavior-btn, and dispatches click sequence.
- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/BrowserTabRuntime.cs: manages WebView lifecycle, PageTap, and module loading.