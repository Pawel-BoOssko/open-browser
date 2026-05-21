# PY Help

Purpose: Defines model-facing help for the Python executor.

Depends on:
- docs/runtime/hst-help_new.md
- docs/executors/PY/rules_new.md
- docs/agent/coding-invariants_new.md

Impacts:
- PY_HELP output
- Python command usage
- text and data processing
- Unicode-sensitive verification

Update rule: Update this file when PY executor behavior, command usage, encoding behavior, RAW guidance, or Python anti-patterns change.

Latest:
- PY is useful when it gives a concrete advantage.
- PY is not the default replacement for PS.
- PY is preferred for parsing, structured data, larger text operations, and Unicode-sensitive checks.
- For long or fragile payloads, use RAW as described in HST_HELP.

## What PY is for

Use PY when Python gives a clear advantage:
- parsing JSON, CSV, HTML, logs, or NDJSON;
- text processing;
- structured data transformation;
- report generation;
- Unicode-sensitive file verification;
- operations that are clearer in Python than in PowerShell.

Use PS for simple Windows/filesystem/process operations.

## Text and Unicode

For Unicode-sensitive verification, Python is often safer than returned PS stdout.

Use explicit encoding when reading and writing text files, for example UTF-8.

A bad-looking PS return does not automatically mean the file is bad. Verify the file with a controlled method when content integrity matters.

## RAW

For multi-line Python, long text, JSON-heavy code, markdown-heavy code, quote-heavy code, Polish text, or OpenBridge-sensitive payloads, use RAW as described in `HST_HELP`.

RAW is the OpenBridge transport mechanism for fragile payloads. Do not replace it with manual escaping unless the payload is simple.
