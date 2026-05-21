# PageTap Module Rules

Purpose:
Defines rules, limitations, and update obligations for the PageTap WebView page observation and interaction module.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/PageTap/index_new.md
- docs/modules/PageTap/help_new.md
- docs/modules/Downloads/rules_new.md
- docs/agent/current-state_new.md
- docs/test-sources-outline_new.txt

Impacts:
- docs/modules/PageTap/index_new.md
- docs/modules/PageTap/help_new.md
- docs/modules/Downloads/index_new.md
- docs/modules/Downloads/rules_new.md
- docs/agent/current-state_new.md
- docs/agent/documentation-update-checklist_new.md

Update rule:
Update this file when PageTap observation, selectors, DOM interaction, download detection, event reporting, historical download evidence, or page-side boundaries change.

Latest:
- Test source migration is reflected here.
- PageTap operates on the current WebView page surface.
- PageTap may support detection and guarded activation of generated-file controls.
- PageTap must not be treated as long-term project memory.
- PageTap behavior changes may affect Downloads documentation.
- Older download test failure should be treated as historical unless reproduced, because later live checks verified sandbox downloads.

## Rules

### PT-RULE-001: PageTap observes the current page surface

PageTap works with the currently rendered WebView page and its UI state.
It is not the canonical project state, architecture source, or long-term memory.

### PT-RULE-002: DOM interaction is operational, not architectural

DOM selectors, UI controls, and page-side events are operational implementation details.
Architecture decisions and project rules belong in repository documentation.

### PT-RULE-003: Guard repeated activations

When PageTap activates UI controls such as generated-file download controls, it should guard against repeated activation of the same control.

### PT-RULE-004: PageTap changes can affect Downloads

If PageTap detection or activation behavior changes for generated files, check and update Downloads documentation.

### PT-RULE-005: Prefer current-page evidence

When reporting page observations, PageTap-related diagnostics should describe what was observed in the current page state, not infer long-term conversation history.

### PT-RULE-006: Reproduced download failures must create new evidence

If a sandbox download failure is reproduced, create a new field note and update current-state.
Do not treat older superseded test failures as current state without reproduction.

## Documentation triggers

Update PageTap documentation when:
- DOM selector strategy changes;
- page observation behavior changes;
- file control detection changes;
- click or activation behavior changes;
- event names or emitted diagnostics change;
- PageTap boundaries change;
- Downloads behavior depends on a PageTap change;
- new test evidence changes the verified state.