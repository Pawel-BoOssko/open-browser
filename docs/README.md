# docs/

**Purpose:** Documentation for the Open Browser / OpenBridge project.

**Created by:** User (pboru), restructured 2026-05-18 by Claude Code.

**Rules:**
1. When you create a folder, you must add a `README.md` inside it. The README must state: the folder's purpose, who created it, when, and what rules apply to its contents.
2. When you enter a folder, read its `README.md` first.
3. Historical or superseded documents go to `D:\projects\old-files\`.
4. No loose files in the root of `docs/` — only `README.md` and subfolders.

**Structure:**
| Folder | Audience | Role |
|--------|----------|------|
| `runtime/` | LLM inside Open Browser | What it can do, command format, available commands |
| `development/` | Agents developing Open Browser | Journal, rules, decisions |
| `architecture/` | Dev agents (legacy) | Being migrated to `development/` |
| `environment/` | Dev agents (legacy) | Being migrated to `runtime/` and `development/` |
| `handoff/` | Dev agents | One-shot context handovers between agents, then archived |
