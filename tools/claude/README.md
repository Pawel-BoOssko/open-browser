# tools/claude/

**Purpose:** Launcher for Claude Code using the DeepSeek Anthropic-compatible API. Allows models in Open Browser to delegate complex reasoning tasks to Claude Code sessions.

**Created by:** ChatGPT (Open Browser model), 2026-05-18. Integrated into tools by Claude Code.

**Rules:**
1. Requires `DEEPSEEK_API_KEY` environment variable (User or Machine level).
2. Requires `claude` CLI installed and in PATH.
3. First run in a folder starts a new session. Save the session ID. Subsequent runs use `--resume <id>` to continue.

**Files:**
| File | Purpose |
|------|---------|
| `claude-deepseek.ps1` | Main launcher — sets DeepSeek env vars, runs `claude` |
| `test-launcher.ps1` | Validation — checks syntax, Claude Code availability |
