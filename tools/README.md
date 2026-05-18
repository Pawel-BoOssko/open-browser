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
