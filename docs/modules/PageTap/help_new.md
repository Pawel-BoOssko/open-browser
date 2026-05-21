# PageTap Module Help

Purpose:
Defines model-facing help for PageTap as the WebView page observation and interaction module.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/PageTap/index_new.md
- docs/modules/PageTap/rules_new.md
- docs/modules/Downloads/help_new.md
- docs/agent/current-state_new.md

Impacts:
- docs/modules/PageTap/index_new.md
- docs/modules/PageTap/rules_new.md
- docs/modules/Downloads/help_new.md
- docs/runtime/hst-help_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when PageTap model-facing behavior, page observation, UI detection, download control detection, or page interaction instructions change.

Latest:
- PageTap works inside the rendered WebView page.
- PageTap helps OpenBrowser observe current page state and detect relevant UI controls.
- PageTap supports Downloads by detecting generated file controls.
- PageTap is not a long-term memory or architecture source of truth.

## What this module does

PageTap is the page-side module for observing and interacting with the currently loaded web UI.

It can support:
- detecting relevant page elements;
- observing model output when needed;
- detecting generated file controls;
- activating selected controls in a guarded way;
- sending page-side signals to OpenBrowser runtime modules.

## Model usage

A model normally does not call PageTap directly.
The model observes PageTap effects through higher-level behavior, for example automatic download handling or status information.

When the model needs current runtime state, use HST_STATUS.
When the model needs executor details, use executor HELP commands.

## Limitations

PageTap sees the current page surface.
It should not be treated as the project memory, documentation source, architecture record, or complete historical conversation store.