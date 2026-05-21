# AntiGravity Task: OpenBridge Text Transport and Encoding

Purpose: Defines the architecture investigation task for OpenBridge text transport and encoding.

Depends on:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/runtime/hst-help_new.md
- docs/executors/PS/help_new.md
- docs/executors/PY/help_new.md

Impacts:
- OpenBridge envelope parser
- RAW mechanism
- command mapper
- GeneralCommandExecutor
- stdout/stderr capture
- result injection
- executor documentation

Update rule: Update this file when the transport contract investigation is completed, accepted, or replaced by an implementation plan.

## Task for AntiGravity

Read `docs/agent/READ_BEFORE_WORK_new.md` and `handoff.md` first.

Investigate and propose an architecture-level fix for OpenBridge text transport and encoding. Do not patch individual symptoms first.

## Problem

OpenBridge currently relies too much on raw command payloads and process stdout/stderr as text transport.

The issue exists in both directions:

```text
model -> OpenBridge -> executor
executor -> OpenBridge -> model
```

Direction 1: long text, Polish characters, quotes, JSON, markdown, backslashes, and command frames can break payload transport or escaping.

Direction 2: process stdout/stderr, PowerShell, .NET capture, and WebView/result injection can corrupt non-ASCII text or return misleading mojibake.

This is a protocol and transport-contract problem, not only a PowerShell problem.

## Goal

Design a clear transport contract for text and command payloads.

The solution should define how text is encoded, framed, passed to executors, captured from executors, decoded, and returned to the model.

## Required analysis

Inspect:
- envelope parser;
- RAW handling;
- command mapper;
- GeneralCommandExecutor;
- stdout/stderr capture;
- PS and PY execution paths;
- result injection.

Identify:
- where encoding assumptions exist;
- where model-to-executor payloads can break;
- where executor-to-model results can break;
- whether RAW/Base64 solves only input payload transport or should also influence output/result transport.

Compare possible fixes:
- enforced UTF-8 process configuration;
- Base64-wrapped payloads;
- structured JSON result envelope;
- executor-specific wrappers;
- combined approach.

Recommend one architecture-level direction, not a pile of local workarounds.

## Constraints

- Do not remove existing RAW mechanism unless replacing it with a better accepted mechanism.
- Do not add more documentation warnings as the main solution.
- Do not solve this only by telling models to use Python.
- Do not treat PowerShell stdout as reliable Unicode transport unless proven and enforced by code.
- Do not modify unrelated features.

## Deliverable

Return:
1. current failure points;
2. recommended transport contract;
3. minimal implementation plan;
4. files likely affected;
5. tests needed to prove Polish text and long markdown/JSON payloads work in both directions.
