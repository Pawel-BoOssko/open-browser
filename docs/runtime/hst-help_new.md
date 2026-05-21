# HST Help

Purpose: Defines the model-facing help index for OpenBridge Host commands and command discovery.

Depends on:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/current-state_new.md
- docs/runtime/modules-index_new.md
- docs/agent/help-command-routing_new.md

Impacts:
- HST_HELP output
- Host command discovery
- tool help routing
- executor help discovery
- RAW payload usage

Update rule: Update this file when Host commands, envelope format, RAW payload behavior, help command list, tool command list, or system-level anti-patterns change.

Latest:
- HST_HELP is the system help index, not runtime status.
- HST_STATUS is runtime status, not help.
- Tool help commands display the README.md file for that tool and must say which file they display.
- RAW is part of the OpenBridge command framing contract.

## What OpenBridge is

OpenBridge is a local execution bridge between a language model running in OpenBrowser and local executors or tools.

The model does not execute system commands directly. It writes an OpenBridge command frame. OpenBrowser detects the frame, validates it, routes it through the Host, executes the requested command through the selected executor or tool, captures the result, and sends the result back to the model.

The purpose is controlled local work: filesystem inspection, file creation, scripts, local tools, project automation, and selected external service tools.

## Execution flow

```text
model response
-> OpenBridge execution frame
-> parser and validator
-> Host command routing
-> executor or tool
-> result capture
-> result returned to model
```

The model must not invent command syntax. It must use the documented frame and documented command names.

## Command frame

Every executable command must be wrapped like this:

```text
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_STATUS","payload":""}
@@OPENBRIDGE_EXEC_END@@
```

Fields:
- `version`: command frame version, currently `001`;
- `command`: command name;
- `payload`: command payload, usually a string.

A command written outside the execution frame is just text.

## System commands

System commands are Host commands. They use the `HST_` prefix.

Current system commands:
- `HST_HELP`: shows the OpenBridge help index;
- `HST_STATUS`: shows runtime status.

`HST_STATUS` reports current runtime state. It is not a usage manual.

## Executor help commands

Executor help commands explain how to use a specific executor.

Known executor help commands:
- `PS_HELP`: PowerShell executor help;
- `PY_HELP`: Python executor help.

Executor-specific rules belong in executor help, not in HST_HELP.

## Tool help commands

Tool help commands display the README.md file of a tool folder.

Each tool help result must start by saying which file it displays.

Known tool help commands:
- `LINKEDIN_HELP`: reads `tools/linkedin/README.md`, LinkedIn posting tool;
- `GITHUB_HELP`: reads `tools/github/README.md`, GitHub publishing tool;
- `N8N_HELP`: reads `tools/n8n/README.md`, n8n workflow tool;
- `WEB_HELP`: reads `tools/web/README.md`, web utility tool;
- `CLAUDE_HELP`: reads `tools/claude/README.md`, Claude launcher/tooling.

Example tool help header:

```text
LINKEDIN_HELP
This is the content of:
tools/linkedin/README.md
```

## RAW payload mechanism

RAW is part of the OpenBridge command framing contract.

Use RAW when payload text is long, quote-heavy, markdown-heavy, JSON-heavy, contains backslashes, Polish characters, command frames, or other fragile syntax.

RAW content is placed inside the `payload` field as a RAW block.

Before JSON-sensitive processing reaches the executor, OpenBridge protects the RAW block content by encoding the raw text as Base64 internally. The protected content travels through the OpenBridge workflow and is decoded before delivery to the target executor.

The executor receives the original plain text payload.

RAW works only in the documented placement: inside the payload JSON string. RAW outside the JSON object is invalid.

Conceptual shape:

```text
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"@@OPENBRIDGE_RAW_BEGIN@@
plain text goes here
@@OPENBRIDGE_RAW_END@@"}
@@OPENBRIDGE_EXEC_END@@
```

## System anti-patterns

Do not:
- invent command names;
- use removed commands;
- treat status output as help;
- treat legacy documentation as the source of truth;
- search random README files before using the documented help command;
- assume a tool exists unless it is listed by help or status;
- use executor-specific tricks in HST commands.

Executor anti-patterns belong in `PS_HELP`, `PY_HELP`, or the specific tool README.
