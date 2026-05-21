# PS Executor Rules

Purpose:
Defines PowerShell executor rules, invariants, limitations, and update obligations.

Depends on:
- docs/templates/document-header-template_new.md
- docs/executors/PS/index_new.md
- docs/executors/PS/help_new.md
- docs/templates/executor-docs-template_new.md
- docs/agent/coding-invariants_new.md
- docs/runtime/environment.md

Impacts:
- docs/executors/PS/index_new.md
- docs/executors/PS/help_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/documentation-update-checklist_new.md
- scripts that use PowerShell
- generated documentation files

Update rule:
Update this file when PS executor behavior, encoding rules, timeout behavior, output handling, RAW payload handling, file-writing behavior, or recommended PowerShell usage changes.

Latest:
- Source migration from docs/runtime/environment.md is reflected here.
- PowerShell text output and generated files must use explicit UTF-8.
- Do not rely on Windows default encoding.
- Use small, focused PS commands unless a larger scripted operation is explicitly requested.
- Use RAW payload for long or quoting-sensitive PowerShell.
- Large outputs should be written to a file with a short preview returned to chat.

## Rules

### PS-RULE-001: Explicit UTF-8 for text writes

When PowerShell writes text files, generated documentation, generated scripts, logs, reports, or command output files, it must use explicit UTF-8.

Do not rely on Windows default encoding.

### PS-RULE-002: Prefer focused commands

A PS command should normally do one clear thing:
- inspect;
- write;
- verify;
- build;
- test;
- list;
- read.

Larger scripted operations are allowed only when explicitly useful for the task.

### PS-RULE-003: Verify writes immediately

After writing or modifying a file, verify the result by checking at least:
- path;
- size;
- last write time;
- relevant content preview or full content for small files.

### PS-RULE-004: Do not hide important output

If output is large, save full output to a file and return:
- output file path;
- short preview;
- whether the output was truncated.

### PS-RULE-005: Report errors clearly

If a PS command fails, return the relevant error and stop the current operation unless the next diagnostic step is obvious and safe.

### PS-RULE-006: Use RAW for fragile payloads

Use RAW payload for PowerShell commands that are longer than a few lines or contain quotes, backslashes, JSON, markdown, or OpenBridge-sensitive text.

RAW is preferred over manual JSON escaping for complex payloads.

## Documentation triggers

Update PS documentation when:
- PS command behavior changes;
- PS_HELP changes;
- encoding rules change;
- output or timeout behavior changes;
- RAW payload behavior changes;
- working directory assumptions change;
- generated file handling changes.