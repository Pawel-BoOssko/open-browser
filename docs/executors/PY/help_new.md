# PY Executor Help

Purpose:
Defines operational help for using the Python executor through OpenBridge.

Depends on:
- docs/templates/document-header-template_new.md
- docs/executors/PY/index_new.md
- docs/executors/PY/rules_new.md
- docs/templates/executor-docs-template_new.md
- docs/runtime/environment.md

Impacts:
- docs/runtime/hst-help_new.md
- docs/executors/PY/index_new.md
- docs/executors/PY/rules_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when PY command usage, payload expectations, output conventions, RAW payload guidance, examples, or PY-specific constraints change.

Latest:
- Source migration from docs/runtime/environment.md is reflected here.
- PY executes Python 3 payloads.
- Use PY for JSON validation, structured text processing, NDJSON analysis, small data transformations, and precise file operations.
- Use RAW payload for multi-line Python or quote-heavy code.
- For executor-specific constraints, read docs/executors/PY/rules_new.md.

## Command

PY:
Executes a Python 3 payload in the OpenBrowser working environment.

PY_HELP:
Shows help for the Python executor.

## Payload

The payload is Python code. The executor uses Python 3 execution for supplied code.

Use PY for:
- validating JSON;
- parsing structured logs;
- processing NDJSON;
- transforming text precisely;
- checking generated files;
- creating small diagnostic scripts;
- operations where PowerShell escaping would be fragile.

## RAW payload guidance

Use RAW payload for multi-line Python, JSON-heavy scripts, markdown processing, quote-heavy code, or OpenBridge-sensitive text.

RAW is preferred over manual JSON escaping for complex Python payloads.

## Output expectations

PY should return:
- printed output;
- exceptions or errors if present;
- enough information to verify whether the operation succeeded.

For large outputs, write the full output to a file and return a short preview plus path.

## Practical rule

Use Python when it is the right tool for precise parsing, encoding-sensitive processing, or structured validation.
Do not use Python as a way to hide many unrelated operations in one command.