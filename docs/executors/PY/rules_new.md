# PY Executor Rules

Purpose:
Defines Python executor rules, invariants, limitations, and update obligations.

Depends on:
- docs/templates/document-header-template_new.md
- docs/executors/PY/index_new.md
- docs/executors/PY/help_new.md
- docs/templates/executor-docs-template_new.md
- docs/agent/coding-invariants_new.md
- docs/runtime/environment.md

Impacts:
- docs/executors/PY/index_new.md
- docs/executors/PY/help_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/documentation-update-checklist_new.md
- scripts that use Python
- generated documentation files

Update rule:
Update this file when PY executor behavior, encoding rules, timeout behavior, output handling, RAW payload handling, file-writing behavior, or recommended Python usage changes.

Latest:
- Source migration from docs/runtime/environment.md is reflected here.
- Use PY for structured parsing, JSON validation, NDJSON analysis, precise text processing, and encoding-sensitive operations.
- PY should not hide many unrelated steps inside one large script unless the task explicitly requires it.
- Use RAW payload for multi-line or quoting-sensitive Python.
- Large outputs should be written to a file with a short preview returned to chat.
- Generated text files should use explicit UTF-8.

## Rules

### PY-RULE-001: Use Python for structured precision

Use PY when the task benefits from Python's stronger handling of structured data, JSON, NDJSON, text transformation, validation, or encoding-sensitive processing.

### PY-RULE-002: Do not hide unrelated work

A PY command should normally have one clear responsibility.
Do not use Python as a way to hide many unrelated operations inside one command.

### PY-RULE-003: Explicit UTF-8 for text writes

When Python writes text files, generated documentation, reports, logs, JSON, or markdown, use explicit UTF-8.

### PY-RULE-004: Verify writes immediately

After writing or modifying a file, verify the result by checking at least:
- path;
- size;
- last write time;
- relevant content preview or full content for small files.

### PY-RULE-005: Keep large output out of chat

If output is large, save full output to a file and return:
- output file path;
- short preview;
- whether the output was truncated.

### PY-RULE-006: Report exceptions clearly

If a PY command fails, return the exception or error and stop the current operation unless the next diagnostic step is obvious and safe.

### PY-RULE-007: Use RAW for fragile payloads

Use RAW payload for Python code that is multi-line, quote-heavy, JSON-heavy, markdown-heavy, or contains OpenBridge-sensitive text.

RAW is preferred over manual JSON escaping for complex payloads.

## Documentation triggers

Update PY documentation when:
- PY command behavior changes;
- PY_HELP changes;
- encoding rules change;
- output or timeout behavior changes;
- RAW payload behavior changes;
- working directory assumptions change;
- generated file handling changes.