<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/agent/current-state_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# docs/runtime/

**Purpose:** Documents for the LLM running inside Open Browser at runtime. This model has no knowledge of the project's history or source code. It only needs to know: who it is, what it can do, and how to send commands.

**Created by:** Claude Code, 2026-05-18.

**Rules:**
1. Keep everything in the smallest possible number of files. The runtime model has no context for multi-page documentation.
2. Runtime information only — no project history, no architecture, no C# source references.
3. The envelope format and command list must always be current. This is the contract between the system and the model.

**Files:**
| File | Role |
|------|------|
| `README.md` | This file — folder rules |
| `environment.md` | The runtime contract: who the model is, available commands, envelope format, what to expect |
