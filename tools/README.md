<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# tools/

**Purpose:** Utility scripts and tools for working with external services from Open Browser. All tools are designed to be called via PowerShell from the `PS` envelope.

**Created by:** Claude Code, 2026-05-18.

**Rules:**
1. Each subfolder has its own `README.md` — read it before use.
2. Tools are language-agnostic (Python, PowerShell, etc.). Prerequisites are documented per tool.
3. Secrets go in `config/local/`, never in `tools/`.
4. No hardcoded IDs, paths, or credentials — everything is parameterized.

**Subfolders:**
| Folder | Purpose |
|--------|---------|
| `n8n/` | n8n workflow management via Public API + Supabase queries |
| `github/` | GitHub file publishing via Contents API (no local git) |
| `linkedin/` | LinkedIn posting via OAuth 2.0 + REST API |
| `claude/` | Claude Code launcher via DeepSeek API — delegate complex tasks |
| `web/` | Browser automation — form filling, data extraction, screenshots |
