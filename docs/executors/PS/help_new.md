# PS Executor Help

Purpose:
Defines operational help for using the PowerShell executor through OpenBridge.

Depends on:
- docs/templates/document-header-template_new.md
- docs/executors/PS/index_new.md
- docs/executors/PS/rules_new.md
- docs/templates/executor-docs-template_new.md
- docs/agent/coding-invariants_new.md
- docs/runtime/environment.md

Impacts:
- docs/runtime/hst-help_new.md
- docs/executors/PS/index_new.md
- docs/executors/PS/rules_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when PS command usage, payload expectations, output conventions, RAW payload guidance, truncation behavior, examples, or PS-specific constraints change.

Latest:
- Source migration from docs/runtime/environment.md is reflected here.
- PS executes PowerShell payloads.
- Use PS for local filesystem checks, diagnostics, small scripts, build commands, and controlled file writes.
- Keep commands simple and single-purpose.
- Use RAW payload for scripts longer than a few lines or containing quotes, backslashes, JSON, markdown, or OpenBridge-sensitive text.
- When writing text files, use explicit UTF-8.
- PS stdout may be truncated by the runtime; for large outputs, write to a file and return path plus preview.

## Command

PS:
Executes a PowerShell payload in the OpenBrowser working environment.

PS_HELP:
Shows help for the PowerShell executor.

## Payload

The payload is PowerShell code or a PowerShell command line.

Use PS for:
- listing files;
- reading small files;
- writing controlled files;
- running build or diagnostic commands;
- collecting repository state;
- checking logs and downloads;
- creating small helper scripts.

## RAW payload guidance

Use RAW payload when the command:
- is longer than a few lines;
- contains many quotes;
- contains backslashes;
- contains JSON;
- contains markdown;
- contains OpenBridge-sensitive text.

The runtime RAW mechanism lets the model write plain text while the system handles encoding before executor delivery.

## Output expectations

PS should return:
- command output;
- error output if present;
- enough information to verify whether the operation succeeded.

For large outputs, write the full output to a file and return:
- output file path;
- short preview;
- whether the output was truncated.

## Encoding rule

When PS writes text files, it must use explicit UTF-8.
Do not rely on Windows default encoding.

## Practical rule

Use small, focused PS commands. A single command should have one clear responsibility unless the owner/architect explicitly asks for a larger scripted operation.